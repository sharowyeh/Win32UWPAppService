using AppServiceComponent;
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
        private AppServiceClient serviceClient;

        public MainPage()
        {
            this.InitializeComponent();

            /// Handling app service responsed event to show on UI
            App.AppService_RequestResponsed += async (request, response) =>
            {
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
                {
                    var caller = "caller: " + request["caller"] as string;
                    var action = "action: " + request["action"] as string;
                    var timestamp = "timestamp: " + request["timestamp"] as string;
                    var request_content = string.Format("request:\n  {0}\n  {1}\n  {2}", caller, action, timestamp);
                    var status = "status: " + response["status"] as string;
                    var response_content = string.Format("response:\n  {0}", status);
                    activationText.Text = request_content + "\n" + response_content;
                });
            };

            /// UWP App Service Provider project defined both inproc and outproc app service declarations,
            /// change either service name to see what is different
            serviceNameText.Text = "com.msi.spb.appservice.inproc";
            //serviceNameText.Text = "com.msi.spb.appservice.outproc";
            packageNameText.Text = Windows.ApplicationModel.Package.Current.Id.FamilyName;

            serviceClient = new AppServiceClient(serviceNameText.Text, packageNameText.Text);
            serviceClient.ConnectionClosed += async (sender, e) =>
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, new Windows.UI.Core.DispatchedHandler(() =>
                {
                    statusText.Text = "ServiceClosed";
                }));
            };
            serviceClient.ConnectionFailed += async (sender, e) =>
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, new Windows.UI.Core.DispatchedHandler(() =>
                {
                    /// Sender is AppServiceConnectionStatus or AppServiceResponseStatus
                    statusText.Text = sender.ToString();
                }));
            };

            writeButton.Click += WriteButton_Click;
            readButton.Click += ReadButton_Click;
            cleanButton.Click += CleanButton_Click;
        }

        private void DisplayResponse(string action, ValueSet response)
        {
            if (response == null)
            {
                responseText.Text = "response is null";
                return;
            }

            var chunk = "action: " + action + "\n";
            foreach (var pair in response)
            {
                chunk += pair.Key + ": " + pair.Value.ToString() + "\n";
            }
            responseText.Text = chunk;
        }

        private async void WriteButton_Click(object sender, RoutedEventArgs e)
        {
            if (contentText.Text.Length == 0)
                return;
            
            var message = new ValueSet();
            message.Add("action", "write_data");
            message.Add("caller", "UWP-" + Windows.ApplicationModel.Package.Current.DisplayName);
            message.Add("content", contentText.Text);
            message.Add("timestamp", DateTime.Now.ToString());

            var response = await serviceClient.OpenAndSendMessage(message);
            DisplayResponse("write_data", response);
        }

        private async void ReadButton_Click(object sender, RoutedEventArgs e)
        {
            var message = new ValueSet();
            message.Add("action", "read_data");
            message.Add("caller", "UWP-" + Windows.ApplicationModel.Package.Current.DisplayName);
            message.Add("timestamp", DateTime.Now.ToString());
            
            var response = await serviceClient.OpenAndSendMessage(message);
            DisplayResponse("read_data", response);
        }
        
        private async void CleanButton_Click(object sender, RoutedEventArgs e)
        {
            var message = new ValueSet();
            message.Add("action", "clean_data");
            message.Add("caller", "UWP-" + Windows.ApplicationModel.Package.Current.DisplayName);
            message.Add("timestamp", DateTime.Now.ToString());

            var response = await serviceClient.OpenAndSendMessage(message);
            DisplayResponse("clean_data", response);
        }
    }
}
