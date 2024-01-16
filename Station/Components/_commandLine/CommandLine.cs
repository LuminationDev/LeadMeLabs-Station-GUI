using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Sentry;
using Station.Components._notification;
using Station.Components._overlay;
using Station.Components._utils;
using Station.Components._wrapper.steam;
using Station.MVC.Controller;

namespace Station.Components._commandLine;

/// <summary>
/// A static class to call command prompt actions from anywhere that is needed. Has
/// two different commands, the standard command prompt and the steamCMD command.
/// </summary>
public static class CommandLine
{
    /// <summary>
    /// The location of the executing assembly. This is used to find the relative path for externally used applications.
    /// </summary>
    public static readonly string?
        StationLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

    /// <summary>
    /// A string representing the regular command prompt executable.
    /// </summary>
    private static readonly string stationCmd = "cmd.exe";

    /// <summary>
    /// A string representing the powershell executable.
    /// </summary>
    private static readonly string stationPowershell = "powershell.exe";

    /// <summary>
    /// The relative path of the steamCMD executable on the local machine.
    /// </summary>
    public static string steamCmd = @"\external\steamcmd\steamcmd.exe";
    public static string steamCmdFolder = @"\external\steamcmd\";

    /// <summary>
    /// Track if SteamCMD is currently being configured with a Guard Key.
    /// </summary>
    private static bool configuringSteam = false;

    /// <summary>
    /// The relative path to the SetVol executable
    /// </summary>
    public static string SetVol = StationLocation + @"\external\SetVol\SetVol.exe";

    /// <summary>
    /// Sets up a generic process ready for any type of command to be passed. There is no command
    /// window or interface to be shown, only the output is generated to be read.
    /// </summary>
    /// <param name="type">A string representing what applicaiton to be launched</param>
    /// <returns>A instance of the newly created process</returns>
    private static Process? SetupCommand(string type)
    {
        if (string.IsNullOrEmpty(type))
        {
            return null;
        }

        Process temp = new();
        temp.StartInfo.FileName = type;
        temp.StartInfo.RedirectStandardInput = true;
        temp.StartInfo.RedirectStandardError = true;
        temp.StartInfo.RedirectStandardOutput = true;
        temp.StartInfo.CreateNoWindow = true;
        temp.StartInfo.UseShellExecute = false;

        return temp;
    }

    /// <summary>
    /// Determine the outcome of the command action, by creating event handlers to combine output data as it's 
    /// received. This stops buffers from overflowing which leads to thread hanging. It also determines if an error 
    /// has occurred or the operation ran as expected.
    /// </summary>
    /// <param name="temp">A Process that represents a current command process that has been executed.</param>
    /// <returns>A string representing the output or error from the command prompt.</returns>
    private static string? outcome(Process temp)
    {
        string? output = "";
        string? error = "";

        temp.OutputDataReceived += (s, e) => { output += e.Data + "\n"; };
        temp.ErrorDataReceived += (s, e) => { error += e.Data + "\n"; };

        temp.BeginOutputReadLine();
        temp.BeginErrorReadLine();
        temp.StandardInput.Flush();
        temp.StandardInput.Close();
        temp.WaitForExit();

        if (error != null)
        {
            return output;
        }

        return error;
    }

    /// <summary>
    /// Run a supplied executable from the command line with option arguments.
    /// </summary>
    /// <param name="executable">A string representing the address of the executable on the local machine</param>
    /// <param name="arguments">A string representing the any arguements the program needs while opening</param>
    public static void StartProgram(string executable, string arguments = "")
    {
        Process? cmd = SetupCommand(executable);
        if(cmd == null)
        {
            Logger.WriteLog($"Cannot start: {executable}, StartProgram -> SetupCommand returned null value.", MockConsole.LogLevel.Error);
            return;
        }

        cmd.StartInfo.Arguments = arguments;
        cmd.Start();
        cmd.Close();
    }

    public static string? RunProgramWithOutput(string executable, string arguments = "")
    {
        Process? cmd = SetupCommand(executable);
        if (cmd == null)
        {
            Logger.WriteLog($"Cannot start: {executable}, RunProgramWithOutput -> SetupCommand returned null value.", MockConsole.LogLevel.Error);
            return null;
        }

        cmd.StartInfo.Arguments = arguments;
        cmd.Start();
        string? result = outcome(cmd);
        cmd.Close();
        return result;
    }

    /// <summary>
    /// Used to execute command line prompts related to the local machine.
    /// Specifically used for one off actions such as opening an application/querying
    /// running processes.
    /// </summary>
    /// <param name="command">A string representing the command line to be executed</param>
    /// <returns>A string representing the result of the command</returns>
    public static string? ExecuteStationCommand(string command)
    {
        Process? cmd = SetupCommand(stationCmd);
        if (cmd == null)
        {
            Logger.WriteLog($"Cannot start: {stationCmd} and run '{command}', ExecuteStationCommand -> SetupCommand returned null value.", MockConsole.LogLevel.Error);
            return null;
        }
        cmd.Start();
        cmd.StandardInput.WriteLine(command);

        return outcome(cmd);
    }

    /// <summary>
    /// Used to interact with a browser. Uses the Arguments parameter for passing a specific URL.
    /// </summary>
    /// <param name="url">A string representing the url of a website being pushed.</param>
    /// <returns>A string representing the results of the command</returns>
    public static void ExecuteBrowserCommand(string url)
    {
        Process.Start(new ProcessStartInfo(url) {UseShellExecute = true});
    }

    /// <summary>
    /// Abort a previously lodged Shutdown or Restart command.
    /// </summary>
    public static string? CancelShutdown()
    {
        string? output = ExecuteStationCommand("shutdown /a");
        return output;
    }

    public static string? ShutdownStation(int time)
    {
        string? output = ExecuteStationCommand("shutdown /s /t " + time);
        return output;
    }

    public static string? RestartStation(int time)
    {
        string? output = ExecuteStationCommand("shutdown /r /t " + time);
        return output;
    }

    /// <summary>
    /// Kill off the launcher program if the time is between a set amount. The Software_Checker scheduler task will
    /// automatically restart the application within the next five minutes, updating the Launcher and Station software.
    /// </summary>
    public static void RestartProgram()
    {
        //Log the daily restart and write the Work Queue before exiting.
        Logger.WriteLog("Daily restart", MockConsole.LogLevel.Verbose);
        Logger.WorkQueue();

        Process[] processes = ProcessManager.GetProcessesByName("LeadMe");

        foreach (Process process in processes)
        {
            try
            {
                process.Kill(true);
            }
            catch (Exception e)
            {
                Logger.WriteLog($"Error: {e}", MockConsole.LogLevel.Normal);
            }
        }

        // Exit the application
        Environment.Exit(0);
    }
    
    ///////////////////////////////////////////////
    /// Wrapper Specific Methods
    ///////////////////////////////////////////////
    /// <summary>
    /// Configure SteamCMD for the current computer, monitor the output to determine what the
    /// result of the entered code was. Sending a message back to the android tablet of the 
    /// outcome.
    /// </summary>
    /// <param name="command">A command to set the steam guard key for the local SteamCMD</param>
    public static void MonitorSteamConfiguration(string command)
    {
        if (!configuringSteam)
        {
            configuringSteam = true;

            if (string.IsNullOrEmpty(StationLocation))
            {
                Logger.WriteLog($"Station location null or empty: cannot run '{command}', MonitorSteamConfiguration -> SetupCommand returned null value.", MockConsole.LogLevel.Error);
                return;
            }
            string fullPath = StationLocation + steamCmd;

            Process ? cmd = SetupCommand(fullPath);
            if (cmd == null)
            {
                Logger.WriteLog($"Cannot start: {fullPath} and run '{command}', MonitorSteamConfiguration -> SetupCommand returned null value.", MockConsole.LogLevel.Error);
                return;
            }

            cmd.StartInfo.Arguments = command;
            cmd.Start();

            //Check the output for a result
            string? output = outcome(cmd);

            if (output == null)
            {
                Logger.WriteLog("Unable to read output", MockConsole.LogLevel.Normal);
                MessageController.SendResponse("Android", "Station", "SetValue:steamCMD:error");
                configuringSteam = false;
                return;
            }

            Logger.WriteLog(output, MockConsole.LogLevel.Normal);

            if (output.Contains("FAILED (Invalid Login Auth Code)"))
            {
                Logger.WriteLog("AUTH FAILED", MockConsole.LogLevel.Normal);
                MessageController.SendResponse("Android", "Station", "SetValue:steamCMD:failure");
                configuringSteam = false;
            }
            else if (output.Contains("OK"))
            {
                Logger.WriteLog("AUTH SUCCESS, restarting VR system", MockConsole.LogLevel.Normal);
                MessageController.SendResponse("Android", "Station", "SetValue:steamCMD:configured");

                //Recollect the installed experiences
                MainController.wrapperManager?.ActionHandler("CollectApplications");
                configuringSteam = false;
            }

            //Manually kill the process or it will stay on the guard code input 
            cmd.Kill(true);
        }
    }

    /// <summary>
    /// Used to interact with SteamCMD. Uses the Arguments parameter for issuing commands instead
    /// of the writeline function like in executeStationCommand. This way it can run multiple commands
    /// in sequence. Most likely used for gathering, installing and uninstalling applications.
    /// </summary>
    /// <param name="command">A collection of steam commands that will be run in sequence.</param>
    /// <returns>A string representing the results of the command</returns>
    public static string? ExecuteSteamCommand(string command)
    {
        if (string.IsNullOrEmpty(StationLocation))
        {
            Logger.WriteLog($"Station location null or empty: cannot run '{command}', MonitorSteamConfiguration -> SetupCommand returned null value.", MockConsole.LogLevel.Error);
            return null;
        }
        string fullPath = StationLocation + steamCmd;

        if (!File.Exists(fullPath))
        {
            SessionController.PassStationMessage($"StationError,File not found:{fullPath}");
            SteamScripts.steamCMDConfigured = "steamcmd.exe not found";
            return null;
        }

        Process? cmd = SetupCommand(fullPath);
        if (cmd == null)
        {
            Logger.WriteLog($"Cannot start: {fullPath} and run '{command}', ExecuteSteamCommand -> SetupCommand returned null value.", MockConsole.LogLevel.Error);
            return null;
        }
        cmd.StartInfo.Arguments = "\"+force_install_dir \\\"C:/Program Files (x86)/Steam\\\"\" " + command;
        cmd.Start();

        string? output = outcome(cmd);

        if (output == null)
        {
            Logger.WriteLog($"ExecuteSteamCommand -> SteamCMD output returned null value.", MockConsole.LogLevel.Error);
            return null;
        }

        if (output.Contains("Steam Guard code:"))
        {
            MessageController.SendResponse("Android", "Station", "SetValue:steamCMD:required");
            MockConsole.WriteLine("Steam Guard is not enabled for this account.");
            SteamScripts.steamCMDConfigured = "Missing";

            //Manually kill the process or it will stay on the guard code input 
            cmd.Kill(true);
            return null;
        }

        MessageController.SendResponse("Android", "Station", "SetValue:steamCMD:configured");
        SteamScripts.steamCMDConfigured = "Configured";

        return output;
    }

    /// <summary>
    /// Used to interact with Steamcmd. Uses the Arguments parameter for issuing commands instead
    /// of the writeline funciton like in executeStationCommand. This way it can run multiple commands
    /// in sequence. Most likely used for gathering, installing and uninstalling applications.
    /// </summary>
    /// <param name="command">A collection of steam commands that will be run in sequence.</param>
    /// <returns>A string representing the results of the command</returns>
    public static string? ExecuteSteamCommandSDrive(string command)
    {
        if (string.IsNullOrEmpty(StationLocation))
        {
            Logger.WriteLog($"Station location null or empty: cannot run '{command}', MonitorSteamConfiguration -> SetupCommand returned null value.", MockConsole.LogLevel.Error);
            return null;
        }
        string fullPath = StationLocation + steamCmd;

        if (!File.Exists(fullPath))
        {
            SessionController.PassStationMessage($"StationError,File not found:{fullPath}");
            return null;
        }

        Process? cmd = SetupCommand(fullPath);
        if (cmd == null)
        {
            Logger.WriteLog($"Cannot start: {fullPath} and run '{command}', ExecuteSteamCommandSDrive -> SetupCommand returned null value.", MockConsole.LogLevel.Error);
            return null;
        }
        cmd.StartInfo.Arguments = "\"+force_install_dir \\\"S:/SteamLibrary\\\"\" " + command;
        cmd.Start();

        return outcome(cmd);
    }

    /// <summary>
    /// Destroy the Steam sign in window if this happens to be up when going to launch an application.
    /// </summary>
    public static void KillSteamSigninWindow()
    {
        List<Process> list = ProcessManager.GetProcessesByNames(new List<string> {"steam"});
        foreach (Process process in list)
        {
            Logger.WriteLog(
                $"Inside kill steam signin: Process: {process.ProcessName} ID: {process.Id}, MainWindowTitle: {process.MainWindowTitle}",
                MockConsole.LogLevel.Debug);

            if (process.MainWindowTitle.Equals("Steam Sign In"))
            {
                Logger.WriteLog(
                    $"Killing Process: {process.ProcessName} ID: {process.Id}, MainWindowTitle: {process.MainWindowTitle}",
                    MockConsole.LogLevel.Debug);
                process.Kill();
            }
        }
    }

    /// <summary>
    /// Query processes to see if any are currently running.
    /// </summary>
    /// <param name="processes">An array of strings containing the name of processes to search for</param>
    /// <param name="kill">A bool representing if the processes should be stopped or not</param>
    /// <returns>A boolean representing if a process is still running</returns>
    public static bool QueryVRProcesses(List<string> processes, bool kill = false)
    {
        List<Process> list = ProcessManager.GetProcessesByNames(processes);

        foreach (Process process in list)
        {
            Logger.WriteLog($"Process: {process.ProcessName} ID: {process.Id}", MockConsole.LogLevel.Verbose);

            if (kill)
            {
                try
                {
                    process.Kill();
                }
                catch
                {

                }
            }
        }

        return list.Any();
    }

    /// <summary>
    /// Start a powershell window to check the free space of the local computer.
    /// </summary>
    /// <returns>An integer representing the free space of the computer in GB</returns>
    public static int? GetFreeStorage()
    {
        try
        {
            Process? cmd = SetupCommand(stationPowershell);
            if (cmd == null)
            {
                Logger.WriteLog($"Cannot start: {stationPowershell}, GetFreeStorage -> SetupCommand returned null value.", MockConsole.LogLevel.Error);
                return 9999;
            }
            cmd.Start();
            cmd.StandardInput.WriteLine(
                "Get-WmiObject -Class win32_logicaldisk | Format-Table @{n=\"FreeSpace\";e={[math]::Round($_.FreeSpace/1GB,2)}}");
            string? output = outcome(cmd);

            if (output == null)
            {
                return 9999;
            }

            string[] outputP = output.Split("\n");
            // if there is less than 14 items, the app probably hasn't launched yet
            if (output.Length < 10)
            {
                return 9999;
            }

            if (outputP[7].Equals("FreeSpace"))
            {
                int result = Convert.ToInt32(Math.Floor(Convert.ToDouble(outputP[9].Trim())));
                return result;
            }
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
        }

        return 9999;
    }

    public static async Task UploadLogFile()
    {
        string accessToken = await RemoteAccess.GetAccessToken();
        if (String.IsNullOrEmpty(accessToken))
        {
            return;
        }

        var fileStream = File.OpenRead(Logger.GetCurrentLogFilePath());

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
        var content = new StreamContent(fileStream);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        var response = await httpClient.PostAsync(
            "https://us-central1-leadme-labs.cloudfunctions.net/uploadFile",
            content
        );
    }

    [DllImport("user32.dll")]
    public static extern int SetForegroundWindow(int hwnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(int handle, int state);


    private static string[] loadingMessages = {
        "Preparing immersive learning environment...",
        "Loading immersive experiences...",
        "Configuring VR settings...",
        "Automating the boring stuff...",
        "Preparing virtual learning tools...",
        "Sparking the lightbulb moment...",
        "Almost there..."
    };

    public async static void PowershellCommand(Process steamSignInWindow)
    {
        OverlayManager.SetText(loadingMessages[0]);
        await Task.Delay(5000);
        OverlayManager.SetText(loadingMessages[1]);
        await Task.Delay(5000);
        OverlayManager.SetText(loadingMessages[2]);
        await Task.Delay(5000);
        OverlayManager.SetText(loadingMessages[3]);
        Logger.WriteLog($"Tabbing back out of offline warning", MockConsole.LogLevel.Debug);
        Process? cmd = SetupCommand(stationPowershell);
        if (cmd == null)
        {
            Logger.WriteLog($"Cannot start: {stationPowershell}, PowershellCommand (cmd) -> SetupCommand returned null value.", MockConsole.LogLevel.Error);
            return;
        }
        cmd.Start();
        cmd.StandardInput.WriteLine("$StartDHCP = New-Object -ComObject wscript.shell;");
        cmd.StandardInput.WriteLine("$StartDHCP.SendKeys('{TAB}')");
        cmd.StandardInput.WriteLine("$StartDHCP.SendKeys('{ENTER}')");
        ShowWindow(steamSignInWindow.MainWindowHandle.ToInt32(), 3);    
        SetForegroundWindow(steamSignInWindow.MainWindowHandle.ToInt32());
        outcome(cmd);
        
        await Task.Delay(2000);
        OverlayManager.SetText(loadingMessages[4]);
        Logger.WriteLog($"Entering steam details", MockConsole.LogLevel.Debug);
        Process? cmd2 = SetupCommand(stationPowershell);
        if (cmd2 == null)
        {
            Logger.WriteLog($"Cannot start: {stationPowershell}, PowershellCommand (cmd2) -> SetupCommand returned null value.", MockConsole.LogLevel.Error);
            return;
        }
        cmd2.Start();
        cmd2.StandardInput.WriteLine("$StartDHCP = New-Object -ComObject wscript.shell;");
        cmd2.StandardInput.WriteLine($"$StartDHCP.SendKeys('{Environment.GetEnvironmentVariable("SteamPassword", EnvironmentVariableTarget.Process)}')");
        cmd2.StandardInput.WriteLine("$StartDHCP.SendKeys('{TAB}')");
        cmd2.StandardInput.WriteLine("$StartDHCP.SendKeys('{TAB}')");
        cmd2.StandardInput.WriteLine("$StartDHCP.SendKeys('{ENTER}')");
        ShowWindow(steamSignInWindow.MainWindowHandle.ToInt32(), 3);
        SetForegroundWindow(steamSignInWindow.MainWindowHandle.ToInt32());
        outcome(cmd2);
        
        await Task.Delay(5000);
        OverlayManager.SetText(loadingMessages[5]);
        await Task.Delay(5000);
        OverlayManager.SetText(loadingMessages[6]);
        Logger.WriteLog($"Submitting offline form", MockConsole.LogLevel.Debug);
        Process cmd3 = SetupCommand(stationPowershell);
        if (cmd3 == null)
        {
            Logger.WriteLog($"Cannot start: {stationPowershell}, PowershellCommand (cmd3) -> SetupCommand returned null value.", MockConsole.LogLevel.Error);
            return;
        }
        cmd3.Start();
        cmd3.StandardInput.WriteLine("$StartDHCP = New-Object -ComObject wscript.shell;");
        cmd3.StandardInput.WriteLine("$StartDHCP.SendKeys('{TAB}')");
        cmd3.StandardInput.WriteLine("$StartDHCP.SendKeys('{TAB}')");
        cmd3.StandardInput.WriteLine("$StartDHCP.SendKeys('{ENTER}')");
        ShowWindow(steamSignInWindow.MainWindowHandle.ToInt32(), 3);
        SetForegroundWindow(steamSignInWindow.MainWindowHandle.ToInt32());
        outcome(cmd3);

        await Task.Delay(2000);
        OverlayManager.ManualStop();
    }

    /// <summary>
    /// Pass in a steam application directory to get the process id if running
    /// </summary>
    /// <param name="dir">Fully qualified directory where a steam application executable is stored</param>
    /// <returns>Process id if the application is running</returns>
    public static string? GetProcessIdFromDir(string dir)
    {
        Logger.WriteLog("gps | where {$_.Path -Like \"" + dir + "*\"} | where {$_.MainWindowHandle -ne 0} | select ID", MockConsole.LogLevel.Debug);

        Process? cmd = SetupCommand(stationPowershell);
        if (cmd == null)
        {
            Logger.WriteLog($"Cannot start: {stationPowershell}, GetProcessIdFromDir -> SetupCommand returned null value.", MockConsole.LogLevel.Error);
            return null;
        }
        cmd.Start();
        cmd.StandardInput.WriteLine("gps | where {$_.Path -Like \"" + dir + "*\"} | where {$_.MainWindowHandle -ne 0} | select ID");

        //This works as an alternate to the powershell command
        //Process cmd = setupCommand(stationCmd);
        //cmd.Start();
        //cmd.StandardInput.WriteLine($"wmic process where \"ExecutablePath like '%{dir}%'\" get ProcessID,ExecutablePath");

        string? output = outcome(cmd);

        if (output == null)
        {
            Logger.WriteLog($"No output recorded for {dir}", MockConsole.LogLevel.Debug);
            return null;
        }

        Logger.WriteLog(output, MockConsole.LogLevel.Debug);

        string[] outputP = output.Split("\n");

        // if there is less than 14 items, the app probably hasn't launched yet
        int iterator = 0;
        while (iterator < outputP.Length)
        {
            Logger.WriteLog($"Output line {iterator}: {outputP[iterator].Trim()}", MockConsole.LogLevel.Debug);

            if (outputP[iterator].Trim().Equals("Id"))
            {
                break;
            }

            iterator++;
        }

        if (outputP.Length < iterator + 2)
        {
            return null;
        }

        Logger.WriteLog($"ID: {outputP[iterator + 2].Trim()}", MockConsole.LogLevel.Debug);

        return outputP[iterator + 2].Trim();
    }
}
