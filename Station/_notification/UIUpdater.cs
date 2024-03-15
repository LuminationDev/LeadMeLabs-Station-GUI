using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Station._notification;

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
    /// Update the current process displayed on the Xaml window
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
    /// Update a status on the OpenVR Xaml panel
    /// </summary>
    /// <param name="label">A string of the Label to update.</param>
    /// <param name="status">A string of the current status.</param>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public static void UpdateOpenVRStatus(string label, string status)
    {
        bool isOnline;
        BitmapImage imageSource;
        
        Application.Current.Dispatcher.Invoke(delegate {
            switch (label)
            {
                case "headsetDescription":
                    if (MainWindow.headsetDescription == null) return;
                    MainWindow.headsetDescription.Content = status;
                    break;
                case "headsetConnection":
                    if (MainWindow.headsetConnection == null) return;
                    isOnline = status.Equals("Connected");
                    imageSource = GetActiveIcon(isOnline);
                    MainWindow.headsetConnection.ToolTip = isOnline ? "Headset Connected" : "Headset Lost";
                    MainWindow.headsetConnection.Source = imageSource;
                    break;
                case "leftControllerConnection":
                    if (MainWindow.leftControllerConnection == null) return;
                    isOnline = status.Equals("Connected");
                    imageSource = GetActiveIcon(isOnline);
                    MainWindow.leftControllerConnection.ToolTip = isOnline ? "Left Controller Connected" : "Left Controller Lost";
                    MainWindow.leftControllerConnection.Source = imageSource;
                    break;
                case "leftControllerBattery":
                    if (MainWindow.leftControllerBattery == null) return;
                    MainWindow.leftControllerBattery.Content = $"{status}%";
                    break;
                case "rightControllerConnection":
                    if (MainWindow.rightControllerConnection == null) return;
                    isOnline = status.Equals("Connected");
                    imageSource = GetActiveIcon(isOnline);
                    MainWindow.rightControllerConnection.ToolTip = isOnline ? "Right Controller Connected" : "Right Controller Lost";
                    MainWindow.rightControllerConnection.Source = imageSource;
                    break;
                case "rightControllerBattery":
                    if (MainWindow.rightControllerBattery == null) return;
                    MainWindow.rightControllerBattery.Content = $"{status}%";
                    break;
                case "baseStationActive":
                    if (MainWindow.baseStationActive == null) return;
                    MainWindow.baseStationActive.Content = status;
                    break;
                case "baseStationAmount":
                    if (MainWindow.baseStationAmount == null) return;
                    MainWindow.baseStationAmount.Content = status;
                    break;
            }
        });
    }

    /// <summary>
    /// Change the colour of the OpenVR image to symbolise the current connection status.
    /// </summary>
    /// <param name="type">A string of the VR management type that is being updated</param>
    /// <param name="isOnline">A bool representing if the connection is online (true) or offline (false)</param>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public static void LoadImageFromAssetFolder(string type, bool isOnline)
    {
        try
        {
            // Assign the image source to the Image control
            Application.Current.Dispatcher.Invoke(delegate {
                BitmapImage imageSource = GetActiveIcon(isOnline);

                switch (type)
                {
                    case "OpenVR":
                        if (MainWindow.openVrConnection == null) return;
                        MainWindow.openVrConnection.ToolTip = isOnline ? "OpenVR Online" : "OpenVR Offline";
                        MainWindow.openVrConnection.Source = imageSource;
                        break;
                    
                    case "ThirdParty":
                        if (MainWindow.headsetVrConnection == null) return;
                        MainWindow.headsetVrConnection.ToolTip = isOnline ? "HeadsetVR Online" : "HeadsetVR Offline";
                        MainWindow.headsetVrConnection.Source = imageSource;
                        break;
                }
            });
        }
        catch (Exception ex)
        {
            // Handle any exceptions that may occur during loading
            MockConsole.WriteLine($"Error loading image: {ex.Message}", MockConsole.LogLevel.Error);
        }
    }

    /// <summary>
    /// Retrieve the correct bitmap icon depending on if the supplied value is true (online)
    /// or false (offline).
    /// </summary>
    /// <param name="isOnline">A bool of if the value is online.</param>
    /// <returns>A Bitmap of the correct icon to display.</returns>
    private static BitmapImage GetActiveIcon(bool isOnline)
    {
        string iconPath = isOnline ? "openvr_online.ico" : "openvr_offline.ico";

        // Load the image from the asset folder
        BitmapImage imageSource = new BitmapImage();
        imageSource.BeginInit();
        imageSource.UriSource = new Uri($"pack://application:,,,/Station;component/Assets/Icons/{iconPath}");
        imageSource.EndInit();

        return imageSource;
    }
}
