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

public static class QualityManager
{
    private static readonly NetworkChecks NetworkChecks = new();
    private static readonly ImvrChecks ImvrChecks = new();
    private static readonly WindowChecks WindowChecks = new();
    private static readonly SoftwareChecks SoftwareChecks = new();
    private static readonly ConfigChecks ConfigChecks = new();
    private static readonly SteamConfigChecks SteamConfigChecks = new();
    private static readonly StationConnectionChecks StationConnectionChecks = new();
    
    private static string labType = "Online"; 
    
    /// <summary>
    /// Run the requested software check.
    /// </summary>
    public static async void HandleQualityAssurance(string additionalData)
    {
        JObject requestData = JObject.Parse(additionalData);
        var action = requestData.GetValue("action").ToString();
        var actionData = (JObject) requestData.GetValue("actionData");
        var parameters = (JObject) actionData?.GetValue("parameters");
        labType = actionData?.GetValue("labType")?.ToString() ?? "Online";

        switch (action)
        {
            case "ConnectStation":
            {
                JObject response = new JObject();
                response.Add("response", "StationConnected");
                
                JObject responseData = new JObject();
                responseData.Add("ipAddress", SystemInformation.GetIPAddress().ToString());
                responseData.Add("nucIpAddress", Environment.GetEnvironmentVariable("NucAddress", EnvironmentVariableTarget.Process) ?? "Not found");
                responseData.Add("id", Environment.GetEnvironmentVariable("StationId", EnvironmentVariableTarget.Process) ?? "Not found");
                responseData.Add("labLocation", Environment.GetEnvironmentVariable("LabLocation", EnvironmentVariableTarget.Process) ?? "Not found");
                responseData.Add("stationMode", Environment.GetEnvironmentVariable("StationMode", EnvironmentVariableTarget.Process) ?? "Not found");
                responseData.Add("room", Environment.GetEnvironmentVariable("room", EnvironmentVariableTarget.Process) ?? "Not found");
                responseData.Add("macAddress", SystemInformation.GetMACAddress());
                responseData.Add("expectedStationId", actionData.GetValue("expectedStationId"));
                response.Add("responseData", responseData);
            
                Manager.SendResponse("QA:" + requestData.GetValue("qaToolAddress") , "QA", response.ToString());
                break;
            }
            
            case "RunGroup":
            {
                string group = actionData.GetValue("group").ToString();
                string result = "";
                switch (group)
                {
                    case "station_connection_checks":
                        result = JsonConvert.SerializeObject(StationConnectionChecks.RunQa(labType));
                        break;
                    
                    case "windows_checks":
                        result = JsonConvert.SerializeObject(WindowChecks.RunQa(labType));
                        break;
                    
                    case "software_checks":
                        result = JsonConvert.SerializeObject(await SoftwareChecks.RunQa(labType));
                        new Thread(() =>
                        {
                            string output = JsonConvert.SerializeObject(SoftwareChecks.RunSlowQaChecks(labType));
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
                        result = JsonConvert.SerializeObject(SteamConfigChecks.RunQa(labType));
                        break;
                    
                    case "network_checks":
                        result = JsonConvert.SerializeObject(NetworkChecks.RunQa(parameters?.GetValue("networkType")?.ToString() ?? "Milesight"));

                        if (labType.ToLower().Equals("online"))
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
                        string expectedHeadset = parameters?.GetValue("headset")?.ToString() ?? "";
                        result = JsonConvert.SerializeObject(ImvrChecks.RunQa(expectedHeadset));
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
                break;
            }
            
            case "LaunchExperience":
            {
                string experienceId = actionData.GetValue("experienceId").ToString();
                Experience experience = WrapperManager.ApplicationList.GetValueOrDefault(experienceId);

                JObject response = new JObject();
                response.Add("response", "ExperienceLaunchAttempt");
                JObject responseData = new JObject();
                
                if (!Helper.GetStationMode().Equals(Helper.STATION_MODE_VR) && experience.IsVr)
                {
                    responseData.Add("result", "warning");
                    responseData.Add("message", "Experience is a VR experience and Station is a non-vr Station");
                    responseData.Add("experienceId", experienceId);
                    response.Add("responseData", responseData);
                    Manager.SendResponse("NUC", "QA", response.ToString());
                    return;
                }
            
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
            
            case "GetVrStatuses":
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
                break;
            }
            
            default:
                MockConsole.WriteLine($"Unknown QA request {additionalData}", MockConsole.LogLevel.Normal);
                break;
        }
    }
}
