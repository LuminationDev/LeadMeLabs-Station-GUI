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
    
    public void Reset()
    {
        AutoMinimise = true;
        AutoStartPrograms = true;
        AutoScroll = true;
    }
}
