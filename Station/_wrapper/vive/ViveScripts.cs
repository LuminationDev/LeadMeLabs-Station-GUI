using leadme_api;
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
        public static void stopMonitoring()
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
        public static async Task<bool> viveCheck(string type)
        {
            //Determine if the awaiting headset connection has already been sent.
            bool sent = false;
            int count = 0;

            //Logger.WriteLog("About to launch a steam app, vive status is: " + MonitoringThread.viveStatus);
            //while (!MonitoringThread.viveStatus.Contains("CONNECTED"))
            //{
            //    App.LogHandler("Vive check looping");

            //    activelyMonitoring = true;

            //    if (MonitoringThread.viveStatus.Contains("Terminated"))
            //    {
            //        SessionController.startVRSession(type);
            //        SessionController.SendStationMessage($"ApplicationUpdate,Starting VR Session...");

            //        await Task.Delay(5000);
            //    }
            //    else if (!sent)
            //    {
            //        sent = true;

            //        SessionController.SendStationMessage($"ApplicationUpdate,Awaiting headset connection...");
            //        await Task.Delay(2000);
            //    }
            //    else //Message has already been sent to the NUC, block so it does not take up too much processing power
            //    {
            //        if (count == 30) // (30 * 2000ms) this loop + 2000ms initial loop 
            //        {
            //            terminateMonitoring = true;
            //            SessionController.SendStationMessage("MessageToAndroid,HeadsetTimeout");
            //        }
            //        else
            //        {
            //            count++;
            //        }
            //        await Task.Delay(2000);
            //    }

            //    //Externally stop the loop incase of ending VR session
            //    if (terminateMonitoring)
            //    {
            //        SessionController.SendStationMessage("MessageToAndroid,SetValue:gameName:");
            //        await Task.Delay(1000);
            //        SessionController.SendStationMessage("MessageToAndroid,SetValue:status:On");

            //        activelyMonitoring = false;
            //        terminateMonitoring = false;
            //        return false;
            //    }
            //}

            activelyMonitoring = false;
            return true;
        }
    }
}
