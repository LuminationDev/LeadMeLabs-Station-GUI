using System;
using System.Windows;
using Sentry;
using Application = System.Windows.Application;

namespace Station
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            if (e.Args.Length > 0 && e.Args[0].Trim().ToLower() == "writeversion")
            {
                Updater.GenerateVersion();
                Environment.Exit(1);
                return;
            }
            Logger.WriteLog("Updated to OpenVR Version", MockConsole.LogLevel.Error);
            SteamConfig.VerifySteamConfig();

            MainWindow mainWindow = new();
            mainWindow.Show();

            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += UnhandledExceptionHandler;
            currentDomain.ProcessExit += ProcessExitHandler;
            InitSentry();
            CheckStorage();

            Manager.StartProgram();
        }

        /// <summary>
        /// Check the local storage, sending a sentry error message if there is less than 10GB of free space.
        /// </summary>
        private static void CheckStorage()
        {
            int? freeStorage = CommandLine.GetFreeStorage();
            if (freeStorage != null && freeStorage < 10)
            {
                SentrySdk.CaptureMessage("Low memory detected (" + freeStorage + ") at: " +
                                         (Environment.GetEnvironmentVariable("LabLocation",
                                             EnvironmentVariableTarget.Process) ?? "Unknown"));
            }
        }

        /// <summary>
        /// Update the title of the MainWindow, this is designed to show the User the Station ID as well as the Current IP address.
        /// </summary>
        /// <param name="title"></param>
        public static void SetWindowTitle(string title)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                Application.Current.MainWindow.Title = title;
            }));
        }

        static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs args)
        {
            Exception e = (Exception)args.ExceptionObject;
            Logger.WriteLog("UnhandledExceptionHandler caught : " + e.Message, MockConsole.LogLevel.Error);
            Logger.WriteLog($"Runtime terminating: {args.IsTerminating}", MockConsole.LogLevel.Error);
            Logger.WorkQueue();
            Manager.SendResponse("Android", "Station", "SetValue:status:Off");
            Manager.SendResponse("Android", "Station", "SetValue:gameName:Unexpected error occured, please restart station");
            Manager.SendResponse("Android", "Station", "SetValue:gameId:");
        }

        static void ProcessExitHandler(object? sender, EventArgs args)
        {
            Logger.WriteLog($"Process Exiting. Sender: {sender}, Event: {args}", MockConsole.LogLevel.Verbose);
            Logger.WorkQueue();
            Manager.SendResponse("Android", "Station", "SetValue:status:Off");
            Manager.SendResponse("Android", "Station", "SetValue:gameName:");
            Manager.SendResponse("Android", "Station", "SetValue:gameId:");

            //Shut down the pipe server if running
            WrapperManager.ClosePipeServer();

            //Shut down any OpenVR systems
            Manager.openVRManager?.OpenVrSystem?.Shutdown();
        }

        public static void InitSentry()
        {
            string? sentryDsn = "";

#if DEBUG
            sentryDsn = "https://ca9abb6c77444340802da0c5a3805841@o1294571.ingest.sentry.io/6704982"; //Development
#elif RELEASE
	        sentryDsn = "https://812f2b29bf3c4d129071683c7cf62361@o1294571.ingest.sentry.io/6518754"; //Production
#endif
            if (sentryDsn != null && sentryDsn.Length > 0)
            {
                SentrySdk.Init(options =>
                {
                    options.Dsn = sentryDsn;
                    options.Debug = false;
                    options.TracesSampleRate = 0.1;

                    options.SetBeforeSend((SentryEvent sentryEvent) =>
                    {
                        if (sentryEvent.Exception != null
                            && sentryEvent.Exception.Message.Contains("Aggregate Exception")
                            && sentryEvent.Exception.Message.Contains("WSACancelBlockingCall"))
                        {
                            return null; // Don't send this event to Sentry
                        }

                        Logger.WriteLog("Sentry Exception", MockConsole.LogLevel.Error);

                        if (sentryEvent.Exception != null)
                        {
                            Logger.WriteLog(sentryEvent.Exception, MockConsole.LogLevel.Error);
                        }
                        if (sentryEvent.Message != null)
                        {
                            Logger.WriteLog(sentryEvent.Message.ToString() ?? "No message", MockConsole.LogLevel.Error);
                        }

                        sentryEvent.ServerName = null; // Never send Server Name to Sentry
                        return sentryEvent;
                    });
                });
                SentrySdk.ConfigureScope(scope =>
                {
                    scope.SetTag("lab_location", Environment.GetEnvironmentVariable("LabLocation",
                                             EnvironmentVariableTarget.Process) ?? "Unknown");
                    scope.SetTag("station_id", Environment.GetEnvironmentVariable("StationId",
                                             EnvironmentVariableTarget.Process) ?? "Unknown");
                    scope.SetTag("headset_type", Environment.GetEnvironmentVariable("HeadsetType",
                                             EnvironmentVariableTarget.Process) ?? "Unknown");
                    scope.SetTag("room", Environment.GetEnvironmentVariable("room",
                                             EnvironmentVariableTarget.Process) ?? "Unknown");
                });
            }
        }
    }
}
