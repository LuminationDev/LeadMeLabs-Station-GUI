using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LeadMeLabsLibrary.Station;

namespace Station._headsets
{
    public class ViveFocus3 : Headset, IVrHeadset
    {
        private Statuses Statuses { get; } = new();

        /// <summary>
        /// The absolute path of the Vive Business Streaming executable on the local machine.
        /// </summary>
        private const string Vive = @"C:\Program Files\VIVE Business Streaming\RRConsole\RRConsole.exe";

        public Statuses GetStatusManager()
        {
            return Statuses;
        }

        /// <summary>
        /// If the headset is managed by more than just OpenVR return the management software connection
        /// status. In this case it is managed by Vive Wireless.
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
            return "RRServer";
        }

        /// <summary>
        /// Collect the connection status of the headset from the headset's specific management software. In this case it
        /// is Vive Wireless.
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
            return new List<string> { "vrmonitor", "steam", "RRConsole", "RRServer", "steamwebhelper" };
        }
        
        /// <summary>
        /// Minimise the software that handles the headset.
        /// </summary>
        /// <param name="attemptLimit"></param>
        public void MinimizeSoftware(int attemptLimit = 6)
        {
            Minimize(GetProcessesToQuery(), attemptLimit);
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
            CommandLine.StartProgram(SessionController.Steam, " -login " + 
                Environment.GetEnvironmentVariable("SteamUserName", EnvironmentVariableTarget.Process) + " " + 
                Environment.GetEnvironmentVariable("SteamPassword", EnvironmentVariableTarget.Process) + " steam://rungameid/250820"); //Open up steam and run steamVR
            CommandLine.StartProgram(Vive); //Start Vive business streaming

            MinimizeSoftware();
        }

        public void MonitorVrConnection()
        {
            var directory = new DirectoryInfo(@"C:\ProgramData\HTC\ViveSoftware\ViveRR\Log");
            var file = directory.GetFiles()
                .Where(f => f.Name.Contains("RRConsole"))
                .OrderByDescending(f => f.LastWriteTime)
                .First();
            
            bool containsOnHmdReady = false; // Flag to track if the string is found
            ReverseLineReader reverseLineReader = new ReverseLineReader(file.FullName, Encoding.Unicode);
            IEnumerator<string?> enumerator = reverseLineReader.GetEnumerator();
            do
            {
                string? current = enumerator.Current;
                if (current == null) continue;
                if (!current.Contains("OnHMDReady")) continue;
                containsOnHmdReady = true;
                
                switch (Statuses.SoftwareStatus)
                {
                    case DeviceStatus.Connected or DeviceStatus.Off when current.Contains("False"):
                        Statuses.UpdateHeadset(VrManager.Software, DeviceStatus.Lost);
                        break;
                    
                    case DeviceStatus.Lost or DeviceStatus.Off when current.Contains("True"):
                        Statuses.UpdateHeadset(VrManager.Software, DeviceStatus.Connected);
                        break;
                }
                enumerator.Dispose();
            } while (enumerator.MoveNext());

            //The software is running but no headset has connected yet.
            if (!containsOnHmdReady)
            {
                Statuses.UpdateHeadset(VrManager.Software, DeviceStatus.Lost);
            }
        }

        /// <summary>
        /// Kill off the Steam VR process.
        /// </summary>
        public void StopProcessesBeforeLaunch()
        {
            //Not currently required for ViveFocus3
        }
    }
}
