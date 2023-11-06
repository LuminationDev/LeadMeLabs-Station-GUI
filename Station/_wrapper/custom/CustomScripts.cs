using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using LeadMeLabsLibrary;

namespace Station
{
    public static class CustomScripts
    {
        public static readonly string CustomManifest = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "leadme_apps", "customapps.vrmanifest"));

        /// <summary>
        /// Read the manifest.json that has been created by the launcher program. Here each application has
        /// a specific entry contain it's ID, name and any launch parameters.
        /// </summary>
        /// <returns>A list of strings that represent all installed Custom experiences on a Station.</returns>
        public static List<string>? LoadAvailableGames()
        {
            if (CommandLine.stationLocation == null)
            {
                SessionController.PassStationMessage("Cannot find working directory for custom experiences");
                return null;
            }

            List<string> apps = new List<string>();

            //Load the local appData/Roaming folder path
            string manifestPath = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "leadme_apps", "manifest.json"));

            if(!File.Exists(manifestPath))
            {
                return null;
            }

            //Read the manifest
            using (StreamReader r = new StreamReader(manifestPath))
            {
                //Read and decipher the encrypted manifest
                string encrytedJson = r.ReadToEnd();
                string json = EncryptionHelper.DecryptNode(encrytedJson);

                dynamic? array = JsonConvert.DeserializeObject(json);

                if (array == null)
                {
                    return null;
                }

                foreach (var item in array)
                {
                    //Do not collect the Station or NUC application from the manifest file.
                    if (item.type == "LeadMe" || item.GetType == "Launcher") continue;

                    //Basic application requirements
                    string application = $"{item.type}|{item.id}|{item.name}";

                    //Determine if there are launch parameters, if so create a passable string for a new process function
                    string? parameters = null;
                    if (item.parameters != null)
                    {
                        if (item.parameters is JObject input)
                        {
                            //Only require the Value, key is simply used for human reference within the manifest.json
                            foreach (var x in input)
                            {
                                parameters += $"{x.Value} ";
                            }
                        }
                    }

                    //Check if there is an alternate path (this is for imported experiences in the launcher)
                    string? altPath = null;
                    if(item.altPath != null)
                    {
                        altPath = item.altPath.ToString();
                    }

                    WrapperManager.StoreApplication(item.type.ToString(), item.id.ToString(), item.name.ToString(), parameters, altPath);
                    apps.Add(application);
                }
            }

            return new List<string> { string.Join('/', apps) };
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
            int index = path.Trim('/', '\\').LastIndexOfAny(new char[] { '\\', '/' });

            // now if index is >= 0 that means we have at least one parent directory, otherwise the given path is the root most.
            if (index >= 0)
                return path.Remove(index);
            else
                return "";
        }
    }
}
