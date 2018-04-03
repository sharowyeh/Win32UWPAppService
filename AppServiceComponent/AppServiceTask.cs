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

        /// <summary>
        /// Request responsed event handler for in-process app service
        /// </summary>
        public event TypedEventHandler<ValueSet, ValueSet> RequestResponsed;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            this.backgroundTaskDeferral = taskInstance.GetDeferral();
            taskInstance.Canceled += async (sender, args) =>
            {
                /// In in-proc app service, save logging file may be written in app life cycle
                /// In out-proc app service, save logging file may be written in this way? (file will be created each 25 seconds...)
                var logFile = await Log.Instance.SaveFile();
                System.Diagnostics.Debug.WriteLineIf(logFile != null, "logging file:" + logFile.Path);

                if (this.backgroundTaskDeferral != null)
                    this.backgroundTaskDeferral.Complete();
            };
            var details = taskInstance.TriggerDetails as AppServiceTriggerDetails;
            /// Run has different instance by different app service name for out-proc and in-proc background task
            /// log session also needs to create different instance to save file
            System.Diagnostics.Debug.WriteLine("task instance:" + taskInstance.InstanceId.ToString());
            System.Diagnostics.Debug.WriteLine("service name:" + details.Name + " caller:" + details.CallerPackageFamilyName);
            Log.UniqueId = Windows.ApplicationModel.Package.Current.DisplayName + "-task" + details.Name.Substring(details.Name.LastIndexOf('.'));
            Log.Instance.MessageInfo("task instance:" + taskInstance.InstanceId.ToString() + ", service name:" + details.Name + " caller:" + details.CallerPackageFamilyName);
            details.AppServiceConnection.RequestReceived += AppServiceConnection_RequestReceived;
            Windows.ApplicationModel.Core.CoreApplication.UnhandledErrorDetected += (sender, args) =>
            {
                System.Diagnostics.Debug.WriteLine("Unhandled:" + sender.ToString());
                System.Diagnostics.Debug.WriteLine("Exception:" + args.UnhandledError.ToString());
            };
        }

        private ApplicationDataContainer GetDataContainer(string name)
        {
            try
            {
                var settings = ApplicationData.Current.LocalSettings;
                var container = settings.CreateContainer(name, ApplicationDataCreateDisposition.Always);
                if (settings.Containers.ContainsKey(name))
                    return container;
                else
                    throw new Exception("container name:" + name + " does not created");
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
                    case "set_caller":
                        response = ResponseSetCaller(message);
                        break;
                    case "get_caller":
                        response = ResponseGetCaller(message);
                        break;
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
                }
            }
            catch (Exception e)
            {
                response = ResponseException(e.Message + "\n" + e.StackTrace);
            }
            
            await args.Request.SendResponseAsync(response);
            messageDeferral.Complete();
            
            try
            {
                if (RequestResponsed != null)
                    RequestResponsed(message, response);
            }
            catch (Exception e)
            {
                Log.Instance.MessageError(e.Message + "\n" + e.StackTrace);
            }
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

        private ValueSet ResponseSetCaller(ValueSet message)
        {
            var response = new ValueSet();
            try
            {
                var caller_id = message["caller_id"] as string;
                var timestamp = message["timestamp"] as string;
                var key = "caller_" + caller_id;
                ApplicationData.Current.LocalSettings.Values[key] = timestamp;

                response.Add("status", "ok");
            }
            catch (Exception e)
            {
                response = ResponseException(e.Message + "\n" + e.StackTrace);
            }
            return response;
        }

        private ValueSet ResponseGetCaller(ValueSet message)
        {
            var response = new ValueSet();
            try
            {
                foreach (var pair in ApplicationData.Current.LocalSettings.Values)
                {
                    if (pair.Key.StartsWith("caller_"))
                    {
                        var caller_id = pair.Key.Substring(7);
                        response.Add(caller_id, pair.Value);
                    }
                }

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
                //onecore\base\appmodel\statemanager\apiset\lib\stateatom.cpp(561)\kernelbase.dll!00007FF806D08D63: (caller: 00007FF806D443BF) ReturnHr(1) tid(44ec) 8007007A The data area passed to a system call is too small.
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
    }
}
