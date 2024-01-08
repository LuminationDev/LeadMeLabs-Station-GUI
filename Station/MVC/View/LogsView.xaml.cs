using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Station.Components._commandLine;
using Station.Components._notification;
using Station.Components._utils;

namespace Station.MVC.View;

public partial class LogsView
{
    public LogsView()
    {
        InitializeComponent();
    }
    
    /// <summary>
    /// Refresh all values for the NUC.
    /// </summary>
    /// <param name="sender">The button that was clicked.</param>
    /// <param name="e">The event arguments.</param>
    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        if (CommandLine.StationLocation == null) return;
        string path = Path.GetFullPath(Path.Combine(CommandLine.StationLocation, "_logs"));
    
        try
        {
            Process.Start("explorer.exe", path);
        }
        catch (Exception ex)
        {
            Logger.WriteLog("An error occurred: " + ex.Message, MockConsole.LogLevel.Error);
        }
    }
}
