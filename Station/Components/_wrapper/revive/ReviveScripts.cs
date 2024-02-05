using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Station.Components._commandLine;
using Station.Components._models;
using Station.Components._notification;
using Station.Components._utils;

namespace Station.Components._wrapper.revive;

public static class ReviveScripts
{
    public const string ReviveManifest = @"C:\Program Files\Revive\revive.vrmanifest";

    /// <summary>
    /// Read through the revive vr manifest to find what applications are installed.
    /// </summary>
    /// <typeparam name="T">The type of experiences to load.</typeparam>
    /// <returns>A list of available experiences of type T, or null if no experiences are available.</returns>
    public static List<T>? LoadAvailableExperiences<T>()
    {
        List<(string appKey, string name)> fileData = ManifestReader.CollectKeyAndName(ReviveManifest);
        if (fileData.Count == 0)
        {
            return null;
        }
        
        List<T> apps = new List<T>();
        
        foreach (var pair in fileData)
        {
            //Prettify the name
            string name = ConvertToCustomTitleCase(pair.name);
            
            //Trim revive.app. off each ID
            string id = pair.appKey.Replace("revive.app.", "");
            
            // Basic application requirements
            if (typeof(T) == typeof(ExperienceDetails))
            {
                ExperienceDetails experience = new ExperienceDetails(ReviveWrapper.WrapperType, name, id, true);
                apps.Add((T)(object)experience);
            }
            else if (typeof(T) == typeof(string))
            {
                string application = $"{ReviveWrapper.WrapperType}|{id}|{name}|{true}";
                apps.Add((T)(object)application);
            }
            
            //item.parameters may be null here
            WrapperManager.StoreApplication(ReviveWrapper.WrapperType, id, name, true);
        }
        
        return apps;
    }
    
    private static string ConvertToCustomTitleCase(string input)
    {
        string[] words = input.Split('-');
        string result = string.Join(" ", CapitalizeFirstWord(words));
        return result;
    }

    private static string[] CapitalizeFirstWord(string[] words)
    {
        if (words.Length > 0)
        {
            words[0] = words[0].Length > 0 ? char.ToUpper(words[0][0]) + words[0].Substring(1) : words[0];
        }
        return words;
    }

    public static void CopyHeaderImageToCache(string experienceKey, string name)
    {
        string destinationDirectory = @$"{CommandLine.StationLocation}\_cache\";
        
        Task.Factory.StartNew(() =>
        {
            string? filePath = ManifestReader.GetApplicationImagePathByAppKey(ReviveManifest, experienceKey);

            if (!File.Exists(filePath))
            {
                MockConsole.WriteLine($"File not found:{filePath}", MockConsole.LogLevel.Error);
                return;
            }
            
            // Construct the destination file path
            string destinationFilePath = Path.Combine(destinationDirectory, $"{name.Replace(":", "")}_header.jpg");

            //Copy the file to the _cache folder
            File.Copy(filePath, destinationFilePath, true);
            
            MockConsole.WriteLine($"Thumbnail for experience: {experienceKey} now copied to the _cache folder.", MockConsole.LogLevel.Error);
        });
    }
}
