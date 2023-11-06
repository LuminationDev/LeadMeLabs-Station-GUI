using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Station._utils;

public class ApplicationEntry
{
    public string app_key { get; set; }
    public string? image_path { get; set; }
    public ApplicationStrings strings { get; set; }
}

public class ApplicationStrings
{
    public Dictionary<string, string> en_us { get; set; }
}

public class RootObject
{
    public List<ApplicationEntry> applications { get; set; }
}

//TODO clean this whole class up
public static class ManifestReader
{
    /// <summary>
    /// Collect the app_key and name for each application that exists in the supplied vrmanifest.
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    public static List<(string, string)>? CollectKeyAndName(string filePath)
    {
        //Use a tuple to store the data
        List<(string appKey, string name)> appData = new();

        if (!File.Exists(filePath))
        {
            Logger.WriteLog($"Manifest file not found at path: {filePath}.", MockConsole.LogLevel.Error);
            return appData;
        }
        
        MockConsole.WriteLine($"Reading vr manifest file: {filePath}", MockConsole.LogLevel.Normal);
        
        try
        {
            //Read in the file data
            string json = File.ReadAllText(filePath);
            
            RootObject? data = JsonConvert.DeserializeObject<RootObject>(json);
            if (data == null)
            {
                Logger.WriteLog($"Manifest file empty or corrupt: {filePath}.", MockConsole.LogLevel.Error);
                return null;
            }

            appData.AddRange(data.applications.Select(app => (app.app_key, app.strings.en_us["name"])));
        }
        catch (Exception ex)
        {
            MockConsole.WriteLine($"ERROR: Reading vr manifest file: {filePath}, message: {ex}", MockConsole.LogLevel.Error);
        }
        
        return appData;
    }

    /// <summary>
    /// Get the name of an application in the supplied file by searching for the associated app_key.
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="appKey"></param>
    /// <returns></returns>
    public static string? GetApplicationNameByAppKey(string? filePath, string? appKey)
    {
        if (filePath == null || appKey == null)
        {
            return null;
        }

        if (!File.Exists(filePath))
        {
            Logger.WriteLog($"Manifest file not found at path: {filePath}.", MockConsole.LogLevel.Error);
            return null;
        }
        
        MockConsole.WriteLine($"Reading vr manifest file: {filePath}", MockConsole.LogLevel.Normal);
        
        try
        {
            //Read in the file data
            string json = File.ReadAllText(filePath);
            
            RootObject? data = JsonConvert.DeserializeObject<RootObject>(json);
            if (data == null)
            {
                Logger.WriteLog($"Manifest file empty or corrupt: {filePath}.", MockConsole.LogLevel.Error);
                return null;
            }

            //TODO .Contains is very dangerous here, replace later
            var specificEntry = data.applications.FirstOrDefault(app => app.app_key.Contains(appKey));
            return specificEntry?.strings.en_us["name"];
        }
        catch (Exception ex)
        {
            MockConsole.WriteLine($"ERROR: Reading vr manifest file: {filePath}, message: {ex}", MockConsole.LogLevel.Error);
        }

        return null;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="appKey"></param>
    /// <returns></returns>
    public static string? GetApplicationImagePathByAppKey(string? filePath, string? appKey)
    {
        if (filePath == null || appKey == null)
        {
            return null;
        }

        if (!File.Exists(filePath))
        {
            Logger.WriteLog($"Manifest file not found at path: {filePath}.", MockConsole.LogLevel.Error);
            return null;
        }
        
        MockConsole.WriteLine($"Reading vr manifest file: {filePath}", MockConsole.LogLevel.Normal);
        
        try
        {
            //Read in the file data
            string json = File.ReadAllText(filePath);
            
            RootObject? data = JsonConvert.DeserializeObject<RootObject>(json);
            if (data == null)
            {
                Logger.WriteLog($"Manifest file empty or corrupt: {filePath}.", MockConsole.LogLevel.Error);
                return null;
            }

            //TODO .Contains is very dangerous here, replace later
            var specificEntry = data.applications.FirstOrDefault(app => app.app_key.Contains(appKey));
            return specificEntry?.image_path;
        }
        catch (Exception ex)
        {
            MockConsole.WriteLine($"ERROR: Reading vr manifest file: {filePath}, message: {ex}", MockConsole.LogLevel.Error);
        }

        return null;
    }
    
    /// <summary>
    /// Edit the manifest so the binary_path_windows points to the correct absolute position instead of relative
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="location"></param>
    public static void ModifyBinaryPath(string? filePath, string location)
    {
        if (!File.Exists(filePath))
        {
            Logger.WriteLog($"Manifest file not found at path: {filePath}.", MockConsole.LogLevel.Error);
            return;
        }
        
        MockConsole.WriteLine($"Editing vr manifest file: {filePath}", MockConsole.LogLevel.Normal);

        try
        {
            //Read in the file data
            string json = File.ReadAllText(filePath);
            
            JObject? data = JObject.Parse(json);
            if (data.Count == 0)
            {
                Logger.WriteLog($"Manifest file empty or corrupt: {filePath}.", MockConsole.LogLevel.Error);
                return;
            }

            JArray? applications = (JArray?)data["applications"];
            if (applications == null)
            {
                Logger.WriteLog($"Manifest file empty or corrupt: {filePath}.", MockConsole.LogLevel.Error);
                return;
            }

            foreach (var jToken in applications)
            {
                var application = (JObject)jToken;
                if (application["binary_path_windows"] == null)
                {
                    continue;
                }
                
                if(application["binary_path_windows"].ToString().StartsWith(location))
                {
                    continue;
                }

                application["binary_path_windows"] = $"{location}/{application["binary_path_windows"]}";
            }
            
            File.WriteAllText(filePath, data.ToString());
        }
        catch (Exception ex)
        {
            MockConsole.WriteLine($"ERROR: Reading vr manifest file: {filePath}, message: {ex}", MockConsole.LogLevel.Error);
        }
    }
}
