using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using leadme_api;
using LeadMeLabsLibrary;
using Newtonsoft.Json.Linq;
using Station._commandLine;
using Station._interfaces;
using Station._manager;
using Station._models;
using Station._monitoring;
using Station._network;
using Station._openvr;
using Station._profiles;
using Station._utils;

namespace Station._wrapper.embedded;

internal class EmbeddedWrapper : IWrapper
{
    public const string WrapperType = "Embedded";
    private static Process? currentProcess;
    public static Experience lastExperience;
    private bool _launchWillHaveFailedFromOpenVrTimeout = true;

    /// <summary>
    /// Track if an experience is being launched.
    /// </summary>
    private static bool launchingExperience;

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
        return lastExperience.Name;
    }
    
    public List<T>? CollectApplications<T>()
    {
        return EmbeddedScripts.LoadAvailableExperiences<T>();
    }

    public void CollectHeaderImage(string experienceId)
    {
        Task.Factory.StartNew(() =>
        {
            WrapperManager.ApplicationList.TryGetValue(experienceId, out var experience);
            string? experienceName = experience.Name;

            if (CommandLine.stationLocation == null)
            {
                MockConsole.WriteLine($"Station working directory not found while searching for header file", MockConsole.LogLevel.Error);
                SessionController.PassStationMessage($"StationError,Station working directory not found while searching for header file.");

                Manager.SendResponse("Android", "Station", $"ThumbnailError:{experienceName}");
                return;
            }

            if (experienceName == null)
            {
                MockConsole.WriteLine($"No experience name found for: {experienceId}", MockConsole.LogLevel.Error);
                return;
            }
            
            if (experience.AltPath == null)
            {
                MockConsole.WriteLine($"No executable path found for experience: {experienceName}", MockConsole.LogLevel.Error);
                return;
            }
            
            //Determine where the header image is location, there may be an alternate path supplied
            string filePath = experience.HeaderPath ?? Path.GetFullPath(Path.Combine(experience.AltPath, "..", "header.jpg"));
            if (!File.Exists(filePath))
            {
                MockConsole.WriteLine($"File not found:{filePath}", MockConsole.LogLevel.Error);
                SessionController.PassStationMessage($"StationError,File not found:{filePath}");
                Manager.SendResponse("Android", "Station", $"ThumbnailError:{experienceName}");
                return;
            }

            //Add the header image to the sending image queue through action transformation
            SocketFile socketImage = new("image", experienceName, filePath);

            //Queue the send function for invoking
            TaskQueue.Queue(false, SendImage);

            MockConsole.WriteLine($"Thumbnail for experience: {experienceName} now queued for transfer.", MockConsole.LogLevel.Error);
            return;

            void SendImage() => socketImage.Send();
        });
    }

    public void PassMessageToProcess(string message)
    {
        PipeClient pipeClient = new(MockConsole.WriteLine, 5);

        Task.Factory.StartNew(() =>
        {
            pipeClient.Send(message);
        });
    }

    public void SetCurrentProcess(Process process)
    {
        if (currentProcess != null)
        {
            currentProcess.Kill(true);
        }
        _launchWillHaveFailedFromOpenVrTimeout = false;
        currentProcess = process;
        ListenForClose();
    }

    public string WrapProcess(Experience experience)
    {
        // Safe cast for potential vr profile
        VrProfile? vrProfile = Profile.CastToType<VrProfile>(SessionController.StationProfile);
        
        _launchWillHaveFailedFromOpenVrTimeout = false;
        if(CommandLine.stationLocation == null)
        {
            SessionController.PassStationMessage("Cannot find working directory");
            return "Cannot find working directory";
        }

        if (vrProfile == null && experience.IsVr)
        {
            SessionController.PassStationMessage("No VR headset set.");
            return "No VR headset set.";
        }

        if (experience.Name == null || experience.ID == null)
        {
            SessionController.PassStationMessage("EmbeddedWrapper.WrapProcess - Experience name cannot be null.");
            return "EmbeddedWrapper.WrapProcess - Experience name cannot be null.";
        }

        //Close any open processes before opening the next one
        if (currentProcess != null)
        {
            MockConsole.WriteLine($"Closing existing process: {lastExperience.Name}", MockConsole.LogLevel.Normal);
            currentProcess.Kill(true);
        }

        lastExperience = experience;

        //Begin monitoring the different processes
        WrapperMonitoringThread.InitializeMonitoring(WrapperType, experience.IsVr);

        if (InternalDebugger.GetHeadsetRequired() && experience.IsVr)
        {
            // Check the current Station profile
            if (vrProfile?.VrHeadset == null)
            {
                return "Station is not a VR profile or has no VR Headset set and is attempting to launch a VR experience";
            }
            
            //Wait for the Headset's connection method to respond
            if (!vrProfile.WaitForConnection(WrapperType))
            {
                lastExperience.Name = null; //Reset for correct headset state
                return "Could not get headset connection";
            }

            //If headset management software is open (with headset connected) and OpenVrSystem cannot initialise then restart SteamVR
            if (!OpenVRManager.WaitForOpenVR().Result)
            {
                lastExperience.Name = null; //Reset for correct headset state
                return "Could not connect to OpenVR";
            }
        }

        MockConsole.WriteLine($"Launching process: {experience.Name} - {experience.ID}", MockConsole.LogLevel.Normal);
        Task.Factory.StartNew(() =>
        {
            if (experience.IsVr)
            {
                //Attempt to start the process using OpenVR
                _launchWillHaveFailedFromOpenVrTimeout = true;
                if (OpenVRManager.LaunchApplication(experience.ID))
                {
                    Logger.WriteLog($"EmbeddedWrapper.WrapProcess: Launching {experience.Name} via OpenVR", MockConsole.LogLevel.Verbose);
                    return;
                }
                _launchWillHaveFailedFromOpenVrTimeout = false;

                //Stop any accessory processes before opening a new process
                vrProfile?.VrHeadset?.StopProcessesBeforeLaunch();
            }
            
            //Fall back to the alternate if it fails or is not a registered VR experience in the vrmanifest
            Logger.WriteLog($"EmbeddedWrapper.WrapProcess - Using AlternateLaunchProcess", MockConsole.LogLevel.Normal);
            AlternateLaunchProcess(experience);
        });
        return "launching";
    }

    #region Alternate Launch Process
    /// <summary>
    /// Launches an alternate process for the given experience if the primary launch process fails.
    /// If the specified station location is unavailable, an error message is sent through the session controller.
    /// If an alternate path for the experience is available, launches the process from that path.
    /// Otherwise, constructs the path using the station location and the experience's name, then launches the process.
    /// If the process executable is not found, an error message is sent through the session controller.
    /// Sets the lastExperience to the given experience and starts the process with any specified parameters.
    /// Finally, searches for the newly launched process and tracks it.
    /// </summary>
    /// <param name="experience">The experience object representing the application to launch.</param>
    private void AlternateLaunchProcess(Experience experience)
    {
        if (CommandLine.stationLocation == null)
        {
            SessionController.PassStationMessage("Cannot find working directory");
            return;
        }
        
        string? filePath = experience.AltPath;
        if (!File.Exists(filePath))
        {
            SessionController.PassStationMessage($"StationError,File not found:{filePath}");
            return;
        }

        currentProcess = new Process();
        currentProcess.StartInfo.FileName = filePath;

        if (experience.Parameters != null)
        {
            currentProcess.StartInfo.Arguments = experience.Parameters;
        }

        currentProcess.Start();

        FindCurrentProcess();
    }

    /// <summary>
    /// Find the active process that has been launched. This may have to loop if the main experience is
    /// opening accompanying software such as SteamVR. 
    /// </summary>
    private void FindCurrentProcess()
    {
        int attempts = 0; //Track the loop for finding child processes

        Process? child = GetExperienceProcess();
        while (child == null && attempts < 20)
        {
            attempts++;
            MockConsole.WriteLine($"Checking for child process...", MockConsole.LogLevel.Debug);
            Task.Delay(3000).Wait();
            child = GetExperienceProcess();
        }
        currentProcess = child;
        launchingExperience = false;

        if (child != null && currentProcess != null && lastExperience.ExeName != null)
        {
            UIUpdater.UpdateProcess(lastExperience.Name ?? "Unknown");
            UIUpdater.UpdateStatus("Running...");
            WindowManager.MaximizeProcess(child); //Maximise the process experience
            SessionController.PassStationMessage($"ApplicationUpdate,{lastExperience.Name}/{lastExperience.ID}/Embedded");
            
            JObject response = new JObject { { "response", "ExperienceLaunched" } };
            JObject responseData = new JObject { { "experienceId", lastExperience.ID } };
            response.Add("responseData", responseData);
            Manager.SendResponse("NUC", "QA", response.ToString());

            MockConsole.WriteLine($"Application launching: {currentProcess.MainWindowTitle}/{lastExperience.ID}", MockConsole.LogLevel.Normal);

            ListenForClose();
        }
        else
        {
            StopCurrentProcess();
            UIUpdater.ResetUIDisplay();
            SessionController.PassStationMessage($"MessageToAndroid,GameLaunchFailed:{lastExperience.Name}");
            
            JObject response = new JObject();
            response.Add("response", "ExperienceLaunchFailed");
            JObject responseData = new JObject();
            responseData.Add("experienceId", lastExperience.ID);
            response.Add("responseData", responseData);
            
            Manager.SendResponse("NUC", "QA", response.ToString());
        }
    }

    /// <summary>
    /// Scan the active processes that to find the launched application. The process is matched by the 
    /// MainWindowTitle that is represented by the Embedded application name.
    /// </summary>
    /// <returns>The launched application process</returns>
    private Process? GetExperienceProcess()
    {
        string? altPathWithoutExe = Path.GetDirectoryName(lastExperience.AltPath);
        Logger.WriteLog($"Attempting to get id for " + altPathWithoutExe, MockConsole.LogLevel.Debug);
        if (string.IsNullOrEmpty(altPathWithoutExe))
        {
            return null;
        }
        
        string? id = CommandLine.GetProcessIdFromDir(altPathWithoutExe);
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }
        Process? proc = ProcessManager.GetProcessById(Int32.Parse(id));

        //Get the steam process name from the CommandLine function and compare here instead of removing any external child processes
        if (proc == null) return null;
        
        Logger.WriteLog($"Application found: {proc.MainWindowTitle}/{lastExperience.ID}", MockConsole.LogLevel.Debug);
        UIUpdater.UpdateProcess(proc.MainWindowTitle);
        UIUpdater.UpdateStatus("Running...");
            
        return proc;

    }
    #endregion

    /// <summary>
    /// Being a new thread with the purpose of detecting if the current process has been exited.
    /// </summary>
    public void ListenForClose()
    {
        Task.Factory.StartNew(() =>
        {
            currentProcess?.WaitForExit();
            lastExperience.Name = null; //Reset for correct headset state
            SessionController.PassStationMessage($"ApplicationClosed");
            UIUpdater.ResetUIDisplay();
        });
    }

    /// <summary>
    /// Check if a process is currently running.
    /// </summary>
    public bool? CheckCurrentProcess()
    {
        return currentProcess?.Responding;
    }

    /// <summary>
    /// Kill the currently running process, releasing all resources associated with it.
    /// </summary>
    public void StopCurrentProcess()
    {
        if (currentProcess != null)
        {
            currentProcess.Kill(true);
            WrapperMonitoringThread.StopMonitoring();
        }
        lastExperience.Name = null; //Reset for correct headset state
    }

    public void RestartCurrentExperience()
    {
        //Create a temp as the StopCurrenProcess alters the current experience
        Experience temp = lastExperience;
        if (currentProcess != null && !lastExperience.IsNull())
        {
            StopCurrentProcess();
            Task.Delay(3000).Wait();
            WrapProcess(temp);
        }
    }
    
    public bool HasCurrentProcess()
    {
        return currentProcess != null;
    }
}
