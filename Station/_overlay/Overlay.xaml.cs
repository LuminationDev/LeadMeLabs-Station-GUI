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
        readonly Overlay current;

        public Overlay()
        {
            InitializeComponent();

            current = this;

            StationName.Text = "Station " + Environment.GetEnvironmentVariable("StationId");
            _ = runTask();
        }

        public async Task runTask()
        {
            await identify();
            Close();
        }

        public async Task identify()
        {
            for (int i = 0; i < 80; i++)
            {
                double level = i / 100;
                current.Opacity = level;
                await Task.Delay(10);
            }

            await Task.Delay(2000);

            for (int i = 80; i > 0; i--)
            {
                double level = i / 100;
                current.Opacity = level;
                await Task.Delay(10);
            }

            OverlayManager.running = false;
        }
    }
}
