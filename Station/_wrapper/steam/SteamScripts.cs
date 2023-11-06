﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LeadMeLabsLibrary.Station;

namespace Station
{
    public static class SteamScripts
    {
        public const string SteamManifest = @"C:\Program Files (x86)\Steam\config\steamapps.vrmanifest";

        // login details as formatted "username password" - need to hide this/turn into a secret
        private static string loginDetailsAnonymous = "anonymous";
        private static string loginDetails = Environment.GetEnvironmentVariable("SteamUserName", EnvironmentVariableTarget.Process) + " " + 
            Environment.GetEnvironmentVariable("SteamPassword", EnvironmentVariableTarget.Process);

        private static string loginAnonymous = $"+login {loginDetailsAnonymous}";
        private static string loginUser = $"+login {loginDetails}";

        //Important to keep the initial space in all commands after the login
        private static string installed = " +apps_installed";
        private static string licenses = " +licenses_print ";
        private static string quit = " +quit";

        public static bool launchingGame = false;
        public static bool popupDetect = false;
        public static string steamCMDConfigured = "Missing";

        private static string availableGames = "";
        private static bool refreshing = false; //keep track of if the station is currently refreshing the steam list

        private static int restartAttempts = 0; //Track how many times SteamVR has failed in a Station session

        /// <summary>
        /// If the vrmonitor process is running but OpenVR has not established a connection, check Steam's vrserver logs
        /// to see if the '[Steam] Steam SHUTDOWN' entry is present.
        /// </summary>
        public static void CheckForSteamLogError()
        {
            try
            {
                string filePath = "C:/Program Files (x86)/Steam/logs/vrserver.txt";
                ReverseLineReader reverseLineReader = new ReverseLineReader(filePath, Encoding.UTF8);
                IEnumerator<string> enumerator = reverseLineReader.GetEnumerator();
                do
                {
                    string current = enumerator.Current;
                    if (current == null)
                    {
                        continue;
                    }
                    if (current.Contains("[Steam] Steam SHUTDOWN"))
                    {
                        //Double check that the vrmonitor program is still running in case it picked up the end of a session/restart vr session.
                        Task.Delay(3000).Wait(); //vrserver takes 2.8 seconds to fully shut down
                        if (Process.GetProcessesByName("vrmonitor").Length == 0)
                        {
                            enumerator.Dispose();
                            break;
                        }

                        restartAttempts++;

                        //Send a message to the tablet advising the Station be restarted.
                        if(restartAttempts > 2)
                        {
                            Logger.WriteLog("CheckForSteamLogError - SteamVR Error: restarts failed, sending message to tablet.", MockConsole.LogLevel.Normal);
                            Manager.SendResponse("Android", "Station", "SteamVRError");
                            break;
                        }

                        Logger.WriteLog("CheckForSteamLogError - SteamVR Error: restarting SteamVR", MockConsole.LogLevel.Normal);

                        //Kill SteamVR
                        CommandLine.QueryVRProcesses(new List<string> { "vrmonitor" }, true);

                        Task.Delay(5000).Wait();

                        //Relaunch SteamVR
                        SteamWrapper.LauncherSteamVR();

                        enumerator.Dispose();
                    }

                    //Steam always separates a new vrserver session from older ones using the following string
                    if (current.Contains("================================================================================================"))
                    {
                        restartAttempts = 0;
                        MockConsole.WriteLine("SteamVR awaiting headset connection", MockConsole.LogLevel.Verbose);
                        enumerator.Dispose();
                    }
                } while (enumerator.MoveNext());
            } catch (Exception e)
            {
                MockConsole.WriteLine($"CheckForSteamLogError - Reading vrserver file failed: {e}", MockConsole.LogLevel.Normal);
            }
        }

        /// <summary>
        /// Run a basic Steam Command query to check if the Steam Guard has been configured or not. Afterwards restart
        /// the VR processes so that Steam GUI is not disconnected.
        /// </summary>
        public static void QuerySteamConfig()
        {
            CommandLine.ExecuteSteamCommand(loginUser + licenses + quit);
            _ = WrapperManager.RestartVRProcesses();
        }

        /// <summary>
        /// Create a command to pass user details and a steam guard code to a command line process in order
        /// to set the SteamCMD steam gaurd.
        /// </summary>
        /// <param name="guardKey">A 5 digit string representing the SteamGuard</param>
        public static void ConfigureSteamCommand(string guardKey)
        {
            string command = "\"+force_install_dir \\\"C:/Program Files (x86)/Steam\\\"\" ";
            command += $"{loginUser} {guardKey} {quit}";
            CommandLine.MonitorSteamConfiguration(command);
        }

        /// <summary>
        /// Filter through the steam command output to select the Application IDs and names.
        /// Joining them together with specific delimiters to enable the Android tablet to
        /// decipher them.
        /// </summary>
        /// <returns>A string of IDs and names of installed applications</returns>
        public static List<string>? LoadAvailableGames()
        {
            //Close Steam if it is open
            CommandLine.QueryVRProcesses(WrapperMonitoringThread.SteamProcesses, true);

            if (!Network.CheckIfConnectedToInternet())
            {
                return LoadAvailableGamesWithoutUsingInternetConnection();
            }
            else
            {
                return LoadAvailableGamesUsingInternetConnection();
            }
        }

        private static List<string> AddInstalledSteamApplicationsFromDirectoryToList(List<string> list, string directoryPath)
        {
            List<string> blacklistedGames = new List<string>();
            blacklistedGames.Add("1635730"); // vive console // todo this needs to be abstracted
            if (Directory.Exists(directoryPath))
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(directoryPath);
                foreach (var file in directoryInfo.GetFiles("appmanifest_*.acf"))
                {
                    AcfReader acfReader = new AcfReader(file.FullName, true);
                    acfReader.ACFFileToStruct();
                    if (acfReader.gameName != null && acfReader.appId != null)
                    {
                        if (blacklistedGames.Contains(acfReader.appId))
                        {
                            continue;
                        }
                        list.Add($"{SteamWrapper.WrapperType}|{acfReader.appId}|{acfReader.gameName}");
                        WrapperManager.StoreApplication(SteamWrapper.WrapperType, acfReader.appId, acfReader.gameName); // todo, I don't like this line here as it's a side-effect to the function
                    }
                }
            }

            return list;
        }

        private static List<string>? LoadAvailableGamesWithoutUsingInternetConnection()
        {
            List<string> installedGames = new List<string>();

            installedGames =
                AddInstalledSteamApplicationsFromDirectoryToList(installedGames, "S:\\SteamLibrary\\steamapps");
            installedGames =
                AddInstalledSteamApplicationsFromDirectoryToList(installedGames, "C:\\Program Files (x86)\\Steam\\steamapps");

            return installedGames;
        }

        private static List<string>? LoadAvailableGamesUsingInternetConnection()
        {
            //Check if SteamCMD has been initialised
            string filePath = CommandLine.stationLocation + @"\external\steamcmd\steamerrorreporter.exe";
            
            if(!File.Exists(filePath))
            {
                Logger.WriteLog($"SteamCMD not initialised yet. Initialising now.", MockConsole.LogLevel.Error);
                Manager.SendResponse("Android", "Station", "SetValue:steamCMD:required");

                steamCMDConfigured = "Missing";

                //Login to initialise/update SteamCMD and get the Steam Guard email sent off
                string command = $"{loginDetails} {quit}";
                CommandLine.ExecuteSteamCommand(command);
                
                return null;
            }

            string? steamResponse = CommandLine.ExecuteSteamCommand(loginAnonymous + installed + quit);
            if(steamResponse == null)
            {
                return null;
            }

            List<string> installedGames = steamResponse.Split('\n').ToList();

            if (Directory.Exists("S:\\SteamLibrary\\steamapps"))
            {
                string? additionalSteamResponse = CommandLine.ExecuteSteamCommandSDrive(loginAnonymous + installed + quit);
                if (additionalSteamResponse == null)
                {
                    return null;
                }
                List<string> additionalInstalledGames = additionalSteamResponse.Split('\n').ToList();
                installedGames.AddRange(additionalInstalledGames);
            }

            List<string>? licenseList = CommandLine.ExecuteSteamCommand(loginUser + licenses + quit)?.Split('\n').ToList();
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
                            filter.RemoveAt(0);
                            filter.RemoveAt(filter.Count - 1); // remove file location
                            filter.RemoveAt(filter.Count - 1); // remove drive name
                            string name = string.Join(":", filter.ToArray()).Replace("\\", "").Trim();
                            if (name.Contains("appid_")) // as a backup if steamcmd doesn't load the game name, we get it from the acf file
                            {
                                AcfReader acfReader = new AcfReader(ID);
                                acfReader.ACFFileToStruct();
                                if (acfReader.gameName != null)
                                {
                                    name = acfReader.gameName;
                                }
                            }
                            string application = $"{SteamWrapper.WrapperType}|{ID}|{name}";

                            //item.parameters may be null here
                            WrapperManager.StoreApplication(SteamWrapper.WrapperType, ID, name);
                            apps.Add(application);
                        }
                    }
                }
            }

            availableGames = string.Join('/', apps);

            return apps;
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
