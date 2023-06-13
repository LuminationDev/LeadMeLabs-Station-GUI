using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LeadMeLabsLibrary;

namespace Station._details
{
    /// <summary>
    /// Interaction logic for DetailsWindow.xaml
    /// </summary>
    public partial class DetailsWindow : Window
    {
        private readonly Dictionary<string, RotateTransform> buttonRotationMap;

        public DetailsWindow()
        {
            InitializeComponent();
            LoadInitialValues();

            //Steam is handled differently
            steamGuard.Text = SteamScripts.steamCMDConfigured;

            buttonRotationMap = new Dictionary<string, RotateTransform>
            {
                { "IPAddress", RotationIP },
                { "MAC", RotationMac },
                { "Version", RotationVersion },
                { "SteamGuard", RotationSteam }
            };
        }

        /// <summary>
        /// Load in the inital details of the software.
        /// </summary>
        private void LoadInitialValues()
        {
            ipAddress.Text = Manager.GetIPAddress();
            macAddress.Text = Manager.GetMACAddress();
            versionNumber.Text = Manager.GetVersionNumber();
            ProcessConsole.Text = Environment.GetEnvironmentVariable("NUCAddress");
            StatusConsole.Text = FirewallManagement.IsProgramAllowedThroughFirewall();
        }

        private DoubleAnimation _animation = new DoubleAnimation
        {
            From = 0,
            To = 360,
            Duration = TimeSpan.FromSeconds(2),
            RepeatBehavior = new RepeatBehavior(1)
        };

        /// <summary>
        /// Event handler for the button click event.
        /// Retrieves the associated text block and applies rotation animation to it.
        /// </summary>
        /// <param name="sender">The button that was clicked.</param>
        /// <param name="e">The event arguments.</param>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Button clickedButton = (Button)sender;
            TextBlock? textBlock = FindModelDetailsTextBlock(clickedButton);
            if (textBlock == null) return;
            textBlock.Text = "Loading";

            if (clickedButton.Name.Equals("SteamGuard"))
            {
                steamGuard.Text = "Loading";
                DoubleAnimation animation = new DoubleAnimation
                {
                    From = 0,
                    To = 360,
                    Duration = TimeSpan.FromSeconds(5),
                    RepeatBehavior = new RepeatBehavior(1)
                };
                RotationSteam.BeginAnimation(RotateTransform.AngleProperty, animation);
                RefreshSteamGuard();
            } else
            {
                RotateTransform? rotation = GetRotationTransform(clickedButton, textBlock);
                if (rotation == null) return;
                rotation.BeginAnimation(RotateTransform.AngleProperty, _animation);
            }
        }

        /// <summary>
        /// Collect the associated InfoText Textblock a button is assocaited with.
        /// </summary>
        private static TextBlock? FindModelDetailsTextBlock(Button button)
        {
            DependencyObject parent = VisualTreeHelper.GetParent(button);

            while (parent != null && parent is not WrapPanel)
            {
                parent = VisualTreeHelper.GetParent(parent);
            }

            if (parent is WrapPanel wrapPanel)
            {
                var infoTextBlock = wrapPanel.Children.OfType<TextBlock>()
                    .FirstOrDefault(textBlock => textBlock.Tag is string tag && tag == "InfoText");

                return infoTextBlock;
            }

            return null;
        }

        /// <summary>
        /// Retrieves the corresponding rotation transform for the given button and updates the text block with new text asynchronously.
        /// </summary>
        /// <param name="button">The button associated with the rotation transform.</param>
        /// <param name="textBlock">The text block to update with new text.</param>
        /// <returns>The rotation transform associated with the button, or null if not found.</returns>
        private RotateTransform? GetRotationTransform(Button button, TextBlock textBlock)
        {
            if (buttonRotationMap.TryGetValue(button.Name, out RotateTransform? rotationTransform))
            {
                DelayAndRefresh(textBlock, button.Name);
                return rotationTransform;
            }

            return null;
        }

        /// <summary>
        /// Refreshes the text of the given text block after a delay.
        /// </summary>
        /// <param name="textBlock">The text block to refresh.</param>
        /// <param name="newText">The new text to update the text block with.</param>
        private void DelayAndRefresh(TextBlock textBlock, string buttonName)
        {
            Task.Run(async () =>
            {
                string newText = GetNewText(buttonName);
                await Task.Delay(2000);

                // Update the UI on the main thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    textBlock.Text = newText;
                });
            });
        }

        /// <summary>
        /// Gets the new text based on the given element name.
        /// </summary>
        /// <param name="buttonName">The name of the button.</param>
        /// <returns>The new text to update the text block with.</returns>
        private static string GetNewText(string buttonName)
        {
            return buttonName switch
            {
                "IPAddress" => Manager.GetIPAddress() ?? "Unknown",
                "MAC" => Manager.GetMACAddress() ?? "Unknown",
                "Version" => Manager.GetVersionNumber() ?? "Unknown",
                _ => string.Empty,
            };
        }

        /// <summary>
        /// Refresh all values for the NUC.
        /// </summary>
        /// <param name="sender">The button that was clicked.</param>
        /// <param name="e">The event arguments.</param>
        private void RefreshAll_Click(object sender, RoutedEventArgs e)
        {
            Logger.WriteLog("Refreshing all items", MockConsole.LogLevel.Normal);
            ipAddress.Text = "Loading";
            macAddress.Text = "Loading";
            versionNumber.Text = "Loading";
            steamGuard.Text = "Loading";
            RefreshSteamGuard();
            ProcessConsole.Text = "Loading";
            StatusConsole.Text = "Loading";

            Task.Run(async () =>
            {
                await Task.Delay(2000);

                // Update the UI on the main thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    LoadInitialValues();
                });
            });
        }

        private async void RefreshSteamGuard()
        {
            SteamScripts.QuerySteamConfig();
            await Task.Delay(5000);
            Application.Current.Dispatcher.Invoke(() =>
            {
                steamGuard.Text = SteamScripts.steamCMDConfigured;
            });
        }
    }
}
