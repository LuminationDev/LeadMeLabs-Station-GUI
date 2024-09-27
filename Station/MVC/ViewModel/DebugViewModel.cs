using Station.Components._utils;
using Station.Core;

namespace Station.MVC.ViewModel;

public class DebugViewModel : ObservableObject
{
    public RelayCommand ResetDebugCommand { get; }
    
    public DebugViewModel()
    {
        ResetDebugCommand = new RelayCommand(_ => Reset());
    }
    
    private bool _autoMinimise = true;
    public bool AutoMinimise
    {
        get => _autoMinimise;
        set
        {
            InternalDebugger.minimiseVrPrograms = value;
            _autoMinimise = value;
            OnPropertyChanged();
        }
    }
    
    private bool _autoStartPrograms = true;
    public bool AutoStartPrograms
    {
        get => _autoStartPrograms;
        set
        {
            InternalDebugger.autoStartVrPrograms = value;
            _autoStartPrograms = value;
            OnPropertyChanged();
        }
    }
    
    private bool _autoScroll = true;
    public bool AutoScroll
    {
        get => _autoScroll;
        set
        {
            InternalDebugger.autoScroll = value;
            _autoScroll = value;
            OnPropertyChanged();
        }
    }
        
    private bool _headsetRequired = true;
    public bool HeadsetRequired
    {
        get => _headsetRequired;
        set
        {
            InternalDebugger.headsetRequired = value;
            _headsetRequired = value;
            OnPropertyChanged();
        }
    }

    private bool _idleModeActive = false;
    public bool IdleModeActive
    {
        get => InternalDebugger.idleModeActive ?? false;
        set
        {
            InternalDebugger.SetIdleModeActive(value, true);
            _idleModeActive = value;
            OnPropertyChanged();
        }
    }
    
    private void Reset()
    {
        AutoMinimise = true;
        AutoStartPrograms = true;
        AutoScroll = true;
        HeadsetRequired = true;
        IdleModeActive = false;
    }
}
