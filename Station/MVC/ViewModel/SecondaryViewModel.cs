using System;
using System.Windows;
using System.Windows.Controls;
using Station.Core;
using ObservableObject = CommunityToolkit.Mvvm.ComponentModel.ObservableObject;

namespace Station.MVC.ViewModel;

public class SecondaryViewModel : ObservableObject
{
    // Commands
    public RelayCommand LoadedCommand { get; }
    public event Action<MediaElement>? MediaElementLoaded;

    public SecondaryViewModel()
    {
        LoadedCommand = new RelayCommand(_ => Loaded());
    }
    
    private WindowState _windowState;
    public WindowState WindowState
    {
        get => _windowState;
        set
        {
            _windowState = value;
            OnPropertyChanged();
        }
    }
    
    private void Loaded()
    {
        WindowState = WindowState.Maximized;
        MediaElementLoaded?.Invoke(null);
    }
    
    public void PlayMedia(MediaElement? mediaElement)
    {
        mediaElement?.Play();
    }
}