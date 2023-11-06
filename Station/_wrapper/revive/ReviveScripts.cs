using System.Collections.Generic;
using Station._utils;

namespace Station;

public static class ReviveScripts
{
    public static readonly string _reviveManifest = @"C:\Program Files\Revive\revive.vrmanifest";
    
    /// <summary>
    /// Read through the revive vr manifest to find what applications are installed.
    /// </summary>
    /// <returns>A list of applications in string form or null</returns>
    public static List<string>? LoadAvailableGames()
    {
        List<string> apps = new ();
        List<(string appKey, string name)>? fileData = ManifestReader.CollectKeyAndName(_reviveManifest);
        if (fileData == null)
        {
            return apps;
        }
        
        foreach (var pair in fileData)
        {
            //Trim revive.app. off each ID
            string id = pair.appKey.Replace("revive.app.", "");
            
            //Load the _reviveManifest
            string application = $"{ReviveWrapper.wrapperType}|{id}|{pair.name}";

            //item.parameters may be null here
            WrapperManager.StoreApplication(ReviveWrapper.wrapperType, id, pair.name);
            apps.Add(application);
        }
        
        return apps;
    }
}
