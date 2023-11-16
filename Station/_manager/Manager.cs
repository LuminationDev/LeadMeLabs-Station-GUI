using Sentry;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using LeadMeLabsLibrary;
using Station._config;
using Station._monitoring;
using Station._network;
using Station._utils;

namespace Station
{
    /// <summary>
    /// A class to control the main aspects of the program and hold static values for
    /// use in other files. Primary function is to setup the localEndPoint (IPEndPoint) 
    /// and start a server on the specific port.
    /// </summary>
    public static class Manager
    {
        /// <summary>
        /// IPEndPoint representing the server that is running on the android tablet.
        /// </summary>
        public static IPEndPoint remoteEndPoint = new(IPAddress.None, 0);

        /// <summary>
        /// An integer representing the port of the NUC machine.
        /// </summary>
        private static readonly int NUCPort = 55556;

        /// <summary>
        /// IPEndPoint representing the server that is running on the local machine.
        /// </summary>
        public static IPEndPoint localEndPoint = new(IPAddress.None, 0);

        /// <summary>
        /// An integer representing the port of the local machine.
        /// </summary>
        private static readonly int localPort = 55557;

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
        public static OpenVRManager? openVRManager;

        public static string? macAddress = null;
        private static string? versionNumber = null;
        private static Timer? variableCheck;

        public static bool isNucUtf8 = true;

        /// <summary>
        /// Starts the server running on the local machine
        /// </summary>
        public static async void StartProgram()
        {
            MockConsole.ClearConsole();
            Logger.WriteLog("Loading ENV variables", MockConsole.LogLevel.Error);
            
            //Check if the Encryption key and env variables are set correctly before starting anything else
            bool result = false;
            try
            {
                result = await DotEnv.Load();
            }
            catch (Exception ex)
            {
                Logger.WriteLog($"DotEnv Error: {ex}", MockConsole.LogLevel.Error);
            }

            //Even if DotEnv fails, still load up the server details to show the details to the user.
            Logger.WriteLog("Setting up server.", MockConsole.LogLevel.Error);
            bool connected = SetupServerDetails();
            if (!connected)
            {
                Logger.WriteLog("Server details were not collected.", MockConsole.LogLevel.Error);
                return;
            }
            
            //Do not continue if environment variable file is incomplete
            if (!result)
            {
                App.SetWindowTitle("Station - Failed to load ENV variables.");
                Logger.WriteLog("Failed loading ENV variables", MockConsole.LogLevel.Error);
                return;
            }

            StartServer();
            
            //Cannot be any higher - encryption key does not exist before the DotEnv.Load()
            ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage($"SoftwareState,Launching Software"), TimeSpan.FromSeconds(0));

            App.SetWindowTitle($"Station({Environment.GetEnvironmentVariable("StationId", EnvironmentVariableTarget.Process)}) -- {localEndPoint.Address} -- {macAddress} -- {versionNumber}");
            Logger.WriteLog("ENV variables loaded", MockConsole.LogLevel.Error);
            
            //Call as a new task to stop UI and server start up from hanging whilst reading the files
            new Thread(Initialisation).Start();
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
                openVRManager = new OpenVRManager();
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
            if (!Helper.GetStationMode().Equals(Helper.STATION_MODE_APPLIANCE))
            {
                InitialStartUp();
                new Thread(() => SteamConfig.VerifySteamConfig(true)).Start();
            }
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
            if(variableCheck != null)
            {
                variableCheck.Change(Timeout.Infinite, Timeout.Infinite);
                variableCheck.Dispose();
            }
        }

        /// <summary>
        /// Stop all instances of the Station program running
        /// </summary>
        public static void StopProgram()
        {
            new Thread(() =>
            {
                StopVariableTimer();
                StationMonitoringThread.StopMonitoring();
                StopServer();
                wrapperManager?.ShutDownWrapper();
                Logger.WriteLog("Station stopped", MockConsole.LogLevel.Normal);
            }).Start();
        }

        /// <summary>
        /// Restart the station program
        /// </summary>
        public static async void RestartProgram()
        {
            StopProgram();
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
        /// On start up or NUC address change send the status, steam list and the current volume to the NUC.
        /// </summary>
        private static void InitialStartUp()
        {
            SendResponse("NUC", "Station", "SetValue:status:On");
            SendResponse("NUC", "Station", "SetValue:gameName:");
            SendResponse("Android", "Station", "SetValue:gameId:");
            SendResponse("NUC", "Station", $"SetValue:volume:{CommandLine.GetVolume()}");
        }

        /// <summary>
        /// Collect the necessary system details for starting the service. Including the IP address, mac address
        /// and the current version number.
        /// </summary>
        private static bool SetupServerDetails()
        {
            try
            {
                IPAddress? ip = AttemptIPAddressRetrieval();
                if (ip == null) throw new Exception("Manager class: Server IP Address could not be found");

                macAddress = SystemInformation.GetMACAddress() ?? "Unknown";
                versionNumber = Updater.GetVersionNumber() ?? "Unknown";
                localEndPoint = new IPEndPoint(ip.Address, localPort);

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
        private static IPAddress? AttemptIPAddressRetrieval()
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
            remoteEndPoint = new IPEndPoint(IPAddress.Parse((ReadOnlySpan<char>)Environment.GetEnvironmentVariable("NucAddress", EnvironmentVariableTarget.Process)), NUCPort);
        }

        /// <summary>
        /// Create a new script thread and start it, passing in the data collected from 
        /// the recently connected client.
        /// </summary>
        public static void RunScript(string data)
        {
            ScriptThread script = new(data);
            Thread scriptThread = new(script.Run);
            scriptThread.Start();
        }

        /// <summary>
        /// Send a response back to the android server detailing what has happened.
        /// </summary>
        public static void SendResponse(string destination, string actionNamespace, string? additionalData, bool writeToLog = true)
        {
            IPAddress? address = null;
            int? port = null;
            
            string source = $"Station,{Environment.GetEnvironmentVariable("StationId", EnvironmentVariableTarget.Process)}";
            string? response = $"{source}:{destination}:{actionNamespace}";
            if (additionalData != null)
            {
                response = $"{response}:{additionalData}";
            }
            
            if (destination.StartsWith("QA:"))
            {
                address = IPAddress.Parse(destination.Substring(3).Split(":")[0]);
                port = Int32.Parse(destination.Substring(3).Split(":")[1]);
                response = additionalData;
            }
            if (response == null) return;

            Logger.WriteLog($"Sending: {response}", MockConsole.LogLevel.Normal, writeToLog);

            string? key = Environment.GetEnvironmentVariable("AppKey", EnvironmentVariableTarget.Process);
            if (key is null) {
                Logger.WriteLog("Encryption key not set", MockConsole.LogLevel.Normal);
                return;
            }

            string encryptedText;
            //TODO determine connection type
            if (isNucUtf8)
            {
                encryptedText = EncryptionHelper.Encrypt(response, key);
            }
            else
            {
                encryptedText = EncryptionHelper.UnicodeEncrypt(response, key);
            }
            
            SocketClient client = new(encryptedText);
            if (address != null && port != null)
            {
                client.Send(writeToLog, address, port);
            }
            else
            {
                client.Send(writeToLog);
            }
        }
    }
}
