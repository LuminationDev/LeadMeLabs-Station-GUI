using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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
            if(Environment.GetEnvironmentVariable("UserDirectory", EnvironmentVariableTarget.User) != null)
            {
                Migrate();
                ExternalApplications();

                //Update the launcher first
                Updater.UpdateLauncher();

                //Needs to exit the current application and start the 'new' launcher with a command line argument
                //Open launcher with command line
                string launcher = $@"C:\Users\{Environment.GetEnvironmentVariable("UserDirectory", EnvironmentVariableTarget.User)}\Launcher\LeadMe.exe";
                string arguments = $"--software=Station --directory=={Environment.GetEnvironmentVariable("UserDirectory", EnvironmentVariableTarget.User)}";

                Process temp = new();
                temp.StartInfo.FileName = launcher;
                temp.StartInfo.Arguments = arguments;
                temp.Start();
                temp.Close();

                //Immediately close the current application
                Environment.Exit(1);
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
            Environment.SetEnvironmentVariable("UserDirectory", new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)).Name);
#endif

            return true;
        }

        /// <summary>
        /// Migrate any local system variables over to the config.env
        /// </summary>
        public static void Migrate()
        {
            string[] EnvVariables = { "AppKey", "HeadsetType", "LabLocation", "NucAddress", "room", "StationId", "StationMode", "SteamUserName", "SteamPassword", "UserDirectory" };
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
                    } 
                    else if (key == "UserDirectory")
                    {
                        values.Add($"Directory:{value}");
                    }
                    else {
                        values.Add($"{key}:{value}");
                    }
                }
            }

            //Create the config.env file if it doesnt exist
            if(!File.Exists(filePath))
            {
                using (File.Create(filePath)) { };
            }
            
            //update all values in the config.txt
            Update(values);
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
            List<string> listLine = arrLine.ToList();

            // Loop over the supplied values
            foreach (string entry in values)
            {

                string[] split = entry.Split(':');
                string key = split[0];
                string value = split[1];

                Environment.SetEnvironmentVariable(key, value);

                bool exists = false;


                for (int i = 0; i < listLine.Count; i++)
                {
                    if (listLine[i].StartsWith(key))
                    {
                        listLine[i] = $"{key}={value}";
                        exists = true;
                    }
                }

                //If the file does not contain the env variable yet create if here
                if (!exists)
                {
                    listLine.Add($"{key}={value}");
                }
            }

            string encryptedText = EncryptionHelper.EncryptNode(string.Join("\n", listLine));

            //Rewrite the file with the new variables
            File.WriteAllText(filePath, encryptedText);
        }

        /// <summary>
        /// Check for the necessary external applications, moving them if necessary
        /// </summary>
        private static void ExternalApplications()
        {
            //Check for SteamCMD and SetVol, moving them into the external folder if present
            if (File.Exists($@"C:\Users\{Environment.GetEnvironmentVariable("Directory")}\steamcmd\steamcmd.exe"))
            {
                Move($@"C:\Users\{Environment.GetEnvironmentVariable("Directory")}\steamcmd", $@"{externalPath}\steamcmd");
            }

            if (File.Exists($@"C:\Users\{Environment.GetEnvironmentVariable("Directory")}\SetVol\SetVol.exe"))
            {
                Move($@"C:\Users\{Environment.GetEnvironmentVariable("Directory")}\SetVol", $@"{externalPath}\SetVol");
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
                MockConsole.WriteLine(e.Message, MockConsole.LogLevel.Normal);
            }
        }
    }
}
