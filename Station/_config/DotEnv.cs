using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using LeadMeLabsLibrary;
using Station.Components._commandLine;
using Station.Components._notification;
using Station.Components._utils;

namespace Station._config;

public static class DotEnv
{
    private static readonly string PrimaryFilePath = $"{StationCommandLine.StationLocation}\\_config\\config.env";
    private static readonly string BackupFilePath = $"{StationCommandLine.StationLocation}\\_config\\config_backup.env";

    /// <summary>
    /// Loads environment variables asynchronously and handles any exceptions that may occur.
    /// </summary>
    /// <returns>True if the environment variables are loaded successfully, false otherwise.</returns>
    public static async Task<bool> LoadEnvironmentVariablesAsync()
    {
        try
        {
            bool success = await Load(PrimaryFilePath);
            if (!success)
            {
                success = await Load(BackupFilePath);
            }

            return success;
        }
        catch (Exception ex)
        {
            Logger.WriteLog("Failed loading ENV variables", Enums.LogLevel.Error);
            Logger.WriteLog(ex, Enums.LogLevel.Error);
            return false;
        }
    }
    
    /// <summary>
    /// Load the variables within the config.env into the local environment for the running
    /// process.
    /// </summary>
    /// <param name="filePath">The path to the config file.</param>
    /// <param name="isBackUp">If the filePath leads to the config_backup.env.</param>
    private static Task<bool> Load(string filePath, bool isBackUp = false)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                MockConsole.WriteLine($"StationError, Config file not found:{filePath}", Enums.LogLevel.Error);
                return Task.FromResult(false);
            }

            //Decrypt the data in the file
            string decryptedText = EncryptionHelper.DetectFileEncryption(filePath);
            if (string.IsNullOrEmpty(decryptedText))
            {
                MockConsole.WriteLine($"StationError, Config file empty:{filePath}", Enums.LogLevel.Error);
                return Task.FromResult(false);
            }

            foreach (var line in decryptedText.Split('\n'))
            {
                var parts = line.Split(
                    '=',
                    2,
                    StringSplitOptions.RemoveEmptyEntries);

                switch (parts.Length)
                {
                    case <= 0:
                        continue;
                    case 1 when parts[0] != "Directory":
                        MockConsole.WriteLine($"StationError,Config incomplete:{parts[0]} has no value", Enums.LogLevel.Error);
                        return Task.FromResult(false);
                    default:
                        Environment.SetEnvironmentVariable(parts[0], parts[1]);
                        break;
                }
            }
            
            //If we are reading the backup, replace the main config.env as it must of been corrupt or missing
            if (isBackUp)
            {
                ReplaceConfig();
            }
            
#if DEBUG
            IPAddress? ip = SystemInformation.GetIPAddress();
            Environment.SetEnvironmentVariable("nucAddress", ip?.ToString());
#endif
        } 
        catch (Exception ex)
        {
            MockConsole.WriteLine(ex.ToString(), Enums.LogLevel.Error);
        }

        return Task.FromResult(true);
    }

    /// <summary>
    /// Replace the config.env with the config_backup.env in the case the config.env is corrupt or missing.
    /// </summary>
    private static void ReplaceConfig()
    {
        try
        {
            File.Copy(BackupFilePath, PrimaryFilePath, true);
            Logger.WriteLog("File replaced successfully.", Enums.LogLevel.Error);
        }
        catch (IOException ex)
        {
            Logger.WriteLog($"An error occurred: {ex.Message}", Enums.LogLevel.Error);
        }
    }

    /// <summary>
    /// Iterate through a supplied dictionary of key value pairs and update the config.env and config_backup.env with
    /// the new values.
    /// </summary>
    /// <param name="values">A dictionary of key value string pairs.</param>
    public static void Update(Dictionary<string, string> values)
    {
        foreach (KeyValuePair<string, string> kvp in values)
        {
            Update(PrimaryFilePath, kvp.Key, kvp.Value);
            Update(BackupFilePath, kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    /// Update part of the primary config file, automatically detect if a variable already exists or if
    /// it should be added.
    /// </summary>
    /// <param name="key">The key of the environment variable to set.</param>
    /// <param name="value">The value of the provided key.</param>
    public static void Update(string key, string value)
    {
        Update(PrimaryFilePath, key, value);
    }
    
    /// <summary>
    /// Update part of a config file, automatically detect if a variable already exists or if
    /// it should be added.
    /// </summary>
    /// <param name="filePath">The path to the file that will be edited.</param>
    /// <param name="key">The key of the environment variable to set.</param>
    /// <param name="value">The value of the provided key.</param>
    private static void Update(string filePath, string key, string value)
    {
        if (!File.Exists(filePath))
        {
            MockConsole.WriteLine($"Station Error,Config file not found:{filePath}. Creating now.", Enums.LogLevel.Error);
            File.Create(filePath);
        }

        Environment.SetEnvironmentVariable(key, value);

        bool exists = false;
        
        // Read the current config file
        string text = File.ReadAllText(filePath);
        if (text.Length == 0)
        {
            MockConsole.WriteLine($"Station Error,Config file empty:{filePath}", Enums.LogLevel.Error);
            return;
        }

        try
        {
            text = EncryptionHelper.DetectFileEncryption(filePath);
            if (string.IsNullOrEmpty(text))
            {
                MockConsole.WriteLine($"Station Error,Config file returned null:{filePath}", Enums.LogLevel.Error);
                return;
            }
        }
        catch (Exception ex)
        {
            MockConsole.WriteLine($"Station Error,Config file cannot be read:{filePath}. {ex.Message}", Enums.LogLevel.Error);
        }

        string[] arrLine = text.Split("\n");
        List<string> listLine = arrLine.ToList();

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
        
        //Rewrite the file with the new variables
        bool success = EncryptionHelper.EncryptFile(string.Join("\n", listLine), filePath);
        MockConsole.WriteLine(
            success
                ? $"Encrypted file: {filePath} has been updated."
                : $"Encrypted file: {filePath} has failed updating.", Enums.LogLevel.Normal);
    }
    
    /// <summary>
    /// Get the path to the currently running executable and start a new instance of the application before exiting
    /// the current one.
    /// </summary>
    public static void RestartApplication()
    {
        var executablePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (executablePath == null)
        {
            MessageBox.Show("An error has occurred please restart manually.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        Process.Start(executablePath);
        Application.Current.Shutdown();
    }
}
