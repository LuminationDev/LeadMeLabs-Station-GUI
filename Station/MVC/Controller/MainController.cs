using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using LeadMeLabsLibrary;
using Sentry;
using Station._config;
using Station.Components._monitoring;
using Station.Components._network;
using Station.Components._notification;
using Station.Components._openvr;
using Station.Components._organisers;
using Station.Components._utils;
using Station.Components._utils._steamConfig;
using Station.Components._wrapper;

namespace Station.MVC.Controller;

/// <summary>
/// A class to control the main aspects of the program and hold static values for
/// use in other files. Primary function is to setup the localEndPoint (IPEndPoint) 
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
    public static OpenVRManager? openVrManager;

    public static string? macAddress;
    private static string? versionNumber;
    private static Timer? variableCheck;

    public static bool isNucUtf8 = true;

    /// <summary>
    /// Starts the server running on the local machine
    /// </summary>
    public static async void StartProgram()
    {
        //Update the power settings
        UIController.UpdateStationPowerStatus("On");
        
        // Set and log version information
        SetVersionInformation();
        Environment.SetEnvironmentVariable("POWERSHELL_TELEMETRY_OPTOUT", "1");
        
        // Load environment variables
        bool envVariablesLoaded = await LoadEnvironmentVariablesAsync();
        
        // Setup server details
        bool serverSetupSuccessful = SetupServerDetails();

        if (!serverSetupSuccessful)
        {
            Logger.WriteLog("Server details were not collected.", MockConsole.LogLevel.Error);
            return;
        }

        if (!envVariablesLoaded)
        {
            UIController.UpdateCurrentStatus("No config...");
            return;
        }
        
        // Continue with additional tasks if environment variables are loaded successfully
        MockConsole.WriteLine("ENV variables loaded", MockConsole.LogLevel.Error);

        ValidateInstall("Station");
        
        // Additional tasks
        ThumbnailOrganiser.LoadCache();
        StartServer();
        ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage($"SoftwareState,Launching Software"), TimeSpan.FromSeconds(0));
        new Thread(Initialisation).Start(); //Call as a new task to stop UI and server start up from hanging whilst reading the files
    }
    
    /// <summary>
    /// Sets the version information in the view model and then logs version information, including the current version
    /// and a loading message.
    /// </summary>
    private static void SetVersionInformation()
    {
        string? currentVersion = Updater.GetVersionNumber();
        string? currentName = "Ice Cream Sandwich";
        
        UIController.UpdateSoftwareDetails("versionNumber", currentVersion ?? "Unavailable");
        UIController.UpdateSoftwareDetails("versionName", currentName);
        
        Logger.WriteLog($"Version number: {currentVersion}, name: {currentName}", MockConsole.LogLevel.Error);
        MockConsole.WriteLine("Loading ENV variables", MockConsole.LogLevel.Error);
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
            Logger.WriteLog("Failed loading ENV variables", MockConsole.LogLevel.Error);
            Logger.WriteLog(ex, MockConsole.LogLevel.Error);
            return false;
        }
    }

    /// <summary>
    /// Initialise the necessary classes for the software to run.
    /// </summary>
    private static void Initialisation()
    {
        ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage($"SoftwareState,Initialising configuration"), TimeSpan.FromSeconds(2));

        // Schedule the function to run after a 5-minute delay (300,000 milliseconds)
        variableCheck = new Timer(OnTimerCallback, null, 300000, Timeout.Infinite);

        if (!Helper.GetStationMode().Equals(Helper.STATION_MODE_APPLIANCE))
        {
            openVrManager = new OpenVRManager();
            wrapperManager = new WrapperManager();

            //Launch the custom wrapper application here
            wrapperManager.Startup();

            //Use to monitor SetVol and restart application
            StationMonitoringThread.InitializeMonitoring();
        }

        if (Environment.GetEnvironmentVariable("NucAddress", EnvironmentVariableTarget.Process) == null)
        {
            Logger.WriteLog($"Expected NUC address: is null, check environment variables are set.", MockConsole.LogLevel.Normal);
            return;
        }
        
        Logger.WriteLog($"Expected NUC address: {Environment.GetEnvironmentVariable("NucAddress", EnvironmentVariableTarget.Process)}", MockConsole.LogLevel.Normal);
        SetRemoteEndPoint();
        
        if (Helper.GetStationMode().Equals(Helper.STATION_MODE_APPLIANCE)) return;
        MessageController.InitialStartUp();
        new Thread(() => SteamConfig.VerifySteamConfig(true)).Start();
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
                throw new Exception($"ReChecked IP address is not the same. Original: {localEndPoint.Address.Address}, ReChecked: {ip.Address}");
            }

            Logger.WriteLog("Re-checking software details after 5 minutes of operation.", MockConsole.LogLevel.Normal);
            Logger.WriteLog("Server IP Address is: " + ip, MockConsole.LogLevel.Normal);
            Logger.WriteLog("MAC Address is: " + SystemInformation.GetMACAddress() ?? "Unknown", MockConsole.LogLevel.Normal);
            Logger.WriteLog("Version is: " + Updater.GetVersionNumber() ?? "Unknown", MockConsole.LogLevel.Normal);
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            Logger.WriteLog($"Unexpected exception : {e}", MockConsole.LogLevel.Error);
        }
    }

    /// <summary>
    /// Stop and dispose of the variable timer and all resources associated with it.
    /// </summary>
    private static void StopVariableTimer()
    {
        if (variableCheck == null) return;
        
        variableCheck.Change(Timeout.Infinite, Timeout.Infinite);
        variableCheck.Dispose();
    }

    /// <summary>
    /// Stop all instances of the Station program running
    /// </summary>
    public static void StopProgram(bool restarting)
    {
        if (!restarting)
        {
            UIController.UpdateStationPowerStatus("Off");
            UIController.UpdateCurrentStatus("Stopped...");
        }
        
        StopVariableTimer();
        StationMonitoringThread.StopMonitoring();
        StopServer();
        wrapperManager?.ShutDownWrapper();
        Logger.WriteLog("Station stopped", MockConsole.LogLevel.Normal);
    }

    /// <summary>
    /// Restarts the server and associated functions on the local machine
    /// </summary>
    public static async void RestartProgram()
    {
        StopProgram(true);
        Logger.WriteLog("Station restarting", MockConsole.LogLevel.Normal);
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
        MockConsole.WriteLine("Server stopped", MockConsole.LogLevel.Debug);
    }

    /// <summary>
    /// Collect the necessary system details for starting the service. Including the IP address, mac address
    /// and the current version number.
    /// </summary>
    private static bool SetupServerDetails()
    {
        try
        {
            IPAddress? ip = AttemptIpAddressRetrieval();
            if (ip == null) throw new Exception("Manager class: Server IP Address could not be found");

            macAddress = SystemInformation.GetMACAddress();
            versionNumber = Updater.GetVersionNumber() ?? "Unknown";
            localEndPoint = new IPEndPoint(ip.Address, LocalPort);
            
            //Update the home panel UI
            UIController.UpdateSoftwareDetails("ipAddress", localEndPoint.Address.ToString());
            UIController.UpdateSoftwareDetails("macAddress", macAddress);

            Logger.WriteLog("Server IP Address is: " + localEndPoint.Address.ToString(), MockConsole.LogLevel.Normal);
            Logger.WriteLog("MAC Address is: " + macAddress, MockConsole.LogLevel.Normal);
            Logger.WriteLog("Version is: " + versionNumber, MockConsole.LogLevel.Normal);
            return true;
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            Logger.WriteLog($"Unexpected exception : {e}", MockConsole.LogLevel.Error);
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
                Logger.WriteLog($"Unexpected exception AttemptIPAddressRetrieval (attempt {attempts}): {e}", MockConsole.LogLevel.Error);
            }

            Task.Delay(15000).Wait();
            attempts++;
        }

        return ip;
    }

    private static void SetRemoteEndPoint()
    {
        remoteEndPoint = new IPEndPoint(IPAddress.Parse((ReadOnlySpan<char>)Environment.GetEnvironmentVariable("NucAddress", EnvironmentVariableTarget.Process)), NucPort);
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
        
        Logger.WriteLog($"{output.Item2}", MockConsole.LogLevel.Error);
        SentrySdk.CaptureMessage($"{output.Item2}. Location: {Environment.GetEnvironmentVariable("LabLocation", EnvironmentVariableTarget.Process) ?? "Unknown"}");
    }
}
