﻿using Station.MVC.View;

namespace Station.MVC.ViewModel;

public class ViewModelLocator
{
    public MainViewModel? MainViewModel { get; set; }
    public SecondaryWindow? SecondaryViewModel { get; set; }
    public HomeViewModel HomeViewModel { get; } = new ();
    public ConsoleViewModel ConsoleViewModel { get; } = new ();
    public ExperiencesViewModel ExperiencesViewModel { get; } = new ();
    public DebugViewModel DebugViewModel { get; } = new ();
    public LogsViewModel LogsViewModel { get; } = new ();
    public QaViewModel QaViewModel { get; } = new ();
}
