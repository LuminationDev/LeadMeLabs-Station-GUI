using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LeadMeLabsLibrary;

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

        /// <summary>
        /// A default buffer size for File Transfer (much larger than regular buffers).
        /// </summary>
        private static int FileBufferSize = 32768;

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
            CommandLine.GetVolume();

            try
            {
                if (server == null)
                {
                    Logger.WriteLog("Server not initialised..", MockConsole.LogLevel.Error);
                    return;
                }
                
                SessionController.StartVRSession("steam");

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
                using MemoryStream memoryStream = new();
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
                //TODO REMOVE OLD MIGRATOR CODE
                if (headerLength > 144)
                {
                    MockConsole.WriteLine($"NUC version 1 connecting.", MockConsole.LogLevel.Debug);

                    // Reset the position of the MemoryStream to the beginning
                    memoryStream.Position = 0;
                    await StringMessageReceivedAsync(clientConnection, endPoint, memoryStream);
                }
                else
                {
                    // Read the header message type
                    byte[] headerMessageTypeBytes = new byte[headerLength];
                    await memoryStream.ReadAsync(headerMessageTypeBytes, 0, headerLength);
                    string headerMessageType = Encoding.Unicode.GetString(headerMessageTypeBytes);

                    MockConsole.WriteLine($"Header type: {headerMessageType}", MockConsole.LogLevel.Debug);

                    switch (headerMessageType)
                    {
                        case "text":
                            await StringMessageReceivedAsync(clientConnection, endPoint, memoryStream);
                            break;

                        case "file":
                            await FileMessageReceivedAsync(clientConnection, memoryStream);
                            break;

                        default:
                            Logger.WriteLog($"Unknown header connection attempt: {headerMessageType}", MockConsole.LogLevel.Error);
                            break;
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
        private async Task StringMessageReceivedAsync(TcpClient clientConnection, EndPoint? endPoint, MemoryStream stream)
        {
            // Incoming data from the client.
            var stringBuilder = new StringBuilder();
            byte[] buffer = new byte[8192];

            // Read data from the stream into a Memory<byte> buffer
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(), CancellationToken.None).ConfigureAwait(false)) != 0)
            {
                stringBuilder.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));
            }

            string data = stringBuilder.ToString();

            //No point continuing if the data is null
            if (data == null) return;

            MockConsole.WriteLine($"Encrypted Text received : {data}", MockConsole.LogLevel.Verbose);

            //Check for the encryption key
            string? key = Environment.GetEnvironmentVariable("AppKey", EnvironmentVariableTarget.Process);

            if (key == null)
            {
                Logger.WriteLog($"Encryption key is not set in: ServerThread, StringMessageReceivedAsync()", MockConsole.LogLevel.Normal);
                return;
            };

            data = EncryptionHelper.UnicodeDecrypt(data, key);

            Logger.WriteLog($"Decrypted Text received : {data}", MockConsole.LogLevel.Normal);

            //Close the client connection
            clientConnection.Close();

            //Data should never be null at this point
            if (data is not null)
            {
                Logger.WriteLog($"From {endPoint}, Decrypted Text received : {data}", MockConsole.LogLevel.Debug, !data.Contains(":Ping:"));

                //Run the appropriate script
                Manager.RunScript(data);
            }
            else
            {
                Logger.WriteLog($"ServerThread: Data from {endPoint} is null", MockConsole.LogLevel.Error);
            }
        }

        /// <summary>
        /// The server has determined that the incoming message is a file message. Save the file
        /// to the appropriate location for it's type.
        /// </summary>
        private async Task FileMessageReceivedAsync(TcpClient clientConnection, MemoryStream stream)
        {
            // Read the size of the file name
            byte[] header = new byte[4];
            await stream.ReadAsync(header.AsMemory(0, 4));
            int fileNameLen = BitConverter.ToInt32(header, 0);

            // Read the file name from the incoming data
            byte[] fileNameBytes = new byte[fileNameLen];
            await stream.ReadAsync(fileNameBytes.AsMemory(0, fileNameLen));
            string fileName = Encoding.Unicode.GetString(fileNameBytes);

            string? path = DetermineFileType(fileName);
            if (path == null)
            {
                Logger.WriteLog($"File: {fileName} could not find a save path from 'DetermineFileType'.", MockConsole.LogLevel.Error);
                return;
            }
            else if (File.Exists($@"{path}\{fileName}"))
            {
                Logger.WriteLog($"File: {fileName} is already present on the system.", MockConsole.LogLevel.Error);
                return;
            }
            else
            {
                // Open the output file stream
                using (var fileStream = new FileStream($@"{path}\{fileName}", FileMode.Create, FileAccess.Write))
                {
                    // Read and write data from the stream to the file
                    int totalBytes = 0;
                    var buffer = new byte[FileBufferSize];

                    int bytesRead;
                    while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, FileBufferSize))) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                        totalBytes += bytesRead;
                    }

                    // Close the output file stream
                    fileStream.Close();
                }

                Logger.WriteLog($"New File saved: {fileName} at {path}", MockConsole.LogLevel.Error);
            }

            // Close the client connection
            await stream.DisposeAsync();
            await Task.Run(() => clientConnection.Dispose());
        }

        /// <summary>
        /// Determine the incoming File type and collect the folder path it should be saved in.
        /// </summary>
        /// <returns></returns>
        private string? DetermineFileType(string fileName)
        {
            string? folderPath = null;
            string ext = Path.GetExtension(fileName);

            switch (ext)
            {
                case ".jpg":
                    folderPath = $@"{CommandLine.stationLocation}\_cache";
                    Directory.CreateDirectory(folderPath); // Create the directory if required
                    break;
                case ".zip":
                    folderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    break;
                default:
                    break;
            }

            return folderPath;
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
