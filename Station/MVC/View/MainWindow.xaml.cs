﻿using System.Windows;
using System.Windows.Forms;
using Station.Components._windows;


namespace Station.MVC.View;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
        
        int index = WindowTracker.TouchScreenIndex;
        Screen s = Screen.AllScreens[index];
        System.Drawing.Rectangle r  = s.WorkingArea;
        this.Top = r.Top;
        this.Left = r.Left;
        this.Width = r.Width;
        this.Height = r.Height;
    }
    
    private void Window_MouseLeftButtonDown(object sender, RoutedEventArgs e)
    {
        DragMove();
    }
}
