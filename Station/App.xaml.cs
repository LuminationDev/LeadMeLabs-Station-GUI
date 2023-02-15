using System;
using System.Windows;
using Sentry;

namespace Station
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            MainWindow wnd = new MainWindow();
            wnd.Show();

            if (e.Args.Length > 0 && e.Args[0].Trim().ToLower() == "writeversion")
            {
                Updater.generateVersion();
                Environment.Exit(1);
                return;
            }

            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += UnhandledExceptionHandler;
            currentDomain.ProcessExit += ProcessExitHandler;

#if !DEVDEBUG && !DEVRELEASE
            initSentry();
#endif
            Manager.startProgram();
        }

        static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs args)
        {
            Exception e = (Exception)args.ExceptionObject;
            Logger.WriteLog("UnhandledExceptionHandler caught : " + e.Message, MockConsole.LogLevel.Error);
            Logger.WriteLog($"Runtime terminating: {args.IsTerminating}", MockConsole.LogLevel.Error);
            Logger.WorkQueue();
            Manager.sendResponse("Android", "Station", "SetValue:status:Off");
            Manager.sendResponse("Android", "Station", "SetValue:gameName:Unexpected error occured, please restart station");
            Manager.sendResponse("Android", "Station", "SetValue:gameId:");
        }

        static void ProcessExitHandler(object? sender, EventArgs args)
        {
            Logger.WriteLog("Process Exiting", MockConsole.LogLevel.Verbose);
            Logger.WorkQueue();
            Manager.sendResponse("Android", "Station", "SetValue:status:Off");
            Manager.sendResponse("Android", "Station", "SetValue:gameName:");
            Manager.sendResponse("Android", "Station", "SetValue:gameId:");
        }

        public static void initSentry()
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

                    options.BeforeSend = sentryEvent =>
                    {
                        if (sentryEvent.Exception != null
                          && sentryEvent.Exception.Message.Contains("Aggregate Exception")
                          && sentryEvent.Exception.Message.Contains("WSACancelBlockingCall"))
                        {

                            return null; // Don't send this event to Sentry
                        }

                        Console.WriteLine(sentryEvent.Exception);
                        Console.WriteLine(sentryEvent.Message);
                        Logger.WriteLog("Sentry Exception", MockConsole.LogLevel.Error);
                        Logger.WriteLog(sentryEvent.Exception, MockConsole.LogLevel.Error);

                        sentryEvent.ServerName = null; // Never send Server Name to Sentry
                        return sentryEvent;
                    };
                });
                SentrySdk.ConfigureScope(scope =>
                {
                    scope.SetTag("lab_location", Environment.GetEnvironmentVariable("LabLocation") ?? "Unknown");
                    scope.SetTag("station_id", Environment.GetEnvironmentVariable("StationId") ?? "Unknown");
                    scope.SetTag("headset_type", Environment.GetEnvironmentVariable("HeadsetType") ?? "Unknown");
                    scope.SetTag("room", Environment.GetEnvironmentVariable("room") ?? "Unknown");
                });
            }
        }
    }
}
