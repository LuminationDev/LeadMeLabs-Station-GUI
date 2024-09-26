using System;
using System.Windows;
using System.Windows.Forms;
using Station.Components._windows;
using Station.MVC.ViewModel;
using MessageBox = System.Windows.Forms.MessageBox;

namespace Station.MVC.View;

public partial class SecondaryWindow
{
    public SecondaryWindow()
    {
        InitializeComponent();
        
        int index = WindowTracker.PrimaryScreenIndex;
        Screen s = Screen.AllScreens[index];
        System.Drawing.Rectangle r  = s.WorkingArea;
        this.Top = r.Top;
        this.Left = r.Left;
        this.Width = r.Width;
        this.Height = r.Height;
        this.Topmost = true;
        
        // Subscribe to the ViewModel's MediaElementLoaded event
        if (DataContext is not SecondaryViewModel viewModel) return;
        
        // Subscribe to the event, and pass the MediaElement
        viewModel.MediaElementLoaded += _ => viewModel.PlayMedia(VideoControl);
        SecondaryViewModel.ToggleTopmostRequested += SetAlwaysOnTop;
    }
    
    //TODO THIS IS NOT DISENGAGING - NEEDS TO BE ON OWNING THREAD PASS TO DISPATCHER
    /// <summary>
    /// Toggle between the Secondary window being the TopMost (nothing appears in front) and allowing other programs
    /// to be placed on top.
    /// </summary>
    /// <param name="isTopmost">A bool of if the window should be top most (true) or not (false)</param>
    private void SetAlwaysOnTop(bool isTopmost)
    {
        Dispatcher.Invoke((Action)delegate { this.Topmost = isTopmost; });
    }

    private void Window_MouseLeftButtonDown(object sender, RoutedEventArgs e)
    {
        DragMove();
    }

    private void myMediaElement_MediaEnded(object sender, RoutedEventArgs e)
    {
        // Set the position back to the beginning
        VideoControl.Position = TimeSpan.Zero;
        VideoControl.Play(); // Replay the video
    }

    private void myMediaElement_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        MessageBox.Show($"Error loading media: {e.ErrorException.Message}");
    }
}
