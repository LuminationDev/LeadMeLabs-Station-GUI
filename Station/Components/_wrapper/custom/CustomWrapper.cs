using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using leadme_api;
using LeadMeLabsLibrary;
using Newtonsoft.Json.Linq;
using Station.Components._commandLine;
using Station.Components._interfaces;
using Station.Components._managers;
using Station.Components._models;
using Station.Components._monitoring;
using Station.Components._network;
using Station.Components._notification;
using Station.Components._openvr;
using Station.Components._profiles;
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
        return CustomScripts.LoadAvailableExperiences<T>();
    }

    public void CollectHeaderImage(string experienceId)
    {
        Task.Factory.StartNew(() =>
        {
            WrapperManager.ApplicationList.TryGetValue(experienceId, out var experience);
            string? experienceName = experience.Name;
            string? altPath = experience.AltPath;

            if (StationCommandLine.StationLocation == null)
            {
                MockConsole.WriteLine($"Station working directory not found while searching for header file", Enums.LogLevel.Error);
                
                JObject message = new JObject
                {
                    { "action", "StationError" },
                    { "value", "Station working directory not found while searching for header file." }
                };
                SessionController.PassStationMessage(message);

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
                filePath = Path.GetFullPath(Path.Combine(StationCommandLine.StationLocation, @"..\..", $"leadme_apps\\{experienceName}\\header.jpg"));
            }

            if (!File.Exists(filePath))
            {
                MockConsole.WriteLine($"File not found:{filePath}", Enums.LogLevel.Error);
                
                JObject message = new JObject
                {
                    { "action", "StationError" },
                    { "value", $"File not found:{filePath}" }
                };
                SessionController.PassStationMessage(message);
                MessageController.SendResponse("Android", "Station", $"ThumbnailError:{experienceName}");
                return;
            }

            //Add the header image to the sending image queue through action transformation
            SocketFile socketImage = new("experienceThumbnail", experienceName, filePath);
            System.Action sendImage = new(() => socketImage.Send());

            //Queue the send function for invoking
            TaskQueue.Queue(false, sendImage);

            MockConsole.WriteLine($"Thumbnail for experience: {experienceName} now queued for transfer.", Enums.LogLevel.Info);
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
        if(StationCommandLine.StationLocation == null)
        {
            Logger.WriteLog("CustomWrapper - WrapProcess: Cannot find working directory", Enums.LogLevel.Error);
            return "Cannot find working directory";
        }

        if (vrProfile == null && experience.IsVr)
        {
            Logger.WriteLog("CustomWrapper - WrapProcess: No VR headset set.", Enums.LogLevel.Error);
            return "No VR headset set.";
        }

        if (experience.Name == null || experience.ID == null)
        {
            Logger.WriteLog("CustomWrapper.WrapProcess - Experience name cannot be null.", Enums.LogLevel.Error);
            return "CustomWrapper.WrapProcess - Experience name cannot be null.";
        }

        //Close any open custom processes before opening the next one
        if (currentProcess != null)
        {
            MockConsole.WriteLine($"Closing existing process: {lastExperience.Name}", Enums.LogLevel.Normal);
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
            if (!OpenVrManager.WaitForOpenVr().Result)
            {
                lastExperience.Name = null; //Reset for correct headset state
                return "Could not connect to OpenVR";
            }
        }

        MockConsole.WriteLine($"Launching process: {experience.Name} - {experience.ID}", Enums.LogLevel.Normal);
        Task.Factory.StartNew(() =>
        {
            if (experience.IsVr)
            {
                //Attempt to start the process using OpenVR
                _launchWillHaveFailedFromOpenVrTimeout = true;
                if (OpenVrManager.LaunchApplication(experience.ID))
                {
                    Logger.WriteLog($"CustomWrapper.WrapProcess: Launching {experience.Name} via OpenVR", Enums.LogLevel.Verbose);
                    return;
                }
                _launchWillHaveFailedFromOpenVrTimeout = false;

                //Stop any accessory processes before opening a new process
                vrProfile?.VrHeadset?.StopProcessesBeforeLaunch();
            }
            
            //Fall back to the alternate if it fails or is not a registered VR experience in the vrmanifest
            Logger.WriteLog($"CustomWrapper.WrapProcess - Using AlternateLaunchProcess", Enums.LogLevel.Normal);
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
        if (StationCommandLine.StationLocation == null)
        {
            Logger.WriteLog("CustomWrapper - WrapProcess: Cannot find working directory", Enums.LogLevel.Error);
            return;
        }

        string filePath;

        //The existence of an Alternate path means the experience has been imported through the launcher application
        if (experience.AltPath != null)
        {
            filePath = experience.AltPath;
        }
        else
        {
            filePath = Path.GetFullPath(Path.Combine(StationCommandLine.StationLocation, @"..\..", $"leadme_apps\\{experience.Name}\\{experience.Name}.exe"));
        }

        if (!File.Exists(filePath))
        {
            JObject message = new JObject
            {
                { "action", "StationError" },
                { "value", $"File not found:{filePath}" }
            };
            SessionController.PassStationMessage(message);
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
            MockConsole.WriteLine($"Checking for child process...", Enums.LogLevel.Debug);
            Task.Delay(3000).Wait();
            child = GetExperienceProcess();
        }
        currentProcess = child;
        launchingExperience = false;

        if (child != null && currentProcess != null && lastExperience.ExeName != null)
        {
            UiController.UpdateProcessMessages("processName", lastExperience.Name ?? "Unknown");
            UiController.UpdateProcessMessages("processStatus", "Running");
            WindowManager.MaximizeProcess(child); //Maximise the process experience
            
            JObject experienceInformation = new JObject
            {
                { "name", lastExperience.Name },
                { "appId", lastExperience.ID },
                { "wrapper", "Custom" }
            };
            
            JObject message = new JObject
            {
                { "action", "ApplicationUpdate" },
                { "info", experienceInformation }
            };
            SessionController.PassStationMessage(message);
            
            JObject response = new JObject();
            response.Add("response", "ExperienceLaunched");
            JObject responseData = new JObject();
            responseData.Add("experienceId", lastExperience.ID);
            response.Add("responseData", responseData);
            MessageController.SendResponse("NUC", "QA", response.ToString());

            MockConsole.WriteLine($"Application launching: {currentProcess?.MainWindowTitle}/{lastExperience.ID}", Enums.LogLevel.Normal);

            ListenForClose();
        }
        else
        {
            StopCurrentProcess();
            UiController.UpdateProcessMessages("reset");
            
            JObject message = new JObject
            {
                { "action", "MessageToAndroid" },
                { "value", $"GameLaunchFailed:{lastExperience.Name}" }
            };
            SessionController.PassStationMessage(message);
            
            JObject response = new JObject();
            response.Add("response", "ExperienceLaunchFailed");
            JObject responseData = new JObject();
            responseData.Add("experienceId", lastExperience.ID);
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
        string? altPathWithoutExe = Path.GetDirectoryName(lastExperience.AltPath);
        Logger.WriteLog($"Attempting to get id for " + altPathWithoutExe, Enums.LogLevel.Debug);
        if (string.IsNullOrEmpty(altPathWithoutExe))
        {
            return null;
        }
        
        string? id = StationCommandLine.GetProcessIdFromDir(altPathWithoutExe);
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }
        Process? proc = ProcessManager.GetProcessById(Int32.Parse(id));

        //Get the steam process name from the CommandLine function and compare here instead of removing any external child processes
        if (proc == null) return null;
        
        Logger.WriteLog($"Application found: {proc.MainWindowTitle}/{lastExperience.ID}", Enums.LogLevel.Debug);
        UiController.UpdateProcessMessages("processName", proc.MainWindowTitle);
        UiController.UpdateProcessMessages("processStatus", "Running");
            
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
            
            JObject message = new JObject
            {
                { "action", "ApplicationClosed" }
            };
            SessionController.PassStationMessage(message);
            UiController.UpdateProcessMessages("reset");
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
