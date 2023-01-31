using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Station
{
    class CustomScripts
    {
        private static string availableGames = "";

        public static async Task<string> getAvailableGames()
        {
            Logger.WriteLog("Get available games function", MockConsole.LogLevel.Verbose);

            // the load available games method is called on boot, we just need to wait for it to complete
            while (availableGames.Length == 0 || !Char.IsNumber(availableGames[0]))
            {
                Console.WriteLine("LOOPING");
                await Task.Delay(2000);
            }

            Logger.WriteLog(availableGames, MockConsole.LogLevel.Debug);

            return availableGames;
        }

        public static List<string>? loadAvailableGames()
        {
            if (CommandLine.stationLocation == null)
            {
                SessionController.PassStationMessage("Cannot find working directory for custom experiences");
                return null;
            }

            List<string> apps = new List<string>();
            string manifestPath = Path.GetFullPath(Path.Combine(CommandLine.stationLocation, @"..", "manifest.json"));

            if(!File.Exists(manifestPath))
            {
                return null;
            }

            //Read the manifest
            using (StreamReader r = new StreamReader(manifestPath))
            {
                string? json = r.ReadToEnd();

                dynamic? array = JsonConvert.DeserializeObject(json);

                if (array == null)
                {
                    return null;
                }

                foreach (var item in array)
                {
                    Console.WriteLine("{0} {1} {2}", item.type, item.id, item.name);

                    //Do not collect the Station or NUC application from the manifest file.
                    if (item.type != "LeadMe")
                    {
                        string application = $"{item.type}|{item.id}|{item.name}";
                        apps.Add(application);
                    }
                }
            }

            availableGames = String.Join('/', apps);
            return apps;
        }
    }
}
