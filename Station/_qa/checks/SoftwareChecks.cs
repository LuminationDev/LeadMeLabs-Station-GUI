using System;
using System.Diagnostics;
using System.IO;

namespace Station._qa.checks;

public class SoftwareInfo
{
    public string? TaskScheduler { get; set; }
    public string? SetVolExe { get; set; }
    public string? SteamCmdExe { get; set; }
    public string? SteamCmdInitialised { get; set; }
    public string? SteamCmdStatus { get; set; }
}

public class SoftwareChecks
{
    public SoftwareInfo? GetSoftwareInformation()
    {
        SoftwareInfo softwareInfo = new SoftwareInfo
        {
            TaskScheduler = CheckTaskSchedulerItem(), //Move to LeadMeLibrary
            SetVolExe = IsSetVolPresent(),
            SteamCmdExe = IsSteamCmdPresent(),
            SteamCmdInitialised = IsSteamCmdInitialised(),
            SteamCmdStatus = IsSteamCmdConfigured()
        };

        return softwareInfo;
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
    private string IsSetVolPresent()
    {
        string filePath = CommandLine.stationLocation + @"\external\SetVol\SetVol.exe";
        return "SetVol " + (File.Exists(filePath) ? "present" : "missing");
    }
    
    /// <summary>
    /// Check if SteamCMD is present within the Stations' external folder.
    /// </summary>
    private string IsSteamCmdPresent()
    {
        string filePath = CommandLine.stationLocation + @"\external\steamcmd\steamcmd.exe";
        return "SteamCMD " + (File.Exists(filePath) ? "present" : "missing");
    }
    
    /// <summary>
    /// Check if SteamCMD has been initialised.
    /// </summary>
    private string IsSteamCmdInitialised()
    {
        //Check if SteamCMD has been initialised
        string filePath = CommandLine.stationLocation + @"\external\steamcmd\steamerrorreporter.exe";
            
        if(!File.Exists(filePath))
        {
            return "Not Initialised";
        }
        
        return "Initialised";
    }

    /// <summary>
    /// Check if the Steam guard has been entered (or disabled) and that the local details are correct.
    /// </summary>
    private string IsSteamCmdConfigured()
    {
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
            response = error;
        }
        else if (output.Contains("Steam Guard code:"))
        {
            response = "Steam guard code required";
        }
        else if (output.Contains("FAILED (Invalid Login Auth Code)"))
        {
            response = "Invalid steam guard code";
        }
        else if (output.Contains("Invalid Password"))
        {
            response = "Invalid password or username";
        }
        else if (output.Contains("OK"))
        {
            response = "Configured";
        }

        //Manually kill the process or it will stay on the guard code input 
        cmd.Kill(true);

        return response;
    }
}
