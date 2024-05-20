using System;
using System.Text;
using LeadMeLabsLibrary;
using Station.Core;

namespace Station.MVC.ViewModel;

public class ConsoleViewModel : ObservableObject
{
    /// <summary>
    /// The maximum number of lines that can be shown on the console window at any one point.
    /// </summary>
    private const int LineLimit = 100;

    public RelayCommand ChangeLogCommand { get; }

    public ConsoleViewModel()
    {
        ChangeLogCommand = new RelayCommand(_ => ChangeLogLevel());
    }
    
    /// <summary>
    /// Used to display or hide the popout button that opens a new console window.
    /// </summary>
    private bool _showPopoutButton = true;
    public bool ShowPopoutButton
    {
        get => _showPopoutButton;
        set
        {
            _showPopoutButton = value;
            OnPropertyChanged();
        }
    }
    
    /// <summary>
    /// Used for binding the MainWindow Mock Console
    /// </summary>
    private string _consoleText = "";

    // ReSharper disable once MemberCanBePrivate.Global
    public string ConsoleText
    {
        get => _consoleText;
        set
        {
            _consoleText = value;
            OnPropertyChanged();
        }
    }
    
    /// <summary>
    /// Used for binding the MainWindow Mock Console
    /// </summary>
    private Enums.LogLevel _currentLogLevel = Enums.LogLevel.Normal;

    public Enums.LogLevel CurrentLogLevel
    {
        get => _currentLogLevel;
        set
        {
            _currentLogLevel = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Cycle through the log levels, if it has reach the max (Verbose) then reset
    /// back to None.
    /// </summary>
    private void ChangeLogLevel()
    {
        if (CurrentLogLevel == Enums.LogLevel.Verbose)
        {
            CurrentLogLevel = Enums.LogLevel.Off;
        }
        else
        {
            CurrentLogLevel++;
        }
    }

    /// <summary>
    /// Clear the MockConsole of all previous messages. The cleared message will be printed regardless
    /// of log level as to alert the user this is deliberate.
    /// </summary>
    public void ClearConsole()
    {
        ConsoleText = "";
        WriteLine("Cleared", Enums.LogLevel.Error);
    }

    /// <summary>
    /// Log a message to the mock console within the Station form, only print it if it conforms to the current logging level.
    /// </summary>
    /// <param name="message">A string to be printed to the console.</param>
    /// <param name="level">A Loglevel enum representing if it should be displayed at the current logging level.</param>
    public void WriteLine(string message, Enums.LogLevel level)
    {
        if (message.Trim() == "") return;
        
        var builder = new StringBuilder(ConsoleText);

        if (builder.Length > 0 && builder[^1] != '\n')
        {
            builder.AppendLine(); // Ensure the last line ends with a newline
        }
        
        var lineCount = builder.ToString().Split('\n').Length;
        
        if (lineCount >= LineLimit)
        {
            int startIndex = builder.ToString().IndexOf('\n') + 1;
            builder.Remove(0, startIndex);
        }

        builder.AppendLine($"{DateStamp()}{message}");
        
        //If the log level is above what is wanted do not print to the screen
        if (level > CurrentLogLevel || CurrentLogLevel == Enums.LogLevel.Off) return;
        ConsoleText = builder.ToString();
    }

    private static string DateStamp()
    {
        DateTime now = DateTime.Now;
        return $"[{now:dd/MM | hh:mm:ss}] ";
    }
}
