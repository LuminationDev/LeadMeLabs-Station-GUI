using System;
using System.Collections.Generic;
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

        public static Wrapper? CurrentWrapper;
        private static bool alreadyCollecting = false;

        //Store the list of applications (key = ID: [[0] = wrapper type, [1] = application name])
        private readonly static Dictionary<int, List<string>> applicationList = new();

        /// <summary>
        /// Open the pipe server for message to and from external applications (Steam, Custom, etc..) and setup
        /// the saved headset type.
        /// </summary>
        public void Startup()
        {
            ParentPipeServer.Run(LogHandler, ExternalActionHandler);
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
            SessionController.endVRSession();
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
        private void ExternalActionHandler(string message)
        {
            MockConsole.WriteLine($"Pipe message: {message}", MockConsole.LogLevel.Debug);

            //Token break down
            //['ACTIONSPACE','TYPE','MESSAGE']
            string[] tokens = message.Split(',');

            //Determine the action to take
            switch (tokens[0])
            {
                default:
                    LogHandler($"Unknown actionspace: {tokens[0]}");
                    break;
            }
        }

        /// <summary>
        /// Cycle through the different wrappers and collect all the applications installed. Do
        /// not attempt to collect if already part way through.
        /// </summary>
        /// <returns></returns>
        private void CollectAllApplications()
        {
            if (alreadyCollecting)
            {
                SessionController.PassStationMessage("Already collecting applications");
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

            StoreApplications(applications);

            string response = string.Join('/', applications);

            SessionController.PassStationMessage($"ApplicationList,{response}");

            alreadyCollecting = false;
        }

        /// <summary>
        /// Load the collected applications into the local storage list to determine start processes based
        /// on the ID's saved.
        /// </summary>
        /// <param name="list"></param>
        private void StoreApplications(List<string> list)
        {
            //Load all applications for future use
            foreach (string application in list)
            {
                //TOKEN [wrapper type, ID, name]
                string[] appTokens = application.Split("|");
                applicationList.TryAdd(int.Parse(appTokens[1]), new List<string> { appTokens[0], appTokens[2] });
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
                    SessionController.restartVRSession();
                    break;
                case "End":
                    SessionController.endVRSession();
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
            //entry [application type, application name]
            List<string>? entry = applicationList.GetValueOrDefault(int.Parse(appID));
            if (entry == null)
            {
                SessionController.PassStationMessage($"No application found: {appID}");
                return;
            }

            //Determine the wrapper to use
            LoadWrapper(entry[0]);
            if (CurrentWrapper == null)
            {
                SessionController.PassStationMessage("No process wrapper created.");
                return;
            }

            UIUpdater.UpdateProcess("Launching");
            UIUpdater.UpdateStatus("Loading...");

            //TODO clean this up so we dont have to rely on appID or name
            //Determine what is need to launch the process(appID - Steam or name - Custom)
            Task.Factory.StartNew(() =>
            {
                switch (entry[0])
                {
                    case "Custom":
                        CurrentWrapper.WrapProcess(entry[1]);
                        break;
                    case "Steam":
                        CurrentWrapper.WrapProcess(appID);
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
                default:
                    LogHandler($"Unknown actionspace: {type}");
                    break;
            }
        }
    }
}
