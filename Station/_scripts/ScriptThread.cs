using System.Threading.Tasks;

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

                case "Experience":
                    HandleExperience(additionalData);
                    break;

                case "LogFiles":
                    HandleLogFiles(additionalData);
                    break;

                default:
                    break;
            }
        }

        private void HandleConnection(string? additionalData)
        {
            if (additionalData == null) return;
            if (additionalData.Equals("Connect"))
            {
                Manager.SendResponse(source, "Station", "SetValue:status:On");
                Manager.SendResponse(source, "Station", $"SetValue:state:{SessionController.currentState}");
                Manager.SendResponse(source, "Station", "SetValue:gameName:");
                Manager.SendResponse("Android", "Station", "SetValue:gameId:");
                Manager.SendResponse(source, "Station", "SetValue:volume:" + CommandLine.GetVolume());
            }
        }

        private void HandleStation(string additionalData)
        {
            if (additionalData.StartsWith("GetValue"))
            {
                string key = additionalData.Split(":", 2)[1];
                if (key == "installedApplications")
                {
                    Logger.WriteLog("Collecting station experiences", MockConsole.LogLevel.Normal);
                    Manager.wrapperManager?.ActionHandler("CollectApplications");
                }
                if (key == "volume")
                {
                    Manager.SendResponse(source, "Station", "SetValue:" + key + ":" + CommandLine.GetVolume());
                }
                if (key == "devices")
                {
                    //When a tablet connects/reconnects to the NUC, send through the current VR device statuses.
                    SessionController.vrHeadset?.GetStatusManager().QueryStatues();
                }
            }
            if (additionalData.StartsWith("SetValue"))
            {
                string[] keyValue = additionalData.Split(":", 3);
                string key = keyValue[1];
                string? value = keyValue[2];
                if (key == "volume")
                {
                    CommandLine.SetVolume(value);
                }
                if (key == "steamCMD")
                {
                    SteamScripts.ConfigureSteamCommand(value);
                }
            }
        }

        /// <summary>
        /// Launch an internal executable.
        /// </summary>
        private void HandleExecutable(string additionalData)
        {
            string[] split = additionalData.Split(":", 2);
            string action = split[0];
            string path = split[1];
            if (action.Equals("start"))
            {
                Manager.wrapperManager?.ActionHandler("Internal", $"Start:{path}");
            }

            if (action.Equals("stop"))
            {
                Manager.wrapperManager?.ActionHandler("Internal", $"Stop:{path}");
            }
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
        /// The NUC has requested that the log files be transfered over the network.
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
