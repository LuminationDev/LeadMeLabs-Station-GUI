using Sentry;
using System;
using System.Collections.Generic;
using System.IO;

namespace Station
{
    public class SteamConfig
    {
        private static string steamId = "";
        private static readonly string location = Environment.GetEnvironmentVariable("LabLocation") ?? "Unknown";

        public static void VerifySteamConfig()
        {
            GetSteamId();
            VerifySteamLoginUserConfig();
            VerifySteamDefaultPageConfig();
            VerifySteamHideNotificationConfig();
            VerifyConfigSharedConfigFile();
        }

        public static void GetSteamId()
        {
            if (steamId.Length > 0)
            {
                return;
            }
            string fileLocation = "C:\\Program Files (x86)\\Steam\\config\\config.vdf";
            if (!File.Exists(fileLocation))
            {
                Logger.WriteLog(
                    "Could not get steamid " +
                    location, MockConsole.LogLevel.Error);
                return;
            }

            string? username = Environment.GetEnvironmentVariable("SteamUserName");
            if (string.IsNullOrEmpty(username))
            {
                Logger.WriteLog(
                    "Could not get SteamUserName from environment variables " +
                    location, MockConsole.LogLevel.Error);
                return;
            }

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
                    "Could not get steamid " +
                    location, MockConsole.LogLevel.Error);
                    return;
                }

                steamId = (long.Parse(steamCommId) - steamComm).ToString();
            }
            catch (Exception e)
            {
                SentrySdk.CaptureException(e);
            }
        }

        private static void VerifySteamHideNotificationConfig()
        {
            if (steamId.Length == 0)
            {
                Logger.WriteLog(
                    "Could not find steamId: " +
                    location, MockConsole.LogLevel.Error);
                return;
            }
            string fileLocation = $"C:\\Program Files (x86)\\Steam\\userdata\\{steamId}\\config\\localconfig.vdf";
            if (!File.Exists(fileLocation))
            {
                Logger.WriteLog(
                    "Could not verify steam hide notification info: " +
                    location, MockConsole.LogLevel.Error);
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
                SentrySdk.CaptureException(e);
            }
        }

        private static void VerifyConfigSharedConfigFile()
        {
            if (steamId.Length == 0)
            {
                Logger.WriteLog(
                    "Could not find steamId: " +
                    location, MockConsole.LogLevel.Error);
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
                    location, MockConsole.LogLevel.Error);
                return;
            }
            string fileLocation = $"C:\\Program Files (x86)\\Steam\\userdata\\{steamId}\\7\\remote\\sharedconfig.vdf";
            if (!File.Exists(fileLocation))
            {
                Logger.WriteLog(
                    "Could not verify steam default page info: " +
                    location, MockConsole.LogLevel.Error);
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
                    location, MockConsole.LogLevel.Error);
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
                        if (!CommandLine.CheckIfConnectedToInternet())
                        {
                            lines[i] = lines[i].Replace("0", "1");
                        }
                    }
                }

                File.WriteAllLines(fileLocation, lines);
            }
            catch (Exception e)
            {
                SentrySdk.CaptureException(e);
            }
        }
    }
}
