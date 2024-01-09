using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Station._utils;

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

            StartStationCommand = new RelayCommand(Manager.StartProgram);
            RestartStationCommand = new RelayCommand(Manager.RestartProgram);
            StopStationCommand = new RelayCommand(Manager.StopProgram);
            ChangeLogLevelCommand = new RelayCommand(MockConsole.ChangeLogLevel);
            StopCurrentProcess = new RelayCommand(WrapperManager.StopAProcess);
            ResetSteamVrProcess = new RelayCommand(RestartVr);

            NotifyIconOpenCommand = new RelayCommand(() => { WindowState = WindowState.Normal; });
            NotifyIconExitCommand = new RelayCommand(() => { Application.Current.Shutdown(); });
            
            // Debug processes
            ChangeViewConsoleValue = new RelayCommand(() => ViewConsoleWindow = !ViewConsoleWindow);
            ChangeMinimisingValue = new RelayCommand(() => MinimiseVrPrograms = !MinimiseVrPrograms);
            AutoStartVrValue = new RelayCommand(() => AutoStartVrPrograms = !AutoStartVrPrograms);
            HeadsetRequiredValue = new RelayCommand(() => HeadsetRequired = !HeadsetRequired);
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
        public ICommand ResetSteamVrProcess { get; }
        
        // Debug bindings
        public ICommand ChangeViewConsoleValue { get; }
        public ICommand ChangeMinimisingValue { get; }
        public ICommand AutoStartVrValue { get; }
        public ICommand HeadsetRequiredValue { get; }

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

        private void RestartVr()
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
        /// Used for binding the MainWindow MockConsole
        /// </summary>
        private string _consoleText = "";

        public string ConsoleText
        {
            get => _consoleText;
            set => SetProperty(ref _consoleText, value);
        }
        
        /// <summary>
        /// Below are the debug control logic from the UI into the program. 
        /// </summary>
        private bool ViewConsoleWindow
        {
            get => InternalDebugger.viewConsoleWindow;
            set
            {
                InternalDebugger.viewConsoleWindow = value;
                ViewConsoleText = value ? "Yes" : "No";
                if (!value)
                {
                    MockConsole.ClearConsole();
                }
            }
        }
        
        private string _viewConsoleText = "Yes";
        public string ViewConsoleText
        {
            get => _viewConsoleText;
            private set
            {
                if (_viewConsoleText == value) return;
                _viewConsoleText = value;
                OnPropertyChanged();
            }
        }
        
        private bool MinimiseVrPrograms
        {
            get => InternalDebugger.minimiseVrPrograms;
            set
            {
                InternalDebugger.minimiseVrPrograms = value;
                MinimisingText = value ? "Yes" : "No";
            }
        }
        
        private string _minimisingText = "Yes";
        public string MinimisingText
        {
            get => _minimisingText;
            private set
            {
                if (_minimisingText == value) return;
                _minimisingText = value;
                OnPropertyChanged();
            }
        }
        
        private bool AutoStartVrPrograms
        {
            get => InternalDebugger.autoStartVrPrograms;
            set
            {
                InternalDebugger.autoStartVrPrograms = value;
                AutoStartSteamText = value ? "Yes" : "No";
            }
        }
        
        private string _autoStartSteamText = "Yes";
        public string AutoStartSteamText
        {
            get => _autoStartSteamText;
            private set
            {
                if (_autoStartSteamText == value) return;
                _autoStartSteamText = value;
                OnPropertyChanged();
            }
        }
        
        private bool HeadsetRequired
        {
            get => InternalDebugger.headsetRequired;
            set
            {
                InternalDebugger.headsetRequired = value;
                AHeadsetRequiredText = value ? "Yes" : "No";
            }
        }
        
        private string _headsetRequiredText = "Yes";
        public string AHeadsetRequiredText
        {
            get => _headsetRequiredText;
            private set
            {
                if (_headsetRequiredText == value) return;
                _headsetRequiredText = value;
                OnPropertyChanged();
            }
        }
    }
}
