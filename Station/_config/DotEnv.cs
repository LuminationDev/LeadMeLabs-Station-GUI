using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using LeadMeLabsLibrary;
using Station.Components._commandLine;
using Station.Components._notification;

namespace Station._config;

public static class DotEnv
{
    private static readonly string FilePath = $"{StationCommandLine.StationLocation}\\_config\\config.env";

    /// <summary>
    /// Load the variables within the config.env into the local environment for the running
    /// process.
    /// </summary>
    public static Task<bool> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                MockConsole.WriteLine($"StationError, Config file not found:{FilePath}", Enums.LogLevel.Error);
                return Task.FromResult(false);
            }

            //Decrypt the data in the file
            string decryptedText = EncryptionHelper.DetectFileEncryption(FilePath);
            if (string.IsNullOrEmpty(decryptedText))
            {
                MockConsole.WriteLine($"StationError, Config file empty:{FilePath}", Enums.LogLevel.Error);
                return Task.FromResult(false);
            }

            #if DEBUG
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        Environment.SetEnvironmentVariable("nucAddress", ip.ToString());
                    }
                }
                    
                #region override env vars here
                // Environment.SetEnvironmentVariable("AppKey", "testingDK");
                #endregion
            #endif

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
        } 
        catch (Exception ex)
        {
            MockConsole.WriteLine(ex.ToString(), Enums.LogLevel.Error);
        }

        return Task.FromResult(true);
    }
    
    /// <summary>
    /// Update part of the config.env, automatically detect if a variable already exists or if
    /// it should be added.
    /// </summary>
    /// <param name="key">The key of the environment variable to set.</param>
    /// <param name="value">The value of the provided key.</param>
    public static void Update(string key, string value)
    {
        if (!File.Exists(FilePath))
        {
            MockConsole.WriteLine($"Station Error,Config file not found:{FilePath}", Enums.LogLevel.Error);
            return;
        }

        Environment.SetEnvironmentVariable(key, value);

        bool exists = false;

        // Read the current config file
        string text = File.ReadAllText(FilePath);
        if (text.Length == 0)
        {
            MockConsole.WriteLine($"Station Error,Config file empty:{FilePath}", Enums.LogLevel.Error);
            return;
        }

        text = EncryptionHelper.DetectFileEncryption(FilePath);

        if (string.IsNullOrEmpty(text))
        {
            MockConsole.WriteLine($"Station Error,Config file returned null:{FilePath}", Enums.LogLevel.Error);
            return;
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
        bool success = EncryptionHelper.EncryptFile(string.Join("\n", listLine), FilePath);

        MockConsole.WriteLine(
            success
                ? $"Encrypted file: {FilePath} has been updated."
                : $"Encrypted file: {FilePath} has failed updating.", Enums.LogLevel.Normal);
    }
}
