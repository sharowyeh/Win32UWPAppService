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

        public event TypedEventHandler<ValueSet, ValueSet> RequestResponsed;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            this.backgroundTaskDeferral = taskInstance.GetDeferral();
            taskInstance.Canceled += TaskInstance_Canceled;
            var details = taskInstance.TriggerDetails as AppServiceTriggerDetails;
            details.AppServiceConnection.RequestReceived += AppServiceConnection_RequestReceived;
        }

        private ApplicationDataContainer GetDataContainer(string name)
        {
            try
            {
                var settings = ApplicationData.Current.LocalSettings;
                ApplicationDataContainer container = null;
                if (settings.Containers.ContainsKey(name))
                    container = settings.Containers[name];
                else
                    container = settings.CreateContainer(name, ApplicationDataCreateDisposition.Always);
                return container;
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        private void CleanDataContainer(string name)
        {
            try
            {
                var settings = ApplicationData.Current.LocalSettings;
                if (settings.Containers.ContainsKey(name))
                    settings.Containers[name].Values.Clear();
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
                var action = message["action"] as string;
                switch (action)
                {
                    case "set_client":
                        response = ResponseSetClient(message);
                        break;
                    case "get_client":
                        response = ResponseGetClient(message);
                        break;
                    case "clean_data":
                        response = ResponseCleanData(message);
                        break;
                    default:
                        throw new Exception("Invalid action");
                        break;
                }

            }
            catch (Exception e)
            {
                response = ResponseException(e.Message + "\n" + e.StackTrace);
            }
            
            await args.Request.SendResponseAsync(response);
            
            messageDeferral.Complete();
            
            if (RequestResponsed != null)
                RequestResponsed(message, response);
        }

        private ValueSet ResponseException(string error)
        {
            var response = new ValueSet();
            response.Add("status", "error");
            response.Add("exception", error);
            return response;
        }

        private ValueSet ResponseCleanData(ValueSet message)
        {
            var response = new ValueSet();
            try
            {
                var name = message["container_name"] as string;
                CleanDataContainer(name);
                response.Add("status", "ok");
            }
            catch (Exception e)
            {
                response = ResponseException(e.Message + "\n" + e.StackTrace);
            }
            return response;
        }

        private ValueSet ResponseSetClient(ValueSet message)
        {
            var response = new ValueSet();
            try
            {
                var container_name = message["container_name"] as string;
                var container = GetDataContainer(container_name);

                var client = message["client"] as ValueSet;
                var composite = new ApplicationDataCompositeValue();
                composite["assembly"] = client["assembly"];
                composite["platform"] = client["platform"];
                composite["name"] = client["name"];
                composite["timestamp"] = client["timestamp"];

                var key = client["assembly"] as string;
                container.Values[key] = composite;

                response.Add("status", "ok");
            }
            catch (Exception e)
            {
                response = ResponseException(e.Message + "\n" + e.StackTrace);
            }
            return response;
        }

        private ValueSet ResponseGetClient(ValueSet message)
        {
            var response = new ValueSet();
            try
            {
                var container_name = message["container_name"] as string;
                var container = GetDataContainer(container_name);

                var list = new ValueSet();
                foreach (var pair in container.Values)
                {
                    var composite = pair.Value as ApplicationDataCompositeValue;
                    var client = new ValueSet();
                    client.Add("assembly", composite["assembly"]);
                    client.Add("platform", composite["platform"]);
                    client.Add("name", composite["name"]);
                    client.Add("timestamp", composite["timestamp"]);
                    list.Add(pair.Key, client);
                }

                response.Add("status", "ok");
                response.Add("list", list);
            }
            catch (Exception e)
            {
                response = ResponseException(e.Message + "\n" + e.StackTrace);
            }
            return response;
        }

        private void TaskInstance_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            if (this.backgroundTaskDeferral != null)
                this.backgroundTaskDeferral.Complete();
        }
    }
}
