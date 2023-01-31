using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Station
{
    public static class SteamScripts
    {
        // login details as formatted "username password" - need to hide this/turn into a secret
        private static string loginDetailsAnonymous = "anonymous";
        private static string loginDetails = Environment.GetEnvironmentVariable("SteamUserName") + " " + Environment.GetEnvironmentVariable("SteamPassword");

        private static string loginAnonymous = $"+login {loginDetailsAnonymous}";
        private static string loginUser = $"+login {loginDetails}";

        //Important to keep the initial space in all commands after the login
        private static string installed = " +apps_installed";
        private static string licenses = " +licenses_print ";
        private static string quit = " +quit";

        public static string lastAppId = "";
        public static bool launchingGame = false;
        public static bool popupDetect = false;

        private static string availableGames = "";
        private static bool refreshing = false; //keep track of if the station is currently refreshing the steam list

        public static async Task<string> getAvailableGames()
        {
            Logger.WriteLog("Get available games function", MockConsole.LogLevel.Verbose);

            // the load available games method is called on boot, we just need to wait for it to complete
            while (availableGames.Length == 0 || !Char.IsNumber(availableGames[0]))
            {
                Console.WriteLine("LOOPING");
                await Task.Delay(2000);
            }

            Logger.WriteLog(availableGames, MockConsole.LogLevel.Debug);

            return availableGames;
        }

        /// <summary>
        /// Filter through the steam command output to select the Application IDs and names.
        /// Joining them together with specific delimiters to enable the Android tablet to
        /// decipher them.
        /// </summary>
        /// <param name="output">A string representing the raw output from the steamcmd command.</param>
        /// <returns>A string of IDs and names of installed applications</returns>
        public static List<string>? loadAvailableGames()
        {
            //Close Steam if it is open
            CommandLine.queryVRProcesses(WrapperMonitoringThread.steamProcesses, true);

            string? steamResponse = CommandLine.executeSteamCommand(loginAnonymous + installed + quit);
            if(steamResponse == null)
            {
                return null;
            }

            List<string> installedGames = steamResponse.Split('\n').ToList();

            if (Directory.Exists("S:\\SteamLibrary\\steamapps"))
            {
                string? additionalSteamResponse = CommandLine.executeSteamCommandSDrive(loginAnonymous + installed + quit);
                if (additionalSteamResponse == null)
                {
                    return null;
                }
                List<string> additionalInstalledGames = additionalSteamResponse.Split('\n').ToList();
                installedGames.AddRange(additionalInstalledGames);
            }

            List<string>? licenseList = CommandLine.executeSteamCommand(loginUser + licenses + quit)?.Split('\n').ToList();
            if (licenseList == null)
            {
                return null;
            }

            List<string> apps = new List<string>();
            List<string> availableLicenses = new List<string>();
            List<string> approvedGames = getParentalApprovedGames();
            Logger.WriteLog("Approved games length: " + approvedGames.Count, MockConsole.LogLevel.Debug);
            List<string> blacklistedGames = new List<string>();
            blacklistedGames.Add("1635730"); // vive console

            foreach (var line in licenseList)
            {
                if (line.StartsWith(" - Apps"))
                {
                    availableLicenses.Add(line.Substring(10).Split(',')[0]);
                }
            }

            Logger.WriteLog("Within loadAvailableGames", MockConsole.LogLevel.Debug);

            foreach (var line in installedGames)
            {
                Console.WriteLine(line);
                if (line.StartsWith("AppID"))
                {
                    Logger.WriteLog(line, MockConsole.LogLevel.Debug);

                    List<string> filter = line.Split(":").ToList();
                    string ID = filter[0].Replace("AppID", "").Trim();

                    if (availableLicenses.Contains(ID))
                    {
                        if (!blacklistedGames.Contains(ID) && (approvedGames.Count == 0 || approvedGames.Contains(ID))) // if count is zero then all games are approved
                        {
                            string name = filter[1].Replace("\\", "").Trim();
                            if (name.Contains("appid_")) // as a backup if steamcmd doesn't load the game name, we get it from the acf file
                            {
                                AcfReader acfReader = new AcfReader(ID);
                                acfReader.ACFFileToStruct();
                                if (acfReader.gameName != null)
                                {
                                    name = acfReader.gameName;
                                }
                            }
                            string application = $"{SteamWrapper.wrapperType}|{ID}|{name}";
                            apps.Add(application);
                        }
                    }
                }
            }

            availableGames = String.Join('/', apps);

            return apps;
        }

        /// <summary>
        /// Triggered if the tablet/NUC does not have the list of steam games. Load the list if it is in memory
        /// or reload the list from SteamCMD and then restart the VR session to log steam back in.
        /// </summary>
        public static void resendSteamGames()
        {
            if (!refreshing)
            {
                refreshing = true;
                cooldownTimer();
                Logger.WriteLog("Re-loading AvailableGames", MockConsole.LogLevel.Debug);
                loadAvailableGames();

                SessionController.restartVRSession(); //Restart the VR session as to log steam back in
            }
        }

        /// <summary>
        /// Limit the number of Steam refreshes that can be performed within a given time period.
        /// </summary>
        private static async void cooldownTimer()
        {
            await Task.Delay(30 * 1000);
            refreshing = false;
        }

        public static List<string> getParentalApprovedGames()
        {
            List<string> approvedGames = new List<string>();
            var directory = new DirectoryInfo(@"C:\Program Files (x86)\Steam\logs");
            var files = directory.GetFiles("parental_log.txt")
                .OrderByDescending(f => f.LastWriteTime);
            if (!files.Any())
            {
                return approvedGames;
            }

            var file = files.First();
            ReverseLineReader reverseLineReader = new ReverseLineReader(file.FullName, Encoding.UTF8);
            IEnumerator<string> enumerator = reverseLineReader.GetEnumerator();
            do
            {
                string current = enumerator.Current;
                if (current == null)
                {
                    continue;
                }

                if (current.Contains("No custom list"))
                {
                    Logger.WriteLog("No custom list of steam applications", MockConsole.LogLevel.Error);
                    break;
                }
                if (current.Contains("Custom list"))
                {
                    Logger.WriteLog("Reached end of parental approved list", MockConsole.LogLevel.Error);
                    break;
                }

                if (current.Contains("(allowed)"))
                {
                    string[] lineParts = current.Split(" ");
                    if (lineParts.Length > 2)
                    {
                        approvedGames.Add(lineParts[2].Trim());
                    }
                }
            } while (enumerator.MoveNext());
            enumerator.Dispose();
            return approvedGames;
        }
    }
}
