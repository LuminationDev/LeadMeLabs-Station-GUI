using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Station._qa.checks;

public class SoftwareChecks
{
    private List<QaCheck> _qaChecks = new();
    public List<QaCheck> RunQa()
    {
        var TaskScheduler = CheckTaskSchedulerItem(); // todo
        _qaChecks.Add(IsSetVolPresent());
        _qaChecks.Add(IsSteamCmdPresent());
        _qaChecks.Add(IsSteamCmdInitialised());
        _qaChecks.Add(IsSteamCmdConfigured());

        return _qaChecks;
    }

    /// <summary>
    /// Query the local computers Scheduled tasks looking for the Software_Checker, return it display name and the status
    /// of Enabled or Disabled. If there is no task return Not found.
    /// </summary>
    private string CheckTaskSchedulerItem()
    {
        const string taskFolder = "LeadMe\\Software_Checker";
        const string command = $"SCHTASKS /QUERY /TN \"{taskFolder}\" /fo LIST";

        string? stdout = CommandLine.RunProgramWithOutput("cmd.exe", $"/C {command}");
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return "Not found";
        }

        string[] lines = stdout.Split('\n');
        string taskName = "";
        string status = "";

        foreach (string line in lines)
        {
            if (line.Contains("TaskName:"))
            {
                taskName = line.Replace("TaskName:", "").Trim();
            }
            else if (line.Contains("Status:"))
            {
                status = line.Replace("Status:", "").Trim();
            }
        }

        return $"TaskName:{taskName}:Status:{status}";
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
}
