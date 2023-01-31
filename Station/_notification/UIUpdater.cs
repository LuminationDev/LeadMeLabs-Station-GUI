using System.Runtime.CompilerServices;
using System.Windows;

namespace Station
{
    public static class UIUpdater
    {
        /// <summary>
        /// Reset the UI display on the main window to the generic 'No active processes'
        /// message and 'Waiting' status.
        /// </summary>
        public static void ResetUIDisplay()
        {
            UpdateProcess("No active process...");
            UpdateStatus("Waiting...");
        }

        /// <summary>
        /// Log a message to the mock console within the Station form.
        /// </summary>
        /// <param name="message"></param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void UpdateProcess(string message)
        {
            Application.Current.Dispatcher.Invoke(delegate {
                if (MainWindow.processConsole == null) return;

                MainWindow.processConsole.Content = message;
            });
        }

        /// <summary>
        /// Log a message to the mock console within the Station form.
        /// </summary>
        /// <param name="message"></param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void UpdateStatus(string message)
        {
            Application.Current.Dispatcher.Invoke(delegate {
                if (MainWindow.statusConsole == null) return;

                MainWindow.statusConsole.Content = message;
            });
        }
    }
}
