using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace AppServiceProvider
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private AppServiceConnectionStatus serviceStatus = AppServiceConnectionStatus.Unknown;
        private AppServiceConnection serviceConnection = null;

        public MainPage()
        {
            this.InitializeComponent();

            var settings = ApplicationData.Current.LocalSettings;
            if (settings.Containers.ContainsKey("data_store") == false)
                settings.CreateContainer("data_store", ApplicationDataCreateDisposition.Always);

            packageNameText.Text = Windows.ApplicationModel.Package.Current.Id.FamilyName;
            
            sendButton.Click += SendButton_Click;
            cleanupButton.Click += CleanupButton_Click;
        }
        
        public async Task OpenAppServiceConnection()
        {
            serviceConnection = new AppServiceConnection();
            serviceConnection.AppServiceName = serviceNameText.Text;
            serviceConnection.PackageFamilyName = packageNameText.Text;
            serviceConnection.RequestReceived += async (sender, e) =>
            {
                var messageDeferral = e.GetDeferral();
                var message = e.Request.Message;
                await e.Request.SendResponseAsync(message);
                messageDeferral.Complete();
            };
            serviceConnection.ServiceClosed += async (sender, e) =>
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, new Windows.UI.Core.DispatchedHandler(() =>
                {
                    if (serviceConnection != null)
                    {
                        serviceStatus = AppServiceConnectionStatus.Unknown;
                        serviceConnection.Dispose();
                        serviceConnection = null;
                    }
                    statusText.Text = "Closed";
                }));
            };

            serviceStatus = await serviceConnection.OpenAsync();
            statusText.Text = serviceStatus.ToString();
            if (serviceStatus != AppServiceConnectionStatus.Success)
            {
                serviceConnection.Dispose();
                serviceConnection = null;
            }
        }
        
        private async Task<ValueSet> SendMessageToAppService(ValueSet message)
        {
            if (serviceStatus != AppServiceConnectionStatus.Success)
                await OpenAppServiceConnection();
            if (serviceStatus != AppServiceConnectionStatus.Success)
                return null;

            var result = await serviceConnection.SendMessageAsync(message);
            if (result.Status == AppServiceResponseStatus.Success)
                return result.Message;
            else
                return null;
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (inputText.Text.Length == 0)
                return;

            var client = new ValueSet();
            client.Add("assembly", Windows.ApplicationModel.Package.Current.DisplayName);
            client.Add("platform", "app_provider");
            client.Add("name", inputText.Text);
            client.Add("timestamp", DateTime.Now.ToString());
            var message = new ValueSet();
            message.Add("action", "add_client");
            message.Add("client", client);

            var response = await SendMessageToAppService(message);
            if (response != null)
            {
                var chunk = "";
                var list = response["list"] as ValueSet;
                foreach (var item in list)
                {
                    var item_set = item.Value as ValueSet;
                    chunk += item_set["assembly"].ToString() + " name: " + item_set["name"].ToString() + " platform: " + item_set["platform"].ToString() + " at " + item_set["timestamp"].ToString() + "\n";
                }
                responseText.Text = chunk;
            }

        }
        
        private async void CleanupButton_Click(object sender, RoutedEventArgs e)
        {
            var message = new ValueSet();
            message.Add("action", "cleanup_data");
            var response = await SendMessageToAppService(message);
        }
    }
}
