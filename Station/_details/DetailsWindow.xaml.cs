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
        private bool SteamRules;

        public DetailsWindow()
        {
            InitializeComponent();
            LoadInitialValues();
            InitaliseSteamRules();

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
            ipAddress.Text = SystemInformation.GetIPAddress()?.ToString() ?? "Unknown";
            macAddress.Text = SystemInformation.GetMACAddress() ?? "Unknown";
            versionNumber.Text = SystemInformation.GetVersionNumber() ?? "Unknown";
            ProcessConsole.Text = Environment.GetEnvironmentVariable("NUCAddress", EnvironmentVariableTarget.Process) ?? "Not found";
            StatusConsole.Text = FirewallManagement.IsProgramAllowedThroughFirewall() ?? "Unknown";
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
                "IPAddress" => SystemInformation.GetIPAddress()?.ToString() ?? "Unknown",
                "MAC" => SystemInformation.GetMACAddress() ?? "Unknown",
                "Version" => SystemInformation.GetVersionNumber() ?? "Unknown",
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

        /// <summary>
        /// Initializes the Steam firewall rules by performing an initial check and updating the UI accordingly.
        /// </summary>
        private void InitaliseSteamRules()
        {
            string response = FirewallManagement.InitialCheck();

            if(!bool.TryParse(response, out SteamRules))
            {
                //Default
                SteamRules = true;
            }
            
            ToggleOffline.Content = "Steam: " + (SteamRules ? "Offline" : "Online");
        }

        /// <summary>
        /// Handles the click event for the Toggle Steam button, toggling the Steam firewall rules and updating the UI accordingly.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event arguments.</param>
        private void ToggleSteam_Click(object sender, RoutedEventArgs e)
        {
            Logger.WriteLog($"Attempting to toggle Steam firewall rules, enabled: {SteamRules}", MockConsole.LogLevel.Error);
            string responseSteam = FirewallManagement.ToggleRule("SteamBlocker", @"C:\Program Files (x86)\Steam\Steam.exe", SteamRules);
            string responseWeb = FirewallManagement.ToggleRule("SteamBlockerWeb", @"C:\Program Files (x86)\Steam\bin\cef\cef.win7x64\steamwebhelper.exe", SteamRules);
            string responseTour = FirewallManagement.ToggleRule("SteamBlockerTours", @"C:\program files (x86)\steam\steamapps\common\steamvr\tools\steamvr_environments\game\bin\win64\steamtours.exe", SteamRules);
            string responseVR = FirewallManagement.ToggleRule("SteamBlockerVR", @"C:\Program Files (x86)\Steam\steamapps\common\SteamVR\bin\win32\vrstartup.exe", SteamRules);

            bool successSteam = LogResponse(responseSteam, "SteamBlocker", SteamRules);
            bool successWeb = LogResponse(responseWeb, "SteamBlockerWeb", SteamRules);
            bool successTour = LogResponse(responseTour, "SteamBlockerTours", SteamRules);
            bool successVR = LogResponse(responseVR, "SteamBlockerVR", SteamRules);

            if (successSteam && successWeb && successTour && successVR)
            {
                ToggleOffline.Content = "Steam: " + (SteamRules ? "Offline" : "Online");
                Logger.WriteLog("Successfully toggled Steam firewall. Rules now: " + (SteamRules ? "Enabled" : "Disabled"), MockConsole.LogLevel.Error);
                SteamRules = !SteamRules;
            } else
            {
                Logger.WriteLog("Failed to toggle Steam firewall rules.", MockConsole.LogLevel.Error);
            }            
        }

        ///<summary>
        /// Logs the response of a specific action related to an outbound rule.
        ///</summary>
        ///<param name="action">The action performed.</param>
        ///<param name="ruleName">The name of the outbound rule.</param>
        ///<param name="enabled">Indicates whether the outbound rule is enabled or disabled.</param>
        ///<returns>
        /// Returns true if the action is "true" and the log entry is successfully written.
        /// Returns true if the action is "Created" and a new outbound rule is created successfully.
        /// Returns false for any other action.
        ///</returns>
        private bool LogResponse(string action, string ruleName, bool enabled)
        {
            if(action.Equals("true"))
            {
                Logger.WriteLog($"Existing outbound rule '{ruleName}' " + (enabled ? "enabled" : "disabled") + " successfully.", MockConsole.LogLevel.Error);
                return true;
            }
            else if (action.Equals("Created"))
            {
                Logger.WriteLog($"New outbound rule '{ruleName}' created successfully.", MockConsole.LogLevel.Error);
                return true;
            } else
            {
                return false;
            }
        }
    }
}
