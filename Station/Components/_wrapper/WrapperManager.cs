using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using leadme_api;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Station;
using Station.Components._commandLine;
using Station.Components._models;
using Station.Components._monitoring;
using Station.Components._notification;
using Station.Components._organisers;
using Station.Components._utils;
using Station.Components._utils._steamConfig;
using Station.Components._wrapper.custom;
using Station.Components._wrapper.@internal;
using Station.Components._wrapper.revive;
using Station.Components._wrapper.steam;
using Station.Components._wrapper.vive;
using Station.MVC.Controller;
using Station.MVC.ViewModel;

namespace Station.Components._wrapper;

public class WrapperManager {
    //Store each wrapper class
    private static readonly CustomWrapper customWrapper = new ();
    private static readonly SteamWrapper steamWrapper = new ();
    private static readonly ViveWrapper viveWrapper = new ();
    private static readonly ReviveWrapper reviveWrapper = new ();

    //Used for multiple 'internal' applications, operations are separate from the other wrapper classes
    private static readonly InternalWrapper InternalWrapper = new();

    //Track the currently wrapper experience
    public static IWrapper? CurrentWrapper;
    private static bool alreadyCollecting = false;

    //Store the list of applications (key = ID: [[0] = wrapper type, [1] = application name, [2] = launch params (nullable)])
    public static readonly Dictionary<string, Experience> ApplicationList = new();

    /// <summary>
    /// Open the pipe server for message to and from external applications (Steam, Custom, etc..) and setup
    /// the saved headset type.
    /// </summary>
    public void Startup()
    {
        ValidateManifestFiles();
        StartPipeServer();
        SessionController.SetupHeadsetType();
        ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage($"SoftwareState,Loading experiences"), TimeSpan.FromSeconds(2));
        Task.Factory.StartNew(CollectAllApplications);
    }

    /// <summary>
    /// Validate the binary_windows_path inside the Revive vr manifest. More validations can be added later.
    /// </summary>
    private void ValidateManifestFiles()
    {
        //TODO remove Oculus/CoreData/Manifests that have steam apps
        
        //Location is hardcoded for now
        ManifestReader.ModifyBinaryPath(ReviveScripts.ReviveManifest, @"C:/Program Files/Revive");
    }

    /// <summary>
    /// Close the Pipe server and stop any active process.
    /// </summary>
    public void ShutDownWrapper()
    {
        ParentPipeServer.Close();
        CurrentWrapper?.StopCurrentProcess();
        SessionController.EndVRSession();
    }

    /// <summary>
    /// Start a Pipe Server instance to handle any messages from the current application.
    /// </summary>
    private static void StartPipeServer()
    {
        MockConsole.WriteLine("Starting Pipe Server");
        ParentPipeServer.Run(LogHandler, ExternalActionHandler);
    }

    /// <summary>
    /// Close a currently open pipe server.
    /// </summary>
    public static void ClosePipeServer()
    {
        MockConsole.WriteLine("Closing Pipe Server");
        ParentPipeServer.Close();
    }

    /// <summary>
    /// Log any runtime messages that a user may need to analyse.
    /// </summary>
    /// <param name="message">A string of the log to be displayed</param>
    private static void LogHandler(string message)
    {
        Logger.WriteLog(message, MockConsole.LogLevel.Debug);
    }

    /// <summary>
    /// Handle an incoming action from the currently running process
    /// </summary>
    /// <param name="message">A multiple parameter message seperated by ',' detailing what action is to be taken</param>
    /// <returns>An async task associated with the action</returns>
    private static void ExternalActionHandler(string message)
    {
        Logger.WriteLog($"Pipe message: {message}", MockConsole.LogLevel.Normal);

        if (message.Contains("Command received")) return;

        //Token break down
        //['TYPE','MESSAGE']
        string[] tokens = message.Split(',', 2);

        //Determine the action to take
        switch (tokens[0])
        {
            case "details":
                MessageController.SendResponse("Android", "Station", $"SetValue:details:{CheckExperienceName(tokens[1])}");
                break;
            default:
                LogHandler($"Unknown actionspace: {tokens[0]}");
                break;
        }
    }

    /// <summary>
    /// Guarantee that the name of the details being received is the same as the experience that is currently
    /// launched.
    /// </summary>
    /// <param name="jsonMessage">A modified, stringified JSON string with an updated name if it is available</param>
    private static string CheckExperienceName(string jsonMessage)
    {
        // Parse JSON string to JObject
        JObject details = JObject.Parse(jsonMessage);

        if (CurrentWrapper == null) return jsonMessage;
        string? experienceName = CurrentWrapper.GetCurrentExperienceName();
        if (experienceName == null) return jsonMessage;

        // Check the value of the name against what is currently running
        details["name"] = experienceName;

        // Convert JObject back to JSON string
        string newJsonString = JsonConvert.SerializeObject(details);

        return newJsonString;
    }

    /// <summary>
    /// Cycle through the different wrappers and collect all the applications installed. Do
    /// not attempt to collect if already part way through.
    /// </summary>
    private static List<string> CollectAllApplications()
    {
        List<string> applications = new();
        if (alreadyCollecting)
        {
            SessionController.PassStationMessage("Already collecting applications");
            return applications;
        }

        alreadyCollecting = true;

        List<string>? customApplications = customWrapper.CollectApplications();
        if (customApplications != null)
        {
            applications.AddRange(customApplications);
        }

        List<string>? steamApplications = steamWrapper.CollectApplications();
        if (steamApplications != null)
        {
            applications.AddRange(steamApplications);
        }

        List<string>? viveApplications = viveWrapper.CollectApplications();
        if (viveApplications != null)
        {
            applications.AddRange(viveApplications);
        }
        
        List<string>? reviveApplications = reviveWrapper.CollectApplications();
        if (reviveApplications != null)
        {
            applications.AddRange(reviveApplications);
        }

        string response = string.Join('/', applications);

        //Check for any missing thumbnails in the cache folder
        Task.Factory.StartNew(() => ThumbnailOrganiser.CheckCache(response));
        SessionController.PassStationMessage($"ApplicationList,{response}");

        alreadyCollecting = false;

        _ = RestartVrProcesses();
        return applications;
    }

    /// <summary>
    /// Stop any and all processes associated with the VR headset type.
    /// </summary>
    public static void StopVrProcesses()
    {
        List<string> combinedProcesses = new List<string>();
        combinedProcesses.AddRange(WrapperMonitoringThread.SteamProcesses);
        combinedProcesses.AddRange(WrapperMonitoringThread.ViveProcesses);
        combinedProcesses.AddRange(WrapperMonitoringThread.ReviveProcesses);

        CommandLine.QueryVRProcesses(combinedProcesses, true);
    }

    /// <summary>
    /// Start or restart the VR session associated with the VR headset type
    /// </summary>
    public static async Task RestartVrProcesses()
    {
        if (SessionController.VrHeadset != null)
        {
            RoomSetup.CompareRoomSetup();

            StopVrProcesses();
            await SessionController.PutTaskDelay(2000);

            //have to add a waiting time to make sure it has exited
            int attempts = 0;

            if (SessionController.VrHeadset == null)
            {
                SessionController.PassStationMessage("No headset type specified.");
                SessionController.PassStationMessage("Processing,false");
                return;
            }

            List<string> processesToQuery = SessionController.VrHeadset.GetProcessesToQuery();
            while (CommandLine.QueryVRProcesses(processesToQuery))
            {
                await SessionController.PutTaskDelay(1000);
                if (attempts > 20)
                {
                    SessionController.PassStationMessage("MessageToAndroid,FailedRestart");
                    SessionController.PassStationMessage("Processing,false");
                    return;
                }
                attempts++;
            }

            //Reset the VR device statuses
            SessionController.VrHeadset.GetStatusManager().ResetStatuses();

            await SessionController.PutTaskDelay(5000);

            SessionController.PassStationMessage("Processing,false");

            if (!InternalDebugger.GetAutoStart())
            {
                ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage($"SoftwareState,Debug Mode"), TimeSpan.FromSeconds(0));
                return;
            }

            ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage($"SoftwareState,Starting VR processes"), TimeSpan.FromSeconds(0));
            SessionController.VrHeadset.StartVrSession();
            WaitForVrProcesses();
        }
    }

    /// <summary>
    /// Wait for SteamVR and the External headset software to be open, bail out after 3 minutes. Send the outcome 
    /// to the tablet.
    /// </summary>
    private static void WaitForVrProcesses()
    {
        int count = 0;
        do
        {
            Task.Delay(3000).Wait();
            count++;
        } while ((ProcessManager.GetProcessesByName(SessionController.VrHeadset?.GetHeadsetManagementProcessName()).Length == 0) && count <= 60);

        string error = "";
        if (ProcessManager.GetProcessesByName(SessionController.VrHeadset?.GetHeadsetManagementProcessName()).Length == 0)
        {
            error = "Error: Vive could not open";
        }

        string message = count <= 60 ? "Awaiting headset connection..." : error;

        //Only send the message if the headset is not yet connected
        if (SessionController.VrHeadset?.GetStatusManager().SoftwareStatus != DeviceStatus.Connected ||
            SessionController.VrHeadset?.GetStatusManager().OpenVRStatus != DeviceStatus.Connected)
        {
            ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage($"SoftwareState,{message}"),
                TimeSpan.FromSeconds(1));
            ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage("MessageToAndroid,SetValue:session:Restarted"), TimeSpan.FromSeconds(1));
        }
    }

    /// <summary>
    /// Load a collected application into the local storage list to determine start processes based
    /// on the ID's saved. This may also include a string that represents a list of arguments that will
    /// be added on process launch.
    /// </summary>
    /// <param name="wrapperType">A string of the type of application (i.e. Custom, Steam, etc..)</param>
    /// <param name="id">A string of the unique ID of an application</param>
    /// <param name="name">A string representing the Name of the application, this is what will appear on the LeadMe Tablet</param>
    /// <param name="launchParameters">A stringified list of any parameters required at launch.</param>
    /// <param name="altPath"></param>
    public static void StoreApplication(string wrapperType, string id, string name, string? launchParameters = null, string? altPath = null)
    {
        var exeName = altPath != null ? Path.GetFileName(altPath) : name;

        //Add to the WrapperManager list and the ExperienceViewModel ObservableCollection
        Experience newExperience = new Experience(wrapperType, id, name, exeName, launchParameters, altPath);
        ApplicationList.TryAdd(id, newExperience);
        if (wrapperType.Equals("Launcher")) return;
        newExperience.Name = RemoveQuotesAtStartAndEnd(newExperience.Name); //Remove the " from either end
        MainViewModel.ViewModelManager.ExperiencesViewModel.AddExperience(newExperience);
    }
    
    /// <summary>
    /// TrimStart removes leading quotes, TrimEnd removes trailing quotes.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    private static string RemoveQuotesAtStartAndEnd(string? input)
    {
        return input?.TrimStart('"').TrimEnd('"') ?? "Unknown";
    }

    /// <summary>
    /// Cycle through the different wrappers and collect all the header images that are requested. Each 
    /// experience requested will be queued up and sent one at a time.
    /// </summary>
    private void CollectHeaderImages(List<string> list)
    {
        foreach (string application in list)
        {
            //[0]-type, [1]-ID, [2]-name
            string[] appTokens = application.Split('|');

            switch(appTokens[0])
            {
                case "Custom":
                    customWrapper.CollectHeaderImage(appTokens[1]);
                    break;
                case "Revive":
                    reviveWrapper.CollectHeaderImage(appTokens[1]);
                    break;
                case "Steam":
                    LogHandler("CollectHeaderImages not implemented for type: Steam.");
                    break;
                case "Vive":
                    LogHandler("CollectHeaderImages not implemented for type: Vive.");
                    break;
                default:
                    LogHandler($"Unknown actionspace (CollectHeaderImages): {appTokens[0]}");
                    break;
            }
        }
    }

    /// <summary>
    /// Control the currently active session by restarting or stopping the entire process.
    /// </summary>
    /// <param name="action"></param>
    private void SessionControl(string action)
    {
        switch (action)
        {
            case "Restart":
                SessionController.RestartVRSession();
                break;
            case "End":
                SessionController.EndVRSession();
                break;
        }
    }

    /// <summary>
    /// Explored the loaded application list to find the type and name associated with the supplied processID.
    /// Create a wrapper and encapsulate the requested process for further use.
    /// </summary>
    public static async Task<string> StartAProcess(string appId)
    {
        //Get the type from the application dictionary
        //entry [application type, application name, application launch parameters]
        Experience experience = ApplicationList.GetValueOrDefault(appId);
        if (experience.IsNull())
        {
            SessionController.PassStationMessage($"No application found: {appId}");
            return $"No application found: {appId}";
        }

        if(experience.Type == null)
        {
            SessionController.PassStationMessage($"No wrapper associated with experience {appId}.");
            return $"No wrapper associated with experience {appId}.";
        }

        //Determine the wrapper to use
        LoadWrapper(experience.Type);
        if (CurrentWrapper == null)
        {
            SessionController.PassStationMessage("No process wrapper created.");
            return "No process wrapper created.";
        }

        //Stop any current processes (regular or 'visible' internal) before trying to launch a new one
        CurrentWrapper.StopCurrentProcess();
        InternalWrapper.StopCurrentProcess();

        //Update the experience UI
        MainViewModel.ViewModelManager.ExperiencesViewModel.UpdateExperience(experience.Id, "status", "Launching");
        
        //Update the home page UI
        UIController.UpdateProcessMessages("processName", "Launching");
        UIController.UpdateProcessMessages("processStatus", "Loading");

        //Determine what is need to launch the process(appID - Steam or name - Custom)
        //Pass in the launcher parameters if there are any
        string response = await Task.Factory.StartNew(() =>
        {
            switch (experience.Type)
            {
                case "Custom":
                case "Revive":
                case "Steam":
                    return CurrentWrapper.WrapProcess(experience);
                case "Vive":
                    throw new NotImplementedException();
                default:
                    return "Could not find that experience or experience type";
            }
        });
        return response;
    }

    /// <summary>
    /// Create the appropriate wrapper for the incoming process.
    /// </summary>
    /// <param name="type">A string representing the type of wrapper to create.</param>
    /// <returns></returns>
    public static void LoadWrapper(string type)
    {
        switch (type)
        {
            case "Custom":
                CurrentWrapper = customWrapper;
                break;
            case "Revive":
                CurrentWrapper = reviveWrapper;
                break;
            case "Steam":
                CurrentWrapper = steamWrapper;
                break;
            case "Vive":
                CurrentWrapper = viveWrapper;
                break;
        }
    }

    /// <summary>
    /// Check if there is a process running and 
    /// </summary>
    private void CheckAProcess()
    {
        if (CurrentWrapper == null)
        {
            SessionController.PassStationMessage("No process wrapper present.");
            return;
        }

        Task.Factory.StartNew(() => CurrentWrapper.CheckCurrentProcess());
    }

    /// <summary>
    /// Send a message inside the currently active process.
    /// </summary>
    private void PassAMessageToProcess(string message)
    {
        if (CurrentWrapper == null)
        {
            SessionController.PassStationMessage("No process wrapper present, checking internal.");

            if (InternalWrapper.GetCurrentExperienceName() != null)
            {
                Task.Factory.StartNew(() => InternalWrapper.PassMessageToProcess(message));
                return;
            }
            
            SessionController.PassStationMessage("No internal wrapper present.");
            return;
        }

        Task.Factory.StartNew(() => CurrentWrapper.PassMessageToProcess(message));
    }

    /// <summary>
    /// Restart an experience the CurrentWrapper is processing. 
    /// </summary>
    private void RestartAProcess()
    {
        if (CurrentWrapper == null)
        {
            SessionController.PassStationMessage("No process wrapper present.");
            
            if (InternalWrapper.GetCurrentExperienceName() != null)
            {
                Task.Factory.StartNew(() => InternalWrapper.RestartCurrentExperience());
                return;
            }
            
            SessionController.PassStationMessage("No internal wrapper present.");
            return;
        }
        Task.Factory.StartNew(() => CurrentWrapper.RestartCurrentExperience());
    }

    /// <summary>
    /// Stop the currently active process, recycle the current wrapper and stop any monitoring that may be active.
    /// </summary>
    public static void StopAProcess()
    {
        //Stop looking for Vive headset regardless
        ViveScripts.StopMonitoring();

        if (CurrentWrapper == null)
        {
            SessionController.PassStationMessage("No process wrapper present.");
            
            if (InternalWrapper.GetCurrentExperienceName() != null)
            {
                Task.Factory.StartNew(() => InternalWrapper.StopCurrentProcess());
                return;
            }
            
            SessionController.PassStationMessage("No internal wrapper present.");
            return;
        }

        UIController.UpdateProcessMessages("reset");
        CurrentWrapper.StopCurrentProcess();
        WrapperMonitoringThread.StopMonitoring();
    }

    /// <summary>
    /// Handle an incoming action, this may be from the LeadMe Station application
    /// </summary>
    /// <param name="type">A string representing the type of action to take</param>
    /// <param name="message">A multiple parameter message seperated by ',' detailing what action is to be taken</param>
    /// <returns>An async task associated with the action</returns>
    public void ActionHandler(string type, string message = "")
    {
        MockConsole.WriteLine($"Wrapper action type: {type}, message: {message}", MockConsole.LogLevel.Debug);

        //Determine the action to take
        switch (type)
        {
            case "CollectApplications":
                Task.Factory.StartNew(CollectAllApplications);
                break;
            case "CollectHeaderImages":
                Task.Factory.StartNew(() => CollectHeaderImages(message.Split('/').ToList()));
                break;
            case "Session":
                SessionControl(message);
                break;
            case "Start":
                _ = StartAProcess(message);
                break;
            case "Check":
                CheckAProcess();
                break;
            case "Message":
                PassAMessageToProcess(message);
                break;
            case "Restart":
                RestartAProcess();
                break;
            case "Stop":
                StopAProcess();
                break;
            default:
                LogHandler($"Unknown actionspace (ActionHandler): {type}");
                break;
        }
    }
    
    /// <summary>
    /// Manage the internal wrapper, coordinate the running, stopping or other functions that
    /// relate to executables that are not experiences.
    /// </summary>
    /// <param name="action">A string describing the action to take (Start or Stop)</param>
    /// <param name="launchType">A string of if the experience is to show on the tablet (visible) or not (hidden)</param>
    /// <param name="path">A string of the absolute path of the executable to run</param>
    /// <param name="parameters">A string to be passed as the process arguments</param>
    public void HandleInternalExecutable(string action, string launchType, string path, string? parameters)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        string id = "NA";
        
        //Delay until experiences are collected so that if there are any details to be sent to the tablet it syncs correctly
        do
        {
            MockConsole.WriteLine($"InternalWrapper - WrapProcess: Waiting for the software to collect experiences.", MockConsole.LogLevel.Normal);
            Task.Delay(2000).Wait();
        } while (ApplicationList.Count == 0 || alreadyCollecting);
        
        //Check if the application is known to the Software and replace the name with the correct one.
        Dictionary<string, Experience> applicationListCopy = ApplicationList;
        var matchingApplication = applicationListCopy
            .FirstOrDefault(kvp => kvp.Value.AltPath == path);

        if (matchingApplication.Key != null)
        {
            name = matchingApplication.Value.Name ?? name;
            id = matchingApplication.Value.Id ?? "NA";
        }

        //Create a temporary Experience struct to hold the information
        Experience experience = new("Internal", id, name, name, parameters, path);

        switch(action)
        {
            case "Start":
                InternalWrapper.WrapProcess(launchType, experience);
                break;
            case "Stop":
                InternalWrapper.StopAProcess(experience);
                break;
            default:
                LogHandler($"Unknown actionspace (HandleInternalExecutable): {action}");
                break;
        }
    }
}

