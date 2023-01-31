using System.Windows;
using System.Windows.Controls;

namespace Station
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //Reference to the main logging console
        public static TextBox? console;

        //Control the different log levels
        public static Button? logLevel;

        //Labels for the current process
        public static Label? processConsole;
        public static Label? statusConsole;

        public MainWindow()
        {
            InitializeComponent();
            console = this.ConsoleWindow;
            logLevel = this.LoggingLevel;
            processConsole = this.ProcessConsole;
            statusConsole = this.StatusConsole;
            MockConsole.WriteLine("Program Started", MockConsole.LogLevel.Error);
        }
    }
}
