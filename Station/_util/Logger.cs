using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace Station
{
    class Logger
    {
        private static Queue<string> logQueue = new Queue<string>();

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
            using (StreamWriter w = File.AppendText("_logs/" + DateTime.Now.ToString("yyyy_MM_dd") + "_log.txt"))
            {
                while (logQueue.Count > 0)
                {
                    w.WriteLine(logQueue.Dequeue());
                }
            }
        }
    }
}
