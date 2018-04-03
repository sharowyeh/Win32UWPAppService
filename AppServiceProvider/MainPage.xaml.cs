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
            
            /// startup in-process app service when the background task activated from other applications
            /// https://docs.microsoft.com/en-us/windows/uwp/launch-resume/convert-app-service-in-process
            Windows.ApplicationModel.Core.CoreApplication.BackgroundActivated += (sender, args) =>
            {
                var ServiceTask = new AppServiceComponent.AppServiceTask();
                ServiceTask.RequestResponsed += async (message, response) =>
                {
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
                    {
                        var message_content = "message:" + message["action"] as string;
                        var response_content = "response:" + response["status"] as string;
                        //if (message["action"].Equals("set_client"))
                        //{
                        //    var client = message["client"] as ValueSet;
                        //    message_content += " " + client["assembly"].ToString() + " " + client["platform"].ToString();
                        //}
                        //if (message["action"].Equals("get_client"))
                        //{
                        //    var list = response["list"] as ValueSet;
                        //    response_content += " " + list.Keys.Count.ToString() + "=>";
                        //    foreach (var key in list.Keys)
                        //        response_content += key + ",";
                        //}
                        activationText.Text = message_content + "\n" + response_content;
                    });
                };
                ServiceTask.Run(args.TaskInstance);
            };

            /// Can use each of inproc and outproc looks what is different
            serviceNameText.Text = "com.msi.spb.appservice.inproc";
            packageNameText.Text = Windows.ApplicationModel.Package.Current.Id.FamilyName;

            serviceClient = new AppServiceClient(serviceNameText.Text, packageNameText.Text);
            serviceClient.ConnectionClosed += async (sender, e) =>
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, new Windows.UI.Core.DispatchedHandler(() =>
                {
                    statusText.Text = "Closed";
                }));
            };
            serviceClient.ConnectionFailed += (sender, e) =>
            {
                statusText.Text = sender.ToString();
            };

            setButton.Click += SetButton_Click;
            getButton.Click += GetButton_Click;
            cleanButton.Click += CleanButton_Click;
        }

        private async void SetButton_Click(object sender, RoutedEventArgs e)
        {
            if (inputText.Text.Length == 0)
                return;

            //var client = new ValueSet();
            //client.Add("assembly", Windows.ApplicationModel.Package.Current.DisplayName);
            //client.Add("platform", "uwp");
            //client.Add("name", inputText.Text);
            //client.Add("timestamp", DateTime.Now.ToString());
            //var message = new ValueSet();
            //message.Add("action", "set_client");
            //message.Add("container_name", "data_store");
            //message.Add("client", client);
            var message = new ValueSet();
            message.Add("action", "set_caller");
            message.Add("caller_id", "UWP-" + Windows.ApplicationModel.Package.Current.DisplayName);
            message.Add("timestamp", DateTime.Now.ToString());

            var response = await serviceClient.OpenAndSendMessage(message);
        }

        private async void GetButton_Click(object sender, RoutedEventArgs e)
        {
            //var message = new ValueSet();
            //message.Add("action", "get_client");
            //message.Add("container_name", "data_store");
            var message = new ValueSet();
            message.Add("action", "get_caller");
            
            var response = await serviceClient.OpenAndSendMessage(message);
            if (response != null && response.ContainsKey("status") && response["status"].Equals("ok"))
            {
                var chunk = "";
                //var list = response["list"] as ValueSet;
                //foreach (var pair in list)
                //{
                //    var item = pair.Value as ValueSet;
                //    chunk += pair.Key + " name: " + item["name"].ToString() + " platform: " + item["platform"].ToString() + " at " + item["timestamp"].ToString() + "\n";
                //}
                foreach (var pair in response)
                {
                    if (pair.Key.Equals("status"))
                        continue;
                    chunk += "caller: " + pair.Key + " timestamp: " + pair.Value.ToString() + "\n";
                }
                responseText.Text = chunk;
            }
        }
        
        private async void CleanButton_Click(object sender, RoutedEventArgs e)
        {
            var message = new ValueSet();
            message.Add("action", "clean_data");
            message.Add("container_name", "data_store");
            var response = await serviceClient.OpenAndSendMessage(message);
        }
    }
}
