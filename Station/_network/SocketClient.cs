using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

// Client app is the one sending messages to a Server/listener.
// Both listener and client can send messages back and forth once a
// communication is established.
namespace Station
{
    public class SocketClient
    {
        /// <summary>
        /// A message that is to be sent to the android tablet's server.
        /// </summery>
        private string message = "";

        private TcpClient? client;

        //Timeout for the socket connection in seconds
        private int timeOut = 1;

        public SocketClient(string message)
        {
            this.message = message;
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
                };

                // Connect the socket to the remote endpoint. Catch any errors.
                try
                {
                    if (client.Client.RemoteEndPoint == null)
                    {
                        return;
                    }

                    // Get a client stream for reading and writing.
                    NetworkStream stream = client.GetStream();

                    Logger.WriteLog($"Socket connected to {client.Client.RemoteEndPoint.ToString()}", MockConsole.LogLevel.Debug, writeToLog);

                    // Translate the passed message into ASCII and store it as a Byte array.
                    Byte[] data = System.Text.Encoding.ASCII.GetBytes(this.message);


                    // Send the message to the connected TcpServer.
                    stream.Write(data, 0, data.Length);

                    Logger.WriteLog($"Sent: {message}", MockConsole.LogLevel.Normal, writeToLog);

                    // Close everything.
                    stream.Close();
                    client.Close();
                }
                catch (ArgumentNullException ane)
                {
                    Logger.WriteLog($"ArgumentNullException : {ane.ToString()}", MockConsole.LogLevel.Error);
                }
                catch (SocketException se)
                {
                    Logger.WriteLog($"SocketException : {se.ToString()}", MockConsole.LogLevel.Error);
                }
                catch (Exception e)
                {
                    Logger.WriteLog($"Unexpected exception : {e.ToString()}", MockConsole.LogLevel.Error);
                }
            }
            catch (Exception e)
            {
                client?.Dispose();
                client?.Close();

                Logger.WriteLog($"Unexpected exception : {e.ToString()}", MockConsole.LogLevel.Error);
            }
        }
    }
}
