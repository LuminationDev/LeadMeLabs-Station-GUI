using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Station.Components._interfaces;
using Station.Components._managers;
using Station.Components._notification;
using Station.Components._profiles;
using Station.Components._utils;
using Station.MVC.Controller;

namespace Station.Components._wrapper.vive;

public static class ViveScripts
{
    /// <summary>
    /// Track if an external process is stopping the Vive check.
    /// </summary>
    private static bool terminateMonitoring;

    /// <summary>
    /// Track if the ViveCheck is currently running.
    /// </summary>
    private static bool activelyMonitoring;

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
    /// Wait for Vive to be open and connected before going any further with the launcher sequence.
    /// </summary>
    /// <returns></returns>
    public static async Task<bool> WaitForVive(string wrapperType)
    {
        // Safe cast for potential vr profile
        VrProfile? vrProfile = Profile.CastToType<VrProfile>(SessionController.StationProfile);
        if (vrProfile?.VrHeadset == null) return false;
        
        if (!InternalDebugger.GetAutoStart())
        {
            JObject message = new JObject
            {
                { "action", "SoftwareState" },
                { "value", "Debug Mode" }
            };
            ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage(message), TimeSpan.FromSeconds(0));
            return false;
        }

        //Wait for the Vive Check
        Logger.WriteLog("WaitForVive - Attempting to launch an application, vive status is: " +
            Enum.GetName(typeof(DeviceStatus), vrProfile.VrHeadset.GetHeadsetManagementSoftwareStatus()), MockConsole.LogLevel.Normal);
        if (WrapperManager.currentWrapper?.GetLaunchingExperience() ?? false)
        {
            JObject message = new JObject
            {
                { "action", "MessageToAndroid" },
                { "value", "AlreadyLaunchingGame" }
            };
            SessionController.PassStationMessage(message);
            return false;
        }
        WrapperManager.currentWrapper?.SetLaunchingExperience(true);

        if (!await ViveCheck(wrapperType))
        {
            WrapperManager.currentWrapper?.SetLaunchingExperience(false);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Run a while loop to track if the Vive program is up and running.
    /// </summary>
    /// <returns></returns>
    private static async Task<bool> ViveCheck(string type)
    {
        // Safe cast for potential vr profile
        VrProfile? vrProfile = Profile.CastToType<VrProfile>(SessionController.StationProfile);
        if (vrProfile?.VrHeadset == null) return false;

        //Determine if the awaiting headset connection has already been sent.
        bool sent = false;
        int count = 0;

        MockConsole.WriteLine("ViveCheck - About to launch a steam app, vive status is: " + 
            Enum.GetName(typeof(DeviceStatus), vrProfile.VrHeadset.GetHeadsetManagementSoftwareStatus()), MockConsole.LogLevel.Normal);
        while (vrProfile.VrHeadset.GetHeadsetManagementSoftwareStatus() != DeviceStatus.Connected)
        {
            MockConsole.WriteLine("Vive check looping", MockConsole.LogLevel.Debug);

            activelyMonitoring = true;

            if (vrProfile.VrHeadset.GetHeadsetManagementSoftwareStatus() == DeviceStatus.Off)
            {
                SessionController.StartSession(type);
                
                JObject message = new JObject
                {
                    { "action", "SoftwareState" },
                    { "value", "Starting VR Session" }
                };
                ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage(message), TimeSpan.FromSeconds(1));
                if (count == 10) // (10 * 5000ms) this loop + 2000ms initial loop 
                {
                    terminateMonitoring = true;
                    
                    JObject androidMessage = new JObject
                    {
                        { "action", "MessageToAndroid" },
                        { "value", "HeadsetTimeout" }
                    };
                    SessionController.PassStationMessage(androidMessage);
                    
                    JObject stateMessage = new JObject
                    {
                        { "action", "SoftwareState" },
                        { "value", "Awaiting headset connection..." }
                    };
                    ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage(stateMessage), TimeSpan.FromSeconds(1));
                }
                else
                {
                    count++;
                }
                await Task.Delay(5000);
            }
            else if (!sent)
            {
                sent = true;
                JObject message = new JObject
                {
                    { "action", "SoftwareState" },
                    { "value", "Awaiting headset connection..." }
                };
                ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage(message), TimeSpan.FromSeconds(1));
                
                JObject androidMessage = new JObject
                {
                    { "action", "MessageToAndroid" },
                    { "value", "SetValue:session:Restarted" }
                };
                ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage(androidMessage), TimeSpan.FromSeconds(1));
                await Task.Delay(2000);
            }
            else //Message has already been sent to the NUC, block so it does not take up too much processing power
            {
                //Loop for a period of time before declaring a headset timeout
                if (count == 30) // (30 * 2000ms) this loop + 2000ms initial loop 
                {
                    terminateMonitoring = true;
                    
                    JObject message = new JObject
                    {
                        { "action", "MessageToAndroid" },
                        { "value", "HeadsetTimeout" }
                    };
                    SessionController.PassStationMessage(message);
                }
                else
                {
                    count++;
                }
                await Task.Delay(2000);
            }

            //Externally stop the loop in case of ending VR session
            if (!terminateMonitoring) continue;
            
            JObject closedMessage = new JObject
            {
                { "action", "ApplicationClosed" }
            };
            SessionController.PassStationMessage(closedMessage);
            await Task.Delay(1000);
            
            JObject statusMessage = new JObject
            {
                { "action", "MessageToAndroid" },
                { "value", "SetValue:status:On" }
            };
            SessionController.PassStationMessage(statusMessage);

            activelyMonitoring = false;
            terminateMonitoring = false;
            return false;
        }

        activelyMonitoring = false;
        return true;
    }
}
