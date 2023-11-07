using System.Collections.Generic;
using Station._utils;

namespace Station;

public static class ReviveScripts
{
    public const string ReviveManifest = @"C:\Program Files\Revive\revive.vrmanifest";

    /// <summary>
    /// Read through the revive vr manifest to find what applications are installed.
    /// </summary>
    /// <returns>A list of applications in string form or null</returns>
    public static List<string> LoadAvailableGames()
    {
        List<string> apps = new ();
        List<(string appKey, string name)> fileData = ManifestReader.CollectKeyAndName(ReviveManifest);
        if (fileData.Count == 0)
        {
            return apps;
        }
        
        foreach (var pair in fileData)
        {
            //Prettify the name
            string name = ConvertToCustomTitleCase(pair.name);
            
            //Trim revive.app. off each ID
            string id = pair.appKey.Replace("revive.app.", "");
            
            //Load the _reviveManifest
            string application = $"{ReviveWrapper.WrapperType}|{id}|{name}";

            //item.parameters may be null here
            WrapperManager.StoreApplication(ReviveWrapper.WrapperType, id, name);
            apps.Add(application);
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
}
