using System;
using System.Windows;
using System.Threading.Tasks;

namespace Station
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class Overlay : Window
    {
        private readonly Overlay current;

        public Overlay(string? text = null)
        {
            InitializeComponent();

            this.WindowState = WindowState.Maximized;

            current = this;

            StationName.Text = text ?? "Station " + Environment.GetEnvironmentVariable("StationId");
            Spinner.Visibility = Visibility.Collapsed;
        }
        public async Task RunTask()
        {
            current.Opacity = 0.1;
            await Identify();
            Close();
        }

        public async Task Identify()
        {
            for (int i = 0; i < 80; i++)
            {
                double level = (double)i / 100;
                current.Opacity = level;
                await Task.Delay(10);
            }

            await Task.Delay(2000);

            for (int i = 80; i > 0; i--)
            {
                double level = (double)i / 100;
                current.Opacity = level;
                await Task.Delay(10);
            }

            OverlayManager.running = false;
        }

        public async Task ManualRun()
        {
            Spinner.Visibility = Visibility.Visible;
            current.Opacity = 0.1;
            for (int i = 0; i < 100; i++)
            {
                double level = (double)i / 100;
                current.Opacity = level;
                await Task.Delay(10);
            }
        }
        
        public async Task ManualStop()
        {
            for (int i = 80; i > 0; i--)
            {
                double level = (double)i / 100;
                current.Opacity = level;
                await Task.Delay(10);
            }
            Close();
        }
        
        public void SetText(string text)
        {
            StationName.Text = text;
        }
    }
}
