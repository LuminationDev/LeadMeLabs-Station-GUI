using System;
using System.Collections.Generic;
using Station.Components._commandLine;
using Station.Components._interfaces;
using Station.Components._utils;
using Station.Components._utils._steamConfig;
using Station.MVC.Controller;

namespace Station.Components._profiles;

public class ContentProfile: Profile, IProfile
{
    private readonly StartupFunctions _startupFunctions = new();

    /// <summary>
    /// Track the different account names that are on the Station
    /// </summary>
    private readonly List<string> _accounts = new();

    /// <summary>
    /// A list of account associated processes that Station has to monitor.
    /// </summary>
    private readonly List<string> _processesToQuery = new();

    public Variant GetVariant()
    {
        return Variant.Content;
    }

    public ContentProfile()
    {
        SetupContentProfile();
    }

    /// <summary>
    /// Check for the different accounts that may be present on the Station.
    /// </summary>
    private void SetupContentProfile()
    {
        if (HasSteamAccountInformation())
        {
            _accounts.Add("Steam");
            _processesToQuery.Add("steam");
            _startupFunctions.AddStartupFunction(StartSteam);
        }
    }

    /// <summary>
    /// Check the environment variables to see if there is a Steam account on the Station.
    /// </summary>
    /// <returns></returns>
    private bool HasSteamAccountInformation()
    {
        string? user = Environment.GetEnvironmentVariable("SteamUserName", EnvironmentVariableTarget.Process);
        string? password = Environment.GetEnvironmentVariable("SteamPassword", EnvironmentVariableTarget.Process);

        return user != null && password != null;
    }

    /// <summary>
    /// Check if the content profile accounts include the supplied account name.
    /// </summary>
    /// <param name="account">A string of the account to check for. I.e. 'Steam'</param>
    /// <returns>A bool of if the list contains the account.</returns>
    public bool DoesProfileHaveAccount(string account)
    {
        return _accounts.Contains(account);
    }

    public List<string> GetProcessesToQuery()
    {
        return _processesToQuery;
    }

    /// <summary>
    /// Minimise the software that handles the headset.
    /// </summary>
    /// <param name="attemptLimit"></param>
    public void MinimizeSoftware(int attemptLimit = 6)
    {
        Minimize(GetProcessesToQuery(), attemptLimit);
    }

    public void StartSession()
    {
        //Bail out if session processes are already running
        if (QueryMonitorProcesses(GetProcessesToQuery()))
        {
            return;
        }

        ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage($"SoftwareState,Starting processes"), TimeSpan.FromSeconds(0));
        _startupFunctions.ExecuteStartupFunctions();
        MinimizeSoftware();
    }

    /// <summary>
    /// Start steam.exe with the auto login details provided.
    /// </summary>
    private void StartSteam()
    {
        CommandLine.KillSteamSigninWindow();
        SteamConfig.VerifySteamConfig();
        CommandLine.StartProgram(SessionController.Steam, "-noreactlogin -login " +
                                                          Environment.GetEnvironmentVariable("SteamUserName", EnvironmentVariableTarget.Process) + " " +
                                                          Environment.GetEnvironmentVariable("SteamPassword", EnvironmentVariableTarget.Process));
    }
}

public delegate void AdditionalFunction();

/// <summary>
/// Manages a collection of additional startup functions and provides methods to add and execute them.
/// </summary>
public class StartupFunctions
{
    private readonly List<AdditionalFunction> _additionalFunctions = new();

    public void AddStartupFunction(AdditionalFunction additionalFunction)
    {
        _additionalFunctions.Add(additionalFunction);
    }

    /// <summary>
    /// Executes all additional startup functions added to the list.
    /// </summary>
    public void ExecuteStartupFunctions()
    {
        foreach (var function in _additionalFunctions)
        {
            function.Invoke();
        }
    }
}
