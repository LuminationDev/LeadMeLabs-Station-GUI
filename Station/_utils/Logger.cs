using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using LeadMeLabsLibrary;

namespace Station
{
    public class Logger
    {
        private static readonly string filePath = CommandLine.stationLocation + @"\_logs\";
        public static Queue<string> logQueue = new Queue<string>();

        /// <summary>
        /// Writes a log message to a log file and/or the console.
        /// </summary>
        /// <typeparam name="T">The type of the log message.</typeparam>
        /// <param name="logMessage">The content of the log message.</param>
        /// <param name="logLevel">The severity level of the log.</param>
        /// <param name="writeToLogFile">Determines whether to write the log message to the log file. Default is true.</param>
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

        /// <summary>
        /// Processes the log queue by writing the log messages to a log file.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void WorkQueue()
        {
            string logFilePath = filePath + DateTime.Now.ToString("yyyy_MM_dd") + "_log.txt";
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
                MockConsole.WriteLine($"_logs/ cannot be found, please run from Launcher. Looking in: {logFilePath}", MockConsole.LogLevel.Error);
                logQueue.Clear();
            }
        }

        /// <summary>
        /// Logs requests by collecting the log files for the specified number of days and queues them for transfer.
        /// </summary>
        /// <param name="days">The number of days for which to collect the log files.</param>
        public static void LogRequest(int days)
        {
            if (CommandLine.stationLocation == null)
            {
                Logger.WriteLog("Station location not found: LogRequest", MockConsole.LogLevel.Error);
                return;
            }

            //Collect the last x days of log files
            List<string> logs = CollectRecentLogs(days);

            MockConsole.WriteLine($"Number of log files found: {logs.Count}", MockConsole.LogLevel.Debug);

            if(logs.Count == 0)
            {
                Manager.SendResponse("NUC", "Station", "LogRequest:NoLogsFound");
                return;
            }

            foreach (string filePath in logs)
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                MockConsole.WriteLine($"Attempting to send: {fileName}", MockConsole.LogLevel.Debug);

                //Add the header image to the sending image queue through action transformation
                SocketFile socketFile = new("file", $"{fileName}::::{Environment.GetEnvironmentVariable("StationId", EnvironmentVariableTarget.Process)}", $"{filePath}");
                Action sendFile = () => socketFile.send();

                //Queue the send function for invoking
                TaskQueue.Queue(false, sendFile);

                MockConsole.WriteLine($"Log file: {fileName} now queued for transfer.", MockConsole.LogLevel.Debug);
            }            
        }

        /// <summary>
        /// Collects the x most recent log files from the specified log directory.
        /// </summary>
        /// <param name="days">The amount of days to collect logs for.</param>
        /// <returns>A list of the x most recent log file paths.</returns>
        private static List<string> CollectRecentLogs(int days)
        {
            string logDirectory = @$"{CommandLine.stationLocation}\_logs";
            string logFileFormat = "yyyy_MM_dd";

            // Get all log files in the directory
            string[] allLogFiles = Directory.GetFiles(logDirectory);

            MockConsole.WriteLine($"Log directory count: {days}.", MockConsole.LogLevel.Debug);

            // Filter and select the x most recent log files
            List<string> recentLogFiles = allLogFiles
                .Where(file => DateTime.TryParseExact(
                    Path.GetFileNameWithoutExtension(file).Replace("_log", ""),
                    logFileFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out _))
                .OrderByDescending(file => DateTime.ParseExact(
                    Path.GetFileNameWithoutExtension(file).Replace("_log", ""),
                    logFileFormat,
                    CultureInfo.InvariantCulture))
                .Take(days)
                .ToList();

            return recentLogFiles;
        }
    }
}
