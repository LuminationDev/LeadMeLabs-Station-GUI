using System;
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Station.Components._commandLine;
using Station.Components._notification;

namespace Station.Components._utils._steamConfig;

public static class RoomSetup
{
    private static readonly string DefaultFilePath = CommandLine.StationLocation + @"\_config\chaperone_info.vrchap";
    private const string SteamVrFilePath = @"C:\Program Files (x86)\Steam\config\chaperone_info.vrchap";
    private const string HtcFilePath = @"C:\Program Files (x86)\Steam\config\htc_business_streaming\chaperone_info.vrchap";
    private static string? currentFilePath;

    /// <summary>
    /// Determine what sort of headset is currently being used, this changes where the chaperone_info.vrchap is
    /// located for SteamVR.
    /// </summary>
    private static bool DetermineFiletype()
    {
        string headset = (Environment.GetEnvironmentVariable("HeadsetType", EnvironmentVariableTarget.Process) ?? "Unknown");
        switch (headset)
        {
            case "VivePro1":
            case "VivePro2":
                currentFilePath = SteamVrFilePath;
                break;
            case "ViveFocus3":
                currentFilePath = HtcFilePath;
                break;
        }

        return currentFilePath != null;
    }
    
    /// <summary>
    /// The SteamVR room setup has just been completed, input the default collision_bounds and play_area. Afterwards
    /// save the file for future comparison and loading. 
    /// </summary>
    public static string SaveRoomSetup()
    {
        if (!DetermineFiletype())
        {
            Logger.WriteLog($"SaveRoomSetup - chaperone_info.vrchap does not exist in SteamVR at {currentFilePath}", MockConsole.LogLevel.Error);
            return @"chaperone_info.vrchap not found. Attempt SteamVR quick calibrate again.";
        }
        
        // Read the JSON file
        if(!File.Exists(currentFilePath))
        {
            Logger.WriteLog($"SaveRoomSetup - chaperone_info.vrchap does not exist in SteamVR at {currentFilePath}", MockConsole.LogLevel.Error);
            return @"chaperone_info.vrchap not found. Attempt SteamVR quick calibrate again.";
        }

        string jsonContent = File.ReadAllText(currentFilePath);
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
            Logger.WriteLog($"SaveRoomSetup - Unable to DeserializeObject chaperone_info.vrchap at {currentFilePath}", MockConsole.LogLevel.Error);
            return "Unable to DeserializeObject chaperone_info.vrchap";
        }

        // Modify specific properties
        float[][][] newCollisionBounds = DefaultValues.GetCollisionBounds();
        float[] newPlayArea = DefaultValues.GetPlayArea();

        // Find the most recent universe (last in the .vrchap file)
        int mostRecent = chaperoneInfo.universes.Length - 1;
        if (chaperoneInfo.universes[mostRecent] == null)
        {
            Logger.WriteLog($"SaveRoomSetup - Most recent universe [{mostRecent}], cannot be found.", MockConsole.LogLevel.Error);
            return $"Saving file error.";
        }

        chaperoneInfo.universes[mostRecent].collision_bounds = newCollisionBounds;
        chaperoneInfo.universes[mostRecent].play_area = newPlayArea;

        // Serialize the modified object back to JSON
        try
        {
            string modifiedJson = JsonConvert.SerializeObject(chaperoneInfo, Formatting.Indented);
            File.WriteAllText(DefaultFilePath, modifiedJson);
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
        if (!DetermineFiletype() || currentFilePath == null)
        {
            Logger.WriteLog($"SaveRoomSetup - chaperone_info.vrchap does not exist in SteamVR at {currentFilePath}", MockConsole.LogLevel.Error);
            return;
        }
        
        // Check if the files exist
        bool defaultExists = File.Exists(DefaultFilePath);
        bool steamExists = File.Exists(currentFilePath);

        Logger.WriteLog($"CompareRoomSetup - (chaperone_info.vrchap) steamExists: {steamExists}. defaultExists: {defaultExists}", MockConsole.LogLevel.Debug);

        if (defaultExists && steamExists)
        {
            // Compare the content
            bool filesAreEqual = AreFilesEqual(currentFilePath, DefaultFilePath);
            if (!filesAreEqual)
            {
                Logger.WriteLog("CompareRoomSetup - SteamVR is not equal to the Default", MockConsole.LogLevel.Info);
                LoadRoomSetup();
            }
            else
            {
                Logger.WriteLog("CompareRoomSetup - chaperone_info.vrchap is in working order, no action necessary", MockConsole.LogLevel.Info);
            }
        }
        else if (defaultExists)
        {
            LoadRoomSetup();
            Logger.WriteLog("CompareRoomSetup - SteamVR chaperone_info.vrchap does not exist, replacing with Default", MockConsole.LogLevel.Info);
        }
        else if (steamExists)
        {
            Logger.WriteLog("CompareRoomSetup - SteamVR chaperone_info.vrchap does exist, Default does not", MockConsole.LogLevel.Info);
        }
        else
        { 
            Logger.WriteLog($"CompareRoomSetup - chaperone_info.vrchap does not exist in SteamVR: {currentFilePath}. Or in _config: {DefaultFilePath}, ROOM SETUP REQUIRED", MockConsole.LogLevel.Error);
        }
    }

    /// <summary>
    /// Replace the current chaperone_info.vrchap with the saved one.
    /// </summary>
    private static void LoadRoomSetup()
    {
        if (currentFilePath == null)
        {
            Logger.WriteLog($"LoadRoomSetup - currentFilePath is null. Check HeadsetType", MockConsole.LogLevel.Normal);
            return;
        }
        
        try
        {
            if (File.Exists(currentFilePath))
            {
                File.SetAttributes(currentFilePath, FileAttributes.Normal); // Clear read-only attribute
            }

            File.Copy(DefaultFilePath, currentFilePath, true);
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
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        
        byte[] hashBytes = md5.ComputeHash(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
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
