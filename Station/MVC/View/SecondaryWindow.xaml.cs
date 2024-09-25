using System;
using System.Windows;
using System.Windows.Forms;
using Station.MVC.ViewModel;
using MessageBox = System.Windows.Forms.MessageBox;

namespace Station.MVC.View;

public partial class SecondaryWindow
{
    public SecondaryWindow()
    {
        InitializeComponent();

        Screen s = Screen.AllScreens[0];
        System.Drawing.Rectangle r  = s.WorkingArea;
        this.Top = r.Top;
        this.Left = r.Left;
        this.Width = r.Width;
        this.Height = r.Height;
        
        // Subscribe to the ViewModel's MediaElementLoaded event
        if (DataContext is SecondaryViewModel viewModel)
        {
            // Subscribe to the event, and pass the MediaElement
            viewModel.MediaElementLoaded += _ => viewModel.PlayMedia(VideoControl);
        }
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
