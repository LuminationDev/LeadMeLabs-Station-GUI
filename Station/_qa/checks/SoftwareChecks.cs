using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;

namespace Station._qa.checks;

public class SoftwareChecks
{
    private List<QaCheck> _qaChecks = new();
    public List<QaCheck> RunQa(string labType)
    {
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
