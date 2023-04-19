using System;
using System.Threading;
using System.Threading.Tasks;

namespace Station
{
    public class SessionController
    {
        /// <summary>
        /// The absolute path of the steam executable on the local machine.
        /// </summary>
        public static string steam = "C:/Program Files (x86)/Steam/steam.exe";

        /// <summary>
        /// The absolute path of the ViveWireless executable on the local machine.
        /// </summary>
        public static string vive = "C:/Program Files/VIVE Wireless/ConnectionUtility/HtcConnectionUtility.exe";

        /// <summary>
        /// Store the HeadSet type that is linked to the current station.
        /// </summary>
        public static VrHeadset? vrHeadset;

        /// <summary>
        /// Store the current experience type that is running.
        /// </summary>
        public static string? experienceType = null;

        /// <summary>
        /// Read the store headset type from the config.env file and create an instance that 
        /// can be accessed from this class.
        /// </summary>
        public static void SetupHeadsetType()
        {
            //Read from env file
            switch (Environment.GetEnvironmentVariable("HeadsetType"))
            {
                case "VivePro1":
                    vrHeadset = new VivePro1();
                    break;
                case "VivePro2":
                    vrHeadset = new VivePro2();
                    break;
                default:
                    PassStationMessage("No headset type specified.");
                    break;
            }
        }

        /// <summary>
        /// Start up a VR session on the local machine, this may include starting up Steam, steamVR and/or ViveWireless. The
        /// applications that will be started/required depend on the suppiled type.
        /// </summary>
        /// <param name="type">A string of what type of experience is being loaded [Custom, Steam, Vive, etc]</param>
        public static void StartVRSession(string type)
        {
            experienceType = type;
            switch (experienceType)
            {
                case "Custom":
                    MockConsole.WriteLine("startVRSession not implemented for type: Custom.", MockConsole.LogLevel.Error);
                    break;
                case "Steam":
                    vrHeadset?.StartVrSession();
                    break;
                case "Vive":
                    MockConsole.WriteLine("startVRSession not implemented for type: Vive.", MockConsole.LogLevel.Error);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Stop all processes that are associated with a VR session.
        /// </summary>
        public static void RestartVRSession()
        {
            if (experienceType == null)
            {
                PassStationMessage("No experience is currently running.");
                return;
            }

            switch (experienceType)
            {
                case "Custom":
                    MockConsole.WriteLine("restartVRSession not implemented for type: Custom.", MockConsole.LogLevel.Error);
                    break;
                case "Steam":
                    ViveScripts.StopMonitoring();
                    break;
                case "Vive":
                    MockConsole.WriteLine("restartVRSession not implemented for type: Vive.", MockConsole.LogLevel.Error);
                    break;
                default:
                    MockConsole.WriteLine("Wrapper: No experience type set.", MockConsole.LogLevel.Error);
                    SessionController.PassStationMessage("Processing,false");
                    SessionController.PassStationMessage("MessageToAndroid,SetValue:session:Restarted");
                    break;
            }

            WrapperManager.CurrentWrapper?.RestartCurrentSession();
        }

        /// <summary>
        /// Stop all processes that are associated with a VR session.
        /// </summary>
        public static void EndVRSession()
        {
            switch (experienceType)
            {
                case "Custom":
                    MockConsole.WriteLine("endVRSession not implemented for type: Custom.", MockConsole.LogLevel.Error);
                    break;
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

            experienceType = null;
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
        /// Take an action message from the wrapper and pass the response onto the NUC or handle it internally.
        /// </summary>
        /// <param name="message">A string representing the message, different actions are separated by a ','</param>
        public static void PassStationMessage(string message)
        {
            new Thread(() => {
                MockConsole.WriteLine("Action: " + message, MockConsole.LogLevel.Normal);

                //[TYPE, ACTION, INFORMATION]
                string[] tokens = message.Split(',');

                switch (tokens[0])
                {
                    case "MessageToAndroid":
                        Manager.SendResponse("Android", "Station", tokens[1]);
                        break;

                    case "Processing":
                        StationScripts.processing = bool.Parse(tokens[1]);
                        break;

                    case "ApplicationUpdate":
                        string[] values = tokens[1].Split('/');
                        Manager.SendResponse("Android", "Station", $"SetValue:gameName:{values[0]}");

                        if (values.Length > 1)
                        {
                            Manager.SendResponse("Android", "Station", $"SetValue:gameId:{values[1]}");
                            Manager.SendResponse("Android", "Station", $"SetValue:gameType:{values[2]}");
                        }
                        else
                        {
                            Manager.SendResponse("Android", "Station", "SetValue:gameId:");
                            Manager.SendResponse("Android", "Station", "SetValue:gameType:");
                        }
                        break;

                    case "ApplicationList":
                        //Backwards compatability, send both old (steamApplications) and new (installedApplications) commands for now.
                        Manager.SendResponse("Android", "Station", "SetValue:steamApplications:" + tokens[1]);
                        Manager.SendResponse("Android", "Station", "SetValue:installedApplications:" + tokens[1]);
                        break;

                    case "ApplicationClosed":
                        Manager.SendResponse("Android", "Station", "SetValue:gameName:");
                        Manager.SendResponse("Android", "Station", "SetValue:gameId:");
                        Manager.SendResponse("Android", "Station", "SetValue:gameType:");
                        break;

                    case "StationError":
                        //Just print to the Console for now but send message to the NUC/Tablet in the future
                        break;

                    default:
                        MockConsole.WriteLine("Non-primary command", MockConsole.LogLevel.Debug);
                        PassStationMessage("Processing,false");
                        PassStationMessage("MessageToAndroid,SetValue:session:Restarted");
                        break;
                }
            }).Start();
        }
    }
}
