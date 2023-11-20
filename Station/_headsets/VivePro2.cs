using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Station._commandLine;

namespace Station._headsets
{
    public class VivePro2 : Headset, IVrHeadset
    {
        private Statuses Statuses { get; } = new();

        public Statuses GetStatusManager()
        {
            return Statuses;
        }

        /// <summary>
        /// If the headset is managed by more than just OpenVR return the management software connection
        /// status. In this case it is managed by Vive Console.
        /// </summary>
        /// <returns></returns>
        public DeviceStatus GetHeadsetManagementSoftwareStatus()
        {
            return Statuses.SoftwareStatus;
        }
        
        /// <summary>
        /// Return the process name of the headset management software
        /// </summary>
        /// <returns></returns>
        public string GetHeadsetManagementProcessName()
        {
            return "LhStatusMonitor";
        }

        /// <summary>
        /// Collect the connection status of the headset from the headset's specific management software. In this case it
        /// is Vive Console.
        /// </summary>
        /// <param name="wrapperType">A string of the Wrapper type that is being launched, required if the process
        /// needs to restart/start the VR session.</param>
        /// <returns>A bool representing the connection status.</returns>
        public bool WaitForConnection(string wrapperType)
        {
            return ViveScripts.WaitForVive(wrapperType).Result;
        }

        public List<string> GetProcessesToQuery()
        {
            return new List<string> { "vrmonitor", "steam", "LhStatusMonitor" };
        }

        private List<string> GetProcessesToMinimize()
        {
            return new List<string> { "vrmonitor", "steam", "LhStatusMonitor", "WaveConsole", "steamwebhelper" };
        }
        
        /// <summary>
        /// Minimise the software that handles the headset.
        /// </summary>
        /// <param name="attemptLimit"></param>
        public void MinimizeSoftware(int attemptLimit = 6)
        {
            Minimize(GetProcessesToMinimize(), attemptLimit);
        }

        public void StartVrSession()
        {
            //Bail out if Steam and SteamVR are already running
            if (QueryMonitorProcesses(GetProcessesToQuery()))
            {
                return;
            }

            CommandLine.KillSteamSigninWindow();
            SteamConfig.VerifySteamConfig();
            CommandLine.StartProgram(SessionController.Steam, "-noreactlogin -login " +
                Environment.GetEnvironmentVariable("SteamUserName", EnvironmentVariableTarget.Process) + " " +
                Environment.GetEnvironmentVariable("SteamPassword", EnvironmentVariableTarget.Process) + " steam://rungameid/1635730"); //Open up steam and run vive console

            MinimizeSoftware();
        }

        public void MonitorVrConnection()
        {
            Process[] vivePro2Connector = ProcessManager.GetProcessesByName("WaveConsole");
            if (vivePro2Connector.Length > 0)
            {
                if (vivePro2Connector.Any(process => process.MainWindowTitle.Equals("VIVE Console")))
                {
                    Statuses.UpdateHeadset(VrManager.Software, DeviceStatus.Lost);
                    return;
                }
            }
            
            Process[] viveStatusMonitor = ProcessManager.GetProcessesByName("LhStatusMonitor");
            if (viveStatusMonitor.Length > 0)
            {
                Statuses.UpdateHeadset(VrManager.Software, DeviceStatus.Connected);
                return;
            }
            Statuses.UpdateHeadset(VrManager.Software, DeviceStatus.Lost);
        }

        /// <summary>
        /// Kill off the Steam VR process.
        /// </summary>
        public void StopProcessesBeforeLaunch()
        {
            //Not currently required for VivePro2
        }
    }
}
