using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using leadme_api;
using LeadMeLabsLibrary;
using Newtonsoft.Json.Linq;
using Station.Components._commandLine;
using Station.Components._models;
using Station.Components._monitoring;
using Station.Components._network;
using Station.Components._notification;
using Station.Components._openvr;
using Station.Components._utils;
using Station.MVC.Controller;

namespace Station.Components._wrapper.custom;

internal class CustomWrapper : IWrapper
{
    public const string WrapperType = "Custom";
    private static Process? currentProcess;
    public static Experience lastExperience;
    private bool _launchWillHaveFailedFromOpenVrTimeout = true;

    /// <summary>
    /// Track if an experience is being launched.
    /// </summary>
    private static bool launchingExperience = false;

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

    public List<string>? CollectApplications()
    {
        return CustomScripts.LoadAvailableGames();
    }

    public void CollectHeaderImage(string experienceID)
    {
        Task.Factory.StartNew(() =>
        {
            WrapperManager.ApplicationList.TryGetValue(experienceID, out var experience);
            string? experienceName = experience.Name;
            string? altPath = experience.AltPath;

            if (CommandLine.StationLocation == null)
            {
                MockConsole.WriteLine($"Station working directory not found while searching for header file", MockConsole.LogLevel.Error);
                SessionController.PassStationMessage($"StationError,Station working directory not found while searching for header file.");

                MessageController.SendResponse("Android", "Station", $"ThumbnailError:{experienceName}");
                return;
            }

            //Determine if it was imported or downloaded and find the header file
            string filePath;
            if (altPath != null)
            {
                string parentFolder = CustomScripts.GetParentDirPath(altPath);
                filePath = parentFolder + @"\header.jpg";
            } else
            {
                filePath = Path.GetFullPath(Path.Combine(CommandLine.StationLocation, @"..\..", $"leadme_apps\\{experienceName}\\header.jpg"));
            }

            if (!File.Exists(filePath))
            {
                MockConsole.WriteLine($"File not found:{filePath}", MockConsole.LogLevel.Error);
                SessionController.PassStationMessage($"StationError,File not found:{filePath}");
                MessageController.SendResponse("Android", "Station", $"ThumbnailError:{experienceName}");
                return;
            }

            //Add the header image to the sending image queue through action transformation
            SocketFile socketImage = new("image", experienceName, filePath);
            System.Action sendImage = new(() => socketImage.Send());

            //Queue the send function for invoking
            TaskQueue.Queue(false, sendImage);

            MockConsole.WriteLine($"Thumbnail for experience: {experienceName} now queued for transfer.", MockConsole.LogLevel.Error);
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
        _launchWillHaveFailedFromOpenVrTimeout = false;
        if(CommandLine.StationLocation == null)
        {
            SessionController.PassStationMessage("Cannot find working directory");
            return "Cannot find working directory";
        }

        if (SessionController.VrHeadset == null)
        {
            SessionController.PassStationMessage("No VR headset set.");
            return "No VR headset set.";
        }

        if (experience.Name == null || experience.Id == null)
        {
            SessionController.PassStationMessage("CustomWrapper.WrapProcess - Experience name cannot be null.");
            return "CustomWrapper.WrapProcess - Experience name cannot be null.";
        }

        //Close any open custom processes before opening the next one
        if (currentProcess != null)
        {
            MockConsole.WriteLine($"Closing existing process: {lastExperience.Name}", MockConsole.LogLevel.Normal);
            currentProcess.Kill(true);
        }

        lastExperience = experience;

        //Begin monitoring the different processes
        WrapperMonitoringThread.InitializeMonitoring(WrapperType);

        //TODO uncomment this
        // //Wait for the Headset's connection method to respond
        // if (!SessionController.VrHeadset.WaitForConnection(WrapperType)) return "Could not get headset connection";
        //
        // //If headset management software is open (with headset connected) and OpenVrSystem cannot initialise then restart SteamVR
        // if (!OpenVRManager.WaitForOpenVR().Result) return "Could not connect to OpenVR";

        MockConsole.WriteLine($"Launching process: {experience.Name} - {experience.Id}", MockConsole.LogLevel.Normal);
        Task.Factory.StartNew(() =>
        {
            //Attempt to start the process using OpenVR
            _launchWillHaveFailedFromOpenVrTimeout = true;
            if (OpenVRManager.LaunchApplication(experience.Id))
            {
                Logger.WriteLog($"CustomWrapper.WrapProcess: Launching {experience.Name} via OpenVR", MockConsole.LogLevel.Verbose);
                return;
            }
            _launchWillHaveFailedFromOpenVrTimeout = false;

            //Stop any accessory processes before opening a new process
            SessionController.VrHeadset.StopProcessesBeforeLaunch();
            
            //Fall back to the alternate if it fails or is not a registered VR experience in the vrmanifest
            Logger.WriteLog($"CustomWrapper.WrapProcess - Using AlternateLaunchProcess", MockConsole.LogLevel.Normal);
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
        if (CommandLine.StationLocation == null)
        {
            SessionController.PassStationMessage("Cannot find working directory");
            return;
        }

        string filePath;

        //The existance of an Alternate path means the experience has been imported through the launcher application
        if (experience.AltPath != null)
        {
            filePath = experience.AltPath;
        }
        else
        {
            filePath = Path.GetFullPath(Path.Combine(CommandLine.StationLocation, @"..\..", $"leadme_apps\\{experience.Name}\\{experience.Name}.exe"));
        }

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
            UIController.UpdateProcessMessages("processName", lastExperience.Name ?? "Unknown");
            UIController.UpdateProcessMessages("processStatus", "Running");
            WindowManager.MaximizeProcess(child); //Maximise the process experience
            SessionController.PassStationMessage($"ApplicationUpdate,{lastExperience.Name}/{lastExperience.Id}/Custom");
            
            JObject response = new JObject();
            response.Add("response", "ExperienceLaunched");
            JObject responseData = new JObject();
            responseData.Add("experienceId", lastExperience.Id);
            response.Add("responseData", responseData);
            MessageController.SendResponse("NUC", "QA", response.ToString());

            MockConsole.WriteLine($"Application launching: {currentProcess?.MainWindowTitle}/{lastExperience.Id}", MockConsole.LogLevel.Normal);

            ListenForClose();
        }
        else
        {
            StopCurrentProcess();
            UIController.UpdateProcessMessages("reset");
            SessionController.PassStationMessage($"MessageToAndroid,GameLaunchFailed:{lastExperience.Name}");
            
            JObject response = new JObject();
            response.Add("response", "ExperienceLaunchFailed");
            JObject responseData = new JObject();
            responseData.Add("experienceId", lastExperience.Id);
            response.Add("responseData", responseData);
            
            MessageController.SendResponse("NUC", "QA", response.ToString());
        }
    }

    /// <summary>
    /// Scan the active processes that to find the launched application. The process is matched by the 
    /// MainWindowTitle that is represented by the Custom application name.
    /// </summary>
    /// <returns>The launched application process</returns>
    private Process? GetExperienceProcess()
    {
        string altPathWithoutExe = Path.GetDirectoryName(lastExperience.AltPath);
        Logger.WriteLog($"Attempting to get id for " + altPathWithoutExe, MockConsole.LogLevel.Debug);
        string id = CommandLine.GetProcessIdFromDir(altPathWithoutExe);
        if (id == null || id == "")
        {
            return null;
        }
        Process? proc = ProcessManager.GetProcessById(Int32.Parse(id));

        //Get the steam process name from the CommandLine function and compare here instead of removing any external child processes
        if (proc != null)
        {
            Logger.WriteLog($"Application found: {proc.MainWindowTitle}/{lastExperience.Id}", MockConsole.LogLevel.Debug);
            
            // Update the home page UI
            UIController.UpdateProcessMessages("processName", proc.MainWindowTitle);
            UIController.UpdateProcessMessages("processStatus", "Running");
            
            return proc;
        }

        return null;
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
            Trace.WriteLine("The current process has just exited.");
            SessionController.PassStationMessage($"ApplicationClosed");
            UIController.UpdateProcessMessages("reset");
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
