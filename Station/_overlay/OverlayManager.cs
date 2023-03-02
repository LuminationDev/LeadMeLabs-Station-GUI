using System;
using System.Windows;

namespace Station
{
    static class OverlayManager
    {
        /// <summary>
        /// Flag to check if the command is already running. This stops doubling up on the 
        /// flashes.
        /// </summary>
        public static bool running = false;

        /// <summary>
        /// Start a new thread to handle the execution of the ping. Otherwise it will block the operation 
        /// until it has returned.
        /// </summary>
        public static void overlayThread()
        {
            if (!running)
            {
                running = true;

                //Use the UI thread for window control
                Application.Current.Dispatcher.Invoke((Action)delegate {
                    runOverlay();
                });
            }
            else
            {
                MockConsole.WriteLine("Already pinging");
            }
        }

        public static void runOverlay()
        {
            Overlay overlay = new();
            overlay.Show();
        }
    }
}
