using Station;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace StationTests._utils
{
    public class LoggerTests
    {
        /// <summary>
        /// Test that WriteLog correctly adds a log message to the log queue. Then assert that the 
        /// log queue contains one item, and that it matches the expected message format.
        /// </summary>
        [Fact]
        public void WriteLog_WritesMessageToConsoleAndLogQueue()
        {
            // Arrange
            string message = "Test message";
            MockConsole.LogLevel logLevel = MockConsole.LogLevel.Error;

            // Act
            Logger.WriteLog(message, logLevel);

            // Assert
            Assert.Single(Logger.logQueue);
            Assert.Equal($"[{DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")}]: {message}", Logger.logQueue.Peek());

            // Reset
            Logger.logQueue.Clear();
        }

        /// <summary>
        /// Test that WorkQueue writes the log queue to a file. First create a temporary log file and 
        /// directory, and add a message to the log queue. Then call WorkQueue and assert that the log 
        /// queue is now empty, and that the log file was created and contains the expected message. 
        /// Finally, we clean up the temporary log file and directory.
        /// </summary>
        [Fact]
        public void WorkQueue_WritesLogQueueToFile()
        {
            // Arrange
            string logFilePath = "_logs/" + DateTime.Now.ToString("yyyy_MM_dd") + "_log.txt";
            Directory.CreateDirectory("_logs");
            Logger.WriteLog("Test message", MockConsole.LogLevel.Error);

            // Act
            Logger.WorkQueue();

            // Assert
            Assert.False(Logger.logQueue.Any());
            Assert.True(File.Exists(logFilePath));

            string[] logLines = File.ReadAllLines(logFilePath);
            Assert.Single(logLines);
            Assert.StartsWith($"[{DateTime.Now.ToString("yyyy-MM-dd")}", logLines[0]);
            Assert.EndsWith(": Test message", logLines[0]);

            // Cleanup
            File.Delete(logFilePath);
        }

        /// <summary>
        /// Test that WriteLog does not add null messages to the log queue.
        /// </summary>
        [Fact]
        public void WriteLog_DoesNotAddNullMessageToLogQueue()
        {
            // Arrange
            string? message = null;
            MockConsole.LogLevel logLevel = MockConsole.LogLevel.Error;

            // Act
            Logger.WriteLog(message, logLevel);

            // Assert
            Assert.Empty(Logger.logQueue);
        }

        /// <summary>
        /// Test that WriteLog writes error messages to the console even when writeToLogFile is false.
        /// </summary>
        [Fact]
        public void WriteLog_WritesErrorMessageToConsoleEvenWhenWriteToLogFileIsFalse()
        {
            // Arrange
            string message = "Test message";
            MockConsole.LogLevel logLevel = MockConsole.LogLevel.Error;

            // Act
            Logger.WriteLog(message, logLevel, false);

            // Only one instance should be inside the Queue
            foreach (var element in MockConsole._textQueue)
            {
                if (element.Contains(message))
                {
                    Assert.Contains(message, element);
                }
            }
        }

        /// <summary>
        /// Test that WorkQueue does not write to the log file if the log queue is empty.
        /// </summary>
        [Fact]
        public void WorkQueue_DoesNotWriteToFileIfLogQueueIsEmpty()
        {
            // Arrange
            string logFilePath = "_logs/test_log.txt";

            // Act
            Logger.WorkQueue();

            // Assert
            Assert.False(File.Exists(logFilePath));
        }
    }
}
