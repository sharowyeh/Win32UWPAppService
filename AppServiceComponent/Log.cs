using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Diagnostics;
using Windows.Storage;

namespace AppServiceComponent
{
    public sealed class Log : IDisposable
    {
        private string channelName;
        private string sessionName;
        
        private LoggingChannel channel;
        private FileLoggingSession session;

        private Log(string prefix)
        {
            channelName = prefix + "_channel";
            sessionName = prefix + "_session";

            channel = new LoggingChannel(channelName, null);
            
            Windows.ApplicationModel.Core.CoreApplication.Resuming += (sender, obj) =>
            {
                ResumeLogging();
            };

            Windows.ApplicationModel.Core.CoreApplication.Suspending += (sender, args) =>
            {
                var deferral = args.SuspendingOperation.GetDeferral();
                SuspendLogging();
                deferral.Complete();
            };
        }

        ~Log()
        {
            Dispose(false);
        }

        public void SetEnabled(bool isEnabled)
        {
            if (isEnabled)
            {
                if (session == null)
                    session = new FileLoggingSession(sessionName);
                session.AddLoggingChannel(channel, LoggingLevel.Information);
            }
            else
            {
                if (session != null)
                    session.Dispose();
                session = null;
            }
            ApplicationData.Current.LocalSettings.Values["log_enabled"] = isEnabled;
        }

        public void MessageInfo(string content)
        {
            Message(content, LoggingLevel.Information);
        }

        public void MessageError(string content)
        {
            Message(content, LoggingLevel.Error);
        }

        public void Message(string content, LoggingLevel level)
        {
            if (channel == null || channel.Enabled == false)
                return;

            channel.LogMessage(content, level);
        }
        
        public IAsyncOperation<StorageFile> SaveFile()
        {
            return saveFile().AsAsyncOperation();
        }

        private async Task<StorageFile> saveFile()
        {
            if (session == null)
                return null;
            
            var file = await session.CloseAndSaveToFileAsync();
            if (file != null)
            {
                var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("LogFiles", CreationCollisionOption.OpenIfExists);
                var newName = "Log-" + channelName + "-" + DateTime.Now.ToString("HHmmss") + ".etl";
                await file.MoveAsync(folder, newName, NameCollisionOption.ReplaceExisting);
                file = await folder.GetFileAsync(newName);
            }
            session.Dispose();
            session = null;
            return file;
        }
        
        /// <summary>
        /// Save logging state to local settings for resuming
        /// </summary>
        public void SuspendLogging()
        {
            ApplicationData.Current.LocalSettings.Values["log_enabled"] = session != null;
        }

        /// <summary>
        /// Get previous logging state indicated start logging or not from app startup or resuming
        /// </summary>
        public void ResumeLogging()
        {
            object logEnabled;
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("log_enabled", out logEnabled) == false)
            {
                /// Default enable log
                ApplicationData.Current.LocalSettings.Values["log_enabled"] = true;
                logEnabled = true;
            }
            SetEnabled((bool)logEnabled);
        }

        private static Log _instance;
        private static string _uniqueId;

        /// <summary>
        /// Using specific unique name/id to append the end of app/package name for logging session and channel name in different instance
        /// </summary>
        public static string UniqueId
        {
            get
            {
                return _uniqueId;
            }
            set
            {
                if (_uniqueId == null || _uniqueId.Equals(value) == false)
                {
                    if (_instance != null)
                    {
                        _instance.Dispose();
                        _instance = null;
                    }
                    _uniqueId = value;
                }
            }
        }
        
        public static Log Instance
        {
            get
            {
                if (_uniqueId == null)
                    _uniqueId = Windows.ApplicationModel.Package.Current.DisplayName;
                try
                {
                    if (_instance == null)
                        _instance = new Log(_uniqueId);

                    if (_instance.session == null)
                        _instance.ResumeLogging();
                }
                catch (Exception e)
                {
                    /// constructor exception usually occurred if session or channel unique name duplicated
                    System.Diagnostics.Debug.WriteLine(e.Message + "\n" + e.StackTrace);
                }
                return _instance;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private bool isDisposed = false;

        private void Dispose(bool disposing)
        {
            if (isDisposed)
                return;

            isDisposed = true;
            if (disposing)
            {
                if (channel != null)
                    channel.Dispose();
                channel = null;

                if (session != null)
                    session.Dispose();
                session = null;
            }
        }
    }
}
