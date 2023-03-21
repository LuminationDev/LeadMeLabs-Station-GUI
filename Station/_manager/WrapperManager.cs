using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using leadme_api;

namespace Station
{
    public class WrapperManager
    {
        //Store each wrapper class
        private readonly Wrapper customWrapper = new CustomWrapper();
        private readonly Wrapper steamWrapper = new SteamWrapper();
        private readonly Wrapper viveWrapper = new ViveWrapper();

        //Used for multiple 'internal' applications, operations are separate from the other wrapper classes
        private readonly InternalWrapper internalWrapper = new();

        //Track the currently wrapper experience
        public static Wrapper? CurrentWrapper;
        private static bool alreadyCollecting = false;

        //Store the list of applications (key = ID: [[0] = wrapper type, [1] = application name, [2] = launch params (nullable)])
        public readonly static Dictionary<int, Experience> applicationList = new();

        /// <summary>
        /// Open the pipe server for message to and from external applications (Steam, Custom, etc..) and setup
        /// the saved headset type.
        /// </summary>
        public void Startup()
        {
            StartPipeServer();
            SessionController.SetupHeadsetType();
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

        //TODO currently this is not closing anywhere
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
            MockConsole.WriteLine(message, MockConsole.LogLevel.Debug);
        }

        /// <summary>
        /// Handle an incoming action from the currently running process
        /// </summary>
        /// <param name="message">A multiple parameter message seperated by ',' detailing what action is to be taken</param>
        /// <returns>An async task asscoiated with the action</returns>
        private static void ExternalActionHandler(string message)
        {
            MockConsole.WriteLine($"Pipe message: {message}", MockConsole.LogLevel.Normal);


            //Only receiving details for now
            Manager.sendResponse("Android", "Station", $"SetValue:details:{message}");


            ////Token break down
            ////['ACTIONSPACE','TYPE','MESSAGE']
            //string[] tokens = message.Split(',');

            ////Determine the action to take
            //switch (tokens[0])
            //{
            //    default:
            //        LogHandler($"Unknown actionspace: {tokens[0]}");
            //        break;
            //}
        }

        /// <summary>
        /// Cycle through the different wrappers and collect all the applications installed. Do
        /// not attempt to collect if already part way through.
        /// </summary>
        private void CollectAllApplications()
        {
            if (alreadyCollecting)
            {
                SessionController.PassStationMessage("Already collecting applications");
                return;
            }

            alreadyCollecting = true;

            List<string> applications = new();

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

            applicationList.TryAdd(int.Parse(id), new Experience(wrapperType, id, name, exeName, launchParameters, altPath));
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
        private void StartAProcess(string appID)
        {
            //Get the type from the application dictionary
            //entry [application type, application name, application launch parameters]
            Experience experience = applicationList.GetValueOrDefault(int.Parse(appID));
            if (experience.IsNull())
            {
                SessionController.PassStationMessage($"No application found: {appID}");
                return;
            }

            if(experience.Type == null)
            {
                SessionController.PassStationMessage($"No wrapper associated with experience {appID}.");
                return;
            }

            //Determine the wrapper to use
            LoadWrapper(experience.Type);
            if (CurrentWrapper == null)
            {
                SessionController.PassStationMessage("No process wrapper created.");
                return;
            }

            UIUpdater.UpdateProcess("Launching");
            UIUpdater.UpdateStatus("Loading...");

            //TODO clean this up so we dont have to rely on appID or name
            //Determine what is need to launch the process(appID - Steam or name - Custom)
            //Pass in the launcher parameters if there are any
            Task.Factory.StartNew(() =>
            {
                switch (experience.Type)
                {
                    case "Custom":
                        CurrentWrapper.WrapProcess(experience);
                        break;
                    case "Steam":
                        CurrentWrapper.WrapProcess(experience);
                        break;
                    case "Vive":
                        throw new NotImplementedException();
                    default:
                        break;
                }
            });
        }

        /// <summary>
        /// Create the appropriate wrapper for the incoming process.
        /// </summary>
        /// <param name="type">A string representing the type of wrapper to create.</param>
        /// <returns></returns>
        private void LoadWrapper(string type)
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
            Task.Factory.StartNew(() => CurrentWrapper.RestartCurrentProcess());
        }

        /// <summary>
        /// Stop the currently active process, recycle the current wrapper and stop any monitoring that may be active.
        /// </summary>
        public static void StopAProcess()
        {
            if (CurrentWrapper == null)
            {
                SessionController.PassStationMessage("No process wrapper present.");
                return;
            }

            //TODO this is being called from somewhere else?
            UIUpdater.ResetUIDisplay();
            CurrentWrapper.StopCurrentProcess();
            WrapperMonitoringThread.stopMonitoring();
            ViveScripts.stopMonitoring();
            RecycleWrapper();
        }

        /// <summary>
        /// Destroy the current process wrapper ready for the next action.
        /// </summary>
        public static void RecycleWrapper()
        {
            CurrentWrapper = null;
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
