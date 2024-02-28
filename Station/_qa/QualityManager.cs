using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LeadMeLabsLibrary;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sentry;
using Station._commandLine;
using Station._manager;
using Station._models;
using Station._notification;
using Station._profiles;
using Station._qa.checks;
using Station._utils;
using Station._utils._steamConfig;
using Station._wrapper;

namespace Station._qa;

public static class QualityManager
{
    private static readonly NetworkChecks NetworkChecks = new();
    private static readonly ImvrChecks ImvrChecks = new();
    private static readonly WindowChecks WindowChecks = new();
    private static readonly ConfigurationChecks ConfigurationChecks = new();
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
                    
                    case "configuration_checks":
                        result = JsonConvert.SerializeObject(ConfigurationChecks.RunQa(labType));
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
                
                // Safe cast and null checks
                VrProfile? vrProfile = Profile.CastToType<VrProfile>(SessionController.StationProfile);
                if (vrProfile?.VrHeadset == null) break;

                responseData.Add("result", vrProfile.VrHeadset?.GetStatusManager().GetStatusesJson());
                Manager.SendResponse("NUC", "QA", response.ToString());
                break;
            }
            
            default:
                MockConsole.WriteLine($"Unknown QA request {additionalData}", MockConsole.LogLevel.Normal);
                break;
        }
    }
    
    /// <summary>
    /// Run the requested software checks after an update or other significant event. These details are uploaded to
    /// Firebase and displayed on the QA UI page.
    /// </summary>
    public static async void HandleLocalQualityAssurance(bool upload)
    {
        string location = Environment.GetEnvironmentVariable("LabLocation", EnvironmentVariableTarget.Process) ?? "Unknown";
        
        // Check if there is a network connection (or if it is Adelaide/Australian Science and Mathematics School)
        ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage($"SoftwareState,Checking network"), TimeSpan.FromSeconds(0));
        if (location.ToLower().Contains("science and mathematics school") || !Network.CheckIfConnectedToInternet(true)) return;
        
        // Check if the QA has already been uploaded
        if (HasUploadAlreadyBeenCompleted()) return;
        ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage($"SoftwareState,Running QA"), TimeSpan.FromSeconds(0));
        
        Dictionary<string, Dictionary<string, QaCheck>> qaCheckDictionary = new();
        
        // Transform and add checks to the dictionary
        AddChecksToDictionary("Window Checks", WindowChecks.RunQa(labType));
        AddChecksToDictionary("Configuration Checks", ConfigurationChecks.RunQa(labType));
        AddChecksToDictionary("Software Checks", await SoftwareChecks.RunQa(labType));
        AddChecksToDictionary("Network Checks", NetworkChecks.RunQa(""));
        AddChecksToDictionary("Steam Config Checks", SteamConfigChecks.RunQa(labType));

        // Convert to JObject
        JObject jsonObject = JObject.FromObject(qaCheckDictionary);
        
        // Get the current timestamp
        DateTimeOffset timestamp = DateTimeOffset.Now;
        // Convert the timestamp to the local time zone
        DateTimeOffset localTime = timestamp.ToLocalTime();
        // Convert the DateTimeOffset to a human-readable string format
        string readableTime = localTime.ToString("dddd, MMMM dd, yyyy 'at' h:mm:ss tt");
        
        // Add it with an '_' so it will always be in the same position on Firebase
        jsonObject.Add("_Timestamp", readableTime);
        
        // Upload to Firebase
        if (upload)
        {
            UploadToFirebase(jsonObject);
        }

        return;

        // Method to add checks to the dictionary
        void AddChecksToDictionary(string key, List<QaCheck> checks)
        {
            Dictionary<string, QaCheck> checkDictionary = checks.ToDictionary(qaCheck => qaCheck.Id);
            qaCheckDictionary.Add(key, checkDictionary);
        }
    }

    /// <summary>
    /// Upload a string version of the QA check results list. 
    /// </summary>
    /// <param name="qaCheckObject">A JObject of QaChecks, sorted under their type and then id.</param>
    private static async void UploadToFirebase(JObject qaCheckObject)
    {
        using var httpClient = new HttpClient();

        string versionNumber = Updater.GetVersionNumberHyphen();
        string stationId = Environment.GetEnvironmentVariable("StationId", EnvironmentVariableTarget.Process) ?? "Unknown";
        string location = Environment.GetEnvironmentVariable("LabLocation", EnvironmentVariableTarget.Process) ?? "Unknown";
        string strJson = JsonConvert.SerializeObject(qaCheckObject);
 
        StringContent objData = new StringContent(strJson, Encoding.UTF8, "application/json");
        var result = await httpClient.PatchAsync(
            $"https://leadme-labs-default-rtdb.asia-southeast1.firebasedatabase.app/lab_qa_checks/{location}/{versionNumber}/{stationId}.json",
            objData
        );

        if (result.IsSuccessStatusCode)
        {
            Logger.WriteLog($"Uploaded QA test results to Firebase.", MockConsole.LogLevel.Normal);
        }
        else
        {
            Logger.WriteLog($"Qa check capture failed with response code {result.StatusCode}", MockConsole.LogLevel.Normal);
            SentrySdk.CaptureMessage($"Qa check capture failed with response code {result.StatusCode}");
        }
        
        // Achieve that the upload has either succeed or failed
        if (CommandLine.StationLocation == null) return;

        string? version = Updater.GetVersionNumber();
        string details = $"{version}\n{result.IsSuccessStatusCode}";
        WriteFile(CommandLine.StationLocation, details);
    }

    /// <summary>
    /// Checks if the upload process has already been completed based on a stored version number and a boolean flag.
    /// </summary>
    /// <returns>
    /// Returns true if the upload process has already been completed and the software version matches the stored version number,
    /// or if the stored boolean flag is true. Returns false otherwise.
    /// </returns>
    private static bool HasUploadAlreadyBeenCompleted()
    {
        string? version = Updater.GetVersionNumber();

        if (CommandLine.StationLocation == null || version == null) return false;

        // Path to the saved file
        string filePath = $"{CommandLine.StationLocation}\\_logs\\uploaded.txt";

        // Check if the file exists
        if (!File.Exists(filePath))
        {
            return false;
        }

        // Current version of your software
        Version? currentVersion = new Version(version);

        try
        {
            // Read all lines from the file
            string[] lines = File.ReadAllLines(filePath);

            // Parse version number from the first line
            if (lines.Length == 0)
            {
                Logger.WriteLog("HasUploadAlreadyBeenCompleted - File is empty - Uploading.", MockConsole.LogLevel.Normal);
                return false;
            }
            
            Version fileVersion = new Version(lines[0]);
                
            // Compare version numbers
            int versionComparison = currentVersion.CompareTo(fileVersion);
            switch (versionComparison)
            {
                case < 0:
                    Logger.WriteLog("HasUploadAlreadyBeenCompleted - File version is greater than current software version - Uploading", MockConsole.LogLevel.Normal);
                    return false;
                    
                case 0 when lines.Length > 1:
                {
                    // Parse boolean value from the second line
                    if (bool.TryParse(lines[1], out bool isEnabled))
                    {
                        Logger.WriteLog($"HasUploadAlreadyBeenCompleted - Version numbers match. Second line value: {isEnabled}", MockConsole.LogLevel.Normal);
                        return isEnabled;
                    }

                    Logger.WriteLog("HasUploadAlreadyBeenCompleted - Second line does not contain a valid boolean value - Uploading", MockConsole.LogLevel.Normal);
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.WriteLog($"HasUploadAlreadyBeenCompleted - An error occurred: {ex.Message}", MockConsole.LogLevel.Normal);
            return false;
        }

        return false;
    }

    private static void WriteFile(string location, string version)
    {
        File.WriteAllText($"{location}\\_logs\\uploaded.txt", version);
    }
}
