using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Station
{
    public class MainWindowViewModel : ObservableRecipient
    {
        private NotifyIconWrapper.NotifyRequestRecord? _notifyRequest;
        private bool _showInTaskbar;
        private WindowState _windowState;

        public MainWindowViewModel()
        {
            LoadedCommand = new RelayCommand(Loaded);
            ClosingCommand = new RelayCommand<CancelEventArgs>(Closing);

            StartStationCommand = new RelayCommand(() => Manager.StartProgram());
            RestartStationCommand = new RelayCommand(() => Manager.RestartProgram());
            StopStationCommand = new RelayCommand(() => Manager.StopProgram());
            ChangeLogLevelCommand = new RelayCommand(() => MockConsole.ChangeLogLevel());
            StopCurrentProcess = new RelayCommand(() => WrapperManager.StopAProcess());
            ResetSteamVRProcess = new RelayCommand(() => RestartVR());

            NotifyIconOpenCommand = new RelayCommand(() => { WindowState = WindowState.Normal; });
            NotifyIconExitCommand = new RelayCommand(() => { Application.Current.Shutdown(); });
        }

        public ICommand LoadedCommand { get; }
        public ICommand ClosingCommand { get; }
        public ICommand NotifyIconOpenCommand { get; }
        public ICommand NotifyIconExitCommand { get; }

        //Button bindings
        public ICommand StartStationCommand { get; }
        public ICommand RestartStationCommand { get; }
        public ICommand StopStationCommand { get; }
        public ICommand ChangeLogLevelCommand { get; }
        public ICommand StopCurrentProcess { get; }
        public ICommand ResetSteamVRProcess { get; }

        public WindowState WindowState
        {
            get => _windowState;
            set
            {
                ShowInTaskbar = true;
                SetProperty(ref _windowState, value);
                ShowInTaskbar = value != WindowState.Minimized;
            }
        }

        public bool ShowInTaskbar
        {
            get => _showInTaskbar;
            set => SetProperty(ref _showInTaskbar, value);
        }

        public NotifyIconWrapper.NotifyRequestRecord? NotifyRequest
        {
            get => _notifyRequest;
            set => SetProperty(ref _notifyRequest, value);
        }

        private void RestartVR()
        {
            new Task(() =>
            {
                ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage($"SoftwareState,Shutting down VR processes"), TimeSpan.FromSeconds(1));
                _ = WrapperManager.RestartVRProcesses();
            }).Start();
        }

        /// <summary>
        /// Determine how the window is first presented when initially loaded.
        /// </summary>
        private void Loaded()
        {
            WindowState = WindowState.Minimized;
        }

        private void Closing(CancelEventArgs? e)
        {
            if (e == null)
                return;
            e.Cancel = true;
            WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// Used for binding the MainWindow Mockconsole
        /// </summary>
        private string _consoleText = "";

        public string ConsoleText
        {
            get => _consoleText;
            set => SetProperty(ref _consoleText, value);
        }
    }
}
