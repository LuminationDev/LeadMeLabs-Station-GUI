using System;
using System.Threading.Tasks;
using System.Windows;

namespace Station.Components._overlay;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class Overlay
{
    private readonly Overlay _current;

    public Overlay(string? text = null)
    {
        InitializeComponent();

        this.WindowState = WindowState.Maximized;

        _current = this;

        StationName.Text = text ?? "Station " + Environment.GetEnvironmentVariable("StationId", EnvironmentVariableTarget.Process);
        Spinner.Visibility = Visibility.Collapsed;
    }
    public async Task RunTask()
    {
        _current.Opacity = 0.1;
        await Identify();
        Close();
    }

    public async Task Identify()
    {
        for (int i = 0; i < 80; i++)
        {
            double level = (double)i / 100;
            _current.Opacity = level;
            await Task.Delay(10);
        }

        await Task.Delay(2000);

        for (int i = 80; i > 0; i--)
        {
            double level = (double)i / 100;
            _current.Opacity = level;
            await Task.Delay(10);
        }

        OverlayManager.running = false;
    }

    public async Task ManualRun()
    {
        Spinner.Visibility = Visibility.Visible;
        _current.Opacity = 0.1;
        for (int i = 0; i < 80; i++)
        {
            double level = (double)i / 100;
            _current.Opacity = level;
            await Task.Delay(10);
        }
    }
    
    public async Task ManualStop()
    {
        for (int i = 80; i > 0; i--)
        {
            double level = (double)i / 100;
            _current.Opacity = level;
            await Task.Delay(10);
        }

        OverlayManager.running = false;
        Close();
    }
    
    public void SetText(string text)
    {
        StationName.Text = text;
    }
}
