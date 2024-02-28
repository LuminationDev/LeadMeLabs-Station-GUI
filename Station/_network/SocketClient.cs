using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Station._manager;
using Station._notification;
using Station._utils;

// Client app is the one sending messages to a Server/listener.
// Both listener and client can send messages back and forth once a
// communication is established.
namespace Station._network;

public class SocketClient
{
    /// <summary>
    /// A message that is to be sent to the android tablet's server.
    /// </summary>
    private readonly string _message;

    private TcpClient? _client;

    //Timeout for the socket connection in seconds
    private const int TimeOut = 1;

    public SocketClient(string message)
    {
        this._message = message;
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
    public void Send(bool writeToLog = true, IPAddress? address = null, int? destPort = null)
    {
        address ??= Manager.remoteEndPoint.Address;
        int port = destPort ?? Manager.remoteEndPoint.Port; //ConnectAsync does not like int?
        
        try
        {
            // Create a TCP client and connect via the supplied endpoint.
            _client = new TcpClient();
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            CancellationToken token = tokenSource.Token;
            ValueTask connect = _client.ConnectAsync(address, port, token);
            Task<bool> task = TimeoutAfter(connect, TimeOut);

            if (!task.Result)
            {
                tokenSource.Cancel();
                if (NotifyIconWrapper.Instance != null) NotifyIconWrapper.Instance.ChangeIcon("offline");
                throw new Exception($"Socket timeout trying to contact: {Manager.remoteEndPoint.Address}");
            }

            if (NotifyIconWrapper.Instance != null) NotifyIconWrapper.Instance.ChangeIcon("online");

            // Connect the socket to the remote endpoint. Catch any errors.
            try
            {
                if (_client.Client.RemoteEndPoint == null)
                {
                    return;
                }

                // Get a client stream for reading and writing.
                NetworkStream stream = _client.GetStream();

                Logger.WriteLog($"Socket connected to {_client.Client.RemoteEndPoint}", MockConsole.LogLevel.Debug, writeToLog);

                // Translate the passed message into ASCII and store it as a Byte array.
                byte[] data = System.Text.Encoding.ASCII.GetBytes(this._message);

                // Construct and send the header first
                string headerMessageType = "text";
                byte[] headerMessageTypeBytes;
                
                if (Manager.isNucUtf8)
                {
                    headerMessageTypeBytes = System.Text.Encoding.UTF8.GetBytes(headerMessageType);
                }
                else
                {
                    headerMessageTypeBytes = System.Text.Encoding.Unicode.GetBytes(headerMessageType);
                }

                // Convert the header to network byte order
                int headerLength = IPAddress.HostToNetworkOrder(headerMessageTypeBytes.Length);
                byte[] headerLengthBytes = BitConverter.GetBytes(headerLength);
                byte[] headerToSendBytes = headerLengthBytes.Concat(headerMessageTypeBytes).ToArray();
                stream.Write(headerToSendBytes, 0, headerToSendBytes.Length);

                // Convert the data to network byte order
                int dataLength = IPAddress.HostToNetworkOrder(data.Length);
                byte[] lengthBytes = BitConverter.GetBytes(dataLength);
                byte[] dataToSendBytes = lengthBytes.Concat(data).ToArray();

                // Send the message to the connected TcpServer.
                stream.Write(data, 0, data.Length);

                // Close everything.
                stream.Close();
                _client.Close();
            }
            catch (ArgumentNullException ane)
            {
                Logger.WriteLog($"ArgumentNullException : {ane}", MockConsole.LogLevel.Error);
            }
            catch (SocketException se)
            {
                Logger.WriteLog($"SocketException : {se}", MockConsole.LogLevel.Error);
            }
            catch (Exception e)
            {
                Logger.WriteLog($"Unexpected exception : {e}", MockConsole.LogLevel.Error);
            }
        }
        catch (Exception e)
        {
            _client?.Dispose();
            _client?.Close();

            Logger.WriteLog($"Unexpected exception : {e.Message}", MockConsole.LogLevel.Error);
        }
    }
}
