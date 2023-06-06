using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

// Client app is the one sending messages to a Server/listener.
// Both listener and client can send messages back and forth once a
// communication is established.
namespace Station
{
    public class SocketFile
    {
        /// <summary>
        /// The type of file being sent to the NUC.
        /// </summary>
        private readonly string type = "";

        /// <summary>
        /// The name of the file as to be saved on the NUC.
        /// </summary>
        private readonly string name = "";

        /// <summary>
        /// An absolute file path of a local file on the NUC.
        /// </summery>
        private readonly string filePath = "";

        private TcpClient? client;

        //Timeout for the socket connection in seconds
        private int timeOut = 1;

        public SocketFile(string type, string name, string filePath)
        {
            this.type = type;
            this.name = GetName(type, name);
            this.filePath = filePath;
        }

        /// <summary>
        /// Determine if the name needs modification depending on the type of file being sent.
        /// </summary>
        private string GetName(string type, string name)
        {
            return type == "image" ? $"{name}_header.jpg" : name;
        }

        /// <summary>
        /// Run two tasks in, one the supplied task the other a timeout delay. Returns a bool representing if
        /// the supplied task completed before the timeout task. 
        /// </summary>
        /// <param name="timeout">A double representing how long the timeout should be</param>
        private async Task<bool> TimeoutAfter(ValueTask task, double timeout)
        {

            using (var timeoutCancellationTokenSource = new CancellationTokenSource())
            {
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
        }

        /// <summary>
        /// Create a new TcpClient with the details collected from the initial server thread.
        /// Sends a message to the android server with details about certain outputs or machine
        /// states.
        /// </summery>
        public void send(bool writeToLog = true)
        {
            try
            {
                // Create a TCP client and connect via the supplied endpoint.
                client = new TcpClient();
                CancellationTokenSource tokenSource = new CancellationTokenSource();
                CancellationToken token = tokenSource.Token;
                ValueTask connect = client.ConnectAsync(Manager.remoteEndPoint.Address, Manager.remoteEndPoint.Port, token);
                Task<bool> task = TimeoutAfter(connect, timeOut);

                if (!task.Result)
                {
                    tokenSource.Cancel();
                    Console.WriteLine("Socket timeout: " + Manager.remoteEndPoint.Address);
                    throw new SocketException();
                }

                // Connect the socket to the remote endpoint. Catch any errors.
                try
                {
                    if (client.Client.RemoteEndPoint == null)
                    {
                        return;
                    }

                    // Get a client stream for reading and writing.
                    NetworkStream stream = client.GetStream();

                    // Construct and send the header
                    string headerMessageType = this.type;
                    byte[] headerMessageTypeBytes = System.Text.Encoding.UTF8.GetBytes(headerMessageType);

                    // Convert the header to network byte order
                    int headerLength = IPAddress.HostToNetworkOrder(headerMessageTypeBytes.Length);
                    byte[] headerLengthBytes = BitConverter.GetBytes(headerLength);
                    byte[] headerToSendBytes = headerLengthBytes.Concat(headerMessageTypeBytes).ToArray();
                    stream.Write(headerToSendBytes, 0, headerToSendBytes.Length);

                    // Send the file name second
                    string fileName = name;
                    byte[] fileNameBytes = System.Text.Encoding.UTF8.GetBytes(fileName);
                    stream.Write(BitConverter.GetBytes(fileNameBytes.Length), 0, 4);
                    stream.Write(fileNameBytes, 0, fileNameBytes.Length);

                    Logger.WriteLog($"Socket connected to {client.Client.RemoteEndPoint}", MockConsole.LogLevel.Debug, writeToLog);

                    // Turn the image into a data byte stream and send the image data
                    FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                    byte[] buffer = new byte[fs.Length];
                    fs.Read(buffer, 0, (int)fs.Length);

                    // Send the message to the connected TcpServer.
                    stream.Write(buffer, 0, (int)fs.Length);

                    Logger.WriteLog($"Sent {this.type}: {filePath}", MockConsole.LogLevel.Normal, writeToLog);

                    // Close everything.
                    fs.Close();
                    stream.Close();
                    client.Close();
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
                client?.Dispose();
                client?.Close();

                Logger.WriteLog($"Unexpected exception : {e}", MockConsole.LogLevel.Error);

                if (this.type.Equals("file"))
                {
                    Manager.SendResponse("NUC", "Station", "LogRequest:TransferFailed");
                }
            }
        }
    }
}
