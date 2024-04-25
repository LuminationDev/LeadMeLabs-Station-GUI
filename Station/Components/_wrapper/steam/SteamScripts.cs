using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LeadMeLabsLibrary.Station;
using Newtonsoft.Json.Linq;
using Station.Components._commandLine;
using Station.Components._managers;
using Station.Components._models;
using Station.Components._monitoring;
using Station.Components._notification;
using Station.Components._utils;
using Station.MVC.Controller;

namespace Station.Components._wrapper.steam;

public static class SteamScripts
{
    public const string SteamManifest = @"C:\Program Files (x86)\Steam\config\steamapps.vrmanifest";

    // login details as formatted "username password" - need to hide this/turn into a secret
    private static readonly string LoginDetails = 
        Environment.GetEnvironmentVariable("SteamUserName", EnvironmentVariableTarget.Process) + " " + 
        Environment.GetEnvironmentVariable("SteamPassword", EnvironmentVariableTarget.Process);

    private const string LoginAnonymous = $"+login anonymous";
    private static readonly string LoginUser = $"+login {LoginDetails}";

    //Important to keep the initial space in all commands after the login
    private const string Installed = " +apps_installed";
    private const string Licenses = " +licenses_print ";
    private const string Quit = " +quit";
    
    public static bool popupDetect = false;
    public static string steamCmdConfigured = "Missing";

    private static int restartAttempts = 0; //Track how many times SteamVR has failed in a Station session
    
    //Experience constants and lists
    private static List<string> steamCmdInstalledGames = new();
    private static readonly List<string> BlacklistedGames = new() {"1635730"}; // vive console
    private static readonly List<string> AvailableLicenses = new();
    private static List<string> approvedGames = new();
    
    //Track experiences with no licenses or blocked by family mode
    public static List<string> noLicenses = new();
    public static List<string> blockedByFamilyMode = new();

    private static ManifestReader.ManifestApplicationList steamManifestApplicationList = new (SteamManifest);
    public static void RefreshVrManifest()
    {
        steamManifestApplicationList = new ManifestReader.ManifestApplicationList(SteamManifest);   
    }

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
                if (string.IsNullOrEmpty(current))
                {
                    continue;
                }
                if (current.Contains("[Steam] Steam SHUTDOWN"))
                {
                    //Double check that the vrmonitor program is still running in case it picked up the end of a session/restart vr session.
                    Task.Delay(3000).Wait(); //vrserver takes 2.8 seconds (at most) to fully shut down
                    if (ProcessManager.GetProcessesByName("vrmonitor").Length == 0)
                    {
                        enumerator.Dispose();
                        break;
                    }

                    restartAttempts++;

                    //Send a message to the tablet advising the Station be restarted.
                    if(restartAttempts > 2)
                    {
                        Logger.WriteLog("CheckForSteamLogError - SteamVR Error: restarts failed, sending message to tablet.", MockConsole.LogLevel.Normal);
                        MessageController.SendResponse("Android", "Station", "SteamVRError");
                        break;
                    }

                    Logger.WriteLog("CheckForSteamLogError - SteamVR Error: restarting SteamVR", MockConsole.LogLevel.Normal);

                    //Kill SteamVR
                    CommandLine.QueryProcesses(new List<string> { "vrmonitor" }, true);

                    Task.Delay(5000).Wait();

                    //Relaunch SteamVR
                    SteamWrapper.LaunchSteamVR();

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
        CommandLine.ExecuteSteamCommand(LoginUser + Licenses + Quit);
        _ = WrapperManager.RestartVrProcesses();
    }

    /// <summary>
    /// Create a command to pass user details and a steam guard code to a command line process in order
    /// to set the SteamCMD steam guard.
    /// </summary>
    /// <param name="guardKey">A 5 digit string representing the SteamGuard</param>
    public static void ConfigureSteamCommand(string guardKey)
    {
        string command = "\"+force_install_dir \\\"C:/Program Files (x86)/Steam\\\"\" ";
        command += $"{LoginUser} {guardKey} {Quit}";
        CommandLine.MonitorSteamConfiguration(command);
    }

    /// <summary>
    /// Loads available experiences of a generic type, T. Closes Steam processes if open, then checks network connection.
    /// If connected to the internet, loads available games using an internet connection; otherwise, loads available games without using an internet connection.
    /// </summary>
    /// <typeparam name="T">The type of experiences to load.</typeparam>
    /// <returns>A list of available experiences of type T, or null if no experiences are available.</returns>
    public static List<T>? LoadAvailableExperiences<T>()
    {
        //Close Steam if it is open
        CommandLine.QueryProcesses(WrapperMonitoringThread.SteamProcesses, true);
        CommandLine.QueryProcesses(WrapperMonitoringThread.SteamVrProcesses, true);

        return !Network.CheckIfConnectedToInternet() ? LoadAvailableGamesWithoutUsingInternetConnection<T>() : LoadAvailableGamesUsingInternetConnection<T>();
    }

    private static List<T> AddInstalledSteamApplicationsFromDirectoryToList<T>(List<T> list, string directoryPath)
    {
        approvedGames = GetParentalApprovedGames();
        
        Logger.WriteLog("Approved games length: " + approvedGames.Count, MockConsole.LogLevel.Debug);
        if (!Directory.Exists(directoryPath)) return list;
        
        DirectoryInfo directoryInfo = new DirectoryInfo(directoryPath);
        foreach (var file in directoryInfo.GetFiles("appmanifest_*.acf"))
        {
            AcfReader acfReader = new AcfReader(file.FullName, true);
            acfReader.ACFFileToStruct();
            if (string.IsNullOrEmpty(acfReader.gameName) || acfReader.appId == null) continue;
                
            if (BlacklistedGames.Contains(acfReader.appId))
            {
                continue;
            }

            if (approvedGames.Count != 0 && !approvedGames.Contains(acfReader.appId)) continue;
                    
            bool isVr =
                steamManifestApplicationList.IsApplicationInstalledAndVrCompatible("steam.app." + acfReader.appId);
            WrapperManager.StoreApplication(SteamWrapper.WrapperType, acfReader.appId, acfReader.gameName, isVr); // todo, I don't like this line here as it's a side-effect to the function
            if (!Helper.GetStationMode().Equals(Helper.STATION_MODE_VR) && isVr) continue;
                        
            // Basic application requirements
            if (typeof(T) == typeof(ExperienceDetails))
            {
                ExperienceDetails experience = new ExperienceDetails(SteamWrapper.WrapperType, acfReader.gameName, acfReader.appId, isVr);
                list.Add((T)(object)experience);
            }
            else if (typeof(T) == typeof(string))
            {
                string application =
                    $"{SteamWrapper.WrapperType}|{acfReader.appId}|{acfReader.gameName}";
                list.Add((T)(object)application);
            }
        }

        return list;
    }

    private static List<T> LoadAvailableGamesWithoutUsingInternetConnection<T>()
    {
        List<T> installedGames = new List<T>();

        installedGames =
            AddInstalledSteamApplicationsFromDirectoryToList<T>(installedGames, "S:\\SteamLibrary\\steamapps");
        installedGames =
            AddInstalledSteamApplicationsFromDirectoryToList<T>(installedGames, "C:\\Program Files (x86)\\Steam\\steamapps");

        return installedGames;
    }

    private static List<T>? LoadAvailableGamesUsingInternetConnection<T>()
    {
        //Check if SteamCMD has been initialised
        string filePath = CommandLine.StationLocation + @"\external\steamcmd\steamerrorreporter.exe";
        
        if(!File.Exists(filePath))
        {
            Logger.WriteLog($"SteamCMD not initialised yet. Initialising now.", MockConsole.LogLevel.Info);
            MessageController.SendResponse("Android", "Station", "SetValue:steamCMD:required");
            
            steamCmdConfigured = "Missing";

            //Login to initialise/update SteamCMD and get the Steam Guard email sent off
            string command = $"{LoginDetails} {Quit}";
            CommandLine.ExecuteSteamCommand(command);
            
            return null;
        }

        string? steamResponse = CommandLine.ExecuteSteamCommand(LoginAnonymous + Installed + Quit);
        if(steamResponse == null)
        {
            return null;
        }

        steamCmdInstalledGames = steamResponse.Split('\n').ToList();

        if (Directory.Exists("S:\\SteamLibrary\\steamapps"))
        {
            string? additionalSteamResponse = CommandLine.ExecuteSteamCommandSDrive(LoginAnonymous + Installed + Quit);
            if (additionalSteamResponse == null)
            {
                return null;
            }
            List<string> additionalInstalledGames = additionalSteamResponse.Split('\n').ToList();
            steamCmdInstalledGames.AddRange(additionalInstalledGames);
        }

        List<string>? licenseList = CommandLine.ExecuteSteamCommand(LoginUser + Licenses + Quit)?.Split('\n').ToList();
        if (licenseList == null)
        {
            return null;
        }
        
        foreach (var line in licenseList)
        {
            if (line.StartsWith(" - Apps"))
            {
                AvailableLicenses.Add(line.Substring(10).Split(',')[0]);
            }
        }

        return FilterAvailableExperiences<T>();
    }

    /// <summary>
    /// Filter through the raw string that was provided by the steamCMD command for the install applications.
    /// 
    /// NOTE: This can be used if the steamapps.vrmanifest was corrupted when initially read. If a headset is connected,
    /// the manifest would have refreshed so re-scan it using the application list collected by steamCMD at start up,
    /// collecting them without having to re-open steamCMD and disconnect Steam.
    /// Overwrite the stored applications and send the new list.
    /// </summary>
    public static List<T> FilterAvailableExperiences<T>()
    {
        //Reset the lists as to not double up each refresh //TODO SEND THESE LISTS TO THE NUC?? OR JUST QA??
        noLicenses = new List<string>();
        blockedByFamilyMode = new List<string>();
        
         List<T> apps = new List<T>();
        approvedGames = GetParentalApprovedGames();
        Logger.WriteLog("Approved games length: " + approvedGames.Count, MockConsole.LogLevel.Debug);

        Logger.WriteLog("Within loadAvailableGames", MockConsole.LogLevel.Debug);

        foreach (var line in steamCmdInstalledGames.Where(line => line.StartsWith("AppID")))
        {
            Logger.WriteLog(line, MockConsole.LogLevel.Debug);

            List<string> filter = line.Split(":").ToList();
            string id = filter[0].Replace("AppID", "").Trim();

            if (!AvailableLicenses.Contains(id))
            {
                Logger.WriteLog($"SteamScripts - FilterAvailableExperiences: Experience does not have available license: {id}", MockConsole.LogLevel.Info);
                noLicenses.Add(id);
                continue;
            }
            if (BlacklistedGames.Contains(id))
            {
                Logger.WriteLog($"SteamScripts - FilterAvailableExperiences: Experience is black listed: {id}", MockConsole.LogLevel.Info);
                continue;
            } 
            if (approvedGames.Count != 0 && !approvedGames.Contains(id)) 
            {
                Logger.WriteLog($"SteamScripts - FilterAvailableExperiences: Experience is not on approved games list: {id}", MockConsole.LogLevel.Info);
                blockedByFamilyMode.Add(id);
                continue; // if count is zero then all games are approved
            }

            filter.RemoveAt(0); // remove AppID prefix
            filter.RemoveAt(filter.Count - 1); // remove file location
            filter.RemoveAt(filter.Count - 1); // remove drive name
            string name = string.Join(":", filter.ToArray()).Replace("\\", "").Trim();
            name = name.Replace("\"", "").Trim();
            if (name.Contains("appid_")) // as a backup if steamcmd doesn't load the game name, we get it from the acf file
            {
                Logger.WriteLog($"SteamScripts - FilterAvailableExperiences: Experience name not provided got: {name}", MockConsole.LogLevel.Info);
                
                AcfReader acfReader = new AcfReader(id);
                acfReader.ACFFileToStruct();
                if (!string.IsNullOrEmpty(acfReader.gameName))
                {
                    name = acfReader.gameName;
                    Logger.WriteLog($"SteamScripts - FilterAvailableExperiences: Experience name from acf file: {name}", MockConsole.LogLevel.Info);
                }
            }
            //Determine if it is a VR experience
            bool isVr = steamManifestApplicationList.IsApplicationInstalledAndVrCompatible("steam.app." + id);
            if (!Helper.GetStationMode().Equals(Helper.STATION_MODE_VR) && isVr) continue;
            
            Logger.WriteLog($"SteamScripts - FilterAvailableExperiences: Storing new experience: {SteamWrapper.WrapperType}|{id}|{name}|{isVr}", MockConsole.LogLevel.Info);
            //item.parameters may be null here
            WrapperManager.StoreApplication(SteamWrapper.WrapperType, id, name, isVr);
            // Basic application requirements
            if (typeof(T) == typeof(ExperienceDetails))
            {
                ExperienceDetails experience = new ExperienceDetails(SteamWrapper.WrapperType, name, id, isVr);
                apps.Add((T)(object)experience);
            }
            else if (typeof(T) == typeof(string))
            {
                string application = $"{SteamWrapper.WrapperType}|{id}|{name}|{isVr}";
                apps.Add((T)(object)application);
            }
        }
        
        return apps;
    }

    private static List<string> GetParentalApprovedGames()
    {
        List<string> approvedList = new List<string>();
        var directory = new DirectoryInfo(@"C:\Program Files (x86)\Steam\logs");
        var files = directory.GetFiles("parental_log.txt")
            .OrderByDescending(f => f.LastWriteTime);
        
        if (files != null && !files.Any())
        {
            return approvedList;
        }

        var file = files.First();
        ReverseLineReader reverseLineReader = new ReverseLineReader(file.FullName, Encoding.UTF8);
        IEnumerator<string> enumerator = reverseLineReader.GetEnumerator();
        do
        {
            string current = enumerator.Current;
            if (string.IsNullOrEmpty(current))
            {
                continue;
            }

            if (current.Contains("No custom list"))
            {
                Logger.WriteLog("No custom list of steam applications", MockConsole.LogLevel.Info);
                break;
            }
            if (current.Contains("Custom list"))
            {
                Logger.WriteLog("Reached end of parental approved list", MockConsole.LogLevel.Info);
                break;
            }
            if (current.Contains("Custom list"))
            {
                Logger.WriteLog("Reached end of parental approved list", MockConsole.LogLevel.Info);
                
                // check for if family mode is not enabled
                enumerator.MoveNext();
                enumerator.MoveNext();
                if (enumerator.Current.Contains("Enabled: false"))
                {
                    Logger.WriteLog("Reached end of parental approved list, but enabled is false, returning empty list", MockConsole.LogLevel.Info);
                    return new List<string>(); // return an empty list, as this indicates all approved
                }
                break;
            }

            if (current.Contains("(allowed)"))
            {
                string[] lineParts = current.Split(" ");
                if (lineParts.Length > 2)
                {
                    approvedList.Add(lineParts[2].Trim());
                }
            }
        } while (enumerator.MoveNext());
        enumerator.Dispose();
        return approvedList;
    }
    
    //TODO create (wait for designer) an image for the home screen
    /// <summary>
    /// Checks and updates the SteamVR home background image and deletes the Vive Business Streaming image if necessary.
    /// </summary>
    public static void CheckSteamVrHomeImage()
    {
        // Define the path to the local lumination_home.png image
        string luminationHome = CommandLine.StationLocation + @"\Assets\Images\lumination_home.png";

        // If the local file does not exist, exit the function
        if (!File.Exists(luminationHome))
        {
            MockConsole.WriteLine($"No custom home image detected: {luminationHome}", MockConsole.LogLevel.Normal);
            return;
        }

        // Read the SteamVR settings file
        string steamVrSettingsFilePath = @"C:\Program Files (x86)\Steam\config\steamvr.vrsettings";
        try
        {
            // Read the JSON file
            string json = File.ReadAllText(steamVrSettingsFilePath);

            // Parse the JSON string into a JObject
            JObject jObject = JObject.Parse(json);

            // Check if the "steamvr" section and "background" field exist
            if (jObject["steamvr"]?["background"] == null) return;

            // The background has already been set
            if ((string?)jObject["steamvr"]?["background"] == luminationHome)
            {
                return;
            }

            // Replace the current background image with the local one
            jObject["steamvr"]!["background"] = luminationHome;

            // Convert the JObject back to a JSON string and save it
            string modifiedJson = jObject.ToString();
            File.WriteAllText(steamVrSettingsFilePath, modifiedJson);
        }
        catch (Exception e)
        {
            Logger.WriteLog($"Unable to read steamvr.vrsettings: {e}", MockConsole.LogLevel.Normal);
        }

        // Delete the ViveBusinessStreaming image file
        string viveBusinessStreamingImagePath = @"C:\Program Files (x86)\Steam\steamapps\common\SteamVR\resources\backgrounds\ViveBusinessStreaming.png";
        try
        {
            // Check if the file exists before attempting to delete it
            if (File.Exists(viveBusinessStreamingImagePath))
            {
                // Delete the file
                File.Delete(viveBusinessStreamingImagePath);
                MockConsole.WriteLine("Image deleted successfully.", MockConsole.LogLevel.Normal);
            }
            else
            {
                MockConsole.WriteLine("The specified image file does not exist.", MockConsole.LogLevel.Normal);
            }
        }
        catch (Exception ex)
        {
            // Handle any exceptions that may occur during the deletion process
            Logger.WriteLog($"An error occurred while deleting the image: {ex.Message}", MockConsole.LogLevel.Error);
        }
    }
}
