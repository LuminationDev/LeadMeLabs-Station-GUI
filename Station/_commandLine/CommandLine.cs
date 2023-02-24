using Sentry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

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
        /// The location of the executing assembly. This is used to find the relative path for externally used applications.
        /// </summary>
        public static readonly string? stationLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        /// <summary>
        /// A string representing the regular command prompt executable.
        /// </summary>
        private static readonly string stationCmd = "cmd.exe";

        /// <summary>
        /// The relative path of the steamCMD executable on the local machine.
        /// </summary>
        public static string steamCmd = stationLocation + @"\external\steamcmd\steamcmd.exe";

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
        private static Process setupCommand(string type)
        {
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
        public static void startProgram(string executable, string arguments = "")
        {
            Process cmd = setupCommand(executable);
            cmd.StartInfo.Arguments = arguments;
            cmd.Start();
            cmd.Close();
        }

        public static string? runProgramWithOutput(string executable, string arguments = "")
        {
            Process cmd = setupCommand(executable);
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
        public static string? executeStationCommand(string command)
        {
            Process cmd = setupCommand(stationCmd);
            cmd.Start();

            cmd.StandardInput.WriteLine(command);

            return outcome(cmd);
        }

        /// <summary>
        /// Used to interact with a browser. Uses the Arguments parameter for passing a specific URL.
        /// </summary>
        /// <param name="url">A string representing the url of a website being pushed.</param>
        /// <returns>A string representing the results of the command</returns>
        public static void executeBrowserCommand(string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        public static string? cancelShutdown()
        {
            string? output = executeStationCommand("shutdown /a");
            return output;
        }

        public static string? shutdownStation(int time)
        {
            string? output = executeStationCommand("shutdown /s /t " + time);
            return output;
        }

        public static List<Process> getAllProcesses(List<string> processNames, List<string> processIds)
        {
            List<Process> allProcesses = new();
            if (processNames.Count > 0)
            {
                allProcesses.AddRange(getProcessesByName(processNames));
            }

            if (processIds.Count > 0)
            {
                allProcesses.AddRange(getProcessesById(processIds));
            }

            return allProcesses;
        }

        /// <summary>
        /// Retrieve running processes by ids.
        /// </summary>
        /// <param name="processIds">An array of string ids containing the processes to fetch</param>
        /// <returns>A list of the processes for the ids supplied</returns>
        public static List<Process> getProcessesById(List<string> processIds)
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
        public static Process? getProcessById(string processId)
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
        public static Process[] getAllProcesses()
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
        public static List<Process> checkAllProcesses()
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
        public static void restartProgram()
        {
            Logger.WriteLog("Daily restart", MockConsole.LogLevel.Verbose);

            List<Process> matches = getProcessesByName(new List<string> { launcherProcessName });

            if (matches.Count == 1)
            {
                matches[0].Kill(true);
            }
            else if (matches.Count > 1)
            {
                Logger.WriteLog("ERROR: Multiple LeadMe Launcher instances detected", MockConsole.LogLevel.Error);
            }
        }

        /// <summary>
        /// Set the volume of the Station using the third party SetVol program.
        /// </summary>
        /// <param name="volume">A string representing the level at which to set the volume.</param>
        public static void setVolume(string volume)
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
                        startProgram(SetVol, "mute");
                    }
                    else
                    {
                        startProgram(SetVol, "unmute");
                    }
                    startProgram(SetVol, volume);
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
        public static string? getVolume()
        {
            if (!File.Exists(SetVol))
            {
                SessionController.PassStationMessage($"StationError,File not found:{SetVol}");
                return null;
            }

            try
            {
                string? output = runProgramWithOutput(SetVol, "report");
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


        ///Wrapper Specific Methods

        /// <summary>
        /// Used to interact with Steamcmd. Uses the Arguments parameter for issuing commands instead
        /// of the writeline funciton like in executeStationCommand. This way it can run multiple commands
        /// in sequence. Most likely used for gathering, installing and uninstalling applications.
        /// </summary>
        /// <param name="command">A collection of steam commands that will be run in sequence.</param>
        /// <returns>A string representing the results of the command</returns>
        public static string? executeSteamCommand(string command)
        {
            if (!File.Exists(steamCmd))
            {
                SessionController.PassStationMessage($"StationError,File not found:{steamCmd}");
                return null;
            }

            Process cmd = setupCommand(steamCmd);
            cmd.StartInfo.Arguments = "\"+force_install_dir \\\"C:/Program Files (x86)/Steam\\\"\" " + command;
            cmd.Start();

            string? output = outcome(cmd);

            if (output.Contains("Steam Guard code:"))
            {
                MockConsole.WriteLine("Steam Guard is not enabled for this account.");
                return null;
            }

            return output;
        }

        /// <summary>
        /// Used to interact with Steamcmd. Uses the Arguments parameter for issuing commands instead
        /// of the writeline funciton like in executeStationCommand. This way it can run multiple commands
        /// in sequence. Most likely used for gathering, installing and uninstalling applications.
        /// </summary>
        /// <param name="command">A collection of steam commands that will be run in sequence.</param>
        /// <returns>A string representing the results of the command</returns>
        public static string? executeSteamCommandSDrive(string command)
        {
            if (!File.Exists(steamCmd))
            {
                SessionController.PassStationMessage($"StationError,File not found:{steamCmd}");
                return null;
            }

            Process cmd = setupCommand(steamCmd);
            cmd.StartInfo.Arguments = "\"+force_install_dir \\\"S:/SteamLibrary\\\"\" " + command;
            cmd.Start();

            return outcome(cmd);
        }

        /// <summary>
        /// Retrieve any processes running on the local machine that have the same name as any of the supplied strings.
        /// </summary>
        /// <param name="processTitles">An array of strings containing the name of processes to search for</param>
        /// <returns>A list of all active processes for the titles supplied</returns>
        public static List<Process> getProcessesByName(List<string> processTitles)
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
            List<Process> list = getProcessesByName(new List<string> { "steam" });
            foreach (Process process in list)
            {
                Logger.WriteLog($"Inside kill steam signin: Process: {process.ProcessName} ID: {process.Id}, MainWindowTitle: {process.MainWindowTitle}", MockConsole.LogLevel.Debug);

                if (process.MainWindowTitle.Equals("Steam Sign In"))
                {
                    Logger.WriteLog($"Killing Process: {process.ProcessName} ID: {process.Id}, MainWindowTitle: {process.MainWindowTitle}", MockConsole.LogLevel.Debug);
                    process.Kill();
                }
            }
        }

        /// <summary>
        /// Provide a list of process names and ids and get a boolean if they are responding
        /// If the process names do not have a running process, they will NOT return false
        /// </summary>
        /// <param name="allProcesses">Array of process names to check</param>
        /// <returns>boolean of if any of the processes are not responding according to powershell</returns>
        public static bool checkThatAllProcessesAreResponding(List<Process> allProcesses)
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
        public static bool queryVRProcesses(List<string> processes, bool kill = false)
        {
            List<Process> list = getProcessesByName(processes);

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
    }
}
