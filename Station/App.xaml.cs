using System;
using System.Windows;
using System.Windows.Forms;
using Sentry;
using Station.Components._commandLine;
using Station.Components._managers;
using Station.Components._notification;
using Station.Components._utils;
using Station.Components._utils._steamConfig;
using Station.MVC.Controller;

namespace Station
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        
        public static int steamProcessId = 0;
        public static WindowEventTracker? windowEventTracker;
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

            SecondaryWindow secondaryWindow = new();
            secondaryWindow.Show();
            
            windowEventTracker = new WindowEventTracker(); // must be done here on main thread

            InitSentry();
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += UnhandledExceptionHandler;
            currentDomain.ProcessExit += ProcessExitHandler;
            CheckStorage();

            MainController.StartProgram();
        }

        /// <summary>
        /// Check the local storage, sending a sentry error message if there is less than 10GB of free space.
        /// </summary>
        private static void CheckStorage()
        {
            int? freeStorage = CommandLine.GetFreeStorage();
            if (freeStorage is < 10)
            {
                SentrySdk.CaptureMessage("Low memory detected (" + freeStorage + ") at: " +
                                         (Environment.GetEnvironmentVariable("LabLocation",
                                             EnvironmentVariableTarget.Process) ?? "Unknown"));
            }
        }

        private static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs args)
        {
            Exception e = (Exception)args.ExceptionObject;
            Logger.WriteLog("UnhandledExceptionHandler caught: " + e.Message, MockConsole.LogLevel.Error);
            Logger.WriteLog($"Runtime terminating: {args.IsTerminating}", MockConsole.LogLevel.Error);
            Logger.WorkQueue();
            MessageController.SendResponse("Android", "Station", "SetValue:status:Off");
            MessageController.SendResponse("Android", "Station", "SetValue:gameName:Unexpected error occured, please restart station");
            MessageController.SendResponse("Android", "Station", "SetValue:gameId:");
            try
            {
                SentrySdk.CaptureException(e);
            }
            catch (Exception e2)
            {
                Logger.WriteLog("UnhandledExceptionHandler caught while reporting: " + e2.Message, MockConsole.LogLevel.Error);
            }
        }

        private static void ProcessExitHandler(object? sender, EventArgs args)
        {
            Logger.WriteLog($"Process Exiting. Sender: {sender}, Event: {args}", MockConsole.LogLevel.Verbose);
            Logger.WorkQueue();
            MessageController.SendResponse("Android", "Station", "SetValue:status:Off");
            MessageController.SendResponse("Android", "Station", "SetValue:gameName:");
            MessageController.SendResponse("Android", "Station", "SetValue:gameId:");

            //Shut down the pipe server if running
            WrapperManager.ClosePipeServer();

            //Shut down any OpenVR systems
            MainController.openVrManager?.OpenVrSystem?.Shutdown();
        }

        private static void InitSentry()
        {
#if DEBUG
            var sentryDsn = "https://ca9abb6c77444340802da0c5a3805841@o1294571.ingest.sentry.io/6704982"; //Development
#elif RELEASE
	        var sentryDsn = "https://812f2b29bf3c4d129071683c7cf62361@o1294571.ingest.sentry.io/6518754"; //Production
#endif
            if (sentryDsn.Length <= 0) return;
            
            SentrySdk.Init(options =>
            {
                options.Dsn = sentryDsn;
                options.Debug = false;
                options.TracesSampleRate = 0.1;

                options.SetBeforeSend(sentryEvent =>
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
