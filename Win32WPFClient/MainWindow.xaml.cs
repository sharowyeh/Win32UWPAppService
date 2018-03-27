using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

namespace Win32WPFClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        AppServiceConnectionStatus serviceStatus = AppServiceConnectionStatus.Unknown;
        AppServiceConnection serviceConnection = null;

        public MainWindow()
        {
            InitializeComponent();

            // Starts connection when it needs send message
            //Task.Factory.StartNew(async ()=>
            //{
            //    await OpenAppServiceConnection();
            //});
            
            sendButton.Click += SendButton_Click;
            cleanupButton.Click += CleanupButton_Click;
        }

        /// <summary>
        /// App service connection is based on background task that will be closed in 25 seconds automatically after its opening
        /// </summary>
        /// <returns></returns>
        public async Task OpenAppServiceConnection()
        {
            serviceConnection = new AppServiceConnection();
            serviceConnection.AppServiceName = "com.msi.spb.appservice";
            serviceConnection.PackageFamilyName = "36bcb093-0e11-4114-a4fe-25fc6238d827_7wtyq8e5t5wne";
            serviceConnection.ServiceClosed += async (sender, e) =>
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    if (serviceConnection != null)
                    {
                        serviceStatus = AppServiceConnectionStatus.Unknown;
                        serviceConnection.Dispose();
                        serviceConnection = null;
                    }
                    statusText.Text = "Closed";
                }, System.Windows.Threading.DispatcherPriority.Send);
            };

            serviceStatus = await serviceConnection.OpenAsync();
            statusText.Text = serviceStatus.ToString();
            if (serviceStatus != AppServiceConnectionStatus.Success)
            {
                MessageBox.Show("App service connection failed: " + serviceStatus.ToString());
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
            client.Add("assembly", System.Reflection.Assembly.GetExecutingAssembly().FullName.Split(',')[0]);
            client.Add("platform", "wpf");
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
