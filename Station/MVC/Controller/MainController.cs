using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using LeadMeLabsLibrary;
using Newtonsoft.Json.Linq;
using Sentry;
using Station._config;
using Station.Components._enums;
using Station.Components._managers;
using Station.Components._monitoring;
using Station.Components._network;
using Station.Components._notification;
using Station.Components._openvr;
using Station.Components._organisers;
using Station.Components._profiles;
using Station.Components._utils;
using Station.Components._utils._steamConfig;
using Station.Components._wrapper.steam;
using Station.QA;

namespace Station.MVC.Controller;

//TODO finish the UiController implementations and fix the errors

/// <summary>
/// A class to control the main aspects of the program and hold static values for
/// use in other files. Primary function is to set up the localEndPoint (IPEndPoint) 
/// and start a server on the specific port.
/// </summary>
public static class MainController
{
    /// <summary>
    /// IPEndPoint representing the server that is running on the android tablet.
    /// </summary>
    public static IPEndPoint remoteEndPoint = new(IPAddress.None, 0);

    /// <summary>
    /// An integer representing the port of the NUC machine.
    /// </summary>
    private const int NucPort = 55556;

    /// <summary>
    /// IPEndPoint representing the server that is running on the local machine.
    /// </summary>
    public static IPEndPoint localEndPoint = new(IPAddress.None, 0);

    /// <summary>
    /// An integer representing the port of the local machine.
    /// </summary>
    private const int LocalPort = 55557;

    /// <summary>
    /// Access to the thread running the server thread
    /// </summary>
    private static Thread? serverThread;

    /// <summary>
    /// Access to the thread running the server
    /// </summary>
    private static ServerThread? server;

    /// <summary>
    /// Access the non-static classes of the wrapper manager.
    /// </summary>
    public static WrapperManager? wrapperManager;

    /// <summary>
    /// Access the non-static classes of the openvr manager
    /// </summary>
    public static OpenVrManager? openVrManager;

    public static string? macAddress;
    private static string? versionNumber;
    private static Timer? variableCheck;

    /// <summary>
    /// Starts the server running on the local machine
    /// </summary>
    public static async void StartProgram()
    {
        //Update the power settings
        Helper.FireAndForget(Task.Run(() => UiController.UpdateStationPowerStatus("On")));
        
        // Set and log version information
        SetVersionInformation();
        Environment.SetEnvironmentVariable("POWERSHELL_TELEMETRY_OPTOUT", "1");
        
        // Load environment variables
        bool envVariablesLoaded = await LoadEnvironmentVariablesAsync();
        
        // Setup server details
        bool serverSetupSuccessful = await SetupServerDetailsAsync();
        if (!serverSetupSuccessful)
        {
            Logger.WriteLog("Server details were not collected.", Enums.LogLevel.Error);
            return;
        }

        if (!envVariablesLoaded)
        {
            UiController.UpdateCurrentState("No config...");
            return;
        }
        
        // Initialise Segment
        Helper.FireAndForget(Task.Run(Components._segment.Segment.Initialise));
        
        //Do not continue if the NUC address is not supplied
        bool collectedIpAddress = SetRemoteEndPoint();
        if (!collectedIpAddress)
        {
            Logger.WriteLog("Could not collect saved NUC address.", Enums.LogLevel.Error);
            return;
        }

        // Update the Station mode (this controls the VR status view on the home page)
        Helper.FireAndForget(Task.Run(() => UiController.UpdateStationMode(Helper.GetStationMode().Equals(Helper.STATION_MODE_VR))));

        // Set the id of the Station for UI binding
        Helper.FireAndForget(Task.Run(() => UiController.UpdateStationId(Environment.GetEnvironmentVariable("stationId", EnvironmentVariableTarget.Process) ?? "")));

        // Continue with additional tasks if environment variables are loaded successfully
        Helper.FireAndForget(Task.Run(() => MockConsole.WriteLine("ENV variables loaded", Enums.LogLevel.Error)));
        
        // Validate the Station install location
        Helper.FireAndForget(Task.Run(() => ValidateInstall("Station")));
        
        // Collect audio devices and videos before starting the server
        var initialiseTasks = new List<Task>
        {
            Task.Run(AudioManager.Initialise),
            Task.Run(VideoManager.Initialise),
            Task.Run(FileManager.Initialise),
        };
        // Wait for initialise tasks in parallel
        await Task.WhenAll(initialiseTasks);
        
        // Additional tasks
        ThumbnailOrganiser.LoadCache();
        StartServer();
        
        //Cannot be any higher - encryption key does not exist before the DotEnv.Load()
        new Task(Initialisation).Start(); //Call as a new task to stop UI and server start up from hanging whilst reading the files
        
        if (Environment.GetEnvironmentVariable("IdleMode", EnvironmentVariableTarget.User) != null)
        {
            InternalDebugger.SetIdleModeActive(Environment.GetEnvironmentVariable("IdleMode", EnvironmentVariableTarget.User).Equals("On"));
        }
        if (InternalDebugger.GetIdleModeActive())
        {
            ModeTracker.Initialise(); //Start tracking any idle time
        }
    }
    
    /// <summary>
    /// Sets the version information in the view model and then logs version information, including the current version
    /// and a loading message.
    /// </summary>
    private static void SetVersionInformation()
    {
        string currentVersion = Updater.GetVersionNumber();
        string currentName = "Ice Cream Sandwich";
        
        Helper.FireAndForget(Task.Run(() => UiController.UpdateSoftwareDetails("versionNumber", currentVersion)));
        Helper.FireAndForget(Task.Run(() => UiController.UpdateSoftwareDetails("versionName", currentName)));
        
        Helper.FireAndForget(Task.Run(() => Logger.WriteLog($"Version number: {currentVersion}, name: {currentName}", Enums.LogLevel.Error)));
        Helper.FireAndForget(Task.Run(() => MockConsole.WriteLine("Loading ENV variables", Enums.LogLevel.Error)));
    }
    
    /// <summary>
    /// Loads environment variables asynchronously and handles any exceptions that may occur.
    /// </summary>
    /// <returns>True if the environment variables are loaded successfully, false otherwise.</returns>
    private static async Task<bool> LoadEnvironmentVariablesAsync()
    {
        try
        {
            return await DotEnv.Load();
        }
        catch (Exception ex)
        {
            Logger.WriteLog("Failed loading ENV variables", Enums.LogLevel.Error);
            Logger.WriteLog(ex, Enums.LogLevel.Error);
            return false;
        }
    }

    /// <summary>
    /// Initialise the necessary classes for the software to run.
    /// </summary>
    private static void Initialisation()
    {
        // Run the local Quality checks before continuing with the setup
        try
        {
            QualityManager.HandleLocalQualityAssurance(true);
        }
        catch (Exception e)
        {
            Logger.WriteLog($"StartProgram - Sentry Exception: {e}", Enums.LogLevel.Error);
            SentrySdk.CaptureException(e);
        }
        
        //Cannot be any higher - encryption key does not exist before the DotEnv.Load()
        InitialConnection();
        
        ScheduledTaskQueue.EnqueueTask(() => SessionController.UpdateState(State.Launching), TimeSpan.FromSeconds(0));
        ScheduledTaskQueue.EnqueueTask(() => SessionController.UpdateState(State.Initialising), TimeSpan.FromSeconds(2));

        // Schedule the function to run after a 5-minute delay (300,000 milliseconds)
        variableCheck = new Timer(OnTimerCallback, null, 300000, Timeout.Infinite);

        if (!Helper.GetStationMode().Equals(Helper.STATION_MODE_APPLIANCE))
        {
            if (Helper.GetStationMode().Equals(Helper.STATION_MODE_VR))
            {
                // Check SteamVRs background image
                SteamScripts.CheckSteamVrHomeImage();
                openVrManager = new OpenVrManager();
            }

            wrapperManager = new WrapperManager();

            //Launch the custom wrapper application here
            wrapperManager.Startup();

            //Use to monitor restart of the application
            StationMonitoringThread.InitializeMonitoring();
        }

        if (Environment.GetEnvironmentVariable("NucAddress", EnvironmentVariableTarget.Process) == null)
        {
            Logger.WriteLog($"Expected NUC address: is null, check environment variables are set.", Enums.LogLevel.Normal);
            return;
        }
        
        Logger.WriteLog($"Expected NUC address: {Environment.GetEnvironmentVariable("NucAddress", EnvironmentVariableTarget.Process)}", Enums.LogLevel.Normal);
        if (Helper.GetStationMode().Equals(Helper.STATION_MODE_APPLIANCE)) return;
        StateController.InitialStartUp();
        
        // Safe cast for potential content profile
        ContentProfile? contentProfile = Profile.CastToType<ContentProfile>(SessionController.StationProfile);
        if (Helper.GetStationMode().Equals(Helper.STATION_MODE_VR) ||
            (contentProfile != null && contentProfile.DoesProfileHaveAccount("Steam")))
        {
            new Thread(() => SteamConfig.VerifySteamConfig(true)).Start();
        }
    }
    
    /// <summary>
    /// Send off the version and lab location, plus any other details at the start of the communication with the NUC.
    /// </summary>
    public static void InitialConnection()
    {
        JObject message = new JObject
        {
            { "Version", Updater.GetVersionNumber() },
            { "LabLocation", Environment.GetEnvironmentVariable("LabLocation",
                EnvironmentVariableTarget.Process) }
        };

        MessageController.SendResponse("NUC", "Environment", message.ToString());
        Task.Delay(2000).Wait(); //Forced delay while waiting for the NUC response
    }

    /// <summary>
    /// Re-check any variables that may have changed in the first 5 minutes of operation.
    /// </summary>
    private static void OnTimerCallback(object? state)
    {
        try
        {
            IPAddress? ip = SystemInformation.GetIPAddress();
            if(ip == null || !ip.Address.Equals(localEndPoint.Address.Address))
            {
                throw new Exception($"ReChecked IP address is not the same. Original: {localEndPoint.Address.Address}, ReChecked: {ip.Address} at site: " + Helper.GetLabLocationWithStationId());
            }

            Logger.WriteLog("Re-checking software details after 5 minutes of operation.", Enums.LogLevel.Normal);
            Logger.WriteLog("Server IP Address is: " + ip, Enums.LogLevel.Normal);
            Logger.WriteLog("MAC Address is: " + SystemInformation.GetMACAddress(), Enums.LogLevel.Normal);
            Logger.WriteLog("Version is: " + Updater.GetVersionNumber(), Enums.LogLevel.Normal);
        }
        catch (Exception e)
        {
            Logger.WriteLog($"OnTimerCallback - Sentry Exception: {e}", Enums.LogLevel.Error);
            SentrySdk.CaptureException(e);
        }
    }

    /// <summary>
    /// Stop and dispose of the variable timer and all resources associated with it.
    /// </summary>
    private static void StopVariableTimer()
    {
        if (variableCheck == null) return;

        try
        {
            variableCheck.Change(Timeout.Infinite, Timeout.Infinite);
            variableCheck.Dispose();
        }
        catch (ObjectDisposedException e)
        {
            Logger.WriteLog($"StopVariableTimer: variableCheck has already been disposed - {e}", Enums.LogLevel.Info);
        }
    }

    /// <summary>
    /// Stop all instances of the Station program running
    /// </summary>
    public static void StopProgram(bool restarting)
    {
        if (!restarting)
        {
            UiController.UpdateStationPowerStatus("Off");
            UiController.UpdateCurrentState("Stopped...");
        }
        
        StopVariableTimer();
        StationMonitoringThread.StopMonitoring();
        StopServer();
        wrapperManager?.ShutDownWrapper();
        Logger.WriteLog("Station stopped", Enums.LogLevel.Normal);
    }

    /// <summary>
    /// Restart the station program
    /// </summary>
    public static async void RestartProgram()
    {
        StopProgram(true);
        Logger.WriteLog("Station restarting", Enums.LogLevel.Normal);
        await Task.Delay(2000);
        StartProgram();
    }

    private static void StartServer()
    {
        server = new ServerThread();
        serverThread = new Thread(async () => await server.RunAsync());
        serverThread.Start();
    }

    public static void StopServer()
    {
        server?.Stop();
        serverThread?.Interrupt();
        MockConsole.WriteLine("Server stopped", Enums.LogLevel.Debug);
    }

    /// <summary>
    /// Collect the necessary system details for starting the service. Including the IP address, mac address
    /// and the current version number.
    /// </summary>
    // private static bool SetupServerDetails()
    private static async Task<bool> SetupServerDetailsAsync()
    {
        try
        {
            // Offload IP retrieval to a background thread
            IPAddress? ip = await Task.Run(AttemptIpAddressRetrieval);
            if (ip == null) throw new Exception("Manager class: Server IP Address could not be found");

            // Retrieve previous IP address from environment variables
            string? ipAddressPreviousLaunch = Environment.GetEnvironmentVariable("previousIpAddress", EnvironmentVariableTarget.User);
            if (ipAddressPreviousLaunch != null && ipAddressPreviousLaunch != ip.Address.ToString()) 
            {
                // Log IP address change in the background
                await Task.Run(() =>
                {
                    SentrySdk.CaptureMessage($"Detected IP Address change at: {Helper.GetLabLocationWithStationId()}", SentryLevel.Fatal);
                });
            }

            // Set the new IP address asynchronously
            await Task.Run(() => Environment.SetEnvironmentVariable("previousIpAddress", ip.Address.ToString(), EnvironmentVariableTarget.User));

            // Run the following independent tasks in parallel
            var macAddressTask = Task.Run(SystemInformation.GetMACAddress);
            var versionNumberTask = Task.Run(Updater.GetVersionNumber);
            var localEndPointTask = Task.Run(() => new IPEndPoint(ip.Address, LocalPort));

            // Await all parallel tasks
            await Task.WhenAll(macAddressTask, versionNumberTask, localEndPointTask);
            macAddress = await macAddressTask;
            versionNumber = await versionNumberTask;
            localEndPoint = await localEndPointTask;

            // Fire-and-forget for UI updates and logging with exception handling
            Helper.FireAndForget(Task.Run(() => UiController.UpdateSoftwareDetails("ipAddress", localEndPoint.Address.ToString())));
            Helper.FireAndForget(Task.Run(() => UiController.UpdateSoftwareDetails("macAddress", macAddress)));
            Helper.FireAndForget(Task.Run(() => Logger.WriteLog("Server IP Address is: " + localEndPoint.Address, Enums.LogLevel.Normal)));
            Helper.FireAndForget(Task.Run(() => Logger.WriteLog("MAC Address is: " + macAddress, Enums.LogLevel.Normal)));
            Helper.FireAndForget(Task.Run(() => Logger.WriteLog("Version is: " + versionNumber, Enums.LogLevel.Normal)));

            return true;
        }
        catch (Exception e)
        {
            await Task.WhenAll(
                Task.Run(() => Logger.WriteLog($"SetupServerDetails - Sentry Exception: {e}", Enums.LogLevel.Error)),
                Task.Run(() => SentrySdk.CaptureException(e))
            );
            return false;
        }
    }

    /// <summary>
    /// Attempt to collect the local IP address, the function will try a total of 5 times to find the IP
    /// address. The attempts are done 15 seconds apart.
    /// </summary>
    /// <returns></returns>
    private static IPAddress? AttemptIpAddressRetrieval()
    {
        int attemptLimit = 5;
        int attempts = 0;
        IPAddress? ip = null;

        while (attempts < attemptLimit)
        {
            try
            {
                ip = SystemInformation.GetIPAddress();
                if (ip != null) break;
            }
            catch (Exception e)
            {
                Logger.WriteLog($"Unexpected exception AttemptIPAddressRetrieval (attempt {attempts}): {e}", Enums.LogLevel.Error);
            }

            Task.Delay(15000).Wait();
            attempts++;
        }

        return ip;
    }

    /// <summary>
    /// Sets the remote end point for communication based on the IP address retrieved from the environment variable "NucAddress".
    /// </summary>
    /// <returns>
    /// True if the remote end point was successfully set; otherwise, false.
    /// </returns>
    private static bool SetRemoteEndPoint()
    {
        try
        {
            IPAddress ipAddress =
                IPAddress.Parse(
                    (ReadOnlySpan<char>)Environment.GetEnvironmentVariable("NucAddress",
                        EnvironmentVariableTarget.Process));

            remoteEndPoint = new IPEndPoint(ipAddress, NucPort);
            return true;
        }
        catch (Exception e)
        {
            Logger.WriteLog($"SetRemoteEndPoint - Sentry Exception: {e}", Enums.LogLevel.Error);
            SentrySdk.CaptureException(e);
        }

        return false;
    }
    
    /// <summary>
    /// Validates the installation directory of a specific software type using the RegistryHelper.
    /// Checks if the installation location is correct and logs relevant information.
    /// </summary>
    /// <param name="softwareType">The type of software to validate (e.g., "Station" or "NUC").</param>
    private static void ValidateInstall(string softwareType)
    {
        Tuple<bool, string> output = RegistryHelper.ValidateElectronInstallDirectory(softwareType);
        if (output.Item2.Equals("Install location already correct.")) return;
        
        Logger.WriteLog($"{output.Item2}", Enums.LogLevel.Error);
        SentrySdk.CaptureMessage($"{output.Item2}. Location: {Helper.GetLabLocationWithStationId()}");
    }
}
