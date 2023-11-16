using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LeadMeLabsLibrary;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUC._qa.checks;
using Station._qa.checks;

namespace Station._qa;

public class QualityManager
{
    private readonly NetworkChecks _networkChecks = new();
    private readonly ImvrChecks _imvrChecks = new();
    private readonly WindowChecks _windowChecks = new();
    private readonly SoftwareChecks _softwareChecks = new();
    private readonly ConfigChecks _configChecks = new();
    private readonly SteamConfigChecks _steamConfigChecks = new();
    private readonly StationConnectionChecks _stationConnectionChecks = new();
    
    private static string labType = "Online"; 
    
    /// <summary>
    /// Run the requested software check.
    /// </summary>
    public static async void HandleQualityAssurance(string additionalData)
    {
        JObject requestData = JObject.Parse(additionalData);
        var action = requestData.GetValue("action").ToString();
        var actionData = (JObject) requestData.GetValue("actionData");
        
        labType = actionData?.GetValue("labType")?.ToString() ?? "Online";

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
            
            Manager.SendResponse("QA:" + requestData.GetValue("qaToolAddress") , "QA", response.ToString());
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
                    result = JsonConvert.SerializeObject(qualityManager._stationConnectionChecks.RunQa(QualityManager.labType));
                    break;
                case "windows_checks":
                    result = JsonConvert.SerializeObject(qualityManager._windowChecks.RunQa(QualityManager.labType));
                    break;
                case "software_checks":
                    result = JsonConvert.SerializeObject(await qualityManager._softwareChecks.RunQa(QualityManager.labType));
                    new Thread(() =>
                    {
                        string output = JsonConvert.SerializeObject(qualityManager._softwareChecks.RunSlowQaChecks(QualityManager.labType));
                        JObject response = new JObject();
                        response.Add("response", "RunGroup");
                        JObject responseData = new JObject();
                        responseData.Add("group", group);
                        responseData.Add("data", output);
                        response.Add("responseData", responseData);
            
                        Manager.SendResponse("NUC", "QA", response.ToString());
                    }).Start();
                    break;
                case "steam_config_checks":
                    result = JsonConvert.SerializeObject(qualityManager._steamConfigChecks.RunQa(QualityManager.labType));
                    break;
                case "network_checks":
                    result = JsonConvert.SerializeObject(qualityManager._networkChecks.RunQa(QualityManager.labType));

                    if (QualityManager.labType.ToLower().Equals("Online"))
                    {
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

                            Manager.SendResponse("NUC", "QA", response.ToString());
                        }).Start();
                    }

                    break;
                case "imvr_checks":
                    result = JsonConvert.SerializeObject(qualityManager._imvrChecks.RunQa(QualityManager.labType));
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
            
            Manager.SendResponse("NUC", "QA", response.ToString());
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
            
            Manager.SendResponse("NUC", "QA", response.ToString());
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
            Manager.SendResponse("NUC", "QA", response.ToString());
        }
        
        MockConsole.WriteLine($"Unknown QA request {additionalData}", MockConsole.LogLevel.Normal);
    }
}
