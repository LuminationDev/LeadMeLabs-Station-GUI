using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        public async static Task<bool> Load()
        {
            //TODO make a call to a secure database in the future


#if RELEASE
            //Check if local system variables exists, if so perform a migration
            if(Environment.GetEnvironmentVariable("UserDirectory", EnvironmentVariableTarget.User) != null && !File.Exists(filePath))
            {
                MockConsole.WriteLine("Old version detected, performing migration", MockConsole.LogLevel.Normal);

                try
                {
                    Migrate();
                    ExternalApplications();

                    //Update the launcher first
                    await Updater.UpdateLauncher();
                }
                catch (Exception ex)
                {
                    MockConsole.WriteLine(ex.ToString(), MockConsole.LogLevel.Error);
                }
            }
#endif

            try
            {
                if (!File.Exists(filePath))
                {
                    MockConsole.WriteLine($"StationError, Config file not found:{filePath}");
                    return false;
                }

                //Decrypt the data in the file
                string text = File.ReadAllText(filePath);
                if (text.Length == 0)
                {
                    MockConsole.WriteLine($"StationError, Config file empty:{filePath}");
                    return false;
                }

                string decryptedText = EncryptionHelper.DecryptNode(text);

                foreach (var line in decryptedText.Split('\n'))
                {
                    var parts = line.Split(
                        '=',
                        StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length > 0)
                    {
                        if (parts.Length == 1 && parts[0] != "Directory")
                        {
                            MockConsole.WriteLine($"StationError,Config incomplete:{parts[0]} has no value", MockConsole.LogLevel.Error);
                            return false;
                        }

                        if (parts.Length > 1)
                        {
                            Environment.SetEnvironmentVariable(parts[0], parts[1]);
                        }
                    }
                }
            } catch (Exception ex)
            {
                MockConsole.WriteLine(ex.ToString(), MockConsole.LogLevel.Error);
            }

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
            if(text.Length != 0)
            {
                text = EncryptionHelper.DecryptNode(text);
            }
            string[] arrLine = text.Split("\n");
            List<string> listLine;

            if(arrLine.Length == 0)
            {
                listLine = new();
            } else
            {
                listLine = arrLine.ToList();
            }

            // Loop over the supplied values
            foreach (string entry in values)
            {

                string[] split = entry.Split(':');
                string key = split[0];
                string value = split[1];

                Environment.SetEnvironmentVariable(key, value);

                bool exists = false;

                if (listLine.Count > 0)
                {
                    for (int i = 0; i < listLine.Count; i++)
                    {
                        if (listLine[i].StartsWith(key))
                        {
                            listLine[i] = $"{key}={value}";
                            exists = true;
                        }
                    }
                }

                //If the file does not contain the env variable yet create if here
                if (!exists)
                {
                    listLine.Add($"{key}={value}");
                }
            }

            MockConsole.WriteLine($"Environment variables added", MockConsole.LogLevel.Normal);

            string encryptedText = EncryptionHelper.EncryptNode(string.Join("\n", listLine));

            //Rewrite the file with the new variables
            File.WriteAllText(filePath, encryptedText);
        }

        /// <summary>
        /// Check for the necessary external applications, moving them if necessary
        /// </summary>
        private static void ExternalApplications()
        {
            MockConsole.WriteLine("Moving steamcmd and SetVol to external folder.");

            string directory = Environment.GetEnvironmentVariable("UserDirectory");

            if (directory == null) return;

            //Create the sub-directories
            if (!Directory.Exists(externalPath))
            {
                MockConsole.WriteLine("External folder not found. Creating now");
                MockConsole.WriteLine($"External: {externalPath}");

                Directory.CreateDirectory(externalPath);
            }

            //Check for SteamCMD and SetVol, moving them into the external folder if present
            if (File.Exists($@"C:\Users\{directory}\steamcmd\steamcmd.exe"))
            {
                MockConsole.WriteLine("Found steamcmd. Attempting to move.");
                Updater.Move($@"C:\Users\{directory}\steamcmd", $@"{externalPath}\steamcmd");
            }

            if (File.Exists($@"C:\Users\{directory}\SetVol\SetVol.exe"))
            {
                MockConsole.WriteLine("Found SetVol. Attempting to move.");
                Updater.Move($@"C:\Users\{directory}\SetVol", $@"{externalPath}\SetVol");
            }
        }
    }
}
