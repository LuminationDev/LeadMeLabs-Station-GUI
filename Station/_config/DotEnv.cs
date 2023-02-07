using System;
using System.IO;

namespace Station
{
    public static class DotEnv
    {
        private static string filePath = $"{CommandLine.stationLocation}\\_config\\config.env";

        /// <summary>
        /// Load the variables within the config.env into the local environment for the running
        /// process.
        /// </summary>
        public static bool Load()
        {
            if (!File.Exists(filePath))
            {
                SessionController.PassStationMessage($"StationError,Config file not found:{filePath}");
                MockConsole.WriteLine($"Station Error: Config file not found at: {filePath}", MockConsole.LogLevel.Error);
                return false;
            }

            foreach (var line in File.ReadAllLines(filePath))
            {
                var parts = line.Split(
                    '=',
                    StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length != 2 && parts[0] != "Directory")
                {
                    SessionController.PassStationMessage($"StationError,Config incomplete:{parts[0]} has no value");
                    MockConsole.WriteLine($"Station Error: Config file missing value: {parts[0]}", MockConsole.LogLevel.Error);
                    return false;
                }

                Environment.SetEnvironmentVariable(parts[0], parts[1]);
            }

            return true;
        }

        /// <summary>
        /// Update part of the config.env, automatically detect if a variable already exists or if
        /// it should be added.
        /// </summary>
        /// <param name="key">The key of the environment variable to set.</param>
        /// <param name="value">The value of the provided key.</param>
        public static void Update(string key, string value)
        {
            if (!File.Exists(filePath))
            {
                MockConsole.WriteLine($"NUCError,Config file not found:{filePath}");
                return;
            }

            Environment.SetEnvironmentVariable(key, value);

            bool exists = false;

            string[] arrLine = File.ReadAllLines(filePath);
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

            //Rewrite the file with the new variables
            File.WriteAllLines(filePath, arrLine);
        }

        /// <summary>
        /// Migrate any local system variables over to the config.env
        /// </summary>
        public static void Migrate()
        {
            string[] EnvVariables = { "AppKey", "HeadsetType", "LabLocation", "NucAddress", "room", "StationId", "StationMode", "SteamUserName", "SteamPassword", "Directory" };

            foreach (string key in EnvVariables)
            {
                string? value = Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.User);

                Update(key, value ?? "");
            }
        }
    }
}
