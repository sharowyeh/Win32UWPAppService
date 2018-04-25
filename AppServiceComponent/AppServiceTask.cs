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
            /// Receive task instance canceled event that raised the task has been completed
            taskInstance.Canceled += (sender, args) =>
            {
                if (this.backgroundTaskDeferral != null)
                    this.backgroundTaskDeferral.Complete();
            };
            /// Get details indicates who trigger the app service
            var details = taskInstance.TriggerDetails as AppServiceTriggerDetails;

            /// This "Run" has different instance by different app service name for out-proc or in-proc background task,
            /// that log session also needs to create different instance to save logging session to file
            System.Diagnostics.Debug.WriteLine("task instance:" + taskInstance.InstanceId.ToString());
            System.Diagnostics.Debug.WriteLine("service name:" + details.Name + " caller:" + details.CallerPackageFamilyName);
            Log.UniqueId = Windows.ApplicationModel.Package.Current.DisplayName + "-task" + details.Name.Substring(details.Name.LastIndexOf('.'));
            Log.Instance.MessageInfo("task instance:" + taskInstance.InstanceId.ToString() + ", service name:" + details.Name + " caller:" + details.CallerPackageFamilyName);

            /// Receive request event from client connection
            details.AppServiceConnection.RequestReceived += AppServiceConnection_RequestReceived;

            Windows.ApplicationModel.Core.CoreApplication.UnhandledErrorDetected += (sender, args) =>
            {
                System.Diagnostics.Debug.WriteLine("Unhandled:" + sender.ToString());
                System.Diagnostics.Debug.WriteLine("Exception:" + args.UnhandledError.ToString());
            };
        }

        /// <summary>
        /// Get specific data container from app local settings container
        /// Also create new if not exists
        /// </summary>
        /// <param name="name">Specific name of data container</param>
        /// <returns></returns>
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

        /// <summary>
        /// Cleanup specific data container
        /// </summary>
        /// <param name="name">Specific name of data container</param>
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
                    case "write_data":
                        response = ResponseWriteData(message);
                        break;
                    case "read_data":
                        response = ResponseReadData(message);
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
                ApplicationData.Current.LocalSettings.Values.Clear();
                response.Add("status", "ok");
            }
            catch (Exception e)
            {
                response = ResponseException(e.Message + "\n" + e.StackTrace);
            }
            return response;
        }

        private ValueSet ResponseWriteData(ValueSet message)
        {
            var response = new ValueSet();
            try
            {
                var caller = message["caller"] as string;
                var content = message["content"] as string;
                var key = "client_" + caller;
                /// Save data to app local settings
                ApplicationData.Current.LocalSettings.Values[key] = content;

                response.Add("status", "ok");
            }
            catch (Exception e)
            {
                response = ResponseException(e.Message + "\n" + e.StackTrace);
            }
            return response;
        }

        private ValueSet ResponseReadData(ValueSet message)
        {
            var response = new ValueSet();
            try
            {
                foreach (var pair in ApplicationData.Current.LocalSettings.Values)
                {
                    if (pair.Key.StartsWith("client_"))
                    {
                        var caller = pair.Key.Substring(7);
                        /// Response all caller's save data
                        response.Add(caller, pair.Value);
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
    }
}
