using System;
using System.Collections.Generic;
using System.Windows;
using LeadMeLabsLibrary;
using Sentry;
using Station.Components._commandLine;
using Station.Components._legacy;
using Station.Components._managers;
using Station.Components._utils;
using Station.Components._utils._steamConfig;
using Station.Components._version;
using Station.MVC.Controller;
using Station.MVC.View;

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
            SteamConfig.VerifySteamConfig();

            MainWindow mainWindow = new();
            mainWindow.Show();
            
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
                string msg =
                    $"Low memory detected ({freeStorage}) at: {(Environment.GetEnvironmentVariable("LabLocation", EnvironmentVariableTarget.Process) ?? "Unknown")}";
                Logger.WriteLog($"CheckStorage - Sentry Message: {msg}", Enums.LogLevel.Error);
                SentrySdk.CaptureMessage(msg);
            }
        }

        static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs args)
        {
            Exception e = (Exception)args.ExceptionObject;
            Logger.WriteLog("UnhandledExceptionHandler caught: " + e.Message, Enums.LogLevel.Error);
            Logger.WriteLog($"Runtime terminating: {args.IsTerminating}", Enums.LogLevel.Error);
            Logger.WorkQueue();
            
            Dictionary<string, object?> stateValues = new()
            {
                { "status", "Off" },
                { "state", "" },
                { "gameName", "Unexpected error occured, please restart station" },
                { "gameId", "" }
            };
            StateController.UpdateStatusBunch(stateValues);

            try
            {
                SentrySdk.CaptureException(e);
            }
            catch (Exception e2)
            {
                Logger.WriteLog("UnhandledExceptionHandler caught while reporting: " + e2.Message, Enums.LogLevel.Error);
            }
        }

        static void ProcessExitHandler(object? sender, EventArgs args)
        {
            Logger.WriteLog($"Process Exiting. Sender: {sender}, Event: {args}", Enums.LogLevel.Verbose);
            Logger.WorkQueue();
            
            Dictionary<string, object?> stateValues = new()
            {
                { "status", "Off" },
                { "state", "" },
                { "gameName", "" },
                { "gameId", "" }
            };
            StateController.UpdateStatusBunch(stateValues);

            //Shut down the pipe server if running
            WrapperManager.ClosePipeServer();

            //Shut down any OpenVR systems
            MainController.openVrManager?.openVrSystem?.Shutdown();
        }

        private static void InitSentry()
        {
#if DEBUG
            var sentryDsn = "https://ca9abb6c77444340802da0c5a3805841@o1294571.ingest.sentry.io/6704982"; //Development
#elif RELEASE
	        var sentryDsn = "https://812f2b29bf3c4d129071683c7cf62361@o1294571.ingest.sentry.io/6518754"; //Production
#endif
            if (sentryDsn is not { Length: > 0 }) return;
            
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

                    Logger.WriteLog("Sentry Exception", Enums.LogLevel.Error);

                    if (sentryEvent.Exception != null)
                    {
                        Logger.WriteLog(sentryEvent.Exception, Enums.LogLevel.Error);
                    }
                    if (sentryEvent.Message != null)
                    {
                        Logger.WriteLog(sentryEvent.Message.ToString() ?? "No message", Enums.LogLevel.Error);
                    }

                    sentryEvent.ServerName = null; // Never send Server Name to Sentry
                    sentryEvent.SetTag("lab_location", Environment.GetEnvironmentVariable("LabLocation",
                        EnvironmentVariableTarget.Process) ?? "Unknown");
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
