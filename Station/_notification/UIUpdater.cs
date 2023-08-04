using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Imaging;

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
        
        public static void UpdateStatus(string message)
        {
            Application.Current.Dispatcher.Invoke(delegate {
                if (MainWindow.statusConsole == null) return;

                MainWindow.statusConsole.Content = message;
            });
        }
        
        /// <summary>
        /// Change the colour of the OpenVR image to symbolise the current connection status.
        /// </summary>
        /// <param name="isOnline">A bool representing if the connection is online (true) or offline (false)</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void LoadImageFromAssetFolder(bool isOnline)
        {
            try
            {
                // Assign the image source to the Image control
                Application.Current.Dispatcher.Invoke(delegate {
                    string iconPath = isOnline ? "openvr_online.ico" : "openvr_offline.ico";
                    
                    // Load the image from the asset folder
                    BitmapImage imageSource = new BitmapImage();
                    imageSource.BeginInit();
                    imageSource.UriSource = new Uri($"pack://application:,,,/Station;component/Assets/{iconPath}");
                    imageSource.EndInit();

                    if (MainWindow.openVrConnection == null) return;

                    MainWindow.openVrConnection.ToolTip = isOnline ? "OpenVR Online" : "OpenVR Offline";
                    MainWindow.openVrConnection.Source = imageSource;
                });
            }
            catch (Exception ex)
            {
                // Handle any exceptions that may occur during loading
                MockConsole.WriteLine($"Error loading image: {ex.Message}", MockConsole.LogLevel.Error);
            }
        }
    }
}
