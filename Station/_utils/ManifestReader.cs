using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Station._utils;

public static class ManifestReader
{
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
            Logger.WriteLog($"Manifest file not found at path: {filePath}.", MockConsole.LogLevel.Error);
            return null;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            return json.Length == 0 ? null : JObject.Parse(json);
        }
        catch (Exception ex)
        {
            Logger.WriteLog($"Error reading vr manifest file: {filePath}, message: {ex}", MockConsole.LogLevel.Error);
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

        var applications = (JArray)data?["applications"]!;
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
        if (data != null && data.ContainsKey("applications")) return false;
        
        Logger.WriteLog($"Manifest file is not in the expected format: {filePath}.", MockConsole.LogLevel.Error);
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
}
