using System;
using System.Threading.Tasks;
using Station._manager;
using Station._qa;
using Station._utils;

namespace Station
{
    public class ScriptThread
    {
        private readonly string data;
        private readonly string source;
        private readonly string destination;
        private readonly string actionNamespace;
        private readonly string? additionalData;

        public ScriptThread(string data)
        {
            this.data = data;
            string[] dataParts = data.Split(":", 4);
            source = dataParts[0];
            destination = dataParts[1];
            actionNamespace = dataParts[2];
            additionalData = dataParts.Length > 3 ? dataParts[3] : null;
        }

        /// <summary>
        /// Determines what to do with the data that was supplied at creation. Executes a script depending
        /// on what the data message contains. Once an action has been taken, a response is sent back.
        /// </summary>
        public void Run()
        {
            //Based on the data, build/run a script and then send the output back to the client
            //Everything below is just for testing - definitely going to need something better to determine in the future
            if (actionNamespace == "Connection")
            {
                HandleConnection(additionalData);
            }

            if (additionalData == null) return;

            switch (actionNamespace)
            {
                case "CommandLine":
                    StationScripts.Execute(source, additionalData);
                    break;

                case "Station":
                    HandleStation(additionalData);
                    break;

                case "HandleExecutable":
                    HandleExecutable(additionalData);
                    break;
                
                case "DisplayChange":
                    HandleDisplayChange(additionalData);
                    break;

                case "Experience":
                    HandleExperience(additionalData);
                    break;

                case "LogFiles":
                    HandleLogFiles(additionalData);
                    break;
                
                case "QA":
                    QualityManager.HandleQualityAssurance(additionalData);
                    break;
            }
        }

        private void HandleConnection(string? additionalData)
        {
            if (additionalData == null) return;
            if (additionalData.Contains("Connect"))
            {
                Manager.SendResponse(source, "Station", "SetValue:status:On");
                Manager.SendResponse(source, "Station", $"SetValue:state:{SessionController.CurrentState}");
                Manager.SendResponse(source, "Station", "SetValue:gameName:");
                Manager.SendResponse("Android", "Station", "SetValue:gameId:");
                AudioManager.Initialise();
            }
        }

        private async void HandleStation(string additionalData)
        {
            if (additionalData.StartsWith("GetValue"))
            {
                string key = additionalData.Split(":", 2)[1];
                switch (key)
                {
                    case "installedApplications":
                        Logger.WriteLog("Collecting station experiences", MockConsole.LogLevel.Normal);
                        Manager.wrapperManager?.ActionHandler("CollectApplications");
                        break;

                    case "volume":
                        string currentVolume = await AudioManager.GetVolume();
                        Manager.SendResponse(source, "Station", "SetValue:" + key + ":" + currentVolume);
                        break;

                    case "muted":
                        string isMuted = await AudioManager.GetMuted();
                        Manager.SendResponse(source, "Station", "SetValue:" + key + ":" + isMuted);
                        break;

                    case "devices":
                        //When a tablet connects/reconnects to the NUC, send through the current VR device statuses.
                        SessionController.VrHeadset?.GetStatusManager().QueryStatuses();
                        break;
                }
            }
            
            if (additionalData.StartsWith("SetValue"))
            {
                string[] keyValue = additionalData.Split(":", 3);
                string key = keyValue[1];
                string? value = keyValue[2];
                
                switch (key)
                {
                    case "volume":
                        AudioManager.SetVolume(value);
                        break;

                    case "activeAudioDevice":
                        AudioManager.SetCurrentAudioDevice(value);
                        break;

                    case "muted":
                        AudioManager.SetMuted(value);
                        break;

                    case "steamCMD":
                        SteamScripts.ConfigureSteamCommand(value);
                        break;
                }
            }
        }

        /// <summary>
        /// Launch an internal executable.
        /// </summary>
        private void HandleExecutable(string additionalData)
        {
            string[] split = additionalData.Split(":", 4);
            string action = split[0];
            string launchType = split[1];

            //Convert the path back to absolute (NUC changed it for sending)
            string safePath = split[2];
            string path = safePath.Replace("%", ":");
            string safeParameters = split.Length > 3 ? split[3] : "";
            string parameters = safeParameters.Replace("%", ":");
            
            Manager.wrapperManager?.HandleInternalExecutable(action, launchType, path, parameters);
        }
        
        /// <summary>
        /// Check that new display value is valid and then change to it
        /// <param name="additionalData">Expected format: Height:1080:Width:1920</param>
        /// </summary>
        private void HandleDisplayChange(string additionalData)
        {
            string[] split = additionalData.Split(":", 4);
            if (split.Length < 4)
            {
                Logger.WriteLog($"Could not parse display change for additional data {additionalData}", MockConsole.LogLevel.Error);
                return;
            }
            string heightString = split[1];
            string widthString = split[3];
            int height = 0;
            int width = 0;
            if (!Int32.TryParse(heightString, out height) || !Int32.TryParse(widthString, out width))
            {
                Logger.WriteLog($"Could not parse display change for values Height: {heightString}, Width: {widthString}", MockConsole.LogLevel.Error);
                return;
            }

            if (!DisplayController.IsDisplayModeSupported(width, height, 32))
            {
                Logger.WriteLog($"Invalid display change for values Height: {heightString}, Width: {widthString}", MockConsole.LogLevel.Error);
                return;
            }

            DisplayController.ChangeDisplaySettings(width, height, 32);
            Logger.WriteLog($"Changed display settings to Height: {heightString}, Width: {widthString}", MockConsole.LogLevel.Debug);
        }

        /// <summary>
        /// Utilises the pipe server to send the incoming message into an active experience.
        /// </summary>
        private async void HandleExperience(string additionalData)
        {
            if (additionalData.StartsWith("Refresh"))
            {
                Manager.wrapperManager?.ActionHandler("CollectApplications");
            }

            if (additionalData.StartsWith("Restart"))
            {
                Manager.wrapperManager?.ActionHandler("Restart");
            }

            if (additionalData.StartsWith("Thumbnails"))
            {
                string[] split = additionalData.Split(":", 2);
                Manager.wrapperManager?.ActionHandler("CollectHeaderImages", split[1]);
            }

            if (additionalData.StartsWith("Launch"))
            {
                string id = additionalData.Split(":")[1]; // todo - tidy this up
                Manager.wrapperManager?.ActionHandler("Stop");

                await Task.Delay(2000);

                Manager.wrapperManager?.ActionHandler("Start", id);
            }

            if (additionalData.StartsWith("PassToExperience"))
            {
                string[] split = additionalData.Split(":", 2);
                Manager.wrapperManager?.ActionHandler("Message", split[1]);
            }
        }

        /// <summary>
        /// The NUC has requested that the log files be transferred over the network.
        /// </summary>
        private void HandleLogFiles(string additionalData)
        {
            if (additionalData.StartsWith("Request"))
            {
                string[] split = additionalData.Split(":", 2);
                Logger.LogRequest(int.Parse(split[1]));
            }
        }
    }
}
