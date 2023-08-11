using System;
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json;

namespace Station
{
    public class RoomSetup
    {
        private static readonly string defaultFilePath = CommandLine.stationLocation + @"\_config\chaperone_info.vrchap";
        private static readonly string steamVRFilePath = @"C:\Program Files (x86)\Steam\config\chaperone_info.vrchap";

        public static string GetSteamVRPath()
        {
            return steamVRFilePath;
        }

        /// <summary>
        /// The SteamVR room setup has just been completed, input the default collision_bounds and play_area. Afterwards
        /// save the file for future comparison and loading. 
        /// </summary>
        public static string SaveRoomSetup()
        {
            // Read the JSON file
            if(!File.Exists(steamVRFilePath))
            {
                Logger.WriteLog($"SaveRoomSetup - chaperone_info.vrchap does not exist in SteamVR at {steamVRFilePath}", MockConsole.LogLevel.Error);
                return @"chaperone_info.vrchap not found. Attempt SteamVR quick calibrate again.";
            }

            string jsonContent = File.ReadAllText(steamVRFilePath);
            ChaperoneInfo? chaperoneInfo = null;

            // Attempt to deserialize the JSON string
            try
            {
                chaperoneInfo = JsonConvert.DeserializeObject<ChaperoneInfo>(jsonContent);
            }
            catch (Exception ex)
            {
                Logger.WriteLog($"SaveRoomSetup - Unable to DeserializeObject chaperone_info.vrchap. {ex}", MockConsole.LogLevel.Error);
            }

            if(chaperoneInfo == null)
            {
                Logger.WriteLog($"SaveRoomSetup - Unable to DeserializeObject chaperone_info.vrchap at {steamVRFilePath}", MockConsole.LogLevel.Error);
                return "Unable to DeserializeObject chaperone_info.vrchap";
            }

            // Modify specific properties
            float[][][] newCollisionBounds = DefaultValues.GetCollisionBounds();
            float[] newPlayArea = DefaultValues.GetPlayArea();

            chaperoneInfo.universes[0].collision_bounds = newCollisionBounds;
            chaperoneInfo.universes[0].play_area = newPlayArea;

            // Serialize the modified object back to JSON
            try
            {
                string modifiedJson = JsonConvert.SerializeObject(chaperoneInfo, Formatting.Indented);
                File.WriteAllText(defaultFilePath, modifiedJson);
            }
            catch (Exception ex)
            {
                Logger.WriteLog($"SaveRoomSetup - Saving file error: {ex}", MockConsole.LogLevel.Error);
                return $"Saving file error: {ex}";
            }

            LoadRoomSetup();

            return "Successfully modified, saved and moved.";
        }

        /// <summary>
        /// Compare the current SteamVR chaperone_info.vrchap with the saved default, if there is one. If there is a 
        /// difference replace the new one with the saved one.
        /// </summary>
        public static void CompareRoomSetup()
        {
            // Check if the files exist
            bool defaultExists = File.Exists(defaultFilePath);
            bool steamExists = File.Exists(steamVRFilePath);

            Logger.WriteLog($"CompareRoomSetup - (chaperone_info.vrchap) steamExists: {steamExists}. defaultExists: {defaultExists}", MockConsole.LogLevel.Debug);

            if (defaultExists && steamExists)
            {
                // Compare the content
                bool filesAreEqual = AreFilesEqual(steamVRFilePath, defaultFilePath);
                if (!filesAreEqual)
                {
                    Logger.WriteLog("CompareRoomSetup - SteamVR is not equal to the Default", MockConsole.LogLevel.Error);
                    LoadRoomSetup();
                }
                else
                {
                    Logger.WriteLog("CompareRoomSetup - chaperone_info.vrchap is in working order, no action necessary", MockConsole.LogLevel.Error);
                }
            }
            else if (defaultExists)
            {
                LoadRoomSetup();
                Logger.WriteLog("CompareRoomSetup - SteamVR chaperone_info.vrchap does not exist, replacing with Default", MockConsole.LogLevel.Error);
            }
            else if (steamExists)
            {
                Logger.WriteLog("CompareRoomSetup - SteamVR chaperone_info.vrchap does exist, Default does not", MockConsole.LogLevel.Error);
            }
            else
            { 
                Logger.WriteLog($"CompareRoomSetup - chaperone_info.vrchap does not exist in SteamVR: {steamVRFilePath}. Or in _config: {defaultFilePath}, ROOM SETUP REQUIRED", MockConsole.LogLevel.Error);
            }
        }

        /// <summary>
        /// Replace the current chaperone_info.vrchap with the saved one.
        /// </summary>
        private static void LoadRoomSetup()
        {
            try
            {
                if (File.Exists(steamVRFilePath))
                {
                    File.SetAttributes(steamVRFilePath, FileAttributes.Normal); // Clear read-only attribute
                }

                File.Copy(defaultFilePath, steamVRFilePath, true);
                Logger.WriteLog("LoadRoomSetup - chaperone_info.vrchap file moved successfully.", MockConsole.LogLevel.Normal);
            }
            catch (Exception ex)
            {
                Logger.WriteLog($"LoadRoomSetup - chaperone_info.vrchap move failed. {ex}", MockConsole.LogLevel.Normal);
            }
        }

        /// <summary>
        /// Calculates the MD5 hash of a file's contents.
        /// </summary>
        /// <param name="filePath">The path to the file.</param>
        /// <returns>The MD5 hash as a lowercase string.</returns>
        private static string CalculateMD5Hash(string filePath)
        {
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hashBytes = md5.ComputeHash(stream);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }

        /// <summary>
        /// Compares the contents of two files using their MD5 hashes.
        /// </summary>
        /// <param name="filePath1">The path to the first file.</param>
        /// <param name="filePath2">The path to the second file.</param>
        /// <returns>True if the file contents are the same, false otherwise.</returns>
        private static bool AreFilesEqual(string filePath1, string filePath2)
        {
            string hash1 = CalculateMD5Hash(filePath1);
            string hash2 = CalculateMD5Hash(filePath2);

            return hash1 == hash2;
        }
    }

    class ChaperoneInfo
    {
        public string jsonid { get; set; }
        public Universe[] universes { get; set; }
        public int version { get; set; }
    }

    class Universe
    {
        public float[][][] collision_bounds { get; set; }
        public float[] play_area { get; set; }
        public Pose seated { get; set; }
        public Pose standing { get; set; }
        public string time { get; set; }
        public Tracker[] trackers { get; set; }
        public string universeID { get; set; }
    }

    class Pose
    {
        public float[] translation { get; set; }
        public float yaw { get; set; }
    }

    class Tracker
    {
        public float[] angOffset { get; set; }
        public float[] posOffset { get; set; }
        public string serial { get; set; }
    }
}
