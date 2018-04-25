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
            
            /// UWP App Service Provider project defined both inproc and outproc app service declarations,
            /// change either service name to see what is different
            //serviceNameText.Text = "com.msi.spb.appservice.inproc";
            serviceNameText.Text = "com.msi.spb.appservice.outproc";
            packageNameText.Text = "36bcb093-0e11-4114-a4fe-25fc6238d827_7wtyq8e5t5wne";

            writeButton.Click += WriteButton_Click;
            readButton.Click += ReadButton_Click;
            cleanButton.Click += CleanButton_Click;
        }

        /// <summary>
        /// App service connection is based on background task that will be closed in 25 seconds automatically after its opening.
        /// Designed connection behavior is:
        /// Open service connection just before it needs to be send message, and store connection status for each action.
        /// </summary>
        /// <returns></returns>
        public async Task OpenAppServiceConnection()
        {
            serviceConnection = new AppServiceConnection();
            serviceConnection.AppServiceName = serviceNameText.Text;
            serviceConnection.PackageFamilyName = packageNameText.Text;
            /// Receive app service closed event to update connection status
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
                    statusText.Text = "ServiceClosed";
                }, System.Windows.Threading.DispatcherPriority.Send);
            };

            serviceStatus = await serviceConnection.OpenAsync();
            if (serviceStatus != AppServiceConnectionStatus.Success)
            {
                System.Diagnostics.Debug.WriteLine("App service connection failed: " + serviceStatus.ToString());
                serviceConnection.Dispose();
                serviceConnection = null;
            }
            statusText.Text = serviceStatus.ToString();
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
            {
                serviceStatus = AppServiceConnectionStatus.Unknown;
                return null;
            }
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
            message.Add("caller", "WPF-" + System.Reflection.Assembly.GetExecutingAssembly().FullName.Split(',')[0]);
            message.Add("content", contentText.Text);
            message.Add("timestamp", DateTime.Now.ToString());

            var response = await SendMessageToAppService(message);
            DisplayResponse("read_data", response);
        }

        private async void ReadButton_Click(object sender, RoutedEventArgs e)
        {
            var message = new ValueSet();
            message.Add("action", "read_data");
            message.Add("caller", "WPF-" + System.Reflection.Assembly.GetExecutingAssembly().FullName.Split(',')[0]);
            message.Add("timestamp", DateTime.Now.ToString());

            var response = await SendMessageToAppService(message);
            DisplayResponse("read_data", response);
        }

        private async void CleanButton_Click(object sender, RoutedEventArgs e)
        {
            var message = new ValueSet();
            message.Add("action", "clean_data");
            message.Add("caller", "WPF-" + System.Reflection.Assembly.GetExecutingAssembly().FullName.Split(',')[0]);
            message.Add("timestamp", DateTime.Now.ToString());

            var response = await SendMessageToAppService(message);
            DisplayResponse("read_data", response);
        }
    }
}
