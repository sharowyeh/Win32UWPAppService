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

            /// startup app service when the background task activated from other applications
            /// https://docs.microsoft.com/en-us/windows/uwp/launch-resume/convert-app-service-in-process
            Windows.ApplicationModel.Core.CoreApplication.BackgroundActivated += (sender, args) =>
            {
                var ServiceTask = new AppServiceComponent.AppServiceTask();
                ServiceTask.RequestResponsed += async (message, response) =>
                {
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, async () =>
                    {
                        var message_content = "message:" + message["action"] as string;
                        var response_content = "response:" + response["status"] as string;
                        if (message["action"].Equals("set_client"))
                        {
                            var client = message["client"] as ValueSet;
                            message_content += " " + client["assembly"].ToString() + " " + client["platform"].ToString();
                        }
                        if (message["action"].Equals("get_client"))
                        {
                            var list = response["list"] as ValueSet;
                            response_content += " " + list.Keys.Count.ToString() + "=>";
                            foreach (var key in list.Keys)
                                response_content += key + ",";
                        }
                        Windows.UI.Popups.MessageDialog dialog = new Windows.UI.Popups.MessageDialog(message_content + "\r\n" + response_content);
                        await dialog.ShowAsync();
                    });
                };
                ServiceTask.Run(args.TaskInstance);
            };

            packageNameText.Text = Windows.ApplicationModel.Package.Current.Id.FamilyName;
            
            setButton.Click += SetButton_Click;
            getButton.Click += GetButton_Click;
            cleanButton.Click += CleanButton_Click;
        }
        
        public async Task OpenAppServiceConnection()
        {
            serviceConnection = new AppServiceConnection();
            serviceConnection.AppServiceName = serviceNameText.Text;
            serviceConnection.PackageFamilyName = packageNameText.Text;
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

        private async void SetButton_Click(object sender, RoutedEventArgs e)
        {
            if (inputText.Text.Length == 0)
                return;

            var client = new ValueSet();
            client.Add("assembly", Windows.ApplicationModel.Package.Current.DisplayName);
            client.Add("platform", "uwp");
            client.Add("name", inputText.Text);
            client.Add("timestamp", DateTime.Now.ToString());
            var message = new ValueSet();
            message.Add("action", "set_client");
            message.Add("container_name", "data_store");
            message.Add("client", client);

            var response = await SendMessageToAppService(message);
        }

        private async void GetButton_Click(object sender, RoutedEventArgs e)
        {
            var message = new ValueSet();
            message.Add("action", "get_client");
            message.Add("container_name", "data_store");

            var response = await SendMessageToAppService(message);
            if (response != null && response.ContainsKey("status") && response["status"].Equals("ok"))
            {
                var chunk = "";
                var list = response["list"] as ValueSet;
                foreach (var pair in list)
                {
                    var item = pair.Value as ValueSet;
                    chunk += pair.Key + " name: " + item["name"].ToString() + " platform: " + item["platform"].ToString() + " at " + item["timestamp"].ToString() + "\n";
                }
                responseText.Text = chunk;
            }
        }
        
        private async void CleanButton_Click(object sender, RoutedEventArgs e)
        {
            var message = new ValueSet();
            message.Add("action", "clean_data");
            message.Add("container_name", "data_store");
            var response = await SendMessageToAppService(message);
        }
    }
}
