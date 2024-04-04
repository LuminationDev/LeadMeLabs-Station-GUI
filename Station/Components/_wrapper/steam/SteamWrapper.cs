using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Newtonsoft.Json.Linq;
using Station.Components._commandLine;
using Station.Components._interfaces;
using Station.Components._managers;
using Station.Components._models;
using Station.Components._monitoring;
using Station.Components._notification;
using Station.Components._openvr;
using Station.Components._overlay;
using Station.Components._profiles;
using Station.Components._utils;
using Station.Components._wrapper.vive;
using Station.MVC.Controller;
using Timer = System.Timers.Timer;

namespace Station.Components._wrapper.steam;

public class SteamWrapper : IWrapper
{
    public const string WrapperType = "Steam";
    private static Process? currentProcess;
    private static readonly string LaunchParams = "-noreactlogin -login " + 
       Environment.GetEnvironmentVariable("SteamUserName", EnvironmentVariableTarget.Process) + " " + 
       Environment.GetEnvironmentVariable("SteamPassword", EnvironmentVariableTarget.Process) + " steam://rungameid/";
    public static string? experienceName;
    private static string? installDir;
    private static Experience lastExperience;
    private bool _launchWillHaveFailedFromOpenVrTimeout = true;

    /// <summary>
    /// Track if an experience is being launched.
    /// </summary>
    private static bool launchingExperience ;

    public Experience? GetLastExperience()
    {
        return lastExperience;
    }
    
    public void SetLastExperience(Experience experience)
    {
        lastExperience = experience;
    }

    public bool GetLaunchingExperience()
    {
        return launchingExperience;
    }

    public void SetLaunchingExperience(bool isLaunching)
    {
        launchingExperience = isLaunching;
    }
    
    public bool LaunchFailedFromOpenVrTimeout()
    {
        return _launchWillHaveFailedFromOpenVrTimeout;
    }

    public string? GetCurrentExperienceName()
    {
        return experienceName;
    }
    
    public List<T>? CollectApplications<T>()
    {
        return SteamScripts.LoadAvailableExperiences<T>();
    }

    public void CollectHeaderImage(string experienceNameToCollect)
    {
        throw new NotImplementedException();
    }

    public void PassMessageToProcess(string message)
    {
        throw new NotImplementedException();
    }


    public void SetCurrentProcess(Process process)
    {
        currentProcess?.Kill(true);

        _launchWillHaveFailedFromOpenVrTimeout = false;
        currentProcess = process;
        ListenForClose();
    }

    public string WrapProcess(Experience experience)
    {
        // Safe cast for potential vr profile
        VrProfile? vrProfile = Profile.CastToType<VrProfile>(SessionController.StationProfile);
        
        _launchWillHaveFailedFromOpenVrTimeout = false;
        if (experience.ID == null)
        {
            SessionController.PassStationMessage($"MessageToAndroid,GameLaunchFailed:Unknown experience");
            return $"MessageToAndroid,GameLaunchFailed:Unknown experience";
        }

        if (vrProfile?.VrHeadset == null && experience.IsVr)
        {
            SessionController.PassStationMessage("No VR headset set.");
            return "No VR headset set.";
        }

        lastExperience = experience;
        GetGameProcessDetails();

        if(experienceName == null || installDir == null)
        {
            SessionController.PassStationMessage($"MessageToAndroid,GameLaunchFailed:Fail to find experience");
            Logger.WriteLog($"Unable to find Steam experience details (name & install directory) for: {experience.Name}", MockConsole.LogLevel.Normal);
            return $"Unable to find Steam experience details (name & install directory) for: {experience.Name}";
        }

        MockConsole.WriteLine($"Wrapping: {experienceName}", MockConsole.LogLevel.Debug);

        //Start the external processes to handle SteamVR
        if (experience.IsVr)
        {
            SessionController.StartSession(WrapperType);
        }
        
        //Begin monitoring the different processes
        WrapperMonitoringThread.InitializeMonitoring(WrapperType, experience.IsVr);
        
        //Wait for Steam to be signed in
        if (!Profile.WaitForSteamLogin())
        {
            string error = "Error: Steam could not open";
            ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage($"SoftwareState,{error}"),
                TimeSpan.FromSeconds(1)); //Wait for steam/other accounts to login
            return "Error: Steam could not open";
        }
        
        if (InternalDebugger.GetHeadsetRequired() && experience.IsVr)
        {
            // Check the current Station profile
            if (vrProfile?.VrHeadset == null)
            {
                return "Station is not a VR profile or has not VR Headset set and is attempting to launch a VR experience";
            }
            
            //Wait for the Headset's connection method to respond
            if (!vrProfile.WaitForConnection(WrapperType))
            {
                experienceName = null; //Reset for correct headset state
                return "Could not get headset connection";
            }

            //If headset management software is open (with headset connected) and OpenVrSystem cannot initialise then restart SteamVR
            if (!OpenVrManager.WaitForOpenVr().Result)
            {
                experienceName = null; //Reset for correct headset state
                return "Could not connect to OpenVR";
            }
        }

        Task.Factory.StartNew(() =>
        {
            if (experience.IsVr)
            {
                //Attempt to start the process using OpenVR
                _launchWillHaveFailedFromOpenVrTimeout = true;
                if (OpenVrManager.LaunchApplication(experience.ID))
                {
                    Logger.WriteLog($"SteamWrapper.WrapProcess: Launching {experience.Name} via OpenVR", MockConsole.LogLevel.Verbose);
                    return;
                }

                _launchWillHaveFailedFromOpenVrTimeout = false;

                //Fall back to the alternate if OpenVR launch fails or is not a registered VR experience in the vrmanifest
                //Stop any accessory processes before opening a new process
                vrProfile?.VrHeadset?.StopProcessesBeforeLaunch();
            }

            Logger.WriteLog($"SteamWrapper.WrapProcess: Using AlternateLaunchProcess", MockConsole.LogLevel.Normal);
            AlternateLaunchProcess(experience);
        });
        return "launching";
    }

    /// <summary>
    /// Collect the name of the application from the Steam install directory, the executable name is what windows uses
    /// as the 'Image Name' and will not change unless the executable is changed which does not matter for this function.
    /// </summary>
    private void GetGameProcessDetails()
    {
        string fileLocation = "S:\\SteamLibrary\\steamapps\\appmanifest_" + lastExperience.ID + ".acf";
        if (!File.Exists(fileLocation))
        {
            fileLocation = "C:\\Program Files (x86)\\Steam\\steamapps\\appmanifest_" + lastExperience.ID + ".acf";
            if (!File.Exists(fileLocation))
            {
                launchingExperience = false;
                throw new FileNotFoundException("Error", fileLocation);
            }
        }

        Logger.WriteLog($"Steam experience file location: {fileLocation}", MockConsole.LogLevel.Normal);

        experienceName = null;
        installDir = null;

        foreach (string line in File.ReadLines(fileLocation))
        {
            if (line.StartsWith("\t\"name\""))
            {
                experienceName = line.Split("\t")[3].Trim('\"');
            }

            if (line.StartsWith("\t\"installdir\""))
            {
                installDir = line.Split("\t")[3].Trim('\"');
            }

            if (experienceName != null && installDir != null)
            {
                break;
            }
        }

        Logger.WriteLog($"Steam experience install directory: {installDir}", MockConsole.LogLevel.Normal);
    }

    #region Alternate Launch Process
    /// <summary>
    /// Launches an alternate process for the given experience by executing a specified executable (e.g., Steam) with parameters.
    /// Starts a new process using the provided executable path (e.g., SessionController.steam) and the experience's launch parameters.
    /// If any additional experience parameters are available, they are appended to the launch arguments.
    /// After starting the process, searches for the newly launched process and tracks it.
    /// </summary>
    /// <param name="experience">The experience object representing the application to launch.</param>
    private void AlternateLaunchProcess(Experience experience)
    {
        currentProcess = new Process();
        currentProcess.StartInfo.FileName = SessionController.Steam;
        currentProcess.StartInfo.Arguments = LaunchParams + experience.ID;

        //Add any extra launch parameters
        if (experience.Parameters != null)
        {
            //Include a space before added more
            currentProcess.StartInfo.Arguments += $" {experience.Parameters}";
        }

        currentProcess.Start();

        FindCurrentProcess();
    }

    /// <summary>
    /// Find the active process that has been launched.
    /// </summary>
    private void FindCurrentProcess()
    {
        int attempts = 0; //Track the loop for finding child processes

        Process? child = GetExperienceProcess();
        while(child == null && attempts < 10)
        {
            attempts++;
            MockConsole.WriteLine($"Checking for child process...", MockConsole.LogLevel.Debug);
            Task.Delay(3000).Wait();
            child = GetExperienceProcess();
            
            WrapperManager.PerformExperienceWindowConfirmations();
        }
        currentProcess = child;
        launchingExperience = false;

        if(child != null)
        {
            Logger.WriteLog($"Child process found: {child.Id}, {child.MainWindowTitle}, {child.ProcessName}", MockConsole.LogLevel.Normal);

            SteamScripts.popupDetect = false;
            ListenForClose();
            WindowManager.MaximizeProcess(child); //Maximise the process experience
            SessionController.PassStationMessage($"ApplicationUpdate,{experienceName}/{lastExperience.ID}/Steam");
            
            JObject response = new JObject();
            response.Add("response", "ExperienceLaunched");
            JObject responseData = new JObject();
            responseData.Add("experienceId", lastExperience.ID);
            response.Add("responseData", responseData);
            
            MessageController.SendResponse("NUC", "QA", response.ToString());
        } else
        {
            Logger.WriteLog("Game launch failure: " + lastExperience.Name, MockConsole.LogLevel.Normal);
            UiUpdater.ResetUiDisplay();
            SessionController.PassStationMessage($"MessageToAndroid,GameLaunchFailed:{lastExperience.Name}");
            JObject response = new JObject();
            response.Add("response", "ExperienceLaunchFailed");
            JObject responseData = new JObject();
            responseData.Add("experienceId", lastExperience.ID);
            responseData.Add("message", "Launch timed out, there may be a popup that needs confirmation");
            response.Add("responseData", responseData);
            
            MessageController.SendResponse("NUC", "QA", response.ToString());
        }
    }

    /// <summary>
    /// Scan the active processes that to find the launched application. The process is matched by the 
    /// MainWindowTitle that is represented by the Steam application name.
    /// </summary>
    /// <returns>The launched application process</returns>
    private Process? GetExperienceProcess()
    {
        if (installDir == null) return null;
        string? activeProcessId = null;
        string steamPath = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\" + installDir;
        string? processId = CommandLine.GetProcessIdFromDir(steamPath);
        if (processId != null)
        {
            activeProcessId = processId;
        }

        steamPath = "S:\\SteamLibrary\\steamapps\\common\\" + installDir;
        processId = CommandLine.GetProcessIdFromDir(steamPath);
        if (processId != null)
        {
            Logger.WriteLog("A proccess ID was found: " + processId, MockConsole.LogLevel.Normal);
            activeProcessId = processId;
        }

        if (activeProcessId == null) return null;
        
        Process? proc = ProcessManager.GetProcessById(Int32.Parse(activeProcessId));
        Logger.WriteLog($"Application found: {proc.MainWindowTitle}/{proc.Id}", MockConsole.LogLevel.Debug);

        UiUpdater.UpdateProcess(proc.MainWindowTitle);
        UiUpdater.UpdateStatus("Running...");
        return proc;
    }
    #endregion

    public void ListenForClose()
    {
        Task.Factory.StartNew(() =>
        {
            currentProcess?.WaitForExit();
            experienceName = null; //Reset for correct headset state
            SteamScripts.popupDetect = false;
            SessionController.PassStationMessage($"ApplicationClosed");
            UiUpdater.ResetUiDisplay();
        });
    }

    public bool? CheckCurrentProcess()
    {
        return currentProcess?.Responding;
    }
    
    public bool HasCurrentProcess()
    {
        return currentProcess != null;
    }

    public void StopCurrentProcess()
    {
        if (currentProcess != null)
        {
            try
            {
                currentProcess.Kill(true);
            }
            catch (InvalidOperationException e)
            {
                Logger.WriteLog($"StopCurrentProcess - ERROR: {e}", MockConsole.LogLevel.Error);
            }
        }
        CommandLine.StartProgram(SessionController.Steam, " +app_stop " + lastExperience.ID);
        SetLaunchingExperience(false);

        experienceName = null; //Reset for correct headset state
        WrapperMonitoringThread.StopMonitoring();
        ViveScripts.StopMonitoring();
        SteamScripts.popupDetect = false;
    }

    public void RestartCurrentExperience()
    {
        if(currentProcess != null)
        {
            StopCurrentProcess();
            Task.Delay(3000).Wait();
            WrapProcess(lastExperience);
        }
        SteamScripts.popupDetect = false;
    }

    public static void HandleOfflineSteam()
    {
        new Thread(() =>
        {
            OverlayManager.OverlayThreadManual("Initializing VR experiences ");
            Process? steamSignInWindow = null;
            Timer timer = new Timer(1000);
            int attempts = 0;

            void TimerElapsed(object? obj, ElapsedEventArgs args)
            {
                if (attempts > 10)
                {
                    timer.Stop();
                    OverlayManager.ManualStop();
                }
                List<Process> list = ProcessManager.GetProcessesByNames(new List<string> { "steam" });
                foreach (Process process in list)
                {
                    Logger.WriteLog($"Looking for steam sign in process: Process: {process.ProcessName} ID: {process.Id}, MainWindowTitle: {process.MainWindowTitle}", MockConsole.LogLevel.Debug);

                    if (process.MainWindowTitle.Equals("Steam Sign In"))
                    {
                        steamSignInWindow = process;
                        timer.Stop();
                        MockConsole.WriteLine($"Time for powershell command", MockConsole.LogLevel.Debug);
                        CommandLine.PowershellCommand(steamSignInWindow);
                    }
                }

                attempts++;
            }
            timer.Elapsed += TimerElapsed;
            timer.AutoReset = true;
            timer.Enabled = true;
        }).Start();
    }

    /// <summary>
    /// Launch SteamVR as a process, SteamVR's appID is (250820)
    /// </summary>
    public static void LaunchSteamVR()
    {
        if (!InternalDebugger.GetAutoStart()) return;
        
        currentProcess = new Process();
        currentProcess.StartInfo.FileName = SessionController.Steam;
        currentProcess.StartInfo.Arguments = LaunchParams + 250820;
        currentProcess.Start();
    }
}
