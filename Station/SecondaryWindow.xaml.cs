using System;
using System.Windows;
using System.Windows.Forms;
using MessageBox = System.Windows.Forms.MessageBox;

namespace Station
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class SecondaryWindow : Window
    {
        public SecondaryWindow()
        {
            InitializeComponent();
            
            Screen s = Screen.AllScreens[1];
            System.Drawing.Rectangle r  = s.WorkingArea;
            this.Top = r.Top;
            this.Left = r.Left;
            this.Width = r.Width;
            this.Height = r.Height;
            Loaded += SecondaryWindow_Loaded;
        }

        private void Window_MouseLeftButtonDown(object sender, RoutedEventArgs e)
        {
            DragMove();
        }
        
        private void SecondaryWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Maximize after position is defined in constructor
            WindowState = WindowState.Maximized;
            // Screen s = Screen.AllScreens[0];
            // System.Drawing.Rectangle r  = s.WorkingArea;
            // VideoControl.Top = r.Top;
            // VideoControl.Left = r.Left;
            // VideoControl.Width = r.Width;
            // VideoControl.Height = r.Height;
            VideoControl.Play();
        }
        
        private void myMediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            // Set the position back to the beginning
            VideoControl.Position = TimeSpan.Zero;
            // Replay the video
            VideoControl.Play();
        }
        
        private void myMediaElement_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            MessageBox.Show($"Error loading media: {e.ErrorException.Message}");
        }
    }
}