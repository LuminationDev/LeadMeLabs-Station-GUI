using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

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
        private static TcpListener? server;

        public ServerThread()
        {
            server = new TcpListener(Manager.localEndPoint);
        }

        /// <summary>
        /// Start the TCP Listener to act as a server for the station. On initial conneciton, initialise the NUC enpoint.
        /// Any data receieved is passed back to the runScript function held in the Manager class.
        /// </summery>
        public async Task RunAsync()
        {
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
                    Logger.WriteLog("Waiting for a connection on: " + Manager.localEndPoint.Address + ":" + Manager.localEndPoint.Port, MockConsole.LogLevel.Debug, false);
                    TcpClient clientConnection = await server.AcceptTcpClientAsync();

                    //Start new thread so the server can continue straight away
                    _ = Task.Run(() => HandleConnectionAsync(clientConnection));
                }
            }
            catch (SocketException e)
            {
                Logger.WriteLog($"SocketException: {e}", MockConsole.LogLevel.Error);
            }
            catch (Exception e)
            {
                Logger.WriteLog($"Unexpected exception : {e}", MockConsole.LogLevel.Error);
            }
            finally
            {
                // Stop listening for new clients.
                server?.Stop();
            }
        }

        /// <summary>
        /// Handle a newly created connection on the Server Thread, determine if the message is from the Cbus or a LeadMe Labs device.
        /// Decode the message being sent and pass it off to the RelayThread through the Manager.sendAction function. This is kept 
        /// separate form the server loop as it can be run on a new thread allowing the server to quickly process incoming messages.
        /// </summary>
        /// <param name="clientConnection">A TcpClient representing the latest connection information.</param>
        private async Task HandleConnectionAsync(TcpClient clientConnection)
        {
            EndPoint? endPoint = clientConnection.Client.RemoteEndPoint;

            if (endPoint == null)
            {
                MockConsole.WriteLine("Unknown server connection discarded.", MockConsole.LogLevel.Debug);
                return;
            }

            try
            {
                NetworkStream stream = clientConnection.GetStream();

                // Read the incoming data into a MemoryStream so we can re-read it at anytime
                using (MemoryStream memoryStream = new())
                {
                    await stream.CopyToAsync(memoryStream);

                    // Dispose of the original network stream.
                    stream.Close();
                    stream.Dispose();

                    // Reset the position of the MemoryStream to the beginning
                    memoryStream.Position = 0;

                    //Read the header to determine the incoming data
                    byte[] headerLengthBytes = new byte[4];
                    await memoryStream.ReadAsync(headerLengthBytes, 0, headerLengthBytes.Length);
                    int headerLength = BitConverter.ToInt32(headerLengthBytes, 0);

                    MockConsole.WriteLine($"Header length: {headerLength}", MockConsole.LogLevel.Debug);

                    //No header means that an older tablet version has connected and is sending a message
                    if (headerLength > 4)
                    {
                        MockConsole.WriteLine($"NUC version 1 connecting.", MockConsole.LogLevel.Debug);

                        // Reset the position of the MemoryStream to the beginning
                        memoryStream.Position = 0;
                        StringMessageReceived(clientConnection, endPoint, memoryStream);
                    } else
                    {
                        // Read the header message type
                        byte[] headerMessageTypeBytes = new byte[headerLength];
                        await memoryStream.ReadAsync(headerMessageTypeBytes, 0, headerLength);
                        string headerMessageType = Encoding.UTF8.GetString(headerMessageTypeBytes);

                        if (headerMessageType.Equals("text"))
                        {
                            StringMessageReceived(clientConnection, endPoint, memoryStream);
                        }
                        else
                        {
                            Logger.WriteLog($"Unknown header connection attempt: {headerMessageType}", MockConsole.LogLevel.Error);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.WriteLog($"Unknown connection event: {e}", MockConsole.LogLevel.Error);
            }
        }

        /// <summary>
        /// The server has determined that the incoming message is a string based message.
        /// </summary>
        private void StringMessageReceived(TcpClient clientConnection, EndPoint? endPoint, MemoryStream stream)
        {
            // Incoming data from the client.
            string? data = null;
            byte[]? bytes = new byte[1024];

            try
            {
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

                    Logger.WriteLog($"From {endPoint}, Decrypted Text received : {data}", MockConsole.LogLevel.Debug, !data.Contains(":Ping:"));

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
                Logger.WriteLog($"Unknown connection event: {e}", MockConsole.LogLevel.Error);
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
