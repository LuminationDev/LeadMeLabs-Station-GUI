using Station.MVC.ViewModel;

namespace Station.Components._notification;

public static class MockConsole
{
    /// <summary>
    /// Describe the different levels of logging, only the most essential messages are printed at None.
    /// The levels are [None - essential only, Normal - basic messages and commands, Debug - anything that can be used for information, Verbose - everything].
    /// </summary>
    public enum LogLevel
    {
        Off,
        Error,
        Normal,
        Debug,
        Verbose
    }
    
    /// <summary>
    /// A wrapper around the ConsoleViewModel function.
    /// </summary>
    /// <param name="message">A string to be printed to the console.</param>
    /// <param name="level">A Loglevel enum representing if it should be displayed at the current logging level.</param>
    public static void WriteLine(string message, LogLevel level)
    {
        MainViewModel.ViewModelManager.ConsoleViewModel.WriteLine(message, level);
    }
    
    /// <summary>
    /// A wrapper around the ConsoleViewModel function. This requires no log level and is designed to for the leadme_api.
    /// An message sent from within another application is printed as a normal log level.
    /// </summary>
    /// <param name="message">A string to be printed to the console.</param>
    public static void WriteLine(string message)
    {
        MainViewModel.ViewModelManager.ConsoleViewModel.WriteLine(message, LogLevel.Normal);
    }
}
