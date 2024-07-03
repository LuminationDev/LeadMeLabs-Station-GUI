using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LeadMeLabsLibrary;
using Station.Components._commandLine;
using Station.Components._utils;
using Station.Core;

namespace Station.MVC.View;

public partial class LogsView
{
    public LogsView()
    {
        InitializeComponent();
    }
    
    /// <summary>
    /// Open the local log folder.
    /// </summary>
    /// <param name="sender">The button that was clicked.</param>
    /// <param name="e">The event arguments.</param>
    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        if (StationCommandLine.StationLocation == null) return;
        string path = Path.GetFullPath(Path.Combine(StationCommandLine.StationLocation, "_logs"));
    
        try
        {
            Process.Start("explorer.exe", path);
        }
        catch (Exception ex)
        {
            Logger.WriteLog("An error occurred: " + ex.Message, Enums.LogLevel.Error);
        }
    }
    
    private void ListBoxItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Get the ListBoxItem that was right-clicked
        ListBoxItem? listBoxItem = sender as ListBoxItem;

        // Get the content of the ListBoxItem
        string? content = listBoxItem?.Content?.ToString();

        // Copy the content to the clipboard
        if (string.IsNullOrEmpty(content)) return;
        
        Clipboard.SetText(content);
            
        if (NotifyIconWrapper.Instance == null) return;
        NotifyIconWrapper.Instance.NotifyRequest = new NotifyIconWrapper.NotifyRequestRecord
        {
            Title = "Text Copied",
            Text = "Log text copied to clipboard",
            Duration = 5000
        };
    }
}
