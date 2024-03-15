using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LeadMeLabsLibrary;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Station.Components._commandLine;
using Station.Components._managers;
using Station.Components._models;
using Station.Components._notification;
using Station.Components._utils;
using Station.MVC.Controller;

namespace Station.Components._wrapper.embedded;

public static class EmbeddedScripts
{
    private static readonly string EmbeddedManifest = Path.GetFullPath(Path.Combine(CommandLine.StationLocation, "_embedded", "manifest.json"));
    private static readonly string EmbeddedDirectory = Path.GetFullPath(Path.Combine(CommandLine.StationLocation, "_embedded"));
    
    public static readonly string EmbeddedVrManifest = Path.GetFullPath(Path.Combine(CommandLine.StationLocation, @"_embedded\embeddedapps.vrmanifest"));
    private static ManifestReader.ManifestApplicationList? embeddedManifestApplicationList;
    
    /// <summary>
    /// Overwrite the last known manifests with the currently detected ones in the embedded folder.
    /// </summary>
    private static void RegenerateEmbeddedManifests()
    {
        // Clear the old embeddedapps.vrmanifest to not include applications that may not be there anymore
        ManifestReader.ClearApplicationList(EmbeddedVrManifest);
        
        // Regenerate the Embedded/manifest.json and embeddedapps.vrmanifest
        string manifestData = GenerateManifests(EmbeddedDirectory);
        string encryptedText = EncryptionHelper.UnicodeEncryptNode(manifestData);
        File.WriteAllText(EmbeddedManifest, encryptedText);
        
        // Create the manifest list of regeneration
        embeddedManifestApplicationList = new (EmbeddedVrManifest);
    }
    
    /// <summary>
    /// Generates a JSON manifest by searching for "leadme_config.json" files within the immediate subdirectories
    /// (top-level folders) of the specified root folder. Each found configuration file's content is parsed into a
    /// JObject and added to a JArray.
    /// </summary>
    /// <param name="rootFolder">The root folder path to start the search from.</param>
    /// <returns>A JSON string representing the generated manifest.</returns>
    private static string GenerateManifests(string rootFolder)
    {
        JArray config = new();
        
        try
        {
            // Check if the root folder exists
            if (!Directory.Exists(rootFolder))
            {
                MockConsole.WriteLine($"Root folder does not exist: {rootFolder}", MockConsole.LogLevel.Error);
            }
            
            // Get the immediate subdirectories (top-level folders)
            string[] subfolders = Directory.GetDirectories(rootFolder);
            foreach (string subfolder in subfolders)
            {
                // Process each subfolder
                string configFilePath = Path.Combine(subfolder, "leadme_config.json");
                if (!File.Exists(configFilePath))
                {
                    MockConsole.WriteLine($"config.json not found in {subfolder}", MockConsole.LogLevel.Error);
                    continue;
                }

                // Read the content of the config.json file
                string configFileContent = File.ReadAllText(configFilePath);

                try
                {
                    // The config might have multiple version (I.e. CoSpaces & Thinglink in the WebXR Viewer)
                    JArray configs = JArray.Parse(configFileContent);

                    foreach (var jToken in configs)
                    {
                        var temp = (JObject)jToken;
                        
                        //Create an id based on the Launcher's id method
                        temp.Add("id", GenerateUniqueId(temp.GetValue("name")!.ToString()));
                        
                        //Create an altPath based on the current subfolder
                        temp.Add("altPath", $"{subfolder}\\{temp.GetValue("exeName")}");
                        
                        //Check if there is an nested folder for the header image
                        // - This should end up being the folder location plus the folder specified
                        string? headerPath = temp.GetValue("headerFolder")?.ToString();
                        if (headerPath != null)
                        {
                            temp.Add("headerPath", $"{subfolder}\\{headerPath}\\header.jpg");
                        }

                        config.Add(temp);
                        
                        //Create an embeddedapps.vrmanifest entry if the application is VR enabled
                        bool isVr = (bool)(temp.GetValue("isVr") ?? false);
                        if (isVr)
                        {
                            ManifestReader.CreateOrUpdateApplicationEntry(EmbeddedVrManifest, "embedded", temp);
                        }
                    }
                }
                catch (Exception e)
                {
                    MockConsole.WriteLine($"Exception in {configFilePath}", MockConsole.LogLevel.Error);
                    MockConsole.WriteLine($"Exception: {e}", MockConsole.LogLevel.Error);
                }
            }
        }
        catch (Exception ex)
        {
            MockConsole.WriteLine($"Error processing root folder {rootFolder}: {ex.Message}", MockConsole.LogLevel.Error);
        }
        
        return config.ToString();
    }

    /// <summary>
    /// Generates a unique identifier based on the provided name. The identifier is constructed by mapping each
    /// character of the name to its corresponding index in the allowed characters string, then concatenating the
    /// resulting indices.
    /// </summary>
    /// <param name="name">The input name for which to generate the unique identifier.</param>
    /// <returns>A unique identifier string based on the provided name.</returns>
    private static string GenerateUniqueId(string name)
    {
        string allowedChars = "abcdefghijklmnopqrstuvwxyz0123456789 ~`!@#$%^&*()-_=+[{]}\\|;:\'\",<.>/?";

        string id = string.Concat(name.ToLower().Select((c) =>
        {
            int index = allowedChars.IndexOf(c);
            return index >= 0 ? (index + 11).ToString() : "";
        }));
        
        return id;
    }
    
    /// <summary>
    /// Read the manifest.json that has been created by the launcher program. Here each application has
    /// a specific entry contain it's ID, name and any launch parameters.
    /// </summary>
    /// <typeparam name="T">The type of experiences to load.</typeparam>
    /// <returns>A list of available experiences of type T, or null if no experiences are available.</returns>
    public static List<T>? LoadAvailableExperiences<T>()
    {
        RegenerateEmbeddedManifests();
        
        if (CommandLine.StationLocation == null)
        {
            SessionController.PassStationMessage("Cannot find working directory for custom experiences");
            return null;
        }
        
        List<T> apps = new List<T>();
        
        // Load the local Embedded folder path
        if (!File.Exists(EmbeddedManifest))
        {
            return null;
        }
        
        // Read the manifest and modify the file if required
        string? encryptedText = File.ReadAllText(EmbeddedManifest);
        string? decryptedText = EncryptionHelper.UnicodeDecryptNode(encryptedText);
        
        if (string.IsNullOrEmpty(decryptedText)) return null;

        dynamic? array = JsonConvert.DeserializeObject(decryptedText);
        
        if (array == null)
        {
            return null;
        }
        
        foreach (var item in array)
        {
            // Determine if it is a VR experience
            bool isVr = embeddedManifestApplicationList?.IsApplicationInstalledAndVrCompatible("embedded.app." + item.id.ToString());
            
            // Determine if there is a specific subtype
            JObject? subtype = null;
            try
            {
                subtype = item.subtype;
            }
            catch (Exception e)
            {
                MockConsole.WriteLine("No subtype detected", MockConsole.LogLevel.Error);
                MockConsole.WriteLine(e.ToString(), MockConsole.LogLevel.Error);
            }
            
            // Basic application requirements
            if (typeof(T) == typeof(ExperienceDetails))
            {
                ExperienceDetails experience = new ExperienceDetails(EmbeddedWrapper.WrapperType, item.name.ToString(), item.id.ToString(), isVr, subtype);
                apps.Add((T)(object)experience);
            }
            else if (typeof(T) == typeof(string))
            {
                string application = $"{EmbeddedWrapper.WrapperType}|{item.id}|{item.name}|{isVr.ToString()}|{subtype}";
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
                else
                {
                    parameters = item.parameters;
                }
            }

            // Check if there is an alternate path (this is for imported experiences in the launcher)
            string? altPath = null;
            if (item.altPath != null)
            {
                altPath = item.altPath.ToString();
            }
            
            // Check if there is an alternate header image path
            string? headerPath = null;
            if (item.headerPath != null)
            {
                headerPath = item.headerPath;
            }
            
            WrapperManager.StoreApplication(EmbeddedWrapper.WrapperType, item.id.ToString(), item.name.ToString(), isVr, parameters, altPath, subtype, headerPath);
        }

        return apps;
    }
}
