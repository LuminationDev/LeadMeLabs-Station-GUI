using System;
using System.Windows;
using Station.Components._utils;
using Station.MVC.ViewModel;

namespace Station.MVC.View;

public partial class ConsoleView
{
    public ConsoleView()
    {
        InitializeComponent();
    }
    
    /// <summary>
    /// Scroll to the bottom of the scrollViewer when the UserControl is loaded
    /// </summary>
    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        ConsoleScroll.ScrollToEnd();
    }
    
    /// <summary>
    /// Scroll to the end of the scrollViewer. This can be disabled through the debug panel
    /// </summary>
    private void TextBox_TextChanged(object sender, RoutedEventArgs e)
    {
        if (InternalDebugger.GetAutoScroll())
        {
            ConsoleScroll.ScrollToEnd();
        }
    }
    
    /// <summary>
    /// Show the console window in a new pop up. Allowing the user to view the console as they navigate the different
    /// pages of the UI.
    /// </summary>
    private void ShowPopOutWindow_Click(object sender, RoutedEventArgs e)
    {
        // Create an instance of the ConsoleWindow
        ConsolePopoutView consoleWindow = new()
        {
            // Set the DataContext of the ConsoleWindow to the same as the main window
            DataContext = this.DataContext
        };

        // Hide the popout button
        MainViewModel.ViewModelManager.ConsoleViewModel.ShowPopoutButton = false;
        
        // Subscribe to the Closed event of the ConsoleWindow
        consoleWindow.Closed += ConsoleWindow_Closed;
        
        // Show the window
        consoleWindow.Show();
    }
    
    private void ConsoleWindow_Closed(object? sender, EventArgs e)
    {
        // Show the popout button
        MainViewModel.ViewModelManager.ConsoleViewModel.ShowPopoutButton = true;
    }
}
