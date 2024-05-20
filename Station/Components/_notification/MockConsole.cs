using LeadMeLabsLibrary;
using Station.MVC.ViewModel;

namespace Station.Components._notification;

public static class MockConsole
{
    /// <summary>
    /// A wrapper around the ConsoleViewModel function.
    /// </summary>
    /// <param name="message">A string to be printed to the console.</param>
    /// <param name="level">A Loglevel enum representing if it should be displayed at the current logging level.</param>
    public static void WriteLine(string message, Enums.LogLevel level)
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
        MainViewModel.ViewModelManager.ConsoleViewModel.WriteLine(message, Enums.LogLevel.Normal);
    }
}
