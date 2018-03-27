using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace AppServiceComponent
{
    /// <summary>
    /// This App service uses background task runs in host app named AppServiceProvider
    /// </summary>
    public sealed class AppServiceTask : IBackgroundTask
    {
        private BackgroundTaskDeferral backgroundTaskDeferral;
        private static ApplicationDataContainer dataContainer = null;

        public event TypedEventHandler<ValueSet, ValueSet> RequestResponsed;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            this.backgroundTaskDeferral = taskInstance.GetDeferral();
            taskInstance.Canceled += TaskInstance_Canceled;
            var details = taskInstance.TriggerDetails as AppServiceTriggerDetails;
            details.AppServiceConnection.RequestReceived += AppServiceConnection_RequestReceived;
        }

        private ApplicationDataContainer GetDataContainer()
        {
            try
            {
                var settings = ApplicationData.Current.LocalSettings;
                ApplicationDataContainer container = null;
                if (settings.Containers.ContainsKey("data_store"))
                    container = settings.Containers["data_store"];
                else
                    container = settings.CreateContainer("data_store", ApplicationDataCreateDisposition.Always);
                return container;
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        private void CleanupDataContainer()
        {
            try
            {
                var settings = ApplicationData.Current.LocalSettings;
                if (settings.Containers.ContainsKey("data_store"))
                    settings.Containers["data_store"].Values.Clear();
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        private async void AppServiceConnection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            var messageDeferral = args.GetDeferral();
            var message = args.Request.Message;

            ValueSet response = null;

            try
            {
                if (dataContainer == null)
                    dataContainer = GetDataContainer();
                
                var action = message["action"] as string;
                switch (action)
                {
                    case "add_client":
                        response = Response_AddClient(message);
                        break;
                    case "cleanup_data":
                        CleanupDataContainer();
                        dataContainer = null;
                        response = Response_Empty();
                        break;
                    default:
                        response = Response_Exception("invalid action");
                        break;
                }

            }
            catch (Exception e)
            {
                response = Response_Exception(e.Message + "\n" + e.StackTrace);
            }
            
            await args.Request.SendResponseAsync(response);
            
            messageDeferral.Complete();
            
            if (RequestResponsed != null)
                RequestResponsed(message, response);
        }

        private ValueSet Response_Empty()
        {
            var response = new ValueSet();
            response.Add("status", "action completed");
            return response;
        }

        private ValueSet Response_Exception(string error)
        {
            var response = new ValueSet();
            response.Add("exception", error);
            return response;
        }

        private ValueSet Response_AddClient(ValueSet message)
        {
            var client = message["client"] as ValueSet;
            var assembly = client["assembly"] as string;
            var platform = client["platform"] as string;
            var name = client["name"] as string;
            var timestamp = client["timestamp"] as string;

            var composite = new ApplicationDataCompositeValue();
            composite["assembly"] = assembly;
            composite["platform"] = platform;
            composite["name"] = name;
            composite["timestamp"] = timestamp;
            dataContainer.Values[assembly] = composite;

            var response = new ValueSet();
            var list = new ValueSet();
            foreach (var item in dataContainer.Values)
            {
                var item_composite = item.Value as ApplicationDataCompositeValue;
                var data = new ValueSet();
                data.Add("assembly", item_composite["assembly"]);
                data.Add("platform", item_composite["platform"]);
                data.Add("name", item_composite["name"]);
                data.Add("timestamp", item_composite["timestamp"]);
                list.Add(data["assembly"].ToString(), data);
            }
            response.Add("action", "list_clients");
            response.Add("list", list);
            return response;
        }

        private void TaskInstance_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            if (this.backgroundTaskDeferral != null)
                this.backgroundTaskDeferral.Complete();
        }
    }
}
