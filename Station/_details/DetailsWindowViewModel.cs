using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Station._details
{
    public class DetailsWindowViewModel
    {
        public DetailsWindowViewModel()
        {
            RefreshIpAddressCommand = new RelayCommand(() => RefreshIpAddress());
            RefreshMacAddressCommand = new RelayCommand(() => RefreshMacAddress());
            RefreshVersionCommand = new RelayCommand(() => RefreshVersion());
            RefreshSteamGuardCommand = new RelayCommand(() => RefreshSteamGuard());
            RefreshAllCommand = new RelayCommand(() => RefreshAll());
        }

        public ICommand RefreshIpAddressCommand { get; }
        public ICommand RefreshMacAddressCommand { get; }
        public ICommand RefreshVersionCommand { get; }
        public ICommand RefreshSteamGuardCommand { get; }
        public ICommand RefreshAllCommand { get; }

        private async void RefreshIpAddress()
        {
            DetailsWindow.IPAddress.Text = "Loading...";
            Logger.WriteLog("Refreshing IP Address", MockConsole.LogLevel.Normal);
            await Task.Delay(2000);
            DetailsWindow.IPAddress.Text = Manager.GetIPAddress();
        }

        private async void RefreshMacAddress()
        {
            DetailsWindow.MacAddress.Text = "Loading...";
            Logger.WriteLog("Refreshing Mac Address", MockConsole.LogLevel.Normal);
            await Task.Delay(2000);
            DetailsWindow.MacAddress.Text = Manager.GetMACAddress();
        }

        private async void RefreshVersion()
        {
            DetailsWindow.VersionNum.Text = "Loading...";
            Logger.WriteLog("Refreshing Version code", MockConsole.LogLevel.Normal);
            await Task.Delay(2000);
            DetailsWindow.VersionNum.Text = Manager.GetVersionNumber();
        }

        private async void RefreshSteamGuard()
        {
            DetailsWindow.SteamGuard.Text = "Loading...";
            Logger.WriteLog("Refreshing Version code", MockConsole.LogLevel.Normal);
            SteamScripts.QuerySteamConfig();
            await Task.Delay(5000);
            DetailsWindow.SteamGuard.Text = SteamScripts.steamCMDConfigured;
        }

        private void RefreshAll()
        {
            Logger.WriteLog("Refreshing all items", MockConsole.LogLevel.Normal);
            RefreshIpAddress();
            RefreshMacAddress();
            RefreshVersion();
        }
    }
}
