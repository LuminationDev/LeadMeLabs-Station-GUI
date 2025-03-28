using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LeadMeLabsLibrary;
using Newtonsoft.Json.Linq;
using Station.Components._managers;
using Station.Components._models;
using Station.Components._notification;
using Station.Components._wrapper.custom;
using Station.Components._wrapper.embedded;
using Station.Components._wrapper.revive;
using Station.Components._wrapper.steam;
using Station.MVC.Controller;
using Valve.VR;

namespace Station.Components._utils;

public static class ManifestReader
{

    public class ManifestApplicationList
    {
        private readonly JArray _applications = new();
        public ManifestApplicationList(string filePath)
        {
            JArray? newApplications = CollectApplications(filePath);
            if (newApplications != null)
            {
                _applications = newApplications;
            }
        }

        public bool IsApplicationInstalledAndVrCompatible(string appKey)
        {
            var specificEntry = _applications.FirstOrDefault(app => (((string)app["app_key"])!).Contains(appKey));
        
            return !String.IsNullOrEmpty(specificEntry?["strings"]?["en_us"]?["name"]?.ToString());
        }

        public JToken? GetApplication(string appKey)
        {
            var specificEntry = _applications.FirstOrDefault(app => (((string)app["app_key"])!).Contains(appKey));

            return specificEntry;
        }
    }
    
    /// <summary>
    /// Creates a .vrmanifest file with the specified source and an empty list of applications.
    /// </summary>
    /// <param name="filePath">The path to the .vrmanifest file to be created.</param>
    /// <param name="source">The source value to be included in the .vrmanifest file.</param>
    public static void CreateVrManifestFile(string filePath, string source)
    {
        string jsonContent = $@"{{
            ""source"": ""{source}"",
            ""applications"": []
        }}";

        try
        {
            // Write the JSON content to the file
            File.WriteAllText(filePath, jsonContent);
            Logger.WriteLog($"{source} - VR Manifest file created successfully.", Enums.LogLevel.Normal);
        }
        catch (Exception ex)
        {
            Logger.WriteLog($"{source} - ERROR: VR Manifest file not created. {ex.Message}", Enums.LogLevel.Error);
        }
    }
    
    /// <summary>
    /// Reads the content of a VR manifest file from the specified file path and converts it to a JObject.
    /// </summary>
    /// <param name="filePath">The path to the VR manifest file.</param>
    /// <returns>A JObject representing the contents of the manifest file if the file exists and can be read, or null
    /// if the file is not found or an error occurs during reading and parsing.</returns>
    private static JObject? ReadManifestFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            Logger.WriteLog($"Manifest file not found at path: {filePath}.", Enums.LogLevel.Error);
            return null;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            return json.Length == 0 ? null : JObject.Parse(json);
        }
        catch (Exception ex)
        {
            Logger.WriteLog($"Error reading vr manifest file: {filePath}, message: {ex}", Enums.LogLevel.Error);
            return null;
        }
    }

    /// <summary>
    /// Retrieves the array of applications from a VR manifest file.
    /// </summary>
    /// <param name="filePath">The path to the VR manifest file.</param>
    /// <returns>
    /// A JArray containing the list of applications within the manifest file, 
    /// or null if the file does not exist or is not in the expected format.
    /// </returns>
    private static JArray? CollectApplications(string filePath)
    {
        var data = ReadManifestFile(filePath);
        if (IsDataNull(filePath, data)) return null;

        //Check if Steam -> steamapps.vrmanifest has become corrupted
        try
        {
            JArray.Parse(data?["applications"].ToString());
            if (filePath.Equals(SteamScripts.SteamManifest))
            {
                WrapperManager.steamManifestCorrupted = false;
            }
        }
        catch (Exception)
        {
            //Send a message to the Tablet with the following instructions.
            // - Steam application list has become corrupted
            // - Connect a headset to the computer (steamapps.vrmanifest will refresh)
            // - Restart the VR system
            MessageController.SendResponse("Android", "Station", "SteamappsCorrupted");
            return null;
        }

        JArray applications = (JArray)data["applications"]!;
        return applications;
    }

    /// <summary>
    /// Checks if the JObject data retrieved from a VR manifest file is null or lacks the expected 'applications' property.
    /// </summary>
    /// <param name="filePath">The path to the VR manifest file.</param>
    /// <param name="data">The JObject representing the content of the manifest file.</param>
    /// <returns>
    /// Returns 'false' if the JObject is not null and contains the 'applications' property, indicating a valid manifest file structure.
    /// Returns 'true' and logs an error if the JObject is null or does not contain the 'applications' property.
    /// </returns>
    private static bool IsDataNull(string filePath, JObject? data)
    {
        //Check if data or data.ContainsKey is null
        if (data?.ContainsKey("applications") == null) return true;
        if (data.ContainsKey("applications") && data["applications"] is JArray) return false;

        switch (filePath)
        {
            //The automated action has not worked, get the user to manually restart the VR system.
            case SteamScripts.SteamManifest when WrapperManager.steamManifestCorrupted:
                //Send a message to the Tablet that displays the following instructions.
                // - Steam application list has become corrupted
                // - Connect a headset to the computer (steamapps.vrmanifest will refresh)
                // - Restart the VR system
                MessageController.SendResponse("Android", "Station", "SteamappsCorrupted");
                break;

            case SteamScripts.SteamManifest:
                WrapperManager.steamManifestCorrupted = true;
                break;
        }
        
        Logger.WriteLog($"Manifest file is not in the expected format: {filePath}.", Enums.LogLevel.Error);
        return true;
    }

    /// <summary>
    /// Gathers a list of application keys and names from a VR manifest file.
    /// </summary>
    /// <param name="filePath">The path to the VR manifest file.</param>
    /// <returns>
    /// A collection of tuples containing the application keys and names from the manifest file if the file structure is valid.
    /// If the file is not found or does not contain the expected data, it returns an empty list or null.
    /// </returns>
    public static List<(string, string)> CollectKeyAndName(string filePath)
    {
        JArray? applications = CollectApplications(filePath);
        if (applications == null) return new List<(string, string)>();
        
        return applications
            .Select(app => ((string)app["app_key"]!, (string)app["strings"]!["en_us"]!["name"]!))
            .ToList();
    }

    /// <summary>
    /// Retrieves the application name associated with a specific app key from a VR manifest file.
    /// </summary>
    /// <param name="filePath">The path to the VR manifest file.</param>
    /// <param name="appKey">The application key used to search for the associated application name.</param>
    /// <returns>
    /// The application name corresponding to the provided app key if found in the manifest file and its structure is valid.
    /// Returns null if the file is not found, the appKey is null, or if the application name is not present for the given app key.
    /// </returns>
    public static string? GetApplicationNameByAppKey(string filePath, string? appKey)
    {
        JArray? applications = CollectApplications(filePath);
        if (applications == null || appKey == null) return null;

        var specificEntry = applications.FirstOrDefault(app => (((string)app["app_key"])!).Contains(appKey));
        
        return specificEntry?["strings"]?["en_us"]?["name"]?.ToString();
    }

    /// <summary>
    /// Retrieves the image path associated with a specific app key from a VR manifest file.
    /// </summary>
    /// <param name="filePath">The path to the VR manifest file.</param>
    /// <param name="appKey">The application key used to search for the associated image path.</param>
    /// <returns>
    /// The image path corresponding to the provided app key if found in the manifest file and its structure is valid.
    /// Returns null if the file is not found, the appKey is null, or if the image path is not present for the given app key.
    /// </returns>
    public static string? GetApplicationImagePathByAppKey(string filePath, string? appKey)
    {
        JArray? applications = CollectApplications(filePath);
        if (applications == null || appKey == null) return null;
        var specificEntry = applications.FirstOrDefault(app => (((string)app["app_key"])!).Contains(appKey));
        
        return specificEntry?["image_path"]?.ToString();
    }

    /// <summary>
    /// Modifies the 'binary_path_windows' property in the applications of a VR manifest file to update the path location.
    /// </summary>
    /// <param name="filePath">The path to the VR manifest file.</param>
    /// <param name="location">The new location to be prefixed to the 'binary_path_windows' property.</param>
    public static void ModifyBinaryPath(string filePath, string location)
    {
        JObject? data = ReadManifestFile(filePath);
        if (IsDataNull(filePath, data)) return;

        JArray applications = (JArray)data?["applications"]!;
        foreach (var app in applications)
        {
            if (app["binary_path_windows"] != null && !app["binary_path_windows"]!.ToString().StartsWith(location))
            {
                app["binary_path_windows"] = $"{location}/{app["binary_path_windows"]}";
            }
        }

        File.WriteAllText(filePath, data?.ToString());
    }

    /// <summary>
    /// Clear the entire applications array, leaving an empty array in its place.
    /// </summary>
    /// <param name="filePath">The path to the manifest file.</param>
    public static void ClearApplicationList(string filePath)
    {
        JObject? data = ReadManifestFile(filePath);
        if (IsDataNull(filePath, data)) return;
        
        JArray applications = (JArray)data?["applications"]!;
        applications.RemoveAll();
        
        File.WriteAllText(filePath, data?.ToString());
    }

    /// <summary>
    /// Creates or updates an application entry in a manifest file. If an entry with the same app_key already exists,
    /// it updates the existing entry with the provided details. Otherwise, it creates a new entry with the provided
    /// details.
    /// </summary>
    /// <param name="filePath">The path to the manifest file.</param>
    /// <param name="appType">The type of the application.</param>
    /// <param name="details">The details of the application to be added or updated.</param>
    public static void CreateOrUpdateApplicationEntry(string filePath, string appType, JObject details)
    {
        JObject? data = ReadManifestFile(filePath);
        if (IsDataNull(filePath, data)) return;

        JArray applications = (JArray)data?["applications"]!;

        string appKey = $"{appType}.app.{details.GetValue("id")}";
    
        // Search for an existing entry with the same app_key
        JObject? existingEntry = applications.Children<JObject>()
            .FirstOrDefault(app => app["app_key"]?.ToString() == appKey);

        if (existingEntry != null)
        {
            // Update the existing entry
            existingEntry["launch_type"] = "binary";
            existingEntry["binary_path_windows"] = details.GetValue("altPath");
            existingEntry["is_dashboard_overlay"] = true;

            if (details.GetValue("parameters") != null)
            {
                existingEntry["arguments"] = details.GetValue("parameters");
            }
            
            if (details.GetValue("image_path") != null)
            {
                existingEntry["image_path"] = details.GetValue("image_path");
            }

            JObject language = new JObject { { "name", details.GetValue("name") } };
            JObject strings = new JObject { { "en_us", language } };
            existingEntry["strings"] = strings;
        }
        else
        {
            // Create a new entry if not found
            JObject temp = new JObject
            {
                { "app_key", appKey },
                { "launch_type", "binary" },
                { "binary_path_windows", details.GetValue("altPath") },
                { "is_dashboard_overlay", true }
            };

            if (details.GetValue("parameters") != null)
            {
                temp.Add("arguments", details.GetValue("parameters"));
            }
            
            if (details.GetValue("image_path") != null)
            {
                temp.Add("image_path", details.GetValue("image_path"));
            }
        
            JObject language = new JObject { { "name", details.GetValue("name") } };
            JObject strings = new JObject { { "en_us", language } };
            temp.Add("strings", strings);
        
            applications.Add(temp);
        }

        File.WriteAllText(filePath, data?.ToString());
    }
    
    /// <summary>
    /// Modifies the arguments for a specified application in a VR manifest file.
    /// in case the 
    /// </summary>
    /// <param name="appId">The ID of the application.</param>
    /// <param name="arguments">The new arguments to be set for the application.</param>
    public static void ModifyApplicationArguments(string appId, string arguments)
    {
        //Is this the best spot???
        //Need to know the wrapper type, appId and parameters
        Experience experience = WrapperManager.ApplicationList.GetValueOrDefault(appId);
        if (experience.IsNull())
        {
            MockConsole.WriteLine($"No application found: {appId}", Enums.LogLevel.Normal);
            return;
        }

        if(experience.Type == null)
        {
            MockConsole.WriteLine($"No wrapper associated with experience {appId}.", Enums.LogLevel.Normal);
            return;
        }
        
        //No manifest to update if the experience is not VR enabled
        if(experience.IsVr == false)
        {
            return;
        }
        
        string? filePath = null;
        string? keyPrefix = null;
        switch (experience.Type)
        {
            case "Custom":
                filePath = CustomScripts.CustomManifest;
                keyPrefix = "custom.app";
                break;
            
            case "Embedded":
                filePath = EmbeddedScripts.EmbeddedVrManifest;
                keyPrefix = "embedded.app";
                break;
            
            case "Steam":
                filePath = SteamScripts.SteamManifest;
                keyPrefix = "steam.app";
                break;
            
            case "Revive":
                filePath = ReviveScripts.ReviveManifest;
                keyPrefix = "revive.app";
                break;
        }
        
        if (filePath == null) return;
        
        JObject? data = ReadManifestFile(filePath);
        if (IsDataNull(filePath, data)) return;
        
        JArray applications = (JArray)data?["applications"]!;
        foreach (var app in applications)
        {
            if (app["app_key"] == null || !app["app_key"]!.ToString().Equals($"{keyPrefix}.{appId}")) continue;
            
            string? type = experience.Subtype?.GetValue("category")?.ToString();
            if (type is "shareCode")
            {
                //[0] -app
                //[1] (typeValue)
                //[2] -code
                //[3] (codeValue)
                List<string> split = new List<string>(app["arguments"]?.ToString().Split(" ") ?? Array.Empty<string>());
                if (split.Count == 0) return;

                // Replace the codeValue or add it for the first time
                if (split.Count >= 4)
                {
                    split[3] = arguments;
                }
                else
                {
                    split.Add(arguments);
                }

                // Join the arguments with a space between
                app["arguments"] = string.Join(" ", split);
            }
            else
            {
                app["arguments"] = arguments;
            }
        }
        if (data == null) return;

        File.WriteAllText(filePath, data.ToString());
        
        //Reload the VR manifest
        OpenVR.Applications.RemoveApplicationManifest(filePath);
        OpenVR.Applications.AddApplicationManifest(filePath, true);
    }
}
