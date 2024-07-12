using System.Windows;

namespace Station.MVC.View;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }
    
    private void Window_MouseLeftButtonDown(object sender, RoutedEventArgs e)
    {
        DragMove();
    }
}
