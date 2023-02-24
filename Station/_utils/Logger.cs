using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace Station
{
    public class Logger
    {
        public static Queue<string> logQueue = new Queue<string>();

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void WriteLog<T>(T logMessage, MockConsole.LogLevel logLevel, bool writeToLogFile = true)
        {
            if (logMessage == null) return;
            string msg = $"[{DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")}]: {logMessage?.ToString()}";
            if (writeToLogFile)
            {
                logQueue.Enqueue(msg);
            }

            if (logMessage == null) return;
            string? log = logMessage.ToString();

            if (log == null) return;
            MockConsole.WriteLine(log, logLevel);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void WorkQueue()
        {
            string logFilePath = "_logs/" + DateTime.Now.ToString("yyyy_MM_dd") + "_log.txt";
            if (Directory.Exists(Path.GetDirectoryName(logFilePath)))
            {
                using (StreamWriter w = File.AppendText(logFilePath))
                {
                    while (logQueue.Count > 0)
                    {
                        w.WriteLine(logQueue.Dequeue());
                    }
                }
            }
            else
            {
                //Clear the Queue as it will never Dequeue otherwise.
                MockConsole.WriteLine("_logs/ cannot be found, please run from Launcher", MockConsole.LogLevel.Error);
                logQueue.Clear();
            }
        }
    }
}
