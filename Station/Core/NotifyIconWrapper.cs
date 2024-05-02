using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using LeadMeLabsLibrary;
using Station._details;
using Station.Components._commandLine;
using Station.Components._utils;
using Station.Components._utils._steamConfig;
using Application = System.Windows.Application;

namespace Station.Core;

public class NotifyIconWrapper : FrameworkElement, IDisposable
{
    public static NotifyIconWrapper? Instance { get; set; }

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register("Text", typeof(string), typeof(NotifyIconWrapper), new PropertyMetadata(
            (d, e) =>
            {
                var notifyIcon = ((NotifyIconWrapper)d)._notifyIcon;
                if (notifyIcon == null)
                    return;
                notifyIcon.Text = (string)e.NewValue;
            }));

    private static readonly DependencyProperty NotifyRequestProperty =
        DependencyProperty.Register("NotifyRequest", typeof(NotifyRequestRecord), typeof(NotifyIconWrapper),
            new PropertyMetadata(
                (d, e) =>
                {
                    var r = (NotifyRequestRecord)e.NewValue;
                    ((NotifyIconWrapper)d)._notifyIcon?.ShowBalloonTip(r.Duration, r.Title, r.Text, r.Icon);
                }));

    private static readonly RoutedEvent OpenSelectedEvent = EventManager.RegisterRoutedEvent("OpenSelected",
        RoutingStrategy.Direct, typeof(RoutedEventHandler), typeof(NotifyIconWrapper));

    private static readonly RoutedEvent ExitSelectedEvent = EventManager.RegisterRoutedEvent("ExitSelected",
        RoutingStrategy.Direct, typeof(RoutedEventHandler), typeof(NotifyIconWrapper));

    private readonly NotifyIcon? _notifyIcon;
    private string? _iconPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

    public NotifyIconWrapper()
    {
        if (DesignerProperties.GetIsInDesignMode(this))
            return;
        _notifyIcon = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(_iconPath + @"\Assets\Icons\icon.ico"),
            Visible = true,
            Text = "Station",
            ContextMenuStrip = CreateContextMenu()
        };
        _notifyIcon.DoubleClick += OpenItemOnClick;
        Application.Current.Exit += (_, _) => { _notifyIcon.Dispose(); };

        Instance = this;
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public NotifyRequestRecord NotifyRequest
    {
        get => (NotifyRequestRecord)GetValue(NotifyRequestProperty);
        set => SetValue(NotifyRequestProperty, value);
    }

    public void Dispose()
    {
        _notifyIcon?.Dispose();
    }

    public event RoutedEventHandler OpenSelected
    {
        add => AddHandler(OpenSelectedEvent, value);
        remove => RemoveHandler(OpenSelectedEvent, value);
    }

    public event RoutedEventHandler ExitSelected
    {
        add => AddHandler(ExitSelectedEvent, value);
        remove => RemoveHandler(ExitSelectedEvent, value);
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var openItem = new ToolStripMenuItem("Open");
        openItem.Click += OpenItemOnClick;

        var openDetails = new ToolStripMenuItem("Details");
        openDetails.Click += OpenDetailsOnClick;

        var roomSetup = new ToolStripMenuItem("Save room setup");
        roomSetup.Click += SaveRoomSetupClick;

        var goToLogs = new ToolStripMenuItem("Logs");
        goToLogs.Click += GoToLogsOnClick;

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += ExitItemOnClick;

        var contextMenu = new ContextMenuStrip { Items = { openItem, openDetails, roomSetup, goToLogs, exitItem } };
        return contextMenu;
    }

    private void OpenItemOnClick(object? sender, EventArgs eventArgs)
    {
        var args = new RoutedEventArgs(OpenSelectedEvent);
        RaiseEvent(args);
    }

    private void OpenDetailsOnClick(object? sender, EventArgs eventArgs)
    {
        DetailsWindow details = new DetailsWindow();
        details.Show();
    }

    private void SaveRoomSetupClick(object? sender, EventArgs eventArgs)
    {
        string message = RoomSetup.SaveRoomSetup();
        NotifyRequest = new NotifyRequestRecord
        {
            Title = "Room Setup",
            Text = message,
            Duration = 5000
        };
    }

    private void GoToLogsOnClick(object? sender, EventArgs eventArgs)
    {
        string? location = CommandLine.StationLocation;

        if (location == null)
        {
            Logger.WriteLog("NotifyIconWrapper - GoToLogsOnClick: Station location not found.", Enums.LogLevel.Error);
            return;
        }
        
        string path = Path.GetFullPath(Path.Combine(location, "_logs"));

        try
        {
            Process.Start("explorer.exe", path);
        }
        catch (Exception ex)
        {
            Logger.WriteLog("NotifyIconWrapper - GoToLogsOnClick: An error occurred: " + ex.Message, Enums.LogLevel.Error);
        }
    }

    private void ExitItemOnClick(object? sender, EventArgs eventArgs)
    {
        var args = new RoutedEventArgs(ExitSelectedEvent);
        RaiseEvent(args);
    }

    public class NotifyRequestRecord
    {
        public string Title { get; set; } = "";
        public string Text { get; set; } = "";
        public int Duration { get; set; } = 1000;
        public ToolTipIcon Icon { get; set; } = ToolTipIcon.Info;
    }

    /// <summary>
    /// Update the tray icon and tooltip to represent the current status of the software.
    /// </summary>
    /// <param name="status">A string of the current software operating status</param>
    public void ChangeIcon(string status)
    {
        if (_notifyIcon == null || _iconPath == null)
        {
            return;
        }

        //Don't continously set the icon if it is the same
        if (_iconPath.Contains(status))
        {
            return;
        }

        switch (status)
        {
            case "offline":
                _iconPath = @"\Assets\Icons\offline.ico";
                break;
            case "online":
                _iconPath = @"\Assets\Icons\online.ico";
                break;
            default:
                _iconPath = @"\Assets\Icons\icon.ico";
                break;
        }

        _notifyIcon.Icon = Icon.ExtractAssociatedIcon(CommandLine.StationLocation + _iconPath);
        _notifyIcon.Text = $"Station - {status}";
    }
}
