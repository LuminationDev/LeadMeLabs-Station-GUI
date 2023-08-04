using System;
using System.Threading.Tasks;

namespace Station
{
    public class ViveScripts
    {
        /// <summary>
        /// Track if an external process is stopping the Vive check.
        /// </summary>
        private static bool terminateMonitoring = false;

        /// <summary>
        /// Track if the vivecheck is currently running.
        /// </summary>
        private static bool activelyMonitoring = false;

        /// <summary>
        /// Only try terminate the monitoring if it is actively monitoring, otherwise it will immediately 
        /// exit the next time.
        /// </summary>
        public static void StopMonitoring()
        {
            if (activelyMonitoring)
            {
                terminateMonitoring = true;
            }
        }

        /// <summary>
        /// Run a while loop to track if the Vive program is up and running.
        /// </summary>
        /// <returns></returns>
        public static async Task<bool> ViveCheck(string type)
        {
            if (SessionController.vrHeadset == null) return false;

            //Determine if the awaiting headset connection has already been sent.
            bool sent = false;
            int count = 0;

            MockConsole.WriteLine("About to launch a steam app, vive status is: " + Enum.GetName(typeof(HMDStatus), SessionController.vrHeadset.GetConnectionStatus()), MockConsole.LogLevel.Normal);
            while (SessionController.vrHeadset.GetConnectionStatus() != HMDStatus.Connected)
            {
                MockConsole.WriteLine("Vive check looping", MockConsole.LogLevel.Debug);

                activelyMonitoring = true;

                if (SessionController.vrHeadset.GetConnectionStatus() == HMDStatus.Lost)
                {
                    SessionController.StartVRSession(type);
                    SessionController.PassStationMessage($"ApplicationUpdate,Starting VR Session...");

                    await Task.Delay(5000);
                }
                else if (!sent)
                {
                    sent = true;

                    SessionController.PassStationMessage($"ApplicationUpdate,Awaiting headset connection...");
                    await Task.Delay(2000);
                }
                else //Message has already been sent to the NUC, block so it does not take up too much processing power
                {
                    //Loop for a period of time before declaring a headset timeout
                    if (count == 30) // (30 * 2000ms) this loop + 2000ms initial loop 
                    {
                        terminateMonitoring = true;
                        SessionController.PassStationMessage("MessageToAndroid,HeadsetTimeout");
                    }
                    else
                    {
                        count++;
                    }
                    await Task.Delay(2000);
                }

                //Externally stop the loop incase of ending VR session
                if (terminateMonitoring)
                {
                    SessionController.PassStationMessage("ApplicationClosed");
                    await Task.Delay(1000);
                    SessionController.PassStationMessage("MessageToAndroid,SetValue:status:On");

                    activelyMonitoring = false;
                    terminateMonitoring = false;
                    return false;
                }
            }

            activelyMonitoring = false;
            return true;
        }
    }
}
