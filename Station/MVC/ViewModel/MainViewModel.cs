using System.Windows;
using Station.Core;
using Station.MVC.Controller;

namespace Station.MVC.ViewModel;

public class MainViewModel : ObservableObject
{
    public static readonly ViewModelLocator ViewModelManager = new();
    
    // Software control
    public RelayCommand PowerCommand { get; }
    public RelayCommand RestartCommand { get; }
    
    // Views
    public RelayCommand HomeViewCommand { get; }
    public RelayCommand ConsoleViewCommand { get; }
    public RelayCommand ExperiencesViewCommand { get; }
    public RelayCommand DebugViewCommand { get; }
    public RelayCommand LogsViewCommand { get; }
    public RelayCommand QaViewCommand { get; }
    
    private object _currentView = null!;
    public object CurrentView
    {
        get => _currentView;
        private set
        {
            _currentView = value;
            OnPropertyChanged();
        }
    }
    
    private object _status = null!;
    public object Status
    {
        get => _status;
        set
        {
            _status = value;
            OnPropertyChanged();
        }
    }
    
    private string _id = "Station";
    public string Id
    {
        get => _id;
        set
        {
            _id = $"Station {value}";
            OnPropertyChanged();
        }
    }
    
    // Commands
    public RelayCommand LoadedCommand { get; }
    public RelayCommand ResizeCommand { get; }
    public RelayCommand ClosingCommand { get; }
    public RelayCommand NotifyIconOpenCommand { get; }
    public RelayCommand NotifyIconExitCommand { get; }
    
    public MainViewModel()
    {
        LoadedCommand = new RelayCommand(_ => Loaded());
        ResizeCommand = new RelayCommand(_ => MaximiseOrMinimise());
        ClosingCommand = new RelayCommand(_ => Closing());
        NotifyIconOpenCommand = new RelayCommand(_ => { WindowState = WindowState.Normal; });
        NotifyIconExitCommand = new RelayCommand(_ => { Application.Current.Shutdown(); });

        PowerCommand = new RelayCommand(_ => PowerFunction());
        RestartCommand = new RelayCommand(_ => MainController.RestartProgram());
        
        CurrentView = ViewModelManager.HomeViewModel;

        HomeViewCommand = new RelayCommand(_ => CurrentView = ViewModelManager.HomeViewModel);
        ConsoleViewCommand = new RelayCommand(_ => CurrentView = ViewModelManager.ConsoleViewModel);
        ExperiencesViewCommand = new RelayCommand(_ => CurrentView = ViewModelManager.ExperiencesViewModel);
        DebugViewCommand = new RelayCommand(_ => CurrentView = ViewModelManager.DebugViewModel);
        LogsViewCommand = new RelayCommand(_ => CurrentView = ViewModelManager.LogsViewModel);
        QaViewCommand = new RelayCommand(_ => CurrentView = ViewModelManager.QaViewModel);

        ViewModelManager.MainViewModel = this;
    }

    private Core.NotifyIconWrapper.NotifyRequestRecord? _notifyRequest;
    private bool _showInTaskbar;
    private WindowState _windowState;
    
    public WindowState WindowState
    {
        get => _windowState;
        set
        {
            ShowInTaskbar = true;
            _windowState = value;
            OnPropertyChanged();
            ShowInTaskbar = value != WindowState.Minimized;
        }
    }

    public bool ShowInTaskbar
    {
        get => _showInTaskbar;
        private set
        {
            _showInTaskbar = value;
            OnPropertyChanged();
        }
    }

    public Core.NotifyIconWrapper.NotifyRequestRecord? NotifyRequest
    {
        get => _notifyRequest;
        set
        {
            _notifyRequest = value;
            OnPropertyChanged();
        }
    }

    // private void Notify(string message)
    // {
    //     NotifyRequest = new NotifyIconWrapper.NotifyRequestRecord
    //     {
    //         Title = "Notify",
    //         Text = message,
    //         Duration = 1000
    //     };
    // }
    
    private void PowerFunction()
    {
        if (Status.Equals("Off"))
        {
            MainController.StartProgram();
        }
        else if (Status.Equals("On"))
        {
            MainController.StopProgram(false);
        }
    }

    private void Loaded()
    {
        WindowState = WindowState.Normal;
    }
    
    private int _windowRadius = 10;
    public int WindowRadius
    {
        get => _windowRadius;
        set
        {
            _windowRadius = value;
            OnPropertyChanged();
        }
    }

    private int _windowHeight = 600;
    public int WindowHeight
    {
        get => _windowHeight;
        set
        {
            _windowHeight = value;
            OnPropertyChanged();
        }
    }
    
    private int _windowWidth = 900;
    public int WindowWidth
    {
        get => _windowWidth;
        set
        {
            _windowWidth = value;
            OnPropertyChanged();
        }
    }
    
    private void MaximiseOrMinimise()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            WindowHeight = 600;
            WindowWidth = 900;
            WindowRadius = 10;
        }
        else
        {
            WindowState = WindowState.Maximized;
            WindowRadius = 0;
        }
    }
    
    private void Closing()
    {
        WindowState = WindowState.Minimized;
    }
}
