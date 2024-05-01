using System;
using System.Collections.Generic;
using System.IO;
using LeadMeLabsLibrary;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Station.Components._commandLine;
using Station.Components._managers;
using Station.Components._models;
using Station.Components._notification;
using Station.Components._utils;

namespace Station.Components._wrapper.custom;

public static class CustomScripts
{
    public static readonly string CustomManifest = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "leadme_apps", "customapps.vrmanifest"));
    private static readonly ManifestReader.ManifestApplicationList CustomManifestApplicationList = new (CustomManifest);

    /// <summary>
    /// Read the manifest.json that has been created by the launcher program. Here each application has
    /// a specific entry contain it's ID, name and any launch parameters.
    /// </summary>
    /// <typeparam name="T">The type of experiences to load.</typeparam>
    /// <returns>A list of available experiences of type T, or null if no experiences are available.</returns>
    public static List<T>? LoadAvailableExperiences<T>()
    {
        if (CommandLine.StationLocation == null)
        {
            MockConsole.WriteLine("Cannot find working directory for custom experiences", MockConsole.LogLevel.Error);
            return null;
        }

        List<T> apps = new List<T>();

        // Load the local appData/Roaming folder path
        string manifestPath = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "leadme_apps", "manifest.json"));

        if (!File.Exists(manifestPath))
        {
            return null;
        }

        // Read the manifest and modify the file if required
        string decryptedText = EncryptionHelper.DetectFileEncryption(manifestPath);
        if (string.IsNullOrEmpty(decryptedText)) return null;

        dynamic? array = JsonConvert.DeserializeObject(decryptedText);

        if (array == null)
        {
            return null;
        }
        
        foreach (var item in array)
        {
            // Do not collect the anything other than the custom applications from the manifest file.
            if (item.type != "Custom") continue;
            
            // Determine if it is a VR experience
            bool isVr = CustomManifestApplicationList.IsApplicationInstalledAndVrCompatible("custom.app." + item.id.ToString());

            // Basic application requirements
            if (typeof(T) == typeof(ExperienceDetails))
            {
                ExperienceDetails experience = new ExperienceDetails(item.type.ToString(), item.name.ToString(), item.id.ToString(), isVr);
                apps.Add((T)(object)experience);
            }
            else if (typeof(T) == typeof(string))
            {
                string application = $"{item.type}|{item.id}|{item.name}|{isVr.ToString()}";
                apps.Add((T)(object)application);
            }
            
            // Determine if there are launch parameters, if so create a passable string for a new process function
            string? parameters = null;
            if (item.parameters != null)
            {
                if (item.parameters is JObject input)
                {
                    // Only require the Value, key is simply used for human reference within the manifest.json
                    foreach (var x in input)
                    {
                        parameters += $"{x.Value} ";
                    }
                }
            }

            // Check if there is an alternate path (this is for imported experiences in the launcher)
            string? altPath = null;
            if (item.altPath != null)
            {
                altPath = item.altPath.ToString();
            }

            WrapperManager.StoreApplication(item.type.ToString(), item.id.ToString(), item.name.ToString(), isVr, parameters, altPath);
        }

        return apps;
    }

    /// <summary>
    /// Get the direct parent directory, useful for find out which folder an experience belongs to.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static string GetParentDirPath(string path)
    {
        // Used two separators windows style "\\" and linux "/" (for bad formed paths)
        // We make sure to remove extra unneeded characters.
        int index = path.Trim('/', '\\').LastIndexOfAny(new [] { '\\', '/' });

        // now if index is >= 0 that means we have at least one parent directory, otherwise the given path is the root most.
        if (index >= 0)
            return path.Remove(index);
        return "";
    }
}
