using Sentry;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using LeadMeLabsLibrary;

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
        public static readonly int NUCPort = 55556;

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
        /// Starts the server running on the local machine
        /// </summary>
        public async static void StartProgram()
        {
            MockConsole.ClearConsole();
            MockConsole.WriteLine("Loading ENV variables", MockConsole.LogLevel.Error);

            bool result = await DotEnv.Load();

            //Load the environment files, do not continue if file is incomplete
            if (result)
            {
                MockConsole.WriteLine("ENV variables loaded", MockConsole.LogLevel.Error);

                new Thread(() =>
                {
                    if (!Helper.GetStationMode().Equals(Helper.STATION_MODE_APPLIANCE))
                    {
                        VerifySteamLoginUserConfig();

                        wrapperManager = new WrapperManager();

                        //Launch the custom wrapper application here
                        wrapperManager.Startup();

                        //Use to monitor SetVol and restart application
                        StationMonitoringThread.initializeMonitoring();
                    }

                    SetupServerDetails();
                    StartServer();
                    VerifySteamLoginUserConfig();

                    if (Environment.GetEnvironmentVariable("NucAddress") != null)
                    {
                        Logger.WriteLog($"Expected NUC address: {Environment.GetEnvironmentVariable("NucAddress")}", MockConsole.LogLevel.Normal);
                        SetRemoteEndPoint();
                        if (!Helper.GetStationMode().Equals(Helper.STATION_MODE_APPLIANCE))
                        {
                            InitialStartUp();
                        }
                    }
                }).Start();
            } else
            {
                MockConsole.WriteLine("Failed loading ENV variables", MockConsole.LogLevel.Error);
            }
        }

        /// <summary>
        /// Stop all instances of the Station program running
        /// </summary>
        public static void StopProgram()
        {
            new Thread(() =>
            {
                StationMonitoringThread.stopMonitoring();
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

        public static void StartServer()
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
        public static void InitialStartUp()
        {
            Manager.SendResponse("NUC", "Station", "SetValue:status:On");
            Manager.SendResponse("NUC", "Station", "SetValue:gameName:");
            Manager.SendResponse("Android", "Station", "SetValue:gameId:");
            Manager.SendResponse("NUC", "Station", "SetValue:volume:" + CommandLine.GetVolume());
        }

        /// <summary>
        /// Collect the necessary system details for starting the service. Including the IP address, mac address
        /// and the current version number.
        /// </summary>
        public static void SetupServerDetails()
        {
            try
            {
                IPAddress? ip = SystemInformation.GetIPAddress();
                if(ip == null) throw new Exception("Manager class: Server IP Address could not be found");

                string mac = SystemInformation.GetMACAddress() ?? "Unknown";
                string version = SystemInformation.GetVersionNumber() ?? "Unknown";

                localEndPoint = new IPEndPoint(ip.Address, localPort);
                App.SetWindowTitle($"Station({Environment.GetEnvironmentVariable("StationId")}) -- {localEndPoint.Address} -- {mac} -- {version}");

                Logger.WriteLog("Server IP Address is: " + localEndPoint.Address.ToString(), MockConsole.LogLevel.Normal);
                Logger.WriteLog("MAC Address is: " + mac, MockConsole.LogLevel.Normal);
                Logger.WriteLog("Version is: " + version, MockConsole.LogLevel.Normal);
                
            }
            catch (Exception e)
            {
                SentrySdk.CaptureException(e);
                Logger.WriteLog($"Unexpected exception : {e}", MockConsole.LogLevel.Error);
            }
        }

        public static void SetRemoteEndPoint()
        {
            remoteEndPoint = new IPEndPoint(IPAddress.Parse((ReadOnlySpan<char>)Environment.GetEnvironmentVariable("NucAddress")), NUCPort);
        }

        /// <summary>
        /// Create a new script thread and start it, passing in the data collected from 
        /// the recently connected client.
        /// </summary>
        public static void RunScript(string data)
        {
            ScriptThread script = new(data);
            Thread scriptThread = new(script.run);
            scriptThread.Start();
        }

        /// <summary>
        /// Send a response back to the android server detailing what has happened.
        /// </summary>
        public static void SendResponse(string destination, string actionNamespace, string? additionalData, bool writeToLog = true)
        {
            string source = "Station," + Environment.GetEnvironmentVariable("StationId");
            string response = source + ":" + destination + ":" + actionNamespace;
            if (additionalData != null)
            {
                response = response + ":" + additionalData;
            }

            Logger.WriteLog("Sending: " + response, MockConsole.LogLevel.Normal, writeToLog);

            string? key = Environment.GetEnvironmentVariable("AppKey");
            if (key is null) throw new Exception("Encryption key not set");
            SocketClient client = new(EncryptionHelper.Encrypt(response, key));
            client.send(writeToLog);
        }

        private static void VerifySteamLoginUserConfig()
        {
            string fileLocation = "C:\\Program Files (x86)\\Steam\\config\\loginusers.vdf";
            if (!File.Exists(fileLocation))
            {
                Logger.WriteLog("Could not verify steam login info: " + (Environment.GetEnvironmentVariable("LabLocation") ?? "Unknown"), MockConsole.LogLevel.Error);
                return;
            }

            try
            {
                string[] lines = File.ReadAllLines(fileLocation);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Contains("SkipOfflineModeWarning"))
                    {
                        lines[i] = lines[i].Replace("0", "1");
                    }
                    if (lines[i].Contains("AllowAutoLogin"))
                    {
                        lines[i] = lines[i].Replace("0", "1");
                    }
                    if (lines[i].Contains("WantsOfflineMode"))
                    {
                        if (!CommandLine.CheckIfConnectedToInternet())
                        {
                            lines[i] = lines[i].Replace("0", "1");
                        }
                    }
                }

                File.WriteAllLines(fileLocation, lines);
            }
            catch (Exception e)
            {
                SentrySdk.CaptureException(e);
            }
        }
    }
}
