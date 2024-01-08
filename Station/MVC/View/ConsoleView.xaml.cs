using System.Windows;
using Station.Components._utils;

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
}
