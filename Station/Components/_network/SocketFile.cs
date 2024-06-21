using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using LeadMeLabsLibrary;
using Station.Components._utils;
using Station.MVC.Controller;

// Client app is the one sending messages to a Server/listener.
// Both listener and client can send messages back and forth once a
// communication is established.
namespace Station.Components._network;

public class SocketFile
{
    /// <summary>
    /// The type of file being sent to the NUC.
    /// </summary>
    private readonly string _type;

    /// <summary>
    /// The name of the file as to be saved on the NUC.
    /// </summary>
    private readonly string _name;

    /// <summary>
    /// An absolute file path of a local file on the NUC.
    /// </summary>
    private readonly string _filePath;

    private TcpClient? _client;

    //Timeout for the socket connection in seconds
    private const int TimeOut = 1;

    public SocketFile(string type, string name, string filePath)
    {
        this._type = type;
        this._name = GetName(type, name);
        this._filePath = filePath;
    }

    /// <summary>
    /// Determine if the name needs modification depending on the type of file being sent.
    /// </summary>
    private string GetName(string type, string name)
    {
        switch (type)
        {
            case "experienceThumbnail":
                return $"{name.Replace(":", "")}_header.jpg";
            
            case "videoThumbnail":
                return $"{name}_thumbnail.jpg";
            
            default:
                return name;
        }
    }

    /// <summary>
    /// Run two tasks in, one the supplied task the other a timeout delay. Returns a bool representing if
    /// the supplied task completed before the timeout task. 
    /// </summary>
    /// <param name="task"></param>
    /// <param name="timeout">A double representing how long the timeout should be</param>
    private async Task<bool> TimeoutAfter(ValueTask task, double timeout)
    {
        using var timeoutCancellationTokenSource = new CancellationTokenSource();
        var completedTask = await Task.WhenAny(task.AsTask(), Task.Delay(TimeSpan.FromSeconds(timeout), timeoutCancellationTokenSource.Token));

        if (completedTask == task.AsTask())
        {
            timeoutCancellationTokenSource.Cancel();
            await task;  // Very important in order to propagate exceptions
            return true;
        }
        else
        {
            timeoutCancellationTokenSource.Cancel();
            return false;
        }
    }

    /// <summary>
    /// Create a new TcpClient with the details collected from the initial server thread.
    /// Sends a message to the android server with details about certain outputs or machine
    /// states.
    /// </summary>
    public void Send(bool writeToLog = true)
    {
        try
        {
            // Create a TCP client and connect via the supplied endpoint.
            _client = new TcpClient();
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            CancellationToken token = tokenSource.Token;
            ValueTask connect = _client.ConnectAsync(MainController.remoteEndPoint.Address, MainController.remoteEndPoint.Port, token);
            Task<bool> task = TimeoutAfter(connect, TimeOut);

            if (!task.Result)
            {
                tokenSource.Cancel();
                throw new SocketException();
            }

            // Connect the socket to the remote endpoint. Catch any errors.
            try
            {
                if (_client.Client.RemoteEndPoint == null)
                {
                    return;
                }

                // Get a client stream for reading and writing.
                NetworkStream stream = _client.GetStream();

                // Construct and send the header
                string headerMessageType = this._type;
                byte[] headerMessageTypeBytes;
                // if (MainController.isNucUtf8)
                // {
                //     headerMessageTypeBytes = System.Text.Encoding.UTF8.GetBytes(headerMessageType);
                // }
                // else
                // {
                    headerMessageTypeBytes = System.Text.Encoding.Unicode.GetBytes(headerMessageType);
                // }

                // Convert the header to network byte order
                int headerLength = IPAddress.HostToNetworkOrder(headerMessageTypeBytes.Length);
                byte[] headerLengthBytes = BitConverter.GetBytes(headerLength);
                byte[] headerToSendBytes = headerLengthBytes.Concat(headerMessageTypeBytes).ToArray();
                stream.Write(headerToSendBytes, 0, headerToSendBytes.Length);

                // Send the file name second
                string fileName = _name;
                byte[] fileNameBytes;
                
                // if (MainController.isNucUtf8)
                // {
                //     fileNameBytes = System.Text.Encoding.UTF8.GetBytes(fileName);
                //     stream.Write(BitConverter.GetBytes(fileNameBytes.Length), 0, 4);
                // }
                // else
                // {
                    fileNameBytes = System.Text.Encoding.Unicode.GetBytes(fileName);
                    stream.Write(BitConverter.GetBytes(fileNameBytes.Length));
                // }
                
                stream.Write(fileNameBytes, 0, fileNameBytes.Length);

                Logger.WriteLog($"Socket connected to {_client.Client.RemoteEndPoint}", Enums.LogLevel.Debug, writeToLog);

                // Turn the image into a data byte stream and send the image data
                FileStream fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read);
                byte[] buffer = new byte[fs.Length];
                fs.Read(buffer, 0, (int)fs.Length);

                // Send the message to the connected TcpServer.
                stream.Write(buffer, 0, (int)fs.Length);

                Logger.WriteLog($"Sent {this._type}: {_filePath}", Enums.LogLevel.Normal, writeToLog);

                // Close everything.
                fs.Close();
                stream.Close();
                _client.Close();
            }
            catch (ArgumentNullException ane)
            {
                Logger.WriteLog($"ArgumentNullException : {ane}", Enums.LogLevel.Error);
            }
            catch (SocketException se)
            {
                Logger.WriteLog($"SocketException : {se}", Enums.LogLevel.Error);
            }
            catch (Exception e)
            {
                Logger.WriteLog($"Unexpected exception : {e}", Enums.LogLevel.Error);
            }
        }
        catch (Exception e)
        {
            _client?.Dispose();
            _client?.Close();

            Logger.WriteLog($"Unexpected exception : {e}", Enums.LogLevel.Error);

            if (this._type.Equals("file"))
            {
                MessageController.SendResponse("NUC", "LogRequest", "TransferFailed");
            }
        }
    }
}
