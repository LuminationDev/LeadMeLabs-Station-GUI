using System;
using System.IO;
using System.Collections.Generic;

namespace Station
{
    public static class DotEnv
    {
        private static readonly string filePath = $"{CommandLine.stationLocation}\\_config\\config.env";
        private static readonly string externalPath = $"{CommandLine.stationLocation}\\external";


        /// <summary>
        /// Load the variables within the config.env into the local environment for the running
        /// process.
        /// </summary>
        public static bool Load()
        {
            //TODO make a call to a secure database in the future


#if RELEASE
            //Check if local system variables exists, if so perform a migration
            if(Environment.GetEnvironmentVariable("Directory", EnvironmentVariableTarget.User) != null)
            {
                Migrate();
                ExternalApplications();
            }
#endif


            if (!File.Exists(filePath))
            {
                SessionController.PassStationMessage($"StationError,Config file not found:{filePath}");
                return false;
            }

            //Decrypt the data in the file
            string text = File.ReadAllText(filePath);
            if(text.Length == 0)
            {
                SessionController.PassStationMessage($"StationError,Config file empty:{filePath}");
                return false;
            }

            string decryptedText = EncryptionHelper.DecryptNode(text);

            foreach (var line in decryptedText.Split('\n'))
            {
                var parts = line.Split(
                    '=',
                    StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length != 2 && parts[0] != "Directory")
                {
                    SessionController.PassStationMessage($"StationError,Config incomplete:{parts[0]} has no value");
                    return false;
                }

                Environment.SetEnvironmentVariable(parts[0], parts[1]);
            }

#if DEBUG
            Environment.SetEnvironmentVariable("Directory", new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)).Name);
#endif

            return true;
        }

        /// <summary>
        /// Update part of the config.env, automatically detect if a variable already exists or if
        /// it should be added.
        /// </summary>
        /// <param name="values">A string list that contains keys and values for environment variables in the format key:value</param>
        public static void Update(List<string> values)
        {
            if (!File.Exists(filePath))
            {
                MockConsole.WriteLine($"Station Error,Config file not found:{filePath}", MockConsole.LogLevel.Error);
                return;
            }

            // Read the current config file
            string text = File.ReadAllText(filePath);
            string[] arrLine = text.Split("\n");

            // Loop over the supplied values
            foreach (string entry in values) {

                string[] split = entry.Split(':');
                string key = split[0];
                string value = split[1];

                Environment.SetEnvironmentVariable(key, value);

                bool exists = false;


                for (int i = 0; i < arrLine.Length; i++)
                {
                    if (arrLine[i].StartsWith(key))
                    {
                        arrLine[i] = $"{key}={value}";
                        exists = true;
                    }
                }

                //If the file does not contain the env variable yet create if here
                if (!exists)
                {
                    arrLine[arrLine.Length] = $"{key}={value}";
                }
            }
            
            string encryptedText = EncryptionHelper.EncryptNode(string.Join("\n", arrLine));

            //Rewrite the file with the new variables
            File.WriteAllText(filePath, encryptedText);
        }

        /// <summary>
        /// Migrate any local system variables over to the config.env
        /// </summary>
        public static void Migrate()
        {
            string[] EnvVariables = { "AppKey", "HeadsetType", "LabLocation", "NucAddress", "room", "StationId", "StationMode", "SteamUserName", "SteamPassword", "Directory" };
            List<string> values = new();

            foreach (string key in EnvVariables)
            {
                string? value = Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.User);
                if (value != null)
                {
                    //Handle the different StationMode values
                    if (key == "StationMode")
                    {
                        if(value == "vr")
                        {
                            values.Add($"{key}:VR");
                        } else
                        {
                            //Capitalise the first letter of the content or application mode
                            values.Add($"{key}:{value[0].ToString().ToUpper()}{value[1..]}");
                        }
                    } else {
                        values.Add($"{key}:{value}");
                    }
                }
            }

            //update all values in the config.txt
            Update(values);
        }

        /// <summary>
        /// Check for the necessary external applications, moving them if necessary
        /// </summary>
        private static void ExternalApplications()
        {
            //Check for SteamCMD and SetVol, moving them into the external folder if present
            if (File.Exists($@"C:\Users\{Environment.GetEnvironmentVariable("Directory")}\steamcmd\steamcmd.exe"))
            {
                Move($@"C:\Users\{Environment.GetEnvironmentVariable("Directory")}\steamcmd", externalPath);
            }

            if (File.Exists($@"C:\Users\{Environment.GetEnvironmentVariable("Directory")}\SetVol\SetVol.exe"))
            {
                Move($@"C:\Users\{Environment.GetEnvironmentVariable("Directory")}\SetVol", externalPath);
            }
        }

        /// <summary>
        /// Move a directory to another.
        /// </summary>
        private static void Move(string source, string destination)
        {
            try
            {
                Directory.Move(source, destination);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}
