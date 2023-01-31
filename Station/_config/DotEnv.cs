using System;
using System.IO;

namespace Station
{
    public static class DotEnv
    {
        /// <summary>
        /// Load the variables within the config.env into the local environment for the running
        /// process.
        /// </summary>
        public static bool Load()
        {
            string filePath = $@"{CommandLine.stationLocation}\_config\config.env";

            if (!File.Exists(filePath))
            {
                SessionController.PassStationMessage($"StationError,Config file not found:{filePath}");
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
                    return false;
                }

                Environment.SetEnvironmentVariable(parts[0], parts[1]);
            }

            return true;
        }
    }
}
