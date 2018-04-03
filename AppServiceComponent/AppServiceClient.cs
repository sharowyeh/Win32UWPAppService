using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace AppServiceComponent
{
    public sealed class AppServiceClient
    {
        private string appServiceName;
        private string packageFamilyName;
        private AppServiceConnection connection;
        private AppServiceConnectionStatus status = AppServiceConnectionStatus.Unknown;

        /// <summary>
        /// Lose type event for runtime component or register COM GUID event interface
        /// </summary>
        public event EventHandler<object> ConnectionClosed;
        public event EventHandler<object> ConnectionFailed;

        public AppServiceClient(string serviceName, string packageName)
        {
            appServiceName = serviceName;
            packageFamilyName = packageName;
        }

        public IAsyncOperation<ValueSet> OpenAndSendMessage(ValueSet message)
        {
            return openAndSendMessage(message).AsAsyncOperation();
        }

        private async Task<ValueSet> openAndSendMessage(ValueSet message)
        {
            if (connection == null)
            {
                connection = new AppServiceConnection();
                connection.AppServiceName = appServiceName;
                connection.PackageFamilyName = packageFamilyName;
                connection.ServiceClosed += (sender, e) =>
                {
                    ConnectionClosed?.Invoke(sender, e);
                    status = AppServiceConnectionStatus.Unknown;
                    connection.Dispose();
                    connection = null;
                };

                status = await connection.OpenAsync();
                if (status != AppServiceConnectionStatus.Success)
                {
                    connection.Dispose();
                    connection = null;
                    ConnectionFailed?.Invoke(status, null);
                    return null;
                }
            }

            var result = await connection.SendMessageAsync(message);
            if (result.Status == AppServiceResponseStatus.Success)
                return result.Message;
            else
            {
                status = AppServiceConnectionStatus.Unknown;
                connection.Dispose();
                connection = null;
                ConnectionFailed?.Invoke(result.Status, null);
                return null;
            }
        }
    }
}
