using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using LeadMeLabsLibrary;
using Station._commandLine;
using Station._manager;
using Station._utils;
using Station._wrapper;

namespace Station
{
    public static class CustomScripts
    {
        public static readonly string CustomManifest = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "leadme_apps", "customapps.vrmanifest"));
        private static ManifestReader.ManifestApplicationList customManifestApplicationList = new (CustomManifest);

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

            //Read the manifest and modify the file if required
            string? decryptedText = EncryptionHelper.DetectFileEncryption(manifestPath);
            if (string.IsNullOrEmpty(decryptedText)) return new List<string> { string.Join('/', apps) };
            
            dynamic? array = JsonConvert.DeserializeObject(decryptedText);

            if (array == null)
            {
                return null;
            }

            foreach (var item in array)
            {
                //Do not collect the Station or NUC application from the manifest file.
                if (item.type == "LeadMe" || item.GetType == "Launcher") continue;

                //Determine if it is a VR experience
                bool isVr =
                    customManifestApplicationList.IsApplicationInstalledAndVrCompatible("custom.app." + item.id.ToString());

                //Basic application requirements
                string application = $"{item.type}|{item.id}|{item.name}|{isVr.ToString()}";

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

                WrapperManager.StoreApplication(item.type.ToString(), item.id.ToString(), item.name.ToString(), isVr, parameters, altPath);
                apps.Add(application);
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
            int index = path.Trim('/', '\\').LastIndexOfAny(new [] { '\\', '/' });

            // now if index is >= 0 that means we have at least one parent directory, otherwise the given path is the root most.
            if (index >= 0)
                return path.Remove(index);
            return "";
        }
    }
}
