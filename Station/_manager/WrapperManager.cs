using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using leadme_api;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Station
{
    public class WrapperManager
    {
        //Store each wrapper class
        private static readonly CustomWrapper customWrapper = new CustomWrapper();
        private static readonly SteamWrapper steamWrapper = new SteamWrapper();
        private static readonly ViveWrapper viveWrapper = new ViveWrapper();

        //Used for multiple 'internal' applications, operations are separate from the other wrapper classes
        private readonly InternalWrapper internalWrapper = new();

        //Track the currently wrapper experience
        public static Wrapper? CurrentWrapper;
        private static bool alreadyCollecting = false;

        //Store the list of applications (key = ID: [[0] = wrapper type, [1] = application name, [2] = launch params (nullable)])
        public readonly static Dictionary<string, Experience> applicationList = new();

        /// <summary>
        /// Open the pipe server for message to and from external applications (Steam, Custom, etc..) and setup
        /// the saved headset type.
        /// </summary>
        public void Startup()
        {
            StartPipeServer();
            SessionController.SetupHeadsetType();
            ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage($"SoftwareState,Loading experiences"), TimeSpan.FromSeconds(2));
            Task.Factory.StartNew(() => CollectAllApplications());
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
        public static void StartPipeServer()
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
        public static void LogHandler(string message)
        {
            Logger.WriteLog(message, MockConsole.LogLevel.Debug);
        }

        /// <summary>
        /// Handle an incoming action from the currently running process
        /// </summary>
        /// <param name="message">A multiple parameter message seperated by ',' detailing what action is to be taken</param>
        /// <returns>An async task asscoiated with the action</returns>
        private static void ExternalActionHandler(string message)
        {
            Logger.WriteLog($"Pipe message: {message}", MockConsole.LogLevel.Normal);

            if (message.Contains("Command recieved")) return;

            //Token break down
            //['TYPE','MESSAGE']
            string[] tokens = message.Split(',', 2);

            //Determine the action to take
            switch (tokens[0])
            {
                case "details":
                    Manager.SendResponse("Android", "Station", $"SetValue:details:{CheckExperienceName(tokens[1])}");
                    break;
                default:
                    LogHandler($"Unknown actionspace: {tokens[0]}");
                    break;
            }
        }

        /// <summary>
        /// Guarantee that the name of the details being recieved is the same as the experience that is currently
        /// launched.
        /// </summary>
        /// <param name="JSONMessage">A modified, stringified JSON string with an updated name if it is available</param>
        private static string CheckExperienceName(string JSONMessage)
        {
            // Parse JSON string to JObject
            JObject details = JObject.Parse(JSONMessage);

            if (CurrentWrapper == null) return JSONMessage;
            string? experienceName = CurrentWrapper.GetCurrentExperienceName();
            if (experienceName == null) return JSONMessage;

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
        public static List<string> CollectAllApplications()
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

            string response = string.Join('/', applications);

            SessionController.PassStationMessage($"ApplicationList,{response}");

            alreadyCollecting = false;

            _ = RestartVRProcesses();
            return applications;
        }

        /// <summary>
        /// Start or restart the VR session associated with the VR headset type
        /// </summary>
        public static async Task RestartVRProcesses()
        {
            if (SessionController.vrHeadset != null)
            {
                RoomSetup.CompareRoomSetup();

                List<string> combinedProcesses = new List<string>();
                combinedProcesses.AddRange(WrapperMonitoringThread.steamProcesses);
                combinedProcesses.AddRange(WrapperMonitoringThread.viveProcesses);

                CommandLine.QueryVRProcesses(combinedProcesses, true);
                await SessionController.PutTaskDelay(2000);

                //have to add a waiting time to make sure it has exited
                int attempts = 0;

                if (SessionController.vrHeadset == null)
                {
                    SessionController.PassStationMessage("No headset type specified.");
                    SessionController.PassStationMessage("Processing,false");
                    return;
                }

                List<string> processesToQuery = SessionController.vrHeadset.GetProcessesToQuery();
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
                SessionController.vrHeadset.GetStatusManager().ResetStatuses();

                await SessionController.PutTaskDelay(5000);

                SessionController.PassStationMessage("MessageToAndroid,SetValue:session:Restarted");
                SessionController.PassStationMessage("Processing,false");

                ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage($"SoftwareState,Starting VR processes"), TimeSpan.FromSeconds(0));
                SessionController.vrHeadset.StartVrSession();
                WaitForVRProcesses();
            }
        }

        /// <summary>
        /// Wait for SteamVR and the External headset software to be open, bail out after 3 minutes. Send the outcome 
        /// to the tablet.
        /// </summary>
        private static void WaitForVRProcesses()
        {
            int count = 0;
            do
            {
                Task.Delay(3000).Wait();
                count++;
            } while ((Process.GetProcessesByName(SessionController.vrHeadset?.GetHeadsetManagementProcessName()).Length == 0) && count <= 60);

            string error = "";
            if (Process.GetProcessesByName(SessionController.vrHeadset?.GetHeadsetManagementProcessName()).Length == 0)
            {
                error = "Error: Vive could not open";
            }

            string message = count <= 60 ? "Awaiting headset connection..." : error;

            //Only send the message if the headset is not yet connected
            if (SessionController.vrHeadset?.GetStatusManager().SoftwareStatus != DeviceStatus.Connected ||
                SessionController.vrHeadset?.GetStatusManager().OpenVRStatus != DeviceStatus.Connected)
            {
                ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage($"SoftwareState,{message}"),
                    TimeSpan.FromSeconds(1));
            }
        }

        /// <summary>
        /// Load a collected application into the local storage list to determine start processes based
        /// on the ID's saved. This may also include a string that represents a list of arguments that will
        /// be added on process launch.
        /// </summary>
        /// <param name="wrapperType">A string of the type of application (i.e. Custom, Steam, etc..)</param>
        /// <param name="id">A string of the unique ID of an applicaiton</param>
        /// <param name="name">A string representing the Name of the application, this is what will appear on the LeadMe Tablet</param>
        /// <param name="exeName">A string representing the executbale name, this is use to launch the process.</param>
        /// <param name="launchParameters">A stringified list of any parameters required at launch.</param>
        public static void StoreApplication(string wrapperType, string id, string name, string? launchParameters = null, string? altPath = null)
        {
            string? exeName;
            if(altPath != null)
            {
                exeName = Path.GetFileName(altPath);
            } else
            {
                exeName = name;
            }

            applicationList.TryAdd(id, new Experience(wrapperType, id, name, exeName, launchParameters, altPath));
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
        /// Control the currently active session by restarting or stoping the entire process.
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
                default:
                    break;
            }
        }

        /// <summary>
        /// Explored the loaded application list to find the type and name associated with the supplied proccessID.
        /// Create a wrapper and encapsulate the requested process for further use.
        /// </summary>
        public static async Task<string> StartAProcess(string appID)
        {
            //Get the type from the application dictionary
            //entry [application type, application name, application launch parameters]
            Experience experience = applicationList.GetValueOrDefault(appID);
            if (experience.IsNull())
            {
                SessionController.PassStationMessage($"No application found: {appID}");
                return $"No application found: {appID}";
            }

            if(experience.Type == null)
            {
                SessionController.PassStationMessage($"No wrapper associated with experience {appID}.");
                return $"No wrapper associated with experience {appID}.";
            }

            //Determine the wrapper to use
            LoadWrapper(experience.Type);
            if (CurrentWrapper == null)
            {
                SessionController.PassStationMessage("No process wrapper created.");
                return "No process wrapper created.";
            }

            //Stop any current processes before trying to launch a new one
            CurrentWrapper.StopCurrentProcess();

            UIUpdater.UpdateProcess("Launching");
            UIUpdater.UpdateStatus("Loading...");

            //Determine what is need to launch the process(appID - Steam or name - Custom)
            //Pass in the launcher parameters if there are any
            string response = await Task.Factory.StartNew(() =>
            {
                switch (experience.Type)
                {
                    case "Custom":
                        return CurrentWrapper.WrapProcess(experience);
                        break;
                    case "Steam":
                        return CurrentWrapper.WrapProcess(experience);
                        break;
                    case "Vive":
                        throw new NotImplementedException();
                    default:
                        return "Could not find that experience or experience type";
                        break;
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
                case "Steam":
                    CurrentWrapper = steamWrapper;
                    break;
                case "Vive":
                    CurrentWrapper = viveWrapper;
                    break;
                default:
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
                SessionController.PassStationMessage("No process wrapper present.");
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
                return;
            }
            Task.Factory.StartNew(() => CurrentWrapper.RestartCurrentExperience());
        }

        /// <summary>
        /// Stop the currently active process, recycle the current wrapper and stop any monitoring that may be active.
        /// </summary>
        public static void StopAProcess()
        {
            //Stop looking for Vive headset reguardless
            ViveScripts.StopMonitoring();

            if (CurrentWrapper == null)
            {
                SessionController.PassStationMessage("No process wrapper present.");
                return;
            }

            UIUpdater.ResetUIDisplay();
            CurrentWrapper.StopCurrentProcess();
            WrapperMonitoringThread.StopMonitoring();
        }

        /// <summary>
        /// Manage the internal wrapper, coordinate the running, stopping or other functions that
        /// relate to executables that are not experiences.
        /// </summary>
        /// <param name="message">A string of actions separated by ':'</param>
        private void HandleInternalExecutable(string message)
        {
            //[0] - action to take, [1] - executable path
            string[] messageTokens = message.Split(":");

            string name = Path.GetFileNameWithoutExtension(messageTokens[1]);

            //Create a temporary Experience struct to hold the information
            Experience experience = new("Internal", "NA", name, name, null, messageTokens[1]);

            switch(messageTokens[0])
            {
                case "Start":
                    internalWrapper.WrapProcess(experience);
                    break;
                case "Stop":
                    internalWrapper.StopAProcess(experience);
                    break;
                default:
                    LogHandler($"Unknown actionspace (HandleInternalExecutable): {messageTokens[0]}");
                    break;
            }
        }

        /// <summary>
        /// Handle an incoming action, this may be from the LeadMe Station application
        /// </summary>
        /// <param name="type">A string representing the type of action to take</param>
        /// <param name="message">A multiple parameter message seperated by ',' detailing what action is to be taken</param>
        /// <returns>An async task asscoiated with the action</returns>
        public void ActionHandler(string type, string message = "")
        {
            MockConsole.WriteLine($"Wrapper action type: {type}, message: {message}", MockConsole.LogLevel.Debug);

            //Determine the action to take
            switch (type)
            {
                case "CollectApplications":
                    Task.Factory.StartNew(() => CollectAllApplications());
                    break;
                case "CollectHeaderImages":
                    Task.Factory.StartNew(() => CollectHeaderImages(message.Split('/').ToList()));
                    break;
                case "Session":
                    SessionControl(message);
                    break;
                case "Start":
                    StartAProcess(message);
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
                case "Internal":
                    HandleInternalExecutable(message);
                    break;
                default:
                    LogHandler($"Unknown actionspace (ActionHandler): {type}");
                    break;
            }
        }
    }
}
