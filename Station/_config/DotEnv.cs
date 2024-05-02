using System;
using System.IO;
using System.Threading.Tasks;
using LeadMeLabsLibrary;
using Station.Components._commandLine;
using Station.Components._notification;

namespace Station._config;

public static class DotEnv
{
    private static readonly string FilePath = $"{CommandLine.StationLocation}\\_config\\config.env";

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
}
