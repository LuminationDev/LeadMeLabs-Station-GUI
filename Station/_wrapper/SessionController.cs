using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Station._headsets;
using Station._scripts;

namespace Station
{
    public static class SessionController
    {
        /// <summary>
        /// The absolute path of the steam executable on the local machine.
        /// </summary>
        public const string Steam = "C:/Program Files (x86)/Steam/steam.exe";

        /// <summary>
        /// Store the HeadSet type that is linked to the current station.
        /// </summary>
        public static IVrHeadset? VrHeadset { set; get; }

        /// <summary>
        /// Store the current experience type that is running.
        /// </summary>
        public static string? ExperienceType { set; get; }

        /// <summary>
        /// Track the current state of the Station software.
        /// </summary>
        public static string CurrentState { private set; get; } = "";

        /// <summary>
        /// Read the store headset type from the config.env file and create an instance that 
        /// can be accessed from this class.
        /// </summary>
        public static void SetupHeadsetType()
        {
            //Read from env file
            switch (Environment.GetEnvironmentVariable("HeadsetType", EnvironmentVariableTarget.Process))
            {
                case "VivePro1":
                    VrHeadset = new VivePro1();
                    break;
                case "VivePro2":
                    VrHeadset = new VivePro2();
                    break;
                case "ViveFocus3":
                    VrHeadset = new ViveFocus3();
                    break;
                default:
                    PassStationMessage("No headset type specified.");
                    break;
            }
        }

        /// <summary>
        /// Start up a VR session on the local machine, this may include starting up Steam, steamVR and/or ViveWireless. The
        /// applications that will be started/required depend on the supplied type.
        /// </summary>
        /// <param name="type">A string of what type of experience is being loaded [Custom, Steam, Vive, etc]</param>
        public static void StartVRSession(string type)
        {
            ExperienceType = type;
            switch (ExperienceType)
            {
                case "Custom":
                case "Steam":
                case "Revive":
                    VrHeadset?.StartVrSession();
                    break;
                case "Vive":
                    MockConsole.WriteLine("startVRSession not implemented for type: Vive.", MockConsole.LogLevel.Error);
                    break;
            }
            
            //Attempt to minimise other applications (mostly Steam)
            VrHeadset?.MinimizeSoftware(2);
        }

        /// <summary>
        /// Stop all processes that are associated with a VR session.
        /// </summary>
        public static void RestartVRSession()
        {
            ScheduledTaskQueue.EnqueueTask(() => UpdateSoftwareState("Shutting down VR processes"), TimeSpan.FromSeconds(1));
            _ = WrapperManager.RestartVRProcesses();

            if (ExperienceType == null)
            {
                PassStationMessage("No experience is currently running.");
                return;
            }

            switch (ExperienceType)
            {
                case "Custom":
                    MockConsole.WriteLine("restartVRSession not implemented for type: Custom.", MockConsole.LogLevel.Error);
                    break;
                case "Revive":
                case "Steam":
                    ViveScripts.StopMonitoring();
                    break;
                case "Vive":
                    MockConsole.WriteLine("restartVRSession not implemented for type: Vive.", MockConsole.LogLevel.Error);
                    break;
                default:
                    MockConsole.WriteLine("Wrapper: No experience type set.", MockConsole.LogLevel.Error);
                    break;
            }
            
            //Attempt to minimise other applications (mostly Steam)
            VrHeadset?.MinimizeSoftware(2);
        }

        /// <summary>
        /// Stop all processes that are associated with a VR session.
        /// </summary>
        public static void EndVRSession()
        {
            switch (ExperienceType)
            {
                case "Custom":
                    MockConsole.WriteLine("endVRSession not implemented for type: Custom.", MockConsole.LogLevel.Error);
                    break;
                case "Revive":
                case "Steam":
                    ViveScripts.StopMonitoring();
                    break;
                case "Vive":
                    MockConsole.WriteLine("endVRSession not implemented for type: Vive.", MockConsole.LogLevel.Error);
                    break;
                default:
                    MockConsole.WriteLine("Wrapper: No experience type set.", MockConsole.LogLevel.Error);
                    break;
            }

            ExperienceType = null;
            
            //Attempt to minimise other applications (mostly Steam)
            VrHeadset?.MinimizeSoftware(2);
        }

        /// <summary>
        /// A generic way to pause a task but not stop the main thread from running.
        /// </summary>
        /// <param name="delay"></param>
        /// <returns></returns>
        public static async Task PutTaskDelay(int delay)
        {
            await Task.Delay(delay);
        }

        /// <summary>
        /// Update the software state, update both the internal variable and send the state on to the NUC for
        /// further processing.
        /// </summary>
        public static void UpdateSoftwareState(string state)
        {
            CurrentState = state;
            
            JObject values = new()
            {
                { "state", state }
            };
            JObject setValue = new() { { "SetValue", values } };
            PassStationObject(setValue);
        }

        /// <summary>
        /// Take an action object from the wrapper and pass the response onto the NUC.
        /// </summary>
        public static void PassStationObject(JObject message)
        {
            Manager.SendMessage("Android", "Station", message);
        }

        /// <summary>
        /// Take an action message from the wrapper and pass the response onto the NUC or handle it internally.
        /// </summary>
        /// <param name="message">A string representing the message, different actions are separated by a ','</param>
        public static void PassStationMessage(string message)
        {
            new Thread(() => {
                MockConsole.WriteLine("Action: " + message, MockConsole.LogLevel.Normal);

                //[TYPE, ACTION, INFORMATION]
                string[] tokens = message.Split(',');
                
                JObject values = new JObject();
                
                switch (tokens[0])
                {
                    case "MessageToAndroid":
                        JObject androidMessage = new() { { "message", tokens[1] } };
                        Manager.SendMessage("Android", "Station", androidMessage);
                        
                        //BACKWARD COMPAT
                        //Manager.SendResponse("Android", "Station", tokens[1]);
                        return;

                    case "Processing":
                        StationScripts.processing = bool.Parse(tokens[1]);
                        break;

                    case "ApplicationUpdate":
                        string[] info = tokens[1].Split('/');

                        values.Add("gameName", info[0]);
                        
                        //BACKWARD COMPAT
                        //Manager.SendResponse("Android", "Station", $"SetValue:gameName:{info[0]}");

                        if (info.Length > 1)
                        {
                            values.Add("gameId", info[1]);
                            values.Add("gameType", info[2]);
                            
                            //BACKWARD COMPAT
                            // Manager.SendResponse("Android", "Station", $"SetValue:gameId:{info[1]}");
                            // Manager.SendResponse("Android", "Station", $"SetValue:gameType:{info[2]}");
                        }
                        else
                        {
                            values.Add("gameId", "");
                            values.Add("gameType", "");
                            
                            //BACKWARD COMPAT
                            // Manager.SendResponse("Android", "Station", "SetValue:gameId:");
                            // Manager.SendResponse("Android", "Station", "SetValue:gameType:");
                        }
                        break;

                    case "ApplicationList":
                        values.Add("installedApplications", tokens[1]);
                        
                        //BACKWARD COMPAT
                        //  Manager.SendResponse("Android", "Station", "SetValue:installedApplications:" + tokens[1]);
                        break;

                    case "ApplicationClosed":
                        values.Add("gameName", "");
                        values.Add("gameId", "");
                        values.Add("gameType", "");
                        
                        //BACKWARD COMPAT
                        // Manager.SendResponse("Android", "Station", "SetValue:gameName:");
                        // Manager.SendResponse("Android", "Station", "SetValue:gameId:");
                        // Manager.SendResponse("Android", "Station", "SetValue:gameType:");
                        break;

                    case "StationError":
                        //Just print to the Console for now but send message to the NUC/Tablet in the future
                        break;

                    default:
                        MockConsole.WriteLine("Non-primary command", MockConsole.LogLevel.Debug);
                        break;
                }
                
                JObject setValue = new() { { "SetValue", values } };
                Manager.SendMessage("Android", "Station", setValue);
            }).Start();
        }
    }
}
