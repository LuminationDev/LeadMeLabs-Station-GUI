using System;
using System.Text;

namespace Station
{
    public static class MockConsole
    {
        private static MainWindowViewModel? _viewModel;
        private static int __lineCount = 30;

        public static void SetViewModel(MainWindowViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        /// <summary>
        /// Describe the different levels of logging, only the most essential messages are printed at None.
        /// The levels are [None - essential only, Normal - basic messages and commands, Debug - anything that can be used for information, Verbose - everything].
        /// </summary>
        public enum LogLevel
        {
            Off,
            Error,
            Normal,
            Debug,
            Verbose
        }

        private const int LineLimit = 100;
        public static LogLevel _logLevel = LogLevel.Normal;

        /// <summary>
        /// Cycle through the Loglevels, if it has reach the max (Verbose) then reset
        /// back to None.
        /// </summary>
        public static void changeLogLevel()
        {
            if (_logLevel == LogLevel.Verbose)
            {
                _logLevel = LogLevel.Off;
            }
            else
            {
                _logLevel++;
            }

            if (MainWindow.logLevel == null) return;
            MainWindow.logLevel.Content = $"Logging: {Enum.GetName(_logLevel)}";
        }

        //The functions below handle updating the mock console that is present within the MainWindow. This
        //process allows other parts of the project to display information to a user.

        /// <summary>
        /// Clear the MockConsole of all previous messages. The cleared message will be printed regardless
        /// of log level as to alert the user this is deliberate.
        /// </summary>
        public static void ClearConsole()
        {
            if (_viewModel == null) return;
            _viewModel.ConsoleText = "";
            WriteLine("Cleared", LogLevel.Error);
        }

        /// <summary>
        /// This is only to be used for the DLL library callback.
        /// Log a message to the mock console within the Station form, this does not take into account the current log level.
        /// </summary>
        /// <param name="message">A string to be printed to the console.</param>
        public static void WriteLine(string message)
        {
            if (message.Trim() == "" || _viewModel == null) return;
            if (_logLevel == LogLevel.Off) return;

            var builder = new StringBuilder(_viewModel.ConsoleText);

            if (builder.Length > 0 && builder[builder.Length - 1] != '\n')
            {
                builder.AppendLine(); // Ensure the last line ends with a newline
            }
            
            var lineCount = builder.ToString().Split('\n').Length;
            
            if (lineCount >= LineLimit)
            {
                int startIndex = builder.ToString().IndexOf('\n') + 1;
                builder.Remove(0, startIndex);
            }

            builder.AppendLine($"{DateStamp()}{message}");
            _viewModel.ConsoleText = builder.ToString();
        }

        /// <summary>
        /// Log a message to the mock console within the Station form, only print it if it conforms to the current logging level.
        /// </summary>
        /// <param name="message">A string to be printed to the console.</param>
        /// <param name="level">A Loglevel enum representing if it should be displayed at the current logging level.</param>
        public static void WriteLine(string message, LogLevel level)
        {
            if (message.Trim() == "" || _viewModel == null) return;
            if (level > _logLevel || _logLevel == LogLevel.Off) return;
            
            var builder = new StringBuilder(_viewModel.ConsoleText);

            if (builder.Length > 0 && builder[builder.Length - 1] != '\n')
            {
                builder.AppendLine(); // Ensure the last line ends with a newline
            }
            
            var lineCount = builder.ToString().Split('\n').Length;
            
            if (lineCount >= LineLimit)
            {
                int startIndex = builder.ToString().IndexOf('\n') + 1;
                builder.Remove(0, startIndex);
            }

            builder.AppendLine($"{DateStamp()}{message}");
            _viewModel.ConsoleText = builder.ToString();
        }

        private static string DateStamp()
        {
            DateTime now = DateTime.Now;
            return $"[{now:dd/MM | hh:mm:ss}] ";
        }
    }
}
