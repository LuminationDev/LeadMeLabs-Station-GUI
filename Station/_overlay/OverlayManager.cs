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

        private static Overlay? overlay = null;

        /// <summary>
        /// Start a new thread to handle the execution of the ping. Otherwise it will block the operation 
        /// until it has returned.
        /// </summary>
        public static void OverlayThread(string? text = null)
        {
            if (!running)
            {
                MockConsole.WriteLine("Running overlay");

                running = true;

                //Use the UI thread for window control
                Application.Current.Dispatcher.Invoke((Action)delegate {
                    RunOverlay(text);
                });
            }
            else
            {
                MockConsole.WriteLine("Already running");
            }
        }

        public static void RunOverlay(string? text = null)
        {
            overlay = new(text);
            _ = overlay.RunTask();
            overlay.Show();
        }

        public static void OverlayThreadManual(string? text = null)
        {
            Logger.WriteLog("Running offline overlay.", MockConsole.LogLevel.Normal);

            running = true;

            //Use the UI thread for window control
            Application.Current.Dispatcher.Invoke((Action)delegate
            {
                overlay = new Overlay(text);
                _ = overlay.ManualRun();
                overlay.Show();
            });
        }

        public static void ManualStop()
        {
            Logger.WriteLog("Stopping offline overlay.", MockConsole.LogLevel.Normal);

            if (overlay == null)
            {
                return;
            }
            Application.Current.Dispatcher.Invoke((Action)delegate
            {
                _ = overlay.ManualStop();
            });
        }
        
        public static void SetText(string text)
        {
            if (overlay == null)
            {
                return;
            }
            Application.Current.Dispatcher.Invoke((Action)delegate
            {
                overlay.SetText(text);
            });
        }
    }
}
