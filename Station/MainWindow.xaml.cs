using System.Windows;
using System.Windows.Forms;

namespace Station
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            Screen s = Screen.AllScreens[0];
            System.Drawing.Rectangle r  = s.WorkingArea;
            this.Top = r.Top;
            this.Left = r.Left;
            this.Width = r.Width;
            this.Height = r.Height;
            Loaded += MainWindow_Loaded;
        }

        private void Window_MouseLeftButtonDown(object sender, RoutedEventArgs e)
        {
            DragMove();
        }
        
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Maximize after position is defined in constructor
            WindowState = WindowState.Maximized;
        }
    }
}
