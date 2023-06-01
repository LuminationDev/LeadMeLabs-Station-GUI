using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Station._details
{
    /// <summary>
    /// Interaction logic for DetailsWindow.xaml
    /// </summary>
    public partial class DetailsWindow : Window
    {
        public static TextBlock? IPAddress;
        public static TextBlock? MacAddress;
        public static TextBlock? VersionNum;
        public static TextBlock? SteamGuard;

        public DetailsWindow()
        {
            InitializeComponent();

            ipAddress.Text = Manager.GetIPAddress();
            macAddress.Text = Manager.GetMACAddress();
            versionNumber.Text = Manager.GetVersionNumber();
            steamGuard.Text = SteamScripts.steamCMDConfigured;

            IPAddress = ipAddress;
            MacAddress = macAddress;
            VersionNum = versionNumber;
            SteamGuard = steamGuard;

            ProcessConsole.Content = Environment.GetEnvironmentVariable("NUCAddress");
            StatusConsole.Content = FirewallManagement.IsProgramAllowedThroughFirewall();
        }

        private DoubleAnimation _animation = new DoubleAnimation
        {
            From = 0,
            To = 360,
            Duration = TimeSpan.FromSeconds(2),
            RepeatBehavior = new RepeatBehavior(1)
        };

        private void IP_Click(object sender, RoutedEventArgs e)
        {
            RotationIP.BeginAnimation(RotateTransform.AngleProperty, _animation);
        }

        private void Mac_Click(object sender, RoutedEventArgs e)
        {
            RotationMac.BeginAnimation(RotateTransform.AngleProperty, _animation);
        }

        private void Version_Click(object sender, RoutedEventArgs e)
        {
            RotationVersion.BeginAnimation(RotateTransform.AngleProperty, _animation);
        }

        private void SteamGuard_Click(object sender, RoutedEventArgs e)
        {
            DoubleAnimation animation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromSeconds(5),
                RepeatBehavior = new RepeatBehavior(1)
            };
            RotationSteam.BeginAnimation(RotateTransform.AngleProperty, animation);
        }
    }
}
