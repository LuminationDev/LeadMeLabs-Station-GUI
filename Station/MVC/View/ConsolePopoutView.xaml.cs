using System.Windows;

namespace Station.MVC.View;

public partial class ConsolePopoutView
{
    public ConsolePopoutView()
    {
        InitializeComponent();
    }
    
    private void Window_MouseLeftButtonDown(object sender, RoutedEventArgs e)
    {
        DragMove();
    }
    
    private void WindowClose_Click(object sender, RoutedEventArgs e)
    {
        // Close the current window
        Close();
    }
}
