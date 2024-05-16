using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using LeadMeLabsLibrary;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sentry;
using Station.Components._commandLine;
using Station.Components._notification;

namespace Station.Components._utils._steamConfig;

public class SteamConfig
{
    private static string steamId = "";
    private static string location = Environment.GetEnvironmentVariable("LabLocation", EnvironmentVariableTarget.Process) ?? "Unknown";
    private static string stationId = Environment.GetEnvironmentVariable("StationId", EnvironmentVariableTarget.Process) ?? "Unknown";
    private static bool steamStatsReported = false;

    public static void VerifySteamConfig(bool initialCall = false)
    {
        location = Environment.GetEnvironmentVariable("LabLocation", EnvironmentVariableTarget.Process) ?? "Unknown";
        stationId = Environment.GetEnvironmentVariable("StationId", EnvironmentVariableTarget.Process) ?? "Unknown";
        GetSteamId();
        UpdateSteamVRSettings();
        VerifySteamLoginUserConfig();
        VerifySteamDefaultPageConfig();
        // VerifySteamHideNotificationConfig();
        VerifyConfigSharedConfigFile();

        if (initialCall)
        {
            RoomSetup.CompareRoomSetup();
        }
        ReadAndReportSteamStats();
    }

    public static string GetSteamId()
    {
        if (steamId.Length > 0)
        {
            return steamId;
        }
        string fileLocation = "C:\\Program Files (x86)\\Steam\\config\\config.vdf";
        if (!File.Exists(fileLocation))
        {
            Logger.WriteLog(
                "Could not get steamid " +
                location, Enums.LogLevel.Error);
            return "";
        }

        string? username = Environment.GetEnvironmentVariable("SteamUserName", EnvironmentVariableTarget.Process);
        if (string.IsNullOrEmpty(username))
        {
            Logger.WriteLog(
                "Could not get SteamUserName from environment variables " +
                location, Enums.LogLevel.Error);
            return "";
        }

        username = username.ToLower();

        try
        {
            string[] lines = File.ReadAllLines(fileLocation);
            string steamCommId = "";
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(username))
                {
                    if (lines[i + 2].Contains("SteamID"))
                    {
                        steamCommId = lines[i + 2].Replace("\t", "").Replace("\"SteamID\"", "").Replace("\"", "");
                        break;
                    }
                }
            }

            long steamComm = 76561197960265728;
            if (string.IsNullOrEmpty(steamCommId))
            {
                Logger.WriteLog(
                "Could not get steamId: " +
                location, Enums.LogLevel.Error);
                return "";
            }

            steamId = (long.Parse(steamCommId) - steamComm).ToString();
        }
        catch (Exception e)
        {
            Logger.WriteLog($"GetSteamId - Sentry Exception: {e}", Enums.LogLevel.Error);
            SentrySdk.CaptureException(e);
        }

        return steamId;
    }

    private static void VerifySteamHideNotificationConfig()
    {
        if (steamId.Length == 0)
        {
            Logger.WriteLog(
                "Could not find steamId: " +
                location, Enums.LogLevel.Error);
            return;
        }
        string fileLocation = $"C:\\Program Files (x86)\\Steam\\userdata\\{steamId}\\config\\localconfig.vdf";
        if (!File.Exists(fileLocation))
        {
            Logger.WriteLog(
                "Could not verify steam hide notification info: " +
                location, Enums.LogLevel.Error);
            return;
        }

        bool didNotifyAvailableGames = false;
        try
        {
            string[] lines = File.ReadAllLines(fileLocation);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("NotifyAvailableGames"))
                {
                    lines[i] = lines[i].Replace("1", "0");
                    didNotifyAvailableGames = true;
                }
            }

            if (!didNotifyAvailableGames)
            {
                List<string> linesList = new();
                linesList.AddRange(lines);
                linesList.Insert(9, "\t}");
                linesList.Insert(9, "\t\t\"NotifyAvailableGames\"\t\t\"0\"");
                linesList.Insert(9, "\t{");
                linesList.Insert(9, "\t\"News\"");
                lines = linesList.ToArray();
            }

            File.WriteAllLines(fileLocation, lines);
        }
        catch (Exception e)
        {
            Logger.WriteLog($"VerifySteamHideNotificationConfig - Sentry Exception: {e}", Enums.LogLevel.Error);
            SentrySdk.CaptureException(e);
        }
    }
    
    public static List<string> GetAcceptedEulasForAppId(string appId)
    {
        if (steamId.Length == 0)
        {
            Logger.WriteLog(
                "Could not find steamId: " +
                location, Enums.LogLevel.Error);
            return new List<string>();
        }
        string fileLocation = $"C:\\Program Files (x86)\\Steam\\userdata\\{steamId}\\config\\localconfig.vdf";
        if (!File.Exists(fileLocation))
        {
            Logger.WriteLog(
                "Could not verify steam hide notification info: " +
                location, Enums.LogLevel.Error);
            return new List<string>();
        }


        List<string> acceptedEulas = new List<string>();
        try
        {
            string[] lines = File.ReadAllLines(fileLocation);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Equals("\t\t\t\t\t\"" + appId + "\""))
                {
                    for (int j = i; !lines[j].Equals("\t\t\t\t\t}"); j++)
                    {
                        if (lines[j].Contains("eula"))
                        {
                            acceptedEulas.Add(lines[j].Trim('\"').Trim('0').Trim('1').Trim('\"').Trim('\t').Trim('\"'));
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Logger.WriteLog($"GetAcceptedEulasForAppId - Sentry Exception: {e}", Enums.LogLevel.Error);
            SentrySdk.CaptureException(e);
        }

        return acceptedEulas;
    }
    
    public static List<string> GetAllAcceptedEulas()
    {
        if (steamId.Length == 0)
        {
            Logger.WriteLog(
                "Could not find steamId: " +
                location, MockConsole.LogLevel.Error);
            return new List<string>();
        }
        string fileLocation = $"C:\\Program Files (x86)\\Steam\\userdata\\{steamId}\\config\\localconfig.vdf";
        if (!File.Exists(fileLocation))
        {
            Logger.WriteLog(
                "Could not verify steam hide notification info: " +
                location, MockConsole.LogLevel.Error);
            return new List<string>();
        }


        List<string> acceptedEulas = new List<string>();
        try
        {
            string[] lines = File.ReadAllLines(fileLocation);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("eula"))
                {
                    // The line will look like the below
                    // 348250_eula_0        "0"
                    // ^ Eula name           ^ Eula version
                    string[] eulaDetails = lines[i].Trim('\t').Trim('\"').Split("\t");
                    string eulaName = eulaDetails[0].Trim('\"');
                    string appId = eulaName.Split('_')[0];
                    string eulaVersion = eulaDetails[2].Trim('\"');
                    acceptedEulas.Add($"{appId}:{eulaName}:{eulaVersion}");
                }
            }
        }
        catch (Exception e)
        {
            Logger.WriteLog($"GetAllAcceptedEulas - Sentry Exception: {e}", MockConsole.LogLevel.Error);
            SentrySdk.CaptureException(e);
        }

        return acceptedEulas;
    }

    public static List<string> GetAllEulas()
    {
        string filePath = CommandLine.StationLocation + @"\_embedded\LeadMePython.exe";
        if (!File.Exists(filePath))
        {
            return new List<string>();
        }

        string output = CommandLine.RunProgramWithOutput(filePath, "all_eulas");
        List<string> eulas = new List<string>();
        eulas.AddRange(output.Split("\n"));
        return eulas;
    }
    
    public static List<string> GetAllLicenses()
    {
        string filePath = CommandLine.StationLocation + @"\_embedded\LeadMePython.exe";
        if (!File.Exists(filePath))
        {
            return new List<string>() {""};
        }

        string output = CommandLine.RunProgramWithOutput(filePath, $"licenses {Environment.GetEnvironmentVariable("SteamUserName", EnvironmentVariableTarget.Process)} {Environment.GetEnvironmentVariable("SteamPassword", EnvironmentVariableTarget.Process)}");
        List<string> licensedAppIds = new List<string>();
        licensedAppIds.AddRange(output.Split("\n"));
        return licensedAppIds;
    }

    public static List<string> GetUnacceptedEulas()
    {
        List<string> acceptedEulas = GetAllAcceptedEulas();
        List<string> allEulas = GetAllEulas();
        return allEulas.Except(acceptedEulas).ToList();
    }

    private static void VerifyConfigSharedConfigFile()
    {
        if (steamId.Length == 0)
        {
            Logger.WriteLog(
                "Could not find steamId: " +
                location, Enums.LogLevel.Error);
            return;
        }
        string fileLocation = $"C:\\Program Files (x86)\\Steam\\userdata\\{steamId}\\config\\sharedconfig.vdf";

        if (File.Exists(fileLocation))
        {
            try
            {
                string[] lines = File.ReadAllLines(fileLocation);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Contains("CloudEnabled"))
                    {
                        lines[i] = lines[i].Replace("1", "0");
                    }
                }

                File.WriteAllLines(fileLocation, lines);
            }
            catch (Exception e)
            {
                Logger.WriteLog($"VerifyConfigSharedConfigFile - Sentry Exception: {e}", Enums.LogLevel.Error);
                SentrySdk.CaptureException(e);
            }
        }
        else
        {
            try 
            {
                string[] lines = new[]
                {
                    "\"UserRoamingConfigStore\"",
                    "{",
                    "\t\"Software\"",
                    "\t{",
                    "\t\t\"Valve\"",
                    "\t\t{",
                    "\t\t\t\"Steam\"",
                    "\t\t\t{",
                    "\t\t\t\t\"CloudEnabled\"\t\t\"0\"",
                    "\t\t\t}",
                    "\t\t}",
                    "\t}",
                    "}"
                };

                File.WriteAllLines(fileLocation, lines);
            }
            catch (Exception e)
            {
                Logger.WriteLog($"VerifyConfigSharedConfigFile - Sentry Exception: {e}", Enums.LogLevel.Error);
                SentrySdk.CaptureException(e);
            }
        }
    }

    private static void VerifySteamDefaultPageConfig()
    {
        if (steamId.Length == 0)
        {
            Logger.WriteLog(
                "Could not find steamId: " +
                location, Enums.LogLevel.Error);
            return;
        }
        string fileLocation = $"C:\\Program Files (x86)\\Steam\\userdata\\{steamId}\\7\\remote\\sharedconfig.vdf";
        if (!File.Exists(fileLocation))
        {
            Logger.WriteLog(
                "Could not verify steam default page info: " +
                location, Enums.LogLevel.Error);
            return;
        }

        bool didCloudSetting = false;
        bool didDefaultDialogSetting = false;
        try
        {
            string[] lines = File.ReadAllLines(fileLocation);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("SteamDefaultDialog"))
                {
                    lines[i] = lines[i].Replace("#app_store", "#app_games");
                    lines[i] = lines[i].Replace("#app_news", "#app_games");
                    lines[i] = lines[i].Replace("#steam_menu_friend_activity", "#app_games");
                    lines[i] = lines[i].Replace("#steam_menu_community_home", "#app_games");
                    didDefaultDialogSetting = true;
                }
                if (lines[i].Contains("CloudEnabled"))
                {
                    lines[i] = lines[i].Replace("1", "0");
                    didCloudSetting = true;
                }
            }

            if (!didCloudSetting)
            {
                List<string> linesList = new();
                linesList.AddRange(lines);
                linesList.Insert(9, "\t\t\t\t\"CloudEnabled\"\t\t\"0\"");
                lines = linesList.ToArray();
            }
            if (!didDefaultDialogSetting)
            {
                List<string> linesList = new();
                linesList.AddRange(lines);
                linesList.Insert(9, "\t\t\t\t\"SteamDefaultDialog\"\t\t\"#app_games\"");
                lines = linesList.ToArray();
            }

            File.WriteAllLines(fileLocation, lines);
        }
        catch (Exception e)
        {
            Logger.WriteLog($"VerifySteamDefaultPageConfig - Sentry Exception: {e}", Enums.LogLevel.Error);
            SentrySdk.CaptureException(e);
        }
    }

    private static void VerifySteamLoginUserConfig()
    {
        string fileLocation = "C:\\Program Files (x86)\\Steam\\config\\loginusers.vdf";
        if (!File.Exists(fileLocation))
        {
            Logger.WriteLog(
                "Could not verify steam login info: " +
                location, Enums.LogLevel.Error);
            return;
        }

        try
        {
            string[] lines = File.ReadAllLines(fileLocation);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("SkipOfflineModeWarning"))
                {
                    lines[i] = lines[i].Replace("0", "1");
                }

                if (lines[i].Contains("AllowAutoLogin"))
                {
                    lines[i] = lines[i].Replace("0", "1");
                }

                if (lines[i].Contains("WantsOfflineMode"))
                {
                    if (!Network.CheckIfConnectedToInternet())
                    {
                        lines[i] = lines[i].Replace("0", "1");
                    }
                }
            }

            File.WriteAllLines(fileLocation, lines);
        }
        catch (Exception e)
        {
            Logger.WriteLog($"VerifySteamLoginUserConfig - Sentry Exception: {e}", Enums.LogLevel.Error);
            SentrySdk.CaptureException(e);
        }
    }
    
    private static async void ReadAndReportSteamStats()
    {
        if (steamStatsReported)
        {
            return;
        }

        if (steamId.Length == 0)
        {
            Logger.WriteLog(
                "Could not find steamId: " +
                location, Enums.LogLevel.Error);
            return;
        }

        string fileLocation = $"C:\\Program Files (x86)\\Steam\\userdata\\{steamId}\\config\\localconfig.vdf";
        if (!File.Exists(fileLocation))
        {
            Logger.WriteLog(
                "Could not get steam stats: " +
                location, Enums.LogLevel.Error);
            return;
        }
        
        if (!Network.CheckIfConnectedToInternet())
        {
            return;
        }

        try
        {
            Task.Run(async () =>
            {
                Dictionary<String, String> experienceStats = new Dictionary<string, string>();
                using (var fs = new FileStream(fileLocation, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 0x1000, FileOptions.SequentialScan))
                using (var sr = new StreamReader(fs, Encoding.UTF8))
                {
                    string line;
                    string mostRecentId = "";
                    bool startReadingApps = false;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (!startReadingApps && line.Equals("\t\t\t\t\"apps\""))
                        {
                            startReadingApps = true;
                            continue;
                        }

                        if (line.Equals("\t\t\t\t}"))
                        {
                            break;
                        }

                        if (!startReadingApps)
                        {
                            continue;
                        }

                        if (line.StartsWith("\t\t\t\t\t\""))
                        {
                            mostRecentId = line.Trim('\t').Trim('\"');
                            continue;
                        }

                        if (line.Equals("\t\t\t\t\t}"))
                        {
                            mostRecentId = "";
                            continue;
                        }

                        if (mostRecentId.Length > 0 && line.StartsWith("\t\t\t\t\t\t\"Playtime\""))
                        {
                            experienceStats.Add(mostRecentId, line.Remove(0, 19).Trim('\"'));
                        }
                    }
                }

                if (experienceStats.Count > 0)
                {
                    using var httpClient = new HttpClient();
                    string strJSON = "{";
                    var datetime = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
                    foreach (KeyValuePair<string,string> experienceStat in experienceStats)
                    {
                        strJSON += String.Format("\"{0}/{1}\": {2},", experienceStat.Key, datetime, experienceStat.Value);
                    }

                    strJSON = strJSON.Remove(strJSON.Length - 1, 1);
                    strJSON += "}";
                    StringContent objData = new StringContent(strJSON, Encoding.UTF8, "application/json");
                    var result = await httpClient.PatchAsync(
                        $"https://leadme-labs-default-rtdb.asia-southeast1.firebasedatabase.app/lab_experience_playtime/{location}/{stationId}.json",
                        objData
                    );
                    if (result.IsSuccessStatusCode)
                    {
                        steamStatsReported = true;
                    }
                    else
                    {
                        string msg = $"Steam capture failed with response code {result.StatusCode}";
                        Logger.WriteLog($"VerifySteamLoginUserConfig - Sentry Message: {msg}", Enums.LogLevel.Error);
                        SentrySdk.CaptureMessage(msg);
                    }
                }
            });
        }
        catch (Exception e)
        {
            Logger.WriteLog($"ReadAndReportSteamStats - Sentry Exception: {e}", Enums.LogLevel.Error);
            SentrySdk.CaptureException(e);
        }
    }
    
    private static void UpdateSteamVRSettings()
    {
        string fileLocation = "C:\\Program Files (x86)\\Steam\\config\\steamvr.vrsettings";
        if (!File.Exists(fileLocation))
        {
            Logger.WriteLog(
                "Could not update SteamVR settings: " +
                location, Enums.LogLevel.Error);
            return;
        }

        try
        {
            string lines = File.ReadAllText(fileLocation);
            JObject json = (JObject) JsonConvert.DeserializeObject(lines);

            json.TryAdd("steamvr", new JObject());
            json.TryAdd("power", new JObject());
            json.TryAdd("dashboard", new JObject());
            json.TryAdd("userinterface", new JObject());
            json["steamvr"]["enableHomeApp"] = false;
            json["power"]["turnOffScreensTimeout"] = 1800;
            json["power"]["turnOffControllersTimeout"] = 0;
            json["power"]["pauseCompositorOnStandby"] = false;
            json["dashboard"]["enableDashboard"] = false;
            json["userinterface"]["StatusAlwaysOnTop"] = false;
            string? text = JsonConvert.SerializeObject(json);
            if (text != null)
            {
                File.WriteAllText(fileLocation, text);
            }
        }
        catch (Exception e)
        {
            Logger.WriteLog($"UpdateSteamVRSettings - Sentry Exception: {e}", Enums.LogLevel.Error);
            SentrySdk.CaptureException(e);
        }
    }
}
