using System;
using System.Net.Sockets;
using System.Text;

namespace Station
{
    /// <summary>
    /// A server Thread acting as a TCP server, listening for the incoming
    /// messages on the specified port and protocol. A TCP socket will connect,
    /// deliever it's message and then close, there are no long lived sockets.
    /// </summery>
    public class ServerThread
    {
        /// <summary>
        /// A TcpListener to await connections from the android tablet.
        /// </summery>
        public static TcpListener? server;

        public ServerThread()
        {
            server = new TcpListener(Manager.localEndPoint);
        }

        /// <summary>
        /// Start the TCP Listener to act as a server for the station. On initial conneciton, initialise the NUC enpoint.
        /// Any data receieved is passed back to the runScript function held in the Manager class.
        /// </summery>
        public void run()
        {
            Station.App.initSentry();
            CommandLine.getVolume();

            try
            {
                if (server == null)
                {
                    Logger.WriteLog("Server not initialised..", MockConsole.LogLevel.Error);
                    return;
                }

                server.Start();

                //Enter listening loop
                while (true)
                {
                    Logger.WriteLog("Waiting for a connection on: " + Manager.localEndPoint.Address + ":" + Manager.localEndPoint.Port, MockConsole.LogLevel.Normal, false);
                    TcpClient clientConnection = server.AcceptTcpClient();

                    var endpoint = clientConnection.Client.RemoteEndPoint;
                    Logger.WriteLog("Got connection from: " + endpoint, MockConsole.LogLevel.Normal, false);

                    // Incoming data from the client.
                    string? data = null;
                    byte[]? bytes = new byte[1024];

                    try
                    {
                        NetworkStream stream = clientConnection.GetStream();

                        int i;
                        while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                        {
                            data += Encoding.ASCII.GetString(bytes, 0, i);
                        }

                        //Close the client connection
                        clientConnection.Close();

                        //Data should never be null at this point
                        if (data is not null)
                        {
                            string? key = Environment.GetEnvironmentVariable("AppKey");
                            if (key is null) throw new Exception("Encryption key not set");
                            data = EncryptionHelper.Decrypt(data, key);

                            Logger.WriteLog($"From {endpoint}, Decrypted Text received : {data}", MockConsole.LogLevel.Normal, !data.Contains(":Ping:"));

                            //Run the appropriate script
                            Manager.runScript(data);
                        }
                        else
                        {
                            Logger.WriteLog($"ServerThread: Data from android server is null", MockConsole.LogLevel.Error);
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.WriteLog($"Unknown connection event: {e.ToString()}", MockConsole.LogLevel.Error);
                    }
                }
            }
            catch (SocketException e)
            {
                Logger.WriteLog($"SocketException: {e.ToString()}", MockConsole.LogLevel.Error);
            }
            catch (Exception e)
            {
                Logger.WriteLog($"Unexpected exception : {e.ToString()}", MockConsole.LogLevel.Error);
            }
            finally
            {
                // Stop listening for new clients.
                server?.Stop();
            }
        }

        /// <summary>
        /// Stop the server and dispose of any resources that are allocated to it.
        /// </summary>
        public void Stop()
        {
            if (server != null)
            {
                server.Server.Close();
                server.Stop();
            }
        }
    }
}
