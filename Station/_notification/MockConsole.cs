using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;

namespace Station
{
    //TODO improve efficiency for logging
    public static class MockConsole
    {
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

        private static string _textstr = "";
        private static int _lineCount = 0;
        private static int _lineLimit = 250;
        public static LogLevel _logLevel = LogLevel.Off;

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
        //proccess allows other parts of the project to display information to a user.
        public static string Textstr
        {
            get
            {
                return _textstr;
            }
            set
            {
                _textstr = value;
            }
        }

        /// <summary>
        /// Clear the MockConsole of all previous messages. The cleared message will be printed regardless
        /// of log level as to alert the user this is deliberate.
        /// </summary>
        public static void clearConsole()
        {
            Textstr = "";
            WriteLine("Cleared", LogLevel.Error);
        }

        /// <summary>
        /// Log a message to the mock console within the Station form, this does not take into account the current log level.
        /// </summary>
        /// <param name="message">A string to be printed to the console.</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void WriteLine(string message)
        {
            if (_logLevel != LogLevel.Off)
            {
                Application.Current.Dispatcher.Invoke(delegate
                {
                    if (MainWindow.console == null) return;

                    Textstr = Textstr + DateStamp() + message + "\n";
                    MainWindow.console.Text = TrimConsole();
                    _lineCount++;
                });
            }
        }

        /// <summary>
        /// Log a message to the mock console within the Station form, only print it if it conforms to the current logging level.
        /// </summary>
        /// <param name="message">A string to be printed to the console.</param>
        /// <param name="level">A Loglevel enum representing if it should be displayed at the current logging level.</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void WriteLine(string message, LogLevel level)
        {
            if (level <= _logLevel && _logLevel != LogLevel.Off)
            {
                try
                {               
                    Application.Current?.Dispatcher.Invoke(delegate
                    {
                        if (MainWindow.console == null) return;

                        Textstr = Textstr + DateStamp() + message + "\n";
                        MainWindow.console.Text = TrimConsole();
                        _lineCount++;
                    });
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex);
                }
            }
        }

        private static string DateStamp()
        {
            DateTime now = DateTime.Now;
            return "[" + now.ToString("dd/MM") + " | " + now.ToString("hh:mm:ss") + "] ";
        }

        /// <summary>
        /// Trim the earliest message from the console to stop an infinite scroll occuring.
        /// </summary>
        private static string TrimConsole()
        {
            if (_lineCount >= _lineLimit)
            {
                _lineCount--;
                var lines = Regex.Split(Textstr, "\r\n|\r|\n").Skip(1);
                return string.Join(Environment.NewLine, lines.ToArray());
            }

            return Textstr;
        }
    }
}
