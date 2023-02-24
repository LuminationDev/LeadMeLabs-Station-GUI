using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

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
        public static void startProgram()
        {
            MockConsole.ClearConsole();

            //Load the environment files, do not continue if file is incomplete
            if(DotEnv.Load())
            {
#if DEBUG
                //If in development override the program paths with the absolute paths dictated by Directory value in the config.env file.
                CommandLine.steamCmd = $@"C:\Users\{Environment.GetEnvironmentVariable("Directory")}\steamcmd\steamcmd.exe";
                CommandLine.SetVol = $@"C:\Users\{Environment.GetEnvironmentVariable("Directory")}\SetVol\SetVol.exe";
#endif

                new Thread(() =>
                {
                    if (!Helper.GetStationMode().Equals(Helper.STATION_MODE_APPLIANCE))
                    {
                        wrapperManager = new WrapperManager();

                    //Launch the custom wrapper application here
                    wrapperManager.Startup();

                    //Use to monitor SetVol and restart application
                    StationMonitoringThread.initializeMonitoring();
                    }

                    setServerIPAddress();
                    startServer();

                    if (Environment.GetEnvironmentVariable("NucAddress") != null)
                    {
                        Logger.WriteLog(Environment.GetEnvironmentVariable("NucAddress"), MockConsole.LogLevel.Debug);
                        setRemoteEndPoint();
                        if (!Helper.GetStationMode().Equals(Helper.STATION_MODE_APPLIANCE))
                        {
                            initialStartUp();
                        }
                    }
                }).Start();
            };
        }

        /// <summary>
        /// Stop all instances of the Station program running
        /// </summary>
        public static void stopProgram()
        {
            new Thread(() =>
            {
                StationMonitoringThread.stopMonitoring();
                stopServer();
                wrapperManager?.ShutDownWrapper();
                Logger.WriteLog("Station stopped", MockConsole.LogLevel.Normal);
            }).Start();
        }

        /// <summary>
        /// Restart the station program
        /// </summary>
        public static void restartProgram()
        {
            stopProgram();
            Logger.WriteLog("Station restarting", MockConsole.LogLevel.Normal);
            startProgram();
        }

        public static void startServer()
        {
            server = new ServerThread();
            serverThread = new Thread(async () => await server.RunAsync());
            serverThread.Start();
        }

        public static void stopServer()
        {
            server?.Stop();
            serverThread?.Interrupt();
            MockConsole.WriteLine("Server stopped", MockConsole.LogLevel.Debug);
        }

        /// <summary>
        /// On start up or NUC address change send the status, steam list and the current volume to the NUC.
        /// </summary>
        public static void initialStartUp()
        {
            Manager.sendResponse("NUC", "Station", "SetValue:status:On");
            Manager.sendResponse("NUC", "Station", "SetValue:gameName:");
            Manager.sendResponse("Android", "Station", "SetValue:gameId:");
            Manager.sendResponse("NUC", "Station", "SetValue:volume:" + CommandLine.getVolume());
        }

        /// <summary>
        /// Set the IP address of the server running on the local machine
        /// Get Host IP Address that is used to establish a connection
        /// By connecting a UDP socket the correct IP address can be found
        /// when other applications such as VM are running.
        /// </summary>
        public static void setServerIPAddress()
        {
            try
            {
                using Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint? endPoint = socket.LocalEndPoint as IPEndPoint;

                if (endPoint is not null)
                {
                    localEndPoint = new IPEndPoint(endPoint.Address, localPort);

                    Logger.WriteLog("Server IP Address is: " + endPoint.Address.ToString(), MockConsole.LogLevel.Normal);
                }
                else
                {
                    throw new Exception("Manager class: Server IP Address could not be found");
                }
            }
            catch (Exception e)
            {
                Logger.WriteLog($"Unexpected exception : {e}", MockConsole.LogLevel.Error);
            }
        }

        public static void setRemoteEndPoint()
        {
            remoteEndPoint = new IPEndPoint(IPAddress.Parse((ReadOnlySpan<char>)Environment.GetEnvironmentVariable("NucAddress")), NUCPort);
        }

        /// <summary>
        /// Create a new script thread and start it, passing in the data collected from 
        /// the recently connected client.
        /// </summery>
        public static void runScript(string data)
        {
            ScriptThread script = new(data);
            Thread scriptThread = new(script.run);
            scriptThread.Start();
        }

        /// <summary>
        /// Send a response back to the android server detailing what has happened.
        /// </summary>
        public static void sendResponse(string destination, string actionNamespace, string? additionalData, bool writeToLog = true)
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
    }
}
