using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Newtonsoft.Json.Linq;
using Timer = System.Timers.Timer;

namespace Station
{
    public class SynthesisWrapper : Wrapper
    {
        public static string wrapperType = "Synthesis";
        private static Process? currentProcess;
        public static string? experienceName = null;
        public static Experience lastApp;
        private static HttpClient synthesisHttpClient = new HttpClient();

        /// <summary>
        /// Track if an experience is being launched.
        /// </summary>
        public static bool launchingExperience = false;

        public string? GetCurrentExperienceName()
        {
            return experienceName;
        }

        public List<string>? CollectApplications()
        {
            var task = Task.Run(() => synthesisHttpClient.GetAsync("127.0.0.1:8080/control/status/me")); 
            task.Wait();
            HttpResponseMessage response = task.Result;
            var readTask = Task.Run(() => response.Content.ReadAsStringAsync()); 
            readTask.Wait();
            JObject joResponse = JObject.Parse(readTask.Result);
            JObject applications = (JObject) joResponse["games"];
            List<string> applicationsList = new List<string>();

            foreach (var app in applications)
            {
                string id = app.Key;
                string name = ((JObject) app.Value)["name"].ToString(); // todo - error checking
                applicationsList.Add($"{wrapperType}|{id}|{name}");
                WrapperManager.StoreApplication(wrapperType, id, name);
            }
            return applicationsList;
        }

        public void CollectHeaderImage(string experienceName)
        {
            // todo https://svrstorage.s3.amazonaws.com/gameassets/svr_{id}/header.jpg
            throw new NotImplementedException();
        }

        public void PassMessageToProcess(string message)
        {
            throw new NotImplementedException();
        }

        public void WrapProcess(Experience experience)
        {
            if (experience.ID == null)
            {
                SessionController.PassStationMessage($"MessageToAndroid,GameLaunchFailed:Unknown experience");
                return;
            };

            MockConsole.WriteLine($"Wrapping: {experienceName}", MockConsole.LogLevel.Debug);

            //Start the external processes to handle SteamVR
            SessionController.StartVRSession(wrapperType);

            //Begin monitoring the different processes
            WrapperMonitoringThread.initializeMonitoring(wrapperType);

            //Wait for Vive to start
            if (!WaitForVive().Result) return;

            CommandLine.RunPowershellCommand($"Start-Process -FilePath \"svr://startGame/${experience.ID}\"");
            lastApp = experience;
            FindCurrentProcess();
        }

        /// <summary>
        /// Wait for Vive to be open and connected before going any further with the launcher sequence.
        /// </summary>
        /// <returns></returns>
        private async Task<bool> WaitForVive()
        {
            //Wait for the Vive Check
            Logger.WriteLog("About to launch a synthesis app, vive status is: " + WrapperMonitoringThread.viveStatus, MockConsole.LogLevel.Normal);
            if (launchingExperience)
            {
                SessionController.PassStationMessage("MessageToAndroid,AlreadyLaunchingGame");
                return false;
            }
            launchingExperience = true;

            if (!await ViveScripts.ViveCheck(wrapperType))
            {
                launchingExperience = false;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Find the active process that has been launched.
        /// </summary>
        private void FindCurrentProcess()
        {
            int attempts = 0; //Track the loop for finding child processes

            Process? child = GetExperienceProcess();
            while(child == null && attempts < 10)
            {
                attempts++;
                MockConsole.WriteLine($"Checking for child process...", MockConsole.LogLevel.Debug);
                Task.Delay(3000).Wait();
                child = GetExperienceProcess();
            }
            currentProcess = child;
            launchingExperience = false;

            if(child != null)
            {
                Logger.WriteLog($"Child process found: {child.Id}, {child.MainWindowTitle}, {child.ProcessName}", MockConsole.LogLevel.Normal);

                ListenForClose();
                WindowManager.MaximizeProcess(child); //Maximise the process experience
                SessionController.PassStationMessage($"ApplicationUpdate,{experienceName}/{lastApp.ID}/Synthesis");
            } else
            {
                Logger.WriteLog("Game launch failure: " + lastApp.Name, MockConsole.LogLevel.Normal);
                UIUpdater.ResetUIDisplay();
                SessionController.PassStationMessage($"MessageToAndroid,GameLaunchFailed:{lastApp.Name}");
            }
        }

        /// <summary>
        /// Scan the active processes that to find the launched application. The process is matched by the 
        /// MainWindowTitle that is represented by the Steam application name.
        /// </summary>
        /// <returns>The launched application process</returns>
        private Process? GetExperienceProcess()
        {
            string? activeProcessId = null;
            string appDirectory = $"C:\\SynthesisVR Exclusive Content\\${lastApp.ID}\\";
            string? processId = CommandLine.GetProcessIdFromDir(appDirectory);
            if (processId != null)
            {
                activeProcessId = processId;
            }

            if (activeProcessId != null)
            {
                Process proc = Process.GetProcessById(Int32.Parse(activeProcessId));
                Logger.WriteLog($"Application found: {proc.MainWindowTitle}/{proc.Id}", MockConsole.LogLevel.Debug);

                UIUpdater.UpdateProcess(proc.MainWindowTitle);
                UIUpdater.UpdateStatus("Running...");
                return proc;
            }

            return null;
        }

        public void ListenForClose()
        {
            Task.Factory.StartNew(() =>
            {
                currentProcess?.WaitForExit();
                SessionController.PassStationMessage($"ApplicationClosed");
                UIUpdater.ResetUIDisplay();
            });
        }

        public bool? CheckCurrentProcess()
        {
            return currentProcess?.Responding;
        }

        public void StopCurrentProcess()
        {
            CommandLine.RunPowershellCommand("Start-Process -FilePath \"svr://StopCurrentGame\"");
            ViveScripts.StopMonitoring();
        }

        public void RestartCurrentExperience()
        {
            if(currentProcess != null)
            {
                CommandLine.RunPowershellCommand("Start-Process -FilePath \"svr://StopCurrentGame\"");
                Task.Delay(3000).Wait();
                WrapProcess(lastApp);
            }
        }
    }
}
