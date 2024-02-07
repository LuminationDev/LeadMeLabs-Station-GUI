using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using leadme_api;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Station.Components._commandLine;
using Station.Components._interfaces;
using Station.Components._models;
using Station.Components._monitoring;
using Station.Components._notification;
using Station.Components._organisers;
using Station.Components._profiles;
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
    private static readonly CustomWrapper CustomWrapper = new ();
    private static readonly SteamWrapper SteamWrapper = new ();
    private static readonly ViveWrapper ViveWrapper = new ();
    private static readonly ReviveWrapper ReviveWrapper = new ();

    //Used for multiple 'internal' applications, operations are separate from the other wrapper classes
    private static readonly InternalWrapper InternalWrapper = new();

    //Track the currently wrapper experience
    public static IWrapper? currentWrapper;
    private static bool alreadyCollecting;

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
        SessionController.SetupStationProfile(Helper.GetStationMode());
        ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage($"SoftwareState,Loading experiences"), TimeSpan.FromSeconds(2));
        Task.Factory.StartNew(CollectAllApplications);
    }

    /// <summary>
    /// Validate the binary_windows_path inside the Revive vr manifest. More validations can be added later.
    /// </summary>
    private void ValidateManifestFiles()
    {
        //TODO a quick fix is making the OculusClient.exe run as administrator
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
        currentWrapper?.StopCurrentProcess();
        SessionController.EndVrSession();
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
    /// <param name="jsonMessage">A modified, stringify JSON string with an updated name if it is available</param>
    private static string CheckExperienceName(string jsonMessage)
    {
        // Parse JSON string to JObject
        JObject details = JObject.Parse(jsonMessage);

        if (currentWrapper == null) return jsonMessage;
        string? experienceName = currentWrapper.GetCurrentExperienceName();
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
    private static void CollectAllApplications()
    {
        if (alreadyCollecting)
        {
            SessionController.PassStationMessage("Already collecting applications");
            return;
        }
        
        alreadyCollecting = true;
        
        //NEW METHOD
        CollectApplications<ExperienceDetails>(experiences => new JArray(experiences.Select(experience => experience.ToJObject())), "ApplicationJson");

        //BACKWARDS COMPATABILITY
        CollectApplications<string>( apps => string.Join("/", apps), "ApplicationList");

        alreadyCollecting = false;

        _ = RestartVrProcesses();
    }
    
    /// <summary>
    /// Collects applications of type T from various sources, converts them to the desired format, and sends them as JSON messages.
    /// </summary>
    /// <typeparam name="T">The type of applications to collect.</typeparam>
    /// <param name="convertFunc">A function to convert applications to the desired format.</param>
    /// <param name="messageType">The type of message (namespace) that is sent to the NUC.</param>
    private static void CollectApplications<T>(Func<List<T>, object> convertFunc, string messageType)
    {
        List<T> applications = new List<T>();

        List<T>? customApplications = CustomWrapper.CollectApplications<T>();
        if (customApplications != null)
        {
            applications.AddRange(customApplications);
        }

        List<T>? viveApplications = ViveWrapper.CollectApplications<T>();
        if (viveApplications != null)
        {
            applications.AddRange(viveApplications);
        }

        List<T>? reviveApplications = ReviveWrapper.CollectApplications<T>();
        if (reviveApplications != null)
        {
            applications.AddRange(reviveApplications);
        }

        // Check if there are steam details as the Station may be non-VR without a Steam account
        ContentProfile? contentProfile = Profile.CastToType<ContentProfile>(SessionController.StationProfile);
        if (Helper.GetStationMode().Equals(Helper.STATION_MODE_VR) ||
            (contentProfile != null && contentProfile.DoesProfileHaveAccount("Steam")))
        {
            List<T>? steamApplications = SteamWrapper.CollectApplications<T>();
            if (steamApplications != null)
            {
                applications.AddRange(steamApplications);
            }
        }

        // Convert applications to desired format
        object convertedApplications = convertFunc(applications);
        
        //Check for any missing thumbnails in the cache folder
        if (typeof(T) == typeof(string))
        {
            Task.Factory.StartNew(() => ThumbnailOrganiser.CheckCache<string>((string)convertedApplications));
        }
        else if (typeof(T) == typeof(ExperienceDetails))
        {
            Task.Factory.StartNew(() => ThumbnailOrganiser.CheckCache<ExperienceDetails>(convertedApplications.ToString()));
        }

        // Send the JSON message here as the PassStationMessage method splits the supplied message by ','
        if (messageType.Equals("ApplicationJson"))
        {
            MessageController.SendResponse("Android", "Station", "SetValue:installedJsonApplications:" + convertedApplications);
            return;
        }

        SessionController.PassStationMessage($"{messageType},{convertedApplications}");
    }

    /// <summary>
    /// Stop any and all processes associated with the VR headset type.
    /// </summary>
    public static void StopCommonProcesses()
    {
        List<string> combinedProcesses = new List<string>();
        combinedProcesses.AddRange(WrapperMonitoringThread.SteamProcesses);
        combinedProcesses.AddRange(WrapperMonitoringThread.SteamVrProcesses);
        combinedProcesses.AddRange(WrapperMonitoringThread.ViveProcesses);
        combinedProcesses.AddRange(WrapperMonitoringThread.ReviveProcesses);

        CommandLine.QueryProcesses(combinedProcesses, true);
    }

    /// <summary>
    /// Start or restart the VR session associated with the VR headset type
    /// </summary>
    public static async Task RestartVrProcesses()
    {
        bool isVr = SessionController.StationProfile?.GetVariant() == Variant.Vr;
        if (isVr)
        {
            // This must be checked before the VR processes are restarted
            RoomSetup.CompareRoomSetup();
        }
        
        StopCommonProcesses();
        if (SessionController.StationProfile == null)
        {
            SessionController.PassStationMessage("No profile type specified.");
            SessionController.PassStationMessage("Processing,false");
            return;
        }
        
        // Add a waiting time to make sure it has exited
        await SessionController.PutTaskDelay(2000);
        
        // Check that the processes have all stopped
        int attempts = 0;
        List<string> processesToQuery = SessionController.StationProfile.GetProcessesToQuery();
        while (CommandLine.QueryProcesses(processesToQuery))
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
        
        if (isVr)
        {
            // Safe cast and null checks
            VrProfile? vrProfile = Profile.CastToType<VrProfile>(SessionController.StationProfile);
            if (vrProfile?.VrHeadset == null) return;
            
            //Reset the VR device statuses
            vrProfile.VrHeadset.GetStatusManager().ResetStatuses();
        }
        
        await SessionController.PutTaskDelay(5000);
        SessionController.PassStationMessage("Processing,false");

        if (!InternalDebugger.GetAutoStart())
        {
            ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage($"SoftwareState,Debug Mode"), TimeSpan.FromSeconds(0));
            return;
        }
        
        SessionController.StationProfile.StartSession();
        
        // Check if there are steam details as the Station may be non-VR without a Steam account
        ContentProfile? contentProfile = Profile.CastToType<ContentProfile>(SessionController.StationProfile);
        if (contentProfile != null && contentProfile.DoesProfileHaveAccount("Steam"))
        {
            WaitForSteamProcess();
        }
        else if (isVr)
        {
            WaitForVrProcesses();
        }
        else
        {
            ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage($"SoftwareState,Ready to go"),
                TimeSpan.FromSeconds(1));
        }
    }
    
    /// <summary>
    /// Wait for Steam, bail out after 3 minutes. Send the outcome 
    /// to the tablet.
    /// </summary>
    private static void WaitForSteamProcess()
    {
        string error = "Error: Steam could not open";
        string message = Profile.WaitForSteamLogin() ? "Ready to go" : error;

        ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage($"SoftwareState,{message}"),
            TimeSpan.FromSeconds(1)); //Wait for steam/other accounts to login
        ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage("MessageToAndroid,SetValue:session:Restarted"), TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Wait for SteamVR and the External headset software to be open, bail out after 3 minutes. Send the outcome 
    /// to the tablet.
    /// </summary>
    private static void WaitForVrProcesses()
    {
        // Safe cast and null checks
        VrProfile? vrProfile = Profile.CastToType<VrProfile>(SessionController.StationProfile);
        if (vrProfile?.VrHeadset == null) return;
        
        int count = 0;
        do
        {
            Task.Delay(3000).Wait();
            count++;
        } while ((ProcessManager.GetProcessesByName(vrProfile.VrHeadset?.GetHeadsetManagementProcessName()).Length == 0) && count <= 60);

        string error = "";
        if (ProcessManager.GetProcessesByName(vrProfile.VrHeadset?.GetHeadsetManagementProcessName()).Length == 0)
        {
            error = "Error: Vive could not open";
        }

        string message = count <= 60 ? "Awaiting headset connection..." : error;

        //Only send the message if the headset is not yet connected
        if (vrProfile.VrHeadset?.GetStatusManager().SoftwareStatus != DeviceStatus.Connected ||
            vrProfile.VrHeadset.GetStatusManager().OpenVRStatus != DeviceStatus.Connected)
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
    /// <param name="isVr"></param>
    /// <param name="launchParameters">A stringify list of any parameters required at launch.</param>
    /// <param name="altPath"></param>
    public static void StoreApplication(string wrapperType, string id, string name, bool isVr = true, string? launchParameters = null, string? altPath = null)
    {
        if (!Helper.GetStationMode().Equals(Helper.STATION_MODE_VR) && isVr)
        {
            return;
        }
        
        var exeName = altPath != null ? Path.GetFileName(altPath) : name;

        //Add to the WrapperManager list and the ExperienceViewModel ObservableCollection
        Experience newExperience = new Experience(wrapperType, id, name, exeName, launchParameters, altPath, isVr);
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
                    CustomWrapper.CollectHeaderImage(appTokens[1]);
                    break;
                case "Revive":
                    ReviveWrapper.CollectHeaderImage(appTokens[1]);
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
                SessionController.RestartVrSession();
                break;
            case "End":
                SessionController.EndVrSession();
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
        if (currentWrapper == null)
        {
            SessionController.PassStationMessage("No process wrapper created.");
            return "No process wrapper created.";
        }

        //Stop any current processes (regular or 'visible' internal) before trying to launch a new one
        currentWrapper.StopCurrentProcess();
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
                    return currentWrapper.WrapProcess(experience);
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
                currentWrapper = CustomWrapper;
                break;
            case "Revive":
                currentWrapper = ReviveWrapper;
                break;
            case "Steam":
                currentWrapper = SteamWrapper;
                break;
            case "Vive":
                currentWrapper = ViveWrapper;
                break;
        }
    }

    /// <summary>
    /// Check if there is a process running and 
    /// </summary>
    private void CheckAProcess()
    {
        if (currentWrapper == null)
        {
            SessionController.PassStationMessage("No process wrapper present.");
            return;
        }

        Task.Factory.StartNew(() => currentWrapper.CheckCurrentProcess());
    }

    /// <summary>
    /// Send a message inside the currently active process.
    /// </summary>
    private void PassAMessageToProcess(string message)
    {
        if (currentWrapper == null)
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

        Task.Factory.StartNew(() => currentWrapper.PassMessageToProcess(message));
    }

    /// <summary>
    /// Restart an experience the CurrentWrapper is processing. 
    /// </summary>
    private void RestartAProcess()
    {
        if (currentWrapper == null)
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
        Task.Factory.StartNew(() => currentWrapper.RestartCurrentExperience());
    }

    /// <summary>
    /// Stop the currently active process, recycle the current wrapper and stop any monitoring that may be active.
    /// </summary>
    public static void StopAProcess()
    {
        //Stop looking for Vive headset regardless
        ViveScripts.StopMonitoring();

        if (currentWrapper == null)
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
        currentWrapper.StopCurrentProcess();
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
    /// <param name="_isVr">A string of if the experience is VR or not</param>
    public void HandleInternalExecutable(string action, string launchType, string path, string? parameters, string _isVr)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        string id = "NA";
        
        // Other relates to any processes that are fire and forget, like changing the wall paper. These do not need to be
        // tracked or wait for any other processes.
        if (!launchType.Equals("other"))
        {
            // Delay until experiences are collected so that if there are any details to be sent to the tablet it syncs correctly
            do
            {
                MockConsole.WriteLine(
                    $"InternalWrapper - WrapProcess: Waiting for the software to collect experiences.",
                    MockConsole.LogLevel.Normal);
                Task.Delay(2000).Wait();
            } while (ApplicationList.Count == 0 || alreadyCollecting);

            // Check if the application is known to the Software and replace the name with the correct one.
            Dictionary<string, Experience> applicationListCopy = ApplicationList;
            var matchingApplication = applicationListCopy
                .FirstOrDefault(kvp => kvp.Value.AltPath == path);

            if (matchingApplication.Key != null)
            {
                name = matchingApplication.Value.Name ?? name;
                id = matchingApplication.Value.Id ?? "NA";
            }
        }

        // Attempt to convert the string into a boolean or default to false
        if (!bool.TryParse(_isVr, out var isVr))
        {
            isVr = false;
        }

        // Create a temporary Experience struct to hold the information
        Experience experience = new("Internal", id, name, name, parameters, path, isVr);
        
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

