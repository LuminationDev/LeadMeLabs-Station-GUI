using System.Threading.Tasks;
using Station.Components._commandLine;
using Station.Components._notification;
using Station.Components._utils;
using Station.Components._wrapper.steam;
using Station.MVC.Controller;
using Station.QA;

namespace Station.Components._scripts;

public class ScriptThread
{
    private readonly string _data;
    private readonly string _source;
    private readonly string _destination;
    private readonly string _actionNamespace;
    private readonly string? _additionalData;

    public ScriptThread(string data)
    {
        this._data = data;
        string[] dataParts = data.Split(":", 4);
        _source = dataParts[0];
        _destination = dataParts[1];
        _actionNamespace = dataParts[2];
        _additionalData = dataParts.Length > 3 ? dataParts[3] : null;
    }

    /// <summary>
    /// Determines what to do with the data that was supplied at creation. Executes a script depending
    /// on what the data message contains. Once an action has been taken, a response is sent back.
    /// </summary>
    public void Run()
    {
        //Based on the data, build/run a script and then send the output back to the client
        //Everything below is just for testing - definitely going to need something better to determine in the future
        if (_actionNamespace == "Connection")
        {
            HandleConnection(_additionalData);
        }

        if (_additionalData == null) return;

        switch (_actionNamespace)
        {
            case "CommandLine":
                StationScripts.Execute(_source, _additionalData);
                break;

            case "Station":
                HandleStation(_additionalData);
                break;

            case "HandleExecutable":
                HandleExecutable(_additionalData);
                break;

            case "Experience":
                HandleExperience(_additionalData);
                break;

            case "LogFiles":
                HandleLogFiles(_additionalData);
                break;
            
            case "QA":
                QualityManager.HandleQualityAssurance(_additionalData);
                break;
        }
    }

    private void HandleConnection(string? additionalData)
    {
        if (additionalData == null) return;
        if (!additionalData.Contains("Connect")) return;
        
        MessageController.SendResponse(_source, "Station", "SetValue:status:On");
        MessageController.SendResponse(_source, "Station", $"SetValue:state:{SessionController.CurrentState}");
        MessageController.SendResponse(_source, "Station", "SetValue:gameName:");
        MessageController.SendResponse("Android", "Station", "SetValue:gameId:");
        MessageController.SendResponse(_source, "Station", $"SetValue:volume:{CommandLine.GetVolume()}");
    }

    private void HandleStation(string additionalData)
    {
        if (additionalData.StartsWith("GetValue"))
        {
            string key = additionalData.Split(":", 2)[1];
            if (key == "installedApplications")
            {
                Logger.WriteLog("Collecting station experiences", MockConsole.LogLevel.Normal);
                MainController.wrapperManager?.ActionHandler("CollectApplications");
            }
            if (key == "volume")
            {
                MessageController.SendResponse(_source, "Station", "SetValue:" + key + ":" + CommandLine.GetVolume());
            }
            if (key == "devices")
            {
                //When a tablet connects/reconnects to the NUC, send through the current VR device statuses.
                SessionController.VrHeadset?.GetStatusManager().QueryStatuses();
            }
        }
        if (additionalData.StartsWith("SetValue"))
        {
            string[] keyValue = additionalData.Split(":", 3);
            string key = keyValue[1];
            string value = keyValue[2];
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
        string[] split = additionalData.Split(":", 4);
        string action = split[0];
        string launchType = split[1];
        
        //Convert the path back to absolute (NUC changed it for sending)
        string safePath = split[2];
        string path = safePath.Replace("%", ":");
        string safeParameters = split.Length > 3 ? split[3] : "";
        string parameters = safeParameters.Replace("%", ":");

        MainController.wrapperManager?.HandleInternalExecutable(action, launchType, path, parameters);
    }

    /// <summary>
    /// Utilises the pipe server to send the incoming message into an active experience.
    /// </summary>
    private async void HandleExperience(string additionalData)
    {
        if (additionalData.StartsWith("Refresh"))
        {
            MainController.wrapperManager?.ActionHandler("CollectApplications");
        }

        if (additionalData.StartsWith("Restart"))
        {
            MainController.wrapperManager?.ActionHandler("Restart");
        }

        if (additionalData.StartsWith("Thumbnails"))
        {
            string[] split = additionalData.Split(":", 2);
            MainController.wrapperManager?.ActionHandler("CollectHeaderImages", split[1]);
        }

        if (additionalData.StartsWith("Launch"))
        {
            string id = additionalData.Split(":")[1]; // todo - tidy this up
            MainController.wrapperManager?.ActionHandler("Stop");

            await Task.Delay(2000);

            MainController.wrapperManager?.ActionHandler("Start", id);
        }

        if (additionalData.StartsWith("PassToExperience"))
        {
            string[] split = additionalData.Split(":", 2);
            MainController.wrapperManager?.ActionHandler("Message", split[1]);
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
