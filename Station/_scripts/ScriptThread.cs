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
        public async void run()
        {
            //Station.App.initSentry();

            //Based on the data, build/run a script and then send the output back to the client
            //Everything below is just for testing - definitely going to need something better to determine in the future
            if (actionNamespace == "Connection")
            {
                if (additionalData == "Connect")
                {
                    Manager.sendResponse(source, "Station", "SetValue:status:On");
                    Manager.sendResponse(source, "Station", "SetValue:gameName:");
                    Manager.sendResponse("Android", "Station", "SetValue:gameId:");
                    Manager.sendResponse(source, "Station", "SetValue:volume:" + CommandLine.getVolume());
                }
            }
            if (additionalData == null) return;
            if (actionNamespace == "CommandLine")
            {
                StationScripts.execute(source, additionalData);
            }

            if (actionNamespace == "Station")
            {
                if (additionalData.StartsWith("GetValue"))
                {
                    string key = additionalData.Split(":", 2)[1];
                    if (key == "steamApplications")
                    {
                        Logger.WriteLog("Collecting station exeperiences", MockConsole.LogLevel.Normal);
                        Manager.wrapperManager?.ActionHandler("CollectApplications");
                    }
                    if (key == "volume")
                    {
                        Manager.sendResponse(source, "Station", "SetValue:" + key + ":" + CommandLine.getVolume());
                    }
                }
                if (additionalData.StartsWith("SetValue"))
                {
                    string[] keyValue = additionalData.Split(":", 3);
                    string key = keyValue[1];
                    string value = keyValue[2];
                    if (key == "volume")
                    {
                        CommandLine.setVolume(value);
                    }
                }
            }

            if (actionNamespace == "Experience")
            {
                if (additionalData.StartsWith("Refresh"))
                {
                    Manager.wrapperManager?.ActionHandler("CollectApplications");
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
                    Manager.wrapperManager?.ActionHandler("Message", "MESSAGE DETAILS");
                }
            }
        }
    }
}
