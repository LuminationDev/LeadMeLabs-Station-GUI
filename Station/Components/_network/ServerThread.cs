using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LeadMeLabsLibrary;
using Station.Components._commandLine;
using Station.Components._notification;
using Station.Components._utils;
using Station.MVC.Controller;

namespace Station.Components._network;

/// <summary>
/// A server Thread acting as a TCP server, listening for the incoming
/// messages on the specified port and protocol. A TCP socket will connect,
/// deliver it's message and then close, there are no long lived sockets.
/// </summary>
public class ServerThread
{
    /// <summary>
    /// A TcpListener to await connections from the android tablet.
    /// </summary>
    private static TcpListener? server;

    /// <summary>
    /// A default buffer size for File Transfer (much larger than regular buffers).
    /// </summary>
    private const int FileBufferSize = 32768;

    /// <summary>
    /// Track whether an initial connection message has been sent to the NUC.
    /// </summary>
    private static bool connectionMessage = false;

    public ServerThread()
    {
        server = new TcpListener(MainController.localEndPoint);
    }

    /// <summary>
    /// Start the TCP Listener to act as a server for the station. On initial connection, initialise the NUC endpoint.
    /// Any data received is passed back to the runScript function held in the Manager class.
    /// </summary>
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
            
            if (Helper.GetStationMode().Equals(Helper.STATION_MODE_VR))
            {
                SessionController.StartVRSession("steam");
            }

            server.Start();

            //Enter listening loop
            while (true)
            {
                Logger.WriteLog("Waiting for a connection on: " + MainController.localEndPoint.Address + ":" + MainController.localEndPoint.Port, MockConsole.LogLevel.Debug, false);
                TcpClient clientConnection = await server.AcceptTcpClientAsync();

                //Start new Task so the server loop can continue straight away
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
            
            //Close the client connection
            clientConnection.Close();
            clientConnection.Dispose();

            // Reset the position of the MemoryStream to the beginning
            memoryStream.Position = 0;

            //Read the header to determine the incoming data
            byte[] headerLengthBytes = new byte[4];
            await memoryStream.ReadAsync(headerLengthBytes, 0, headerLengthBytes.Length);
            int headerLength = BitConverter.ToInt32(headerLengthBytes, 0);

            MockConsole.WriteLine($"Header length: {headerLength}", MockConsole.LogLevel.Debug);
            
            // Read the header message type
            byte[] headerMessageTypeBytes = new byte[headerLength];
            await memoryStream.ReadAsync(headerMessageTypeBytes, 0, headerLength);
            
            //NOTE: Detect encoding type
            DetermineConnectionType(headerMessageTypeBytes);
            
            string headerMessageType;
            if (MainController.isNucUtf8)
            {
                headerMessageType = Encoding.UTF8.GetString(headerMessageTypeBytes);
            }
            else
            {
                headerMessageType = Encoding.Unicode.GetString(headerMessageTypeBytes);
            }
            
            switch (headerMessageType)
            {
                case "text":
                    await StringMessageReceivedAsync(endPoint, memoryStream);
                    break;
                case "file":
                    await FileMessageReceivedAsync(memoryStream);
                    break;
                default:
                    Logger.WriteLog($"Unknown header connection attempt: {headerMessageType}", MockConsole.LogLevel.Error);
                    break;
            }
        }
        catch (Exception e)
        {
            Logger.WriteLog($"Unknown connection event: {e}", MockConsole.LogLevel.Error);
        }
    }

    /// <summary>
    /// Determine if the incoming message is encoded in UTF8 or Unicode.
    /// </summary>
    /// <param name="headerMessageTypeBytes"></param>
    private void DetermineConnectionType(byte[] headerMessageTypeBytes)
    {
        if (!connectionMessage)
        {
            connectionMessage = true;
            MessageController.SendResponse("NUC", "MessageType", "Station:Unicode");
        }
        
        try
        {
            string test = Encoding.Unicode.GetString(headerMessageTypeBytes);
            if (test.Equals("text") || test.Equals("image") || test.Equals("file"))
            {
                MainController.isNucUtf8 = false;
            }
            else
            {
                //The NUC has been restarted and assumes the Station can't handle Unicode
                connectionMessage = false;
                MainController.isNucUtf8 = true;
            }
        }
        catch (Exception e)
        {
            MockConsole.WriteLine($"Cannot Get string: {e}", MockConsole.LogLevel.Normal);
        }
    }

    /// <summary>
    /// The server has determined that the incoming message is a string based message.
    /// </summary>
    private async Task StringMessageReceivedAsync(EndPoint? endPoint, MemoryStream stream)
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
        if (data.Length == 0) return;
        
        MockConsole.WriteLine($"Encrypted Text received : {data}", MockConsole.LogLevel.Verbose);
        
        //Check for the encryption key
        string? key = Environment.GetEnvironmentVariable("AppKey", EnvironmentVariableTarget.Process);

        if (key == null)
        {
            Logger.WriteLog($"Encryption key is not set in: ServerThread, StringMessageReceivedAsync()", MockConsole.LogLevel.Normal);
            return;
        };

        if (MainController.isNucUtf8)
        {
            data = EncryptionHelper.Decrypt(data, key);
        }
        else
        {
            data = EncryptionHelper.UnicodeDecrypt(data, key);
        }

        //Data should never be null at this point
        Logger.WriteLog($"From {endPoint}, Decrypted Text received : {data}", MockConsole.LogLevel.Debug, !data.Contains(":Ping:"));
        
        //If the data is not a ping run the additional tasks
        if (data.Contains(":Ping:")) return;
        
        //If the task relates to an experience restart the VR processes
        if (data.Contains(":Experience:"))
        {
            //Reset the idle timer
            bool success = await ModeTracker.ResetTimer();
            if (!success) return;
        }

        //Run the appropriate script
        MessageController.RunScript(data);
    }
    
    /// <summary>
    /// The server has determined that the incoming message is a file message. Save the file
    /// to the appropriate location for it's type.
    /// </summary>
    private async Task FileMessageReceivedAsync(MemoryStream stream)
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

        if (File.Exists($@"{path}\{fileName}"))
        {
            Logger.WriteLog($"File: {fileName} is already present on the system.", MockConsole.LogLevel.Error);
            return;
        }
        // Open the output file stream
        await using (var fileStream = new FileStream($@"{path}\{fileName}", FileMode.Create, FileAccess.Write))
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

        // Close the client connection
        await stream.DisposeAsync();
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
                folderPath = $@"{CommandLine.StationLocation}\_cache";
                Directory.CreateDirectory(folderPath); // Create the directory if required
                break;
            case ".zip":
                folderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                break;
            default:
                Logger.WriteLog($"Unknown file type trying to be saved: {fileName}", MockConsole.LogLevel.Error);
                break;
        }

        return folderPath;
    }

    /// <summary>
    /// Stop the server and dispose of any resources that are allocated to it.
    /// </summary>
    public void Stop()
    {
        if (server == null) return;
        
        server.Server.Close();
        server.Stop();
    }
}
