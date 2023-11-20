using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;
using LeadMeLabsLibrary;
using Newtonsoft.Json;

namespace Station._qa.checks;

public class SoftwareChecks
{
    private List<QaCheck> _qaChecks = new();
    public async Task<List<QaCheck>> RunQa(string labType)
    {
        if (labType.ToLower().Equals("online"))
        {
            _qaChecks.Add(await IsLatestSoftwareVersion());
        }
        _qaChecks.Add(IsSetToProductionMode(labType));
        _qaChecks.Add(IsSetVolPresent());
        _qaChecks.Add(IsSteamCmdPresent());
        _qaChecks.Add(IsSteamCmdInitialised());
        _qaChecks.Add(IsSteamCmdConfigured());
        _qaChecks.Add(IsAmdInstalled());
        _qaChecks.Add(IsDriverEasyNotInstalled());
        _qaChecks.Add(IsNvidiaNotInstalled());

        return _qaChecks;
    }

    public List<QaCheck> RunSlowQaChecks(string labType)
    {
        List<QaCheck> qaChecks = new List<QaCheck>();
        qaChecks.Add(IsSteamGuardDisabled());
        return qaChecks;
    }
    
    private async Task<QaCheck> IsLatestSoftwareVersion()
    {
        QaCheck qaCheck = new QaCheck("latest_software_version");
        
        // Call the production heroku to collect the latest version number
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            var response = httpClient.GetAsync("http://learninglablauncher.herokuapp.com/program-station-version").GetAwaiter().GetResult();

            string remoteVersion = "";
            // Check if the request was successful (status code 200 OK)
            if (response.IsSuccessStatusCode)
            {
                // Read and print the content
                var content = await response.Content.ReadAsStringAsync();
                var split = content.Split(" ");
                remoteVersion = split[0];
            }
            else
            {
                qaCheck.SetFailed($"learninglablauncher.herokuapp.com/program-nuc-version request failed with status code: {response.StatusCode}");
            }
            
            string localVersion = Updater.GetVersionNumber() ?? "Unknown";
                
            // Parse version strings
            Version version1 = Version.Parse(localVersion);
            Version version2 = Version.Parse(remoteVersion);

            // Compare versions
            int comparisonResult = version1.CompareTo(version2);

            switch (comparisonResult)
            {
                case < 0:
                    qaCheck.SetFailed($"Local version {version1}, is less than latest {version2}");
                    break;
                case > 0:
                    qaCheck.SetFailed($"Local version {version1}, is greater than latest {version2}. Might be development branch.");
                    break;
                default:
                    qaCheck.SetPassed($"Version is {version1}");
                    break; 
            }
        }
        catch (Exception e)
        {
            qaCheck.SetFailed($"Unexpected error: {e}");
        }
        
        return qaCheck;
    }

    private QaCheck IsSetToProductionMode(string labType)
    {
        QaCheck qaCheck = new QaCheck("production_mode");
        
        //Load the local appData/Roaming folder path
        string manifestPath = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "leadme_apps", "manifest.json"));

        if(!File.Exists(manifestPath))
        {
            qaCheck.SetFailed("Could not find manifestPath at location: " + manifestPath);
            return qaCheck;
        }
        
        //Read the manifest
        string? decryptedText = EncryptionHelper.DetectFileEncryption(manifestPath);

        dynamic? array = JsonConvert.DeserializeObject(decryptedText);
        if (array == null)
        {
            qaCheck.SetFailed("Failed to DeserializeObject in file: " + manifestPath);
            return qaCheck;;
        }
        
        // Determine the mode required for the lab
        string preferredMode = labType.Equals("Online") ? "production" : "offline" ;

        foreach (var item in array)
        {
            //Launcher entry is only there if a user has changed it away from production, otherwise it defaults to production
            if (item.type == "Launcher")
            {
                string mode = (string)item.mode;
                if (mode.ToLower().Equals(preferredMode))
                {
                    qaCheck.SetPassed(null);
                }
                else
                {
                    qaCheck.SetFailed($"Launcher is set to: {item.mode}");
                }

                return qaCheck;
            };
        }

        qaCheck.SetPassed("Launcher is defaulting to production");
        return qaCheck;
    }

    /// <summary>
    /// Check if SetVol is present within the Stations' external folder.
    /// </summary>
    private QaCheck IsSetVolPresent()
    {
        QaCheck qaCheck = new QaCheck("setvol_installed");
        string filePath = CommandLine.stationLocation + @"\external\SetVol\SetVol.exe";
        if (File.Exists(filePath))
        {
            qaCheck.SetPassed(null);
        }
        else
        {
            qaCheck.SetFailed("Could not find SetVol at location: " + filePath);
        }

        return qaCheck;
    }
    
    /// <summary>
    /// Check if SteamCMD is present within the Stations' external folder.
    /// </summary>
    private QaCheck IsSteamCmdPresent()
    {
        QaCheck qaCheck = new QaCheck("steamcmd_installed");
        string filePath = CommandLine.stationLocation + @"\external\steamcmd\steamcmd.exe";
        if (File.Exists(filePath))
        {
            qaCheck.SetPassed(null);
        }
        else
        {
            qaCheck.SetFailed("Could not find SteamCMD at location: " + filePath);
        }

        return qaCheck;
    }
    
    /// <summary>
    /// Check if SteamCMD has been initialised.
    /// </summary>
    private QaCheck IsSteamCmdInitialised()
    {
        QaCheck qaCheck = new QaCheck("steamcmd_initialised");
        string filePath = CommandLine.stationLocation + @"\external\steamcmd\steamerrorreporter.exe";
            
        if(!File.Exists(filePath))
        {
            qaCheck.SetFailed("SteamCMD was not initialised at location: " + filePath);
        }
        else
        {
            qaCheck.SetPassed(null);
        }

        return qaCheck;
    }

    /// <summary>
    /// Check if the Steam guard has been entered (or disabled) and that the local details are correct.
    /// </summary>
    private QaCheck IsSteamCmdConfigured()
    {
        QaCheck qaCheck = new QaCheck("steamcmd_configured");
        string loginDetails = Environment.GetEnvironmentVariable("SteamUserName", EnvironmentVariableTarget.Process) + " " + 
                              Environment.GetEnvironmentVariable("SteamPassword", EnvironmentVariableTarget.Process);
        string loginUser = $"+login {loginDetails}";
        string quit = " +quit";
        
        string fullPath = CommandLine.stationLocation + CommandLine.steamCmd;
        string? output = "";
        string? error = "";
        
        //Need to kill the process if there is a Guard Code input require so process creation is here instead of CommandLine
        Process? cmd = new Process();
        cmd.StartInfo.FileName = fullPath;
        cmd.StartInfo.RedirectStandardInput = true;
        cmd.StartInfo.RedirectStandardError = true;
        cmd.StartInfo.RedirectStandardOutput = true;
        cmd.StartInfo.CreateNoWindow = true;
        cmd.StartInfo.UseShellExecute = false;
        
        cmd.StartInfo.Arguments = "\"+force_install_dir \\\"C:/Program Files (x86)/Steam\\\"\" " + loginUser + quit;
        cmd.Start();
        
        cmd.OutputDataReceived += (s, e) => { output += e.Data + "\n"; };
        cmd.ErrorDataReceived += (s, e) => { error += e.Data + "\n"; };

        cmd.BeginOutputReadLine();
        cmd.BeginErrorReadLine();
        cmd.StandardInput.Flush();
        cmd.StandardInput.Close();
        cmd.WaitForExit();

        string response = "";
        
        if (output == null)
        {
            qaCheck.SetFailed("No output from SteamCMD");
        }
        else if (output.Contains("Steam Guard code:"))
        {
            qaCheck.SetFailed("Steam Guard code has not been set for SteamCMD");
        }
        else if (output.Contains("FAILED (Invalid Login Auth Code)"))
        {
            qaCheck.SetFailed("Steam Guard code that was provided was invalid");
        }
        else if (output.Contains("Invalid Password"))
        {
            qaCheck.SetFailed("Invalid password or username");
        }
        else if (output.Contains("OK"))
        {
            qaCheck.SetPassed(null);
        }

        //Manually kill the process or it will stay on the guard code input 
        cmd.Kill(true);

        return qaCheck;
    }
    
    /// <summary>
    /// Check if the Steam guard has been entered (or disabled) and that the local details are correct.
    /// </summary>
    private QaCheck IsSteamGuardDisabled()
    {
        QaCheck qaCheck = new QaCheck("steam_guard_disabled");
        string loginDetails = Environment.GetEnvironmentVariable("SteamUserName", EnvironmentVariableTarget.Process) + " " + 
                              Environment.GetEnvironmentVariable("SteamPassword", EnvironmentVariableTarget.Process);
        string loginUser = $"+login {loginDetails}";
        string quit = " +quit";
        
        string fullPath = CommandLine.stationLocation + CommandLine.steamCmdFolder;
        string? output = "";
        string? error = "";

        PowerShell powerShell = PowerShell.Create();
        powerShell.AddCommand("Remove-Item").AddParameter("Path", $"{fullPath}temp").AddParameter("Recurse");
        powerShell.Invoke();
        powerShell = PowerShell.Create();
        powerShell.AddCommand("New-Item").AddArgument($"{fullPath}temp").AddParameter("ItemType", "Directory");
        powerShell.Invoke();
        powerShell = PowerShell.Create();
        powerShell.AddCommand("Copy-Item").AddParameter("Path", $"{fullPath}steamcmd.exe").AddParameter("Destination", $"{fullPath}temp");
        powerShell.Invoke();

        //Need to kill the process if there is a Guard Code input require so process creation is here instead of CommandLine
        if (!File.Exists(fullPath + "temp\\steamcmd.exe"))
        {
            qaCheck.SetWarning("Could not complete Steam Guard check");
            return qaCheck;
        }
        Process? cmd = new Process();
        cmd.StartInfo.FileName = fullPath + "temp\\steamcmd.exe";
        cmd.StartInfo.RedirectStandardInput = true;
        cmd.StartInfo.RedirectStandardError = true;
        cmd.StartInfo.RedirectStandardOutput = true;
        cmd.StartInfo.CreateNoWindow = true;
        cmd.StartInfo.UseShellExecute = false;

        cmd.StartInfo.Arguments = "\"+force_install_dir \\\"C:/Program Files (x86)/Steam\\\"\" " + loginUser + quit;
        cmd.Start();
        
        cmd.OutputDataReceived += (s, e) => { output += e.Data + "\n"; };
        cmd.ErrorDataReceived += (s, e) => { error += e.Data + "\n"; };

        cmd.BeginOutputReadLine();
        cmd.BeginErrorReadLine();
        cmd.StandardInput.Flush();
        cmd.StandardInput.Close();
        cmd.WaitForExit();

        string response = "";
        
        if (output == null)
        {
            qaCheck.SetFailed("No output from SteamCMD");
        }
        else if (output.Contains("Steam Guard code:"))
        {
            qaCheck.SetFailed("Steam guard is still enabled");
        }
        else if (output.Contains("OK"))
        {
            qaCheck.SetPassed(null);
        }

        //Manually kill the process or it will stay on the guard code input 
        cmd.Kill(true);

        return qaCheck;
    }
    
    /// <summary>
    /// Checks if AMD Adrenalin is installed on the system.
    /// </summary>
    private QaCheck IsAmdInstalled()
    {
        QaCheck qaCheck = new QaCheck("amd_installed");

        try
        {
            using PowerShell powerShell = PowerShell.Create();
            powerShell.AddCommand("Get-ItemProperty")
                .AddParameter("Path", "HKLM:\\Software\\AMD\\CN");
            var results = powerShell.Invoke();

            if (results.Count > 0 && results[0] != null)
            {
                var adrenalin = results[0].Properties["Adrenalin"]?.Value.ToString();
                if (adrenalin is "True")
                {
                    qaCheck.SetPassed(null);
                    return qaCheck;
                }
            }
            qaCheck.SetFailed("AMD Adrenalin is not installed.");
        }
        catch (Exception ex)
        {
            qaCheck.SetFailed($"Error: {ex.Message}");
        }

        return qaCheck;
    }
    
    /// <summary>
    /// Checks that DriverEasy is not installed
    /// </summary>
    private QaCheck IsDriverEasyNotInstalled()
    {
        QaCheck qaCheck = new QaCheck("drivereasy_not_installed");
        PowerShell powerShell = PowerShell.Create();
        powerShell.AddCommand("Get-ItemProperty")
            .AddParameter("Path", "HKLM:\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\*")
            .AddCommand("Where-Object").AddArgument(ScriptBlock.Create("$_.DisplayName -Like '*Driver Easy*'"));
        var output = powerShell.Invoke();
        if (output.Count == 0)
        {
            qaCheck.SetPassed("Could not find DriverEasy");
            return qaCheck;
        }
        qaCheck.SetFailed("Found DriverEasy at location: " + output[0].Properties.Where(info => info.Name.Contains("App Path")).First()?.Value);
        return qaCheck;
    }
    
    /// <summary>
    /// Checks that NVIDIA is not installed
    /// </summary>
    private QaCheck IsNvidiaNotInstalled()
    {
        QaCheck qaCheck = new QaCheck("nvidia_not_installed");
        PowerShell powerShell = PowerShell.Create();
        powerShell.AddCommand("Get-ItemProperty")
            .AddParameter("Path", "HKLM:\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\*")
            .AddCommand("Where-Object").AddArgument(ScriptBlock.Create("$_.DisplayName -Like '*NVIDIA*'"));
        var output = powerShell.Invoke();
        if (output.Count == 0)
        {
            qaCheck.SetPassed("Could not find NVIDIA");
            return qaCheck;
        }
        qaCheck.SetFailed("Found NVIDIA at location: " + output[0].Properties.Where(info => (info.Name.Contains("App Path") || info.Name.Contains("UninstallString"))).First()?.Value);
        return qaCheck;
    }
}
