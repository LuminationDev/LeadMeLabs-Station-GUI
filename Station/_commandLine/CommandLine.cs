using Sentry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Station
{
    /// <summary>
    /// A static class to call command prompt actions from anywhere that is needed. Has
    /// two different commands, the standard command prompt and the steamCMD command.
    /// </summary>
    public static class CommandLine
    {
        /// <summary>
        /// Process name of the Launcher that coordinates the LeadMe software suite.
        /// </summary>
        private static readonly string launcherProcessName = "LeadMe";

        /// <summary>
        /// Process name of the current application.
        /// </summary>
        private static readonly string stationProcessName = "Station";

        private static bool connected = RunInternetCheck();

        /// <summary>
        /// The location of the executing assembly. This is used to find the relative path for externally used applications.
        /// </summary>
        public static readonly string?
            stationLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

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

        /// <summary>
        /// Track if SteamCMD is currently being configured with a Guard Key.
        /// </summary>
        private static bool configuringSteam = false;

        /// <summary>
        /// The relative path to the SetVol executable
        /// </summary>
        public static string SetVol = stationLocation + @"\external\SetVol\SetVol.exe";

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
            string output = "";
            string error = "";

            temp.OutputDataReceived += new DataReceivedEventHandler((s, e) =>
                {
                    output += e.Data + "\n";
                }
            );

            temp.ErrorDataReceived += new DataReceivedEventHandler((s, e) =>
                {
                    error += e.Data + "\n";
                }
            );

            temp.BeginOutputReadLine();
            temp.BeginErrorReadLine();
            temp.StandardInput.Flush();
            temp.StandardInput.Close();
            temp.WaitForExit();

            if (error != null)
            {
                return output;
            }
            else
            {
                return error;
            }
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

        public static List<Process> GetAllProcesses(List<string> processNames, List<string> processIds)
        {
            List<Process> allProcesses = new();
            if (processNames.Count > 0)
            {
                allProcesses.AddRange(GetProcessesByName(processNames));
            }

            if (processIds.Count > 0)
            {
                allProcesses.AddRange(GetProcessesById(processIds));
            }

            return allProcesses;
        }

        /// <summary>
        /// Retrieve running processes by ids.
        /// </summary>
        /// <param name="processIds">An array of string ids containing the processes to fetch</param>
        /// <returns>A list of the processes for the ids supplied</returns>
        public static List<Process> GetProcessesById(List<string> processIds)
        {
            List<Process> list = new();

            foreach (string processId in processIds)
            {
                try
                {
                    Process process = Process.GetProcessById(Int32.Parse(processId));
                    list.Add(process);
                }
                catch (ArgumentException)
                {
                    continue;
                }
            }

            return list;
        }

        /// <summary>
        /// Retrieve a single process by id
        /// </summary>
        /// <param name="processId">The id of the process to retreive</param>
        /// <returns>The process corresponding to the id, or null if it doesn't exist</returns>
        public static Process? GetProcessById(string processId)
        {
            if (processId.Equals(""))
            {
                return null;
            }

            try
            {
                Process process = Process.GetProcessById(Int32.Parse(processId));
                return process;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>
        /// Retrieve any processes running on the local machine.
        /// </summary>
        /// <returns>A list of all active processes currently running</returns>
        public static Process[] GetAllProcesses()
        {
            Process[] processes = Process.GetProcesses();

            foreach (Process process in processes)
            {
                Logger.WriteLog($"Process: {process.ProcessName} ID: {process.Id}", MockConsole.LogLevel.Verbose);
            }

            return processes;
        }

        /// <summary>
        /// Check if any processes have stopped responding. May have to be manually called
        /// every set amount of time.
        /// </summary>
        /// <returns>A list of processes that have stopped responding</returns>
        public static List<Process> CheckAllProcesses()
        {
            List<Process> list = new();
            Process[] processes = Process.GetProcesses();

            foreach (Process process in processes)
            {
                if (!process.Responding)
                {
                    list.Add(process);
                }
            }

            return list;
        }

        /// <summary>
        /// Kill off the launcher program if the time is between a set amount. The Software_Checker scheduler task will automatically restart the
        /// application within the next five minutes, updating the Launcher and Station software.
        /// </summary>
        /// <param name="time">A list containing the current time sections [0]-hours, [1]-minutes, [2]-seconds</param>
        public static void RestartProgram()
        {
            Logger.WriteLog("Daily restart", MockConsole.LogLevel.Verbose);

            List<Process> processes = GetProcessesByName(new List<string> {launcherProcessName, stationProcessName});

            foreach (Process process in processes)
            {
                process.Kill(true);
            }
        }

        /// <summary>
        /// Set the volume of the Station using the third party SetVol program.
        /// </summary>
        /// <param name="volume">A string representing the level at which to set the volume.</param>
        public static void SetVolume(string volume)
        {
            if (!File.Exists(SetVol))
            {
                SessionController.PassStationMessage($"StationError,File not found:{SetVol}");
                return;
            }

            try
            {
                if (int.TryParse(volume, out _))
                {
                    if (volume.Equals("0"))
                    {
                        StartProgram(SetVol, "mute");
                    }
                    else
                    {
                        StartProgram(SetVol, "unmute");
                    }

                    StartProgram(SetVol, volume);
                }
            }
            catch (Exception e)
            {
                SentrySdk.CaptureException(e);
            }
        }

        /// <summary>
        /// Get the current volume of the Station usgin the third party SetVol program.
        /// </summary>
        /// <returns>A string representing the current volume of the Station.</returns>
        public static string? GetVolume()
        {
            if (!File.Exists(SetVol))
            {
                SessionController.PassStationMessage($"StationError,File not found:{SetVol}");
                return null;
            }

            try
            {
                string? output = RunProgramWithOutput(SetVol, "report");
                if (output == null)
                {
                    return null;
                }

                string[] lines = output.Split("\n");
                foreach (var line in lines)
                {
                    if (line.StartsWith("Master volume level"))
                    {
                        return line.Split(" ")[4];
                    }
                }
            }
            catch (Exception e)
            {
                SentrySdk.CaptureException(e);
            }

            return "0";
        }


        ///////////////////////////////////////////////
        ///Wrapper Specific Methods
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

                if (string.IsNullOrEmpty(stationLocation))
                {
                    Logger.WriteLog($"Station location null or empty: cannot run '{command}', MonitorSteamConfiguration -> SetupCommand returned null value.", MockConsole.LogLevel.Error);
                    return;
                }
                string fullPath = stationLocation + steamCmd;

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
                    Manager.SendResponse("Android", "Station", "SetValue:steamCMD:error");
                    configuringSteam = false;
                    return;
                }

                Logger.WriteLog(output, MockConsole.LogLevel.Normal);

                if (output.Contains("FAILED (Invalid Login Auth Code)"))
                {
                    Logger.WriteLog("AUTH FAILED", MockConsole.LogLevel.Normal);
                    Manager.SendResponse("Android", "Station", "SetValue:steamCMD:failure");
                    configuringSteam = false;
                }
                else if (output.Contains("OK"))
                {
                    Logger.WriteLog("AUTH SUCCESS, restarting VR system", MockConsole.LogLevel.Normal);
                    Manager.SendResponse("Android", "Station", "SetValue:steamCMD:configured");

                    //Recollect the installed experiences
                    Manager.wrapperManager?.ActionHandler("CollectApplications");
                    configuringSteam = false;
                }

                //Manually kill the process or it will stay on the guard code input 
                cmd.Kill(true);
            }

            ;
        }

        /// <summary>
        /// Used to interact with Steamcmd. Uses the Arguments parameter for issuing commands instead
        /// of the writeline funciton like in executeStationCommand. This way it can run multiple commands
        /// in sequence. Most likely used for gathering, installing and uninstalling applications.
        /// </summary>
        /// <param name="command">A collection of steam commands that will be run in sequence.</param>
        /// <returns>A string representing the results of the command</returns>
        public static string? ExecuteSteamCommand(string command)
        {
            if (string.IsNullOrEmpty(stationLocation))
            {
                Logger.WriteLog($"Station location null or empty: cannot run '{command}', MonitorSteamConfiguration -> SetupCommand returned null value.", MockConsole.LogLevel.Error);
                return null;
            }
            string fullPath = stationLocation + steamCmd;

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

            if (output.Contains("Steam Guard code:"))
            {
                Manager.SendResponse("Android", "Station", "SetValue:steamCMD:required");
                MockConsole.WriteLine("Steam Guard is not enabled for this account.");
                SteamScripts.steamCMDConfigured = "Missing";

                //Manually kill the process or it will stay on the guard code input 
                cmd.Kill(true);
                return null;
            }

            Manager.SendResponse("Android", "Station", "SetValue:steamCMD:configured");
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
            if (string.IsNullOrEmpty(stationLocation))
            {
                Logger.WriteLog($"Station location null or empty: cannot run '{command}', MonitorSteamConfiguration -> SetupCommand returned null value.", MockConsole.LogLevel.Error);
                return null;
            }
            string fullPath = stationLocation + steamCmd;

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
        /// Retrieve any processes running on the local machine that have the same name as any of the supplied strings.
        /// </summary>
        /// <param name="processTitles">An array of strings containing the name of processes to search for</param>
        /// <returns>A list of all active processes for the titles supplied</returns>
        public static List<Process> GetProcessesByName(List<string> processTitles)
        {
            List<Process> list = new();

            foreach (string process in processTitles)
            {
                list.AddRange(Process.GetProcessesByName(process));
            }

            return list;
        }

        /// <summary>
        /// Destroy the Steam sign in window if this happens to be up when going to launch an application.
        /// </summary>
        public static void KillSteamSigninWindow()
        {
            List<Process> list = GetProcessesByName(new List<string> {"steam"});
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

        private static bool RunInternetCheck()
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create("http://learninglablauncher.herokuapp.com/program-station-version");
                request.KeepAlive = false;
                request.Timeout = 10000;
                using (var response = (HttpWebResponse)request.GetResponse())
                    return true;
            }
            catch
            {
                return false;
            }
        }
        
        public static bool CheckIfConnectedToInternet()
        {
            return connected;
        }

        /// <summary>
        /// Provide a list of process names and ids and get a boolean if they are responding
        /// If the process names do not have a running process, they will NOT return false
        /// </summary>
        /// <param name="allProcesses">Array of process names to check</param>
        /// <returns>boolean of if any of the processes are not responding according to powershell</returns>
        public static bool CheckThatAllProcessesAreResponding(List<Process> allProcesses)
        {
            foreach (Process process in allProcesses)
            {
                if (!process.Responding)
                {
                    Logger.WriteLog($"Process is not responding: {process.ProcessName}", MockConsole.LogLevel.Normal);
                    return false;
                }
            }

            Logger.WriteLog("All processes are responding", MockConsole.LogLevel.Verbose, false);
            return true;
        }

        /// <summary>
        /// Query processes to see if any are currently running.
        /// </summary>
        /// <param name="processes">An array of strings containing the name of processes to search for</param>
        /// <param name="kill">A bool representing if the processes should be stopped or not</param>
        /// <returns>A boolean representing if a process is still running</returns>
        public static bool QueryVRProcesses(List<string> processes, bool kill = false)
        {
            List<Process> list = GetProcessesByName(processes);

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
            cmd2.StandardInput.WriteLine($"$StartDHCP.SendKeys('{Environment.GetEnvironmentVariable("SteamPassword")}')");
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
}
