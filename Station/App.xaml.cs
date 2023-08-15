using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using Sentry;
using Silk.NET.Core;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.OpenXR;
using Action = System.Action;
using Application = System.Windows.Application;
using Session = Silk.NET.OpenXR.Session;

namespace Station
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private List<string> Extensions = new List<string>();
        public Instance instance;
        public ulong system_id = 0;
        protected internal static Result CheckResult(Result result, string forFunction)
        {
            if ((int)result < 0)
            {
                return result;
                // Window.GenerateGenericError(null, 
                //     $"Only SteamVR is supported as an OpenXR runtime. Make sure it is set & running:\nSteamVR Settings > Show Advanced Settings > Developer > Set SteamVR as OpenXR Runtime\n\nAlso verify the correct High-Speed GPU is running this program.\n\nCode: {result} ({result:X}) in " + forFunction + "\n\nStack Trace: " + (new StackTrace()).ToString(),
                //     "OpenXR Error: is SteamVR set as the OpenXR runtime? Correct GPU?");
            }

            return result;
        }
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            unsafe
            {
                if (e.Args.Length > 0 && e.Args[0].Trim().ToLower() == "writeversion")
                {
                    Updater.GenerateVersion();
                    Environment.Exit(1);
                    return;
                }

                // Environment.SetEnvironmentVariable("XR_RUNTIME_JSON", "C:/Program Files (x86)/Steam/steamapps/common/SteamVR/steamxr_win64.json");
                Silk.NET.OpenXR.Instance ins = new Instance();
                // var api = Silk.NET.OpenXR.XR.GetApi();
                var api = new XR(XR.CreateDefaultContext(new MyOpenXRLibraryNameContainer().GetLibraryNames()));
                InstanceCreateInfo instanceCreateInfo;

                var appInfo = new ApplicationInfo()
                {
                    ApiVersion = new Version64(1, 0, 9)
                };
                Span<byte> appName = new Span<byte>(appInfo.ApplicationName, 128);
                Span<byte> engName = new Span<byte>(appInfo.EngineName, 128);
                SilkMarshal.StringIntoSpan(System.AppDomain.CurrentDomain.FriendlyName, appName);
                SilkMarshal.StringIntoSpan("", engName);
                // Extensions.Add("XR_KHR_vulkan_enable");
                // Extensions.Add("XR_EXT_hp_mixed_reality_controller");
                // Extensions.Add("XR_HTC_vive_cosmos_controller_interaction");
                // Extensions.Add("XR_MSFT_hand_interaction");
                // Extensions.Add("XR_EXT_samsung_odyssey_controller");
                // Extensions.Add("XR_HTC_vive_focus3_controller_interaction");
                // Extensions.Add("XR_HTC_hand_interaction");
                var requestedExtensions = SilkMarshal.StringArrayToPtr(Extensions);
                instanceCreateInfo = new InstanceCreateInfo
                (
                    applicationInfo: appInfo,
                    enabledExtensionCount: (uint)Extensions.Count,
                    enabledExtensionNames: (byte**)requestedExtensions
                );
                Result result = api.CreateInstance(in instanceCreateInfo, ref instance);
                InstanceProperties properties = new();
                Result result2 = api.GetInstanceProperties(instance, ref properties);
                var runtimeName = SilkMarshal.PtrToString((nint)properties.RuntimeName);
                var runtimeVersion = ((Version)(Version64)properties.RuntimeVersion).ToString(3);
                var getInfo = new SystemGetInfo(formFactor: FormFactor.HeadMountedDisplay);
                Result result3 = api.GetSystem(instance, in getInfo, ref system_id);
                Session session;
                SessionCreateInfo session_create_info = new SessionCreateInfo() {
                    Type = StructureType.SessionCreateInfo,
                    SystemId = system_id
                };
                Result result4 = api.CreateSession(instance, session_create_info, &session);
                //
                // Extensions.Clear();
                // //Extensions.Add("XR_KHR_vulkan_enable2");
                // Extensions.Add("XR_KHR_vulkan_enable");
                // Extensions.Add("XR_EXT_hp_mixed_reality_controller");
                // Extensions.Add("XR_HTC_vive_cosmos_controller_interaction");
                // Extensions.Add("XR_MSFT_hand_interaction");
                // Extensions.Add("XR_EXT_samsung_odyssey_controller");
                // Extensions.Add("XR_HTC_vive_focus3_controller_interaction");
                // Extensions.Add("XR_HTC_hand_interaction");
                //
                uint propCount = 0;
                // api.EnumerateInstanceExtensionProperties((byte*)null, 0, &propCount, null);
                //
                // ExtensionProperties[] props = new ExtensionProperties[propCount];
                // for (int i = 0; i < props.Length; i++) props[i].Type = StructureType.ExtensionProperties;
                //
                // fixed (ExtensionProperties* pptr = &props[0])
                //     api.EnumerateInstanceExtensionProperties((byte*)null, propCount, ref propCount, pptr);
                //
                // List<string> AvailableExtensions = new List<string>();
                // for (int i = 0; i < props.Length; i++)
                // {
                //     fixed (void* nptr = props[i].ExtensionName)
                //         AvailableExtensions.Add(Marshal.PtrToStringAnsi(new System.IntPtr(nptr)));
                // }
                //
                // for (int i=0; i<Extensions.Count; i++)
                // {
                //     if (AvailableExtensions.Contains(Extensions[i]) == false)
                //     {
                //         Extensions.RemoveAt(i);
                //         i--;
                //     }
                // }
                //
                // InstanceCreateInfo instanceCreateInfo;
                //
                // var appInfo = new ApplicationInfo()
                // {
                //     ApiVersion = new Version64(1, 0, 9)
                // };
                // Silk.NET.OpenXR.Action a = new Silk.NET.OpenXR.Action();
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
