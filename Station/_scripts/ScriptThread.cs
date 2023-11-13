using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LeadMeLabsLibrary;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUC._qa.checks;
using Station._qa;

namespace Station._scripts;

public class ScriptThread
{
    private readonly string _data;
    private readonly string _source;
    private readonly string _destination;
    private readonly string _actionNamespace;
    private readonly JObject? _additionalData;

    public ScriptThread(string data)
    {
        this._data = data;
        string[] dataParts = data.Split(":", 4);
        _source = dataParts[0];
        _destination = dataParts[1];
        _actionNamespace = dataParts[2];
        _additionalData = dataParts.Length > 3 ? JObject.Parse(dataParts[3]) : null;
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
            HandleConnection();
        }

        if (_additionalData == null) return;

        switch (_actionNamespace)
        {
            case "CommandLine":
                StationScripts.Execute(_source, _additionalData);
                break;

            case "Station":
                HandleStation();
                break;

            case "HandleExecutable":
                HandleExecutable();
                break;

            case "Experience":
                HandleExperience();
                break;

            case "LogFiles":
                HandleLogFiles();
                break;
            
            case "QA":
                HandleQualityAssurance();
                break;
        }
    }

    private void HandleConnection()
    {
        if (_additionalData == null) return;
        if (!_additionalData.ContainsKey("Connect")) return;
        
        JObject values = new JObject
        {
            { "status", "On" },
            { "state", SessionController.CurrentState },
            { "gameName", "" },
            { "gameId", "" },
            { "volume", CommandLine.GetVolume() }
        };
        JObject setValue = new() { { "SetValue", values } };
        Manager.SendMessage(_source, "Station", setValue);
    }

    private void HandleStation()
    {
        if (_additionalData == null) return;
        if (_additionalData.ContainsKey("GetValue"))
        {
            string? key = _additionalData.GetValue("GetValue")?.ToString();
            
            switch (key)
            {
                case "installedApplications":
                    Logger.WriteLog("Collecting station experiences", MockConsole.LogLevel.Normal);
                    Manager.wrapperManager?.ActionHandler("CollectApplications");
                    break;
                case "volume":
                {
                    JObject values = new JObject
                    {
                        { "volume", CommandLine.GetVolume() }
                    };
                    JObject setValue = new() { { "SetValue", values } };
                    Manager.SendMessage(_source, "Station", setValue);
                    break;
                }
                case "devices":
                    //When a tablet connects/reconnects to the NUC, send through the current VR device statuses.
                    SessionController.VrHeadset?.GetStatusManager().QueryStatuses();
                    break;
            }
        }
        
        if (_additionalData.TryGetValue("SetValue", out var value))
        {
            JObject? keyValue = (JObject?)value;
            if (keyValue == null) return;
            
            if (keyValue.ContainsKey("volume"))
            {
                string? volume = keyValue.GetValue("volume")?.ToString();
                if (volume == null) return;
                CommandLine.SetVolume(volume);
            }
            if (keyValue.ContainsKey("steamCMD"))
            {
                string? steamCmd = keyValue.GetValue("volume")?.ToString();
                if (steamCmd == null) return;
                SteamScripts.ConfigureSteamCommand(steamCmd);
            }
        }
    }

    /// <summary>
    /// Launch an internal executable.
    /// </summary>
    private void HandleExecutable()
    {
        if (_additionalData == null) return;
        _additionalData.TryGetValue("ExecutableAction", out var value);
        JObject? keyValue = (JObject?)value;
        if (keyValue == null) return;
        
        string? action = keyValue.GetValue("action")?.ToString();
        string? path = keyValue.GetValue("path")?.ToString();
        
        if (action == null || path == null) return;
        
        switch (action)
        {
            case "start":
                Manager.wrapperManager?.ActionHandler("Internal", $"Start:{path}");
                break;
            case "stop":
                Manager.wrapperManager?.ActionHandler("Internal", $"Stop:{path}");
                break;
        }
    }

    /// <summary>
    /// Utilises the pipe server to send the incoming message into an active experience.
    /// </summary>
    private async void HandleExperience()
    {
        if (_additionalData == null) return;
        if (_additionalData.ContainsKey("Refresh"))
        {
            Manager.wrapperManager?.ActionHandler("CollectApplications");
        }

        if (_additionalData.ContainsKey("Restart"))
        {
            Manager.wrapperManager?.ActionHandler("Restart");
        }

        if (_additionalData.ContainsKey("Thumbnails"))
        {
            string? images = _additionalData.GetValue("Thumbnails")?.ToString();
            if (images == null) return;
            Manager.wrapperManager?.ActionHandler("CollectHeaderImages", images);
        }

        if (_additionalData.ContainsKey("Launch"))
        {
            string? id = _additionalData.GetValue("Launch")?.ToString();
            if (id == null) return;
            
            Manager.wrapperManager?.ActionHandler("Stop");

            await Task.Delay(2000);

            Manager.wrapperManager?.ActionHandler("Start", id);
        }

        if (_additionalData.ContainsKey("PassToExperience"))
        {
            JObject? messageObject = (JObject?)_additionalData.GetValue("PassToExperience");
            string? passMessage = messageObject?.GetValue("action")?.ToString();
            if (passMessage == null) return;
            
            Manager.wrapperManager?.ActionHandler("Message", passMessage);
        }
    }

    /// <summary>
    /// The NUC has requested that the log files be transferred over the network.
    /// </summary>
    private void HandleLogFiles()
    {
        if (_additionalData == null) return;
        if (_additionalData.ContainsKey("Request"))
        {
            string? request = _additionalData.GetValue("Request")?.ToString();
            if (request == null) return;
            Logger.LogRequest(int.Parse(request));
        }
    }
    
    /// <summary>
    /// Run the requested software check.
    /// </summary>
    private async void HandleQualityAssurance()
    {
        if (_additionalData == null) return;
        string? action = _additionalData.GetValue("action")?.ToString();
        JObject? actionData = (JObject?)_additionalData.GetValue("actionData");

        if (action == null) return;
        if (action.Equals("ConnectStation"))
        {
            JObject response = new JObject();
            response.Add("response", "StationConnected");
            JObject responseData = new JObject();
            responseData.Add("ipAddress", SystemInformation.GetIPAddress().ToString());
            responseData.Add("nucIpAddress", Environment.GetEnvironmentVariable("NucAddress", EnvironmentVariableTarget.Process) ?? "Not found");
            responseData.Add("id", Environment.GetEnvironmentVariable("StationId", EnvironmentVariableTarget.Process) ?? "Not found");
            responseData.Add("labLocation", Environment.GetEnvironmentVariable("LabLocation", EnvironmentVariableTarget.Process) ?? "Not found");
            responseData.Add("room", Environment.GetEnvironmentVariable("room", EnvironmentVariableTarget.Process) ?? "Not found");
            responseData.Add("macAddress", SystemInformation.GetMACAddress());
            responseData.Add("expectedStationId", actionData.GetValue("expectedStationId"));
            response.Add("responseData", responseData);
            
            Manager.SendMessage("QA:" + _additionalData.GetValue("qaToolAddress"), "QA", response);
        }
        
        if (action.Equals("RunGroup"))
        {
            string group = actionData.GetValue("group").ToString();
            string? labType = actionData.GetValue("labType")?.ToString();
            QualityManager qualityManager = new QualityManager();

            string result = "";
            switch (group)
            {
                case "station_connection_checks":
                    result = JsonConvert.SerializeObject(qualityManager.stationConnectionChecks.RunQa(labType ?? "Online"));
                    break;
                case "windows_checks":
                    result = JsonConvert.SerializeObject(qualityManager.windowChecks.RunQa(labType ?? "Online"));
                    break;
                case "software_checks":
                    result = JsonConvert.SerializeObject(qualityManager.softwareChecks.RunQa(labType ?? "Online"));
                    new Thread(() =>
                    {
                        string output = JsonConvert.SerializeObject(qualityManager.softwareChecks.RunSlowQaChecks(labType ?? "Online"));
                        JObject response = new JObject();
                        response.Add("response", "RunGroup");
                        JObject responseData = new JObject();
                        responseData.Add("group", group);
                        responseData.Add("data", output);
                        response.Add("responseData", responseData);
            
                        Manager.SendMessage("NUC", "QA", response);
                    }).Start();
                    break;
                case "steam_config_checks":
                    result = JsonConvert.SerializeObject(qualityManager.steamConfigChecks.RunQa(labType ?? "Online"));
                    break;
                case "network_checks":
                    result = JsonConvert.SerializeObject(qualityManager.networkChecks.RunQa(labType ?? "Online"));
                    new Thread(async () =>
                    {
                        int stationId =
                            Int32.Parse(Environment.GetEnvironmentVariable("StationId",
                                EnvironmentVariableTarget.Process));
                        if (stationId > 10)
                        {
                            stationId = 1; // in testing we used 101+ for our ids
                        }
                        await Task.Delay(stationId * 20000);
                        
                        InternetSpeedCheck internetSpeedCheck = new InternetSpeedCheck();
                        QaCheck qaCheck = internetSpeedCheck.RunInternetSpeedTest();
                        List<QaCheck> qaCheckList = new List<QaCheck>();
                        qaCheckList.Add(qaCheck);

                        JObject response = new JObject();
                        response.Add("response", "RunGroup");
                        JObject responseData = new JObject();
                        responseData.Add("group", "network_checks");
                        responseData.Add("data", JsonConvert.SerializeObject(qaCheckList));
                        response.Add("responseData", responseData);
            
                        Manager.SendMessage("NUC", "QA", response);
                    }).Start();
                    break;
                case "imvr_checks":
                    result = JsonConvert.SerializeObject(qualityManager.imvrChecks.RunQa(labType ?? "Online"));
                    break;
                default:
                    return;
            }

            JObject response = new JObject();
            response.Add("response", "RunGroup");
            JObject responseData = new JObject();
            responseData.Add("group", group);
            responseData.Add("data", result);
            response.Add("responseData", responseData);
            
            Manager.SendMessage("NUC", "QA", response);
        }

        if (action.Equals("LaunchExperience"))
        {
            string experienceId = actionData.GetValue("experienceId").ToString();
            
            // check if there is an unaccepted EULA
            // first get app info from steamcmd to get the list of eulas
            // then read the localconfig.vdf file to see what eulas are in the list
            string details = CommandLine.ExecuteSteamCommand($"+app_info_print {experienceId} +quit");
            var data = new List<string>(details.Split("\n")).Where(line => line.Contains("_eula_")).Where(line => !line.Contains("http"));
            List<string> neededEulas = new List<string>();
            foreach (var eula in data)
            {
                neededEulas.Add(eula.Split("\t")[eula.Split("\t").Length - 1].Trim('"'));
            }

            JObject response = new JObject();
            response.Add("response", "ExperienceLaunchAttempt");
            JObject responseData = new JObject();
            
            List<string> acceptedEulas = SteamConfig.GetAcceptedEulasForAppId(experienceId);
            bool allEulasAccepted = !neededEulas.Except(acceptedEulas).Any();
            string experienceLaunchResponse = "";
            if (allEulasAccepted)
            {
                WrapperManager.StopAProcess();
                Task.Delay(3000);
                experienceLaunchResponse = await WrapperManager.StartAProcess(experienceId);
                responseData.Add("result", experienceLaunchResponse.ToLower().Equals("launching") ? "launching" : "failed");
            }
            else
            {
                experienceLaunchResponse = "Found unaccepted EULAs, did not attempt to launch";
                responseData.Add("result", "warning");
            }
            
            responseData.Add("message", experienceLaunchResponse);
            responseData.Add("experienceId", experienceId);
            response.Add("responseData", responseData);
            
            Manager.SendMessage("NUC", "QA", response);
            return;
        }

        if (action.Equals("GetVrStatuses"))
        {
            JObject response = new JObject();
            response.Add("response", "GetVrStatuses");
            JObject responseData = new JObject();
            response.Add("responseData", responseData);
            responseData.Add("result",
                SessionController.VrHeadset == null
                    ? null
                    : SessionController.VrHeadset.GetStatusManager().GetStatusesJson());
            
            Manager.SendMessage("NUC", "QA", response);
        }
        
        MockConsole.WriteLine($"Unknown QA request {this._additionalData}", MockConsole.LogLevel.Normal);
    }
}
