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
using LeadMeLabsLibrary;
using Newtonsoft.Json.Linq;
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
    private const string StationCmd = "cmd.exe";

    /// <summary>
    /// A string representing the powershell executable.
    /// </summary>
    private const string StationPowershell = "powershell.exe";

    /// <summary>
    /// The relative path of the steamCMD executable on the local machine.
    /// </summary>
    public static string steamCmd = @"\external\steamcmd\steamcmd.exe";
    public static string steamCmdFolder = @"\external\steamcmd\";

    /// <summary>
    /// Track if SteamCMD is currently being configured with a Guard Key.
    /// </summary>
    private static bool configuringSteam;

    /// <summary>
    /// Sets up a generic process ready for any type of command to be passed. There is no command
    /// window or interface to be shown, only the output is generated to be read.
    /// </summary>
    /// <param name="type">A string representing what application to be launched</param>
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
    private static string? Outcome(Process temp)
    {
        string output = "";
        string? error = "";

        temp.OutputDataReceived += (_, e) => { output += e.Data + "\n"; };
        temp.ErrorDataReceived += (_, e) => { error += e.Data + "\n"; };

        temp.BeginOutputReadLine();
        temp.BeginErrorReadLine();
        temp.StandardInput.Flush();
        temp.StandardInput.Close();
        temp.WaitForExit();

        return error != null ? output : error;
    }

    /// <summary>
    /// Run a supplied executable from the command line with option arguments.
    /// </summary>
    /// <param name="executable">A string representing the address of the executable on the local machine</param>
    /// <param name="arguments">A string representing the any arguments the program needs while opening</param>
    public static void StartProgram(string executable, string arguments = "")
    {
        Process? cmd = SetupCommand(executable);
        if(cmd == null)
        {
            Logger.WriteLog($"Cannot start: {executable}, StartProgram -> SetupCommand returned null value.", Enums.LogLevel.Error);
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
            Logger.WriteLog($"Cannot start: {executable}, RunProgramWithOutput -> SetupCommand returned null value.", Enums.LogLevel.Error);
            return null;
        }

        cmd.StartInfo.Arguments = arguments;
        cmd.Start();
        string? result = Outcome(cmd);
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
    private static string? ExecuteStationCommand(string command)
    {
        Process? cmd = SetupCommand(StationCmd);
        if (cmd == null)
        {
            Logger.WriteLog($"Cannot start: {StationCmd} and run '{command}', ExecuteStationCommand -> SetupCommand returned null value.", Enums.LogLevel.Error);
            return null;
        }
        cmd.Start();
        cmd.StandardInput.WriteLine(command);

        return Outcome(cmd);
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
        if (DeviceControl.GetIsUpdating())
        {
            Logger.WriteLog("ShutdownStation - Cannot shutdown, currently updating.", Enums.LogLevel.Info);
            return "Updating";
        }
        
        string? output = ExecuteStationCommand("shutdown /s /t " + time);
        return output;
    }

    public static string? RestartStation(int time)
    {
        if (DeviceControl.GetIsUpdating())
        {
            Logger.WriteLog("RestartStation - Cannot restart, currently updating.", Enums.LogLevel.Info);
            return "Updating";
        }
        
        string? output = ExecuteStationCommand("shutdown /r /t " + time);
        return output;
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
        if (configuringSteam) return;
        
        configuringSteam = true;

        if (string.IsNullOrEmpty(StationLocation))
        {
            Logger.WriteLog($"Station location null or empty: cannot run '{command}', MonitorSteamConfiguration -> SetupCommand returned null value.", Enums.LogLevel.Error);
            return;
        }
        string fullPath = StationLocation + steamCmd;

        Process ? cmd = SetupCommand(fullPath);
        if (cmd == null)
        {
            Logger.WriteLog($"Cannot start: {fullPath} and run '{command}', MonitorSteamConfiguration -> SetupCommand returned null value.", Enums.LogLevel.Error);
            return;
        }

        cmd.StartInfo.Arguments = command;
        cmd.Start();

        //Check the output for a result
        string? output = Outcome(cmd);

        if (output == null)
        {
            Logger.WriteLog("Unable to read output", Enums.LogLevel.Normal);
            MessageController.SendResponse("Android", "Station", "SetValue:steamCMD:error");
            configuringSteam = false;
            return;
        }

        Logger.WriteLog(output, Enums.LogLevel.Normal);

        if (output.Contains("FAILED (Invalid Login Auth Code)"))
        {
            Logger.WriteLog("AUTH FAILED", Enums.LogLevel.Normal);
            MessageController.SendResponse("Android", "Station", "SetValue:steamCMD:failure");
            configuringSteam = false;
        }
        else if (output.Contains("OK"))
        {
            Logger.WriteLog("AUTH SUCCESS, restarting VR system", Enums.LogLevel.Normal);
            MessageController.SendResponse("Android", "Station", "SetValue:steamCMD:configured");

            //Recollect the installed experiences
            MainController.wrapperManager?.ActionHandler("CollectApplications");
            configuringSteam = false;
        }

        //Manually kill the process or it will stay on the guard code input 
        cmd.Kill(true);
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
            Logger.WriteLog($"Station location null or empty: cannot run '{command}', MonitorSteamConfiguration -> SetupCommand returned null value.", Enums.LogLevel.Error);
            return null;
        }
        string fullPath = StationLocation + steamCmd;

        if (!File.Exists(fullPath))
        {
            JObject message = new JObject
            {
                { "action", "StationError" },
                { "value", $"File not found:{fullPath}" }
            };
            SessionController.PassStationMessage(message);
            SteamScripts.steamCmdConfigured = "steamcmd.exe not found";
            return null;
        }

        Process? cmd = SetupCommand(fullPath);
        if (cmd == null)
        {
            Logger.WriteLog($"Cannot start: {fullPath} and run '{command}', ExecuteSteamCommand -> SetupCommand returned null value.", Enums.LogLevel.Error);
            return null;
        }
        cmd.StartInfo.Arguments = "\"+force_install_dir \\\"C:/Program Files (x86)/Steam\\\"\" " + command;
        cmd.Start();

        string? output = Outcome(cmd);

        if (output == null)
        {
            Logger.WriteLog($"ExecuteSteamCommand -> SteamCMD output returned null value.", Enums.LogLevel.Error);
            return null;
        }

        if (output.Contains("Steam Guard code:"))
        {
            MessageController.SendResponse("Android", "Station", "SetValue:steamCMD:required");
            MockConsole.WriteLine("Steam Guard is not enabled for this account.");
            SteamScripts.steamCmdConfigured = "Missing";

            //Manually kill the process or it will stay on the guard code input 
            cmd.Kill(true);
            return null;
        }

        MessageController.SendResponse("Android", "Station", "SetValue:steamCMD:configured");
        SteamScripts.steamCmdConfigured = "Configured";
        
        return output;
    }

    /// <summary>
    /// Used to interact with Steamcmd. Uses the Arguments parameter for issuing commands instead
    /// of the writeline function like in executeStationCommand. This way it can run multiple commands
    /// in sequence. Most likely used for gathering, installing and uninstalling applications.
    /// </summary>
    /// <param name="command">A collection of steam commands that will be run in sequence.</param>
    /// <returns>A string representing the results of the command</returns>
    public static string? ExecuteSteamCommandSDrive(string command)
    {
        if (string.IsNullOrEmpty(StationLocation))
        {
            Logger.WriteLog($"Station location null or empty: cannot run '{command}', MonitorSteamConfiguration -> SetupCommand returned null value.", Enums.LogLevel.Error);
            return null;
        }
        string fullPath = StationLocation + steamCmd;

        if (!File.Exists(fullPath))
        {
            JObject message = new JObject
            {
                { "action", "StationError" },
                { "value", $"File not found:{fullPath}" }
            };
            SessionController.PassStationMessage(message);
            return null;
        }

        Process? cmd = SetupCommand(fullPath);
        if (cmd == null)
        {
            Logger.WriteLog($"Cannot start: {fullPath} and run '{command}', ExecuteSteamCommandSDrive -> SetupCommand returned null value.", Enums.LogLevel.Error);
            return null;
        }
        cmd.StartInfo.Arguments = "\"+force_install_dir \\\"S:/SteamLibrary\\\"\" " + command;
        cmd.Start();

        return Outcome(cmd);
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
                Enums.LogLevel.Debug);

            if (process.MainWindowTitle.Equals("Steam Sign In"))
            {
                Logger.WriteLog(
                    $"Killing Process: {process.ProcessName} ID: {process.Id}, MainWindowTitle: {process.MainWindowTitle}",
                    Enums.LogLevel.Debug);
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
    public static bool QueryProcesses(List<string> processes, bool kill = false)
    {
        List<Process> list = ProcessManager.GetProcessesByNames(processes);

        foreach (Process process in list)
        {
            Logger.WriteLog($"Process: {process.ProcessName} ID: {process.Id}", Enums.LogLevel.Verbose);

            if (!kill) continue;
            
            try
            {
                process.Kill();
            }
            catch
            {
                // ignored
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
            Process? cmd = SetupCommand(StationPowershell);
            if (cmd == null)
            {
                Logger.WriteLog($"Cannot start: {StationPowershell}, GetFreeStorage -> SetupCommand returned null value.", Enums.LogLevel.Error);
                return 9999;
            }
            cmd.Start();
            cmd.StandardInput.WriteLine(
                "Get-WmiObject -Class win32_logicaldisk | Format-Table @{n=\"FreeSpace\";e={[math]::Round($_.FreeSpace/1GB,2)}}");
            string? output = Outcome(cmd);

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
            Logger.WriteLog($"GetFreeStorage - Sentry Exception: {e}", Enums.LogLevel.Error);
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
        await httpClient.PostAsync(
            "https://us-central1-leadme-labs.cloudfunctions.net/uploadFile",
            content
        );
    }

    [DllImport("user32.dll")]
    public static extern int SetForegroundWindow(int hwnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(int handle, int state);
    
    private static readonly string[] LoadingMessages = {
        "Preparing immersive learning environment...",
        "Loading immersive experiences...",
        "Configuring VR settings...",
        "Automating the boring stuff...",
        "Preparing virtual learning tools...",
        "Sparking the lightbulb moment...",
        "Almost there..."
    };

    private const int SW_SHOWNORMAL = 1;
    public static async void EnterAcceptEula(int windowHandle, string appId, string eulaName, string eulaVersion)
    {
        ShowWindow(windowHandle, SW_SHOWNORMAL);   
        SetForegroundWindow(windowHandle);
        await Task.Delay(10);
        SendKeysToActiveWindow("^+p");
        await Task.Delay(200);
        ShowWindow(windowHandle, SW_SHOWNORMAL);   
        SetForegroundWindow(windowHandle);
        await Task.Delay(10);
        SendKeysToActiveWindow("Show Console");
        await Task.Delay(200);
        ShowWindow(windowHandle, SW_SHOWNORMAL);   
        SetForegroundWindow(windowHandle);
        await Task.Delay(10);
        SendKeysToActiveWindow("{Enter}");
        await Task.Delay(200);
        ShowWindow(windowHandle, SW_SHOWNORMAL);   
        SetForegroundWindow(windowHandle);
        await Task.Delay(10);
        SendKeysToActiveWindow("this.SteamClient.Apps.MarkEulaAccepted{(}" + $"{appId}, \"{eulaName}\", {eulaVersion}" + "{)}");
        await Task.Delay(200);
        ShowWindow(windowHandle, SW_SHOWNORMAL);   
        SetForegroundWindow(windowHandle);
        await Task.Delay(10);
        SendKeysToActiveWindow("{Enter}");
        await Task.Delay(200);
         ShowWindow(windowHandle, SW_SHOWNORMAL);   
         SetForegroundWindow(windowHandle);
         await Task.Delay(10);
         SendKeysToActiveWindow("{Enter}");
    }
    
    public static void EnterAltF4(int windowHandle)
    {
        ShowWindow(windowHandle, SW_SHOWNORMAL);
        SetForegroundWindow(windowHandle);
        SendKeysToActiveWindow("%({F4})"); // % = Alt, {F4} = F4
    }

    public static string PowershellGetDevToolsChildProcessWindowHandle()
    {
        List<WinStruct> list = ApiDef.GetWindows();
        list = list.FindAll(win => win.WinTitle.Equals("DevTools"));
        if (list.Count == 0)
        {
            return "";
        }

        return list.First().WinHwnd.ToString();
    }

    /// <summary>
    /// Attempts to bypass the experience confirmation window of a specified process by bringing its main window to the foreground and simulating a key press.
    /// </summary>
    /// <param name="windowTitleToFind">The window title to search for in order to identify the process.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task BypassExperienceConfirmationWindow(string windowTitleToFind)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            MockConsole.WriteLine($"Searching for confirmation window: {windowTitleToFind}", Enums.LogLevel.Normal);
            
            // Retrieve all processes
            Process[] processes = Process.GetProcesses();

            foreach (Process process in processes)
            {
                // Skip processes with no main window title or title mismatch
                if (string.IsNullOrEmpty(process.MainWindowTitle) ||
                    !process.MainWindowTitle.Contains(windowTitleToFind)) continue;

                // Print process information
                Console.WriteLine($"Process Name: {process.ProcessName}, Window Title: {process.MainWindowTitle}, Process ID: {process.Id}");
                
                MockConsole.WriteLine($"Confirmation window found attempting to bypass: {windowTitleToFind}", Enums.LogLevel.Normal);

                // Set the found window as foreground and perform action
                SetForegroundWindow(process.MainWindowHandle.ToInt32());
                PressEnterOnActiveWindow();

                // Exit the function after action is performed
                return;
            }

            // Delay for 5 seconds before the next attempt
            await Task.Delay(5000);
        }
        
        MockConsole.WriteLine($"Confirmation window not found, unable to bypass: {windowTitleToFind}", Enums.LogLevel.Normal);
    }

    private static void PressEnterOnActiveWindow()
    {
        Process? cmd = SetupCommand(StationPowershell);
        if (cmd == null)
        {
            Logger.WriteLog($"Cannot start: {StationPowershell}, PowershellCommand (cmd) -> SetupCommand returned null value.", Enums.LogLevel.Error);
            return;
        }
        cmd.Start();
        cmd.StandardInput.WriteLine("$StartDHCP = New-Object -ComObject wscript.shell;");
        cmd.StandardInput.WriteLine("$StartDHCP.SendKeys('{ENTER}')");
        Outcome(cmd);
    }

    public static void SendKeysToActiveWindow(string keys)
    {
        Process? cmd = SetupCommand(StationPowershell);
        if (cmd == null)
        {
            Logger.WriteLog($"Cannot start: {StationPowershell}, PowershellCommand (cmd) -> SetupCommand returned null value.", Enums.LogLevel.Error);
            return;
        }
        cmd.Start();
        cmd.StandardInput.WriteLine("$StartDHCP = New-Object -ComObject wscript.shell;");
        cmd.StandardInput.WriteLine($"$StartDHCP.SendKeys('{keys}')");
        Outcome(cmd);
    }

    public static async void PowershellCommand(Process steamSignInWindow)
    {
        OverlayManager.SetText(LoadingMessages[0]);
        await Task.Delay(5000);
        OverlayManager.SetText(LoadingMessages[1]);
        await Task.Delay(5000);
        OverlayManager.SetText(LoadingMessages[2]);
        await Task.Delay(5000);
        OverlayManager.SetText(LoadingMessages[3]);
        Logger.WriteLog($"Tabbing back out of offline warning", Enums.LogLevel.Debug);
        Process? cmd = SetupCommand(StationPowershell);
        if (cmd == null)
        {
            Logger.WriteLog($"Cannot start: {StationPowershell}, PowershellCommand (cmd) -> SetupCommand returned null value.", Enums.LogLevel.Error);
            return;
        }
        cmd.Start();
        cmd.StandardInput.WriteLine("$StartDHCP = New-Object -ComObject wscript.shell;");
        cmd.StandardInput.WriteLine("$StartDHCP.SendKeys('{TAB}')");
        cmd.StandardInput.WriteLine("$StartDHCP.SendKeys('{ENTER}')");
        ShowWindow(steamSignInWindow.MainWindowHandle.ToInt32(), 3);    
        SetForegroundWindow(steamSignInWindow.MainWindowHandle.ToInt32());
        Outcome(cmd);
        
        await Task.Delay(2000);
        OverlayManager.SetText(LoadingMessages[4]);
        Logger.WriteLog($"Entering steam details", Enums.LogLevel.Debug);
        Process? cmd2 = SetupCommand(StationPowershell);
        if (cmd2 == null)
        {
            Logger.WriteLog($"Cannot start: {StationPowershell}, PowershellCommand (cmd2) -> SetupCommand returned null value.", Enums.LogLevel.Error);
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
        Outcome(cmd2);
        
        await Task.Delay(5000);
        OverlayManager.SetText(LoadingMessages[5]);
        await Task.Delay(5000);
        OverlayManager.SetText(LoadingMessages[6]);
        Logger.WriteLog($"Submitting offline form", Enums.LogLevel.Debug);
        Process cmd3 = SetupCommand(StationPowershell);
        if (cmd3 == null)
        {
            Logger.WriteLog($"Cannot start: {StationPowershell}, PowershellCommand (cmd3) -> SetupCommand returned null value.", Enums.LogLevel.Error);
            return;
        }
        cmd3.Start();
        cmd3.StandardInput.WriteLine("$StartDHCP = New-Object -ComObject wscript.shell;");
        cmd3.StandardInput.WriteLine("$StartDHCP.SendKeys('{TAB}')");
        cmd3.StandardInput.WriteLine("$StartDHCP.SendKeys('{TAB}')");
        cmd3.StandardInput.WriteLine("$StartDHCP.SendKeys('{ENTER}')");
        ShowWindow(steamSignInWindow.MainWindowHandle.ToInt32(), 3);
        SetForegroundWindow(steamSignInWindow.MainWindowHandle.ToInt32());
        Outcome(cmd3);

        await Task.Delay(2000);
        OverlayManager.ManualStop();
    }

    public static void ToggleSteamVrLegacyMirror()
    {
        Process? cmd = SetupCommand(StationPowershell);
        if (cmd == null)
        {
            Logger.WriteLog($"Cannot start: {StationPowershell}, PowershellCommand (cmd) -> SetupCommand returned null value.", Enums.LogLevel.Error);
            return;
        }
        cmd.Start();
        cmd.StandardInput.WriteLine("Start-Process \"vrmonitor://debugcommands/legacy_mirror_view_toggle\"");
        Outcome(cmd);
    }

    public static string? GetProcessIdFromMainWindowTitle(string mainWindowTitle)
    {
        Logger.WriteLog("gps | where {$_.MainWindowTitle -Like \"*" + mainWindowTitle + "*\"} | select ID", Enums.LogLevel.Debug);

        Process? cmd = SetupCommand(StationPowershell);
        if (cmd == null)
        {
            Logger.WriteLog($"Cannot start: {StationPowershell}, GetProcessIdFromDir -> SetupCommand returned null value.", Enums.LogLevel.Error);
            return null;
        }
        cmd.Start();
        cmd.StandardInput.WriteLine("gps | where {$_.MainWindowTitle -Like \"*" + mainWindowTitle + "*\"} | select ID");

        string? output = Outcome(cmd);

        if (output == null)
        {
            Logger.WriteLog($"No output recorded for {mainWindowTitle}", Enums.LogLevel.Debug);
            return null;
        }

        Logger.WriteLog(output, Enums.LogLevel.Debug);

        string[] outputP = output.Split("\n");

        // if there is less than 14 items, the app probably hasn't launched yet
        int iterator = 0;
        while (iterator < outputP.Length)
        {
            Logger.WriteLog($"Output line {iterator}: {outputP[iterator].Trim()}", Enums.LogLevel.Debug);

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

        Logger.WriteLog($"ID: {outputP[iterator + 2].Trim()}", Enums.LogLevel.Debug);

        return outputP[iterator + 2].Trim();
    }

    /// <summary>
    /// Pass in a steam application directory to get the process id if running
    /// </summary>
    /// <param name="dir">Fully qualified directory where a steam application executable is stored</param>
    /// <returns>Process id if the application is running</returns>
    public static string? GetProcessIdFromDir(string dir)
    {
        Logger.WriteLog("gps | where {$_.Path -Like \"" + dir + "*\"} | where {$_.MainWindowHandle -ne 0} | select ID", Enums.LogLevel.Debug);

        Process? cmd = SetupCommand(StationPowershell);
        if (cmd == null)
        {
            Logger.WriteLog($"Cannot start: {StationPowershell}, GetProcessIdFromDir -> SetupCommand returned null value.", Enums.LogLevel.Error);
            return null;
        }
        cmd.Start();
        cmd.StandardInput.WriteLine("gps | where {$_.Path -Like \"" + dir + "*\"} | where {$_.MainWindowHandle -ne 0} | select ID");

        //This works as an alternate to the powershell command
        //Process cmd = setupCommand(stationCmd);
        //cmd.Start();
        //cmd.StandardInput.WriteLine($"wmic process where \"ExecutablePath like '%{dir}%'\" get ProcessID,ExecutablePath");

        string? output = Outcome(cmd);

        if (output == null)
        {
            Logger.WriteLog($"No output recorded for {dir}", Enums.LogLevel.Debug);
            return null;
        }

        Logger.WriteLog(output, Enums.LogLevel.Debug);

        string[] outputP = output.Split("\n");

        // if there is less than 14 items, the app probably hasn't launched yet
        int iterator = 0;
        while (iterator < outputP.Length)
        {
            Logger.WriteLog($"Output line {iterator}: {outputP[iterator].Trim()}", Enums.LogLevel.Debug);

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

        Logger.WriteLog($"ID: {outputP[iterator + 2].Trim()}", Enums.LogLevel.Debug);

        return outputP[iterator + 2].Trim();
    }
}
