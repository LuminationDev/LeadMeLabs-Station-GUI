using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using LeadMeLabsLibrary;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
                
                case "QA":
                    HandleQualityAssurance(additionalData);
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
                    SessionController.vrHeadset?.GetStatusManager().QueryStatuses();
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
        
        /// <summary>
        /// Run the requested software check.
        /// </summary>
        private async void HandleQualityAssurance(string additionalData)
        {
            JObject requestData = JObject.Parse(additionalData);
            var action = requestData.GetValue("action").ToString();
            var actionData = (JObject) requestData.GetValue("actionData");

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
                
                            Manager.SendResponse("NUC", "QA", response.ToString());
                        }).Start();
                        break;
                    case "steam_config_checks":
                        result = JsonConvert.SerializeObject(qualityManager.steamConfigChecks.RunQa(labType ?? "Online"));
                        break;
                    case "network_checks":
                        result = JsonConvert.SerializeObject(qualityManager.networkChecks.RunQa(labType ?? "Online"));
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
                    SessionController.vrHeadset == null
                        ? null
                        : SessionController.vrHeadset.GetStatusManager().GetStatusesJson());
                Manager.SendResponse("NUC", "QA", response.ToString());
            }
            
            MockConsole.WriteLine($"Unknown QA request {this.additionalData}", MockConsole.LogLevel.Normal);

            //Request:ReturnAddress
            // todo - we can probably remove this - just want to keep it for another week (29/09/2023)
            // string[] split = additionalData.Split(":");
            // if (split.Length > 2)
            // {
            //     string? response = new QualityManager().DetermineCheck(split[0]);
            //     if (response == null)
            //     {
            //         return;
            //     }
            //     
            //     string? key = Environment.GetEnvironmentVariable("AppKey", EnvironmentVariableTarget.Process);
            //     if (key is null) {
            //         Logger.WriteLog("Encryption key not set", MockConsole.LogLevel.Normal);
            //         return;
            //     }
            //     
            //     SocketClient client = new(EncryptionHelper.Encrypt($"{split[0]}:{response}", key));
            //     client.Send(false, IPAddress.Parse(split[1]), int.Parse(split[2]));
            // }
        }
    }
}
