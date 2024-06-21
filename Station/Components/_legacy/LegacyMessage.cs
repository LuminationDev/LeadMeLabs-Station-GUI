using System.Threading.Tasks;
using LeadMeLabsLibrary;
using Station.Components._managers;
using Station.Components._profiles;
using Station.Components._utils;
using Station.Components._wrapper.steam;
using Station.MVC.Controller;

namespace Station.Components._legacy;

/// <summary>
/// A dedicated class to hold functions that interpret the legacy string messages. This will condense all functions
/// into a single file that in future can be removed.
/// </summary>
public static class LegacyMessage
{
    public static async void HandleStationString(string source, string additionalData)
    {
        if (additionalData.StartsWith("GetValue"))
        {
            string key = additionalData.Split(":", 2)[1];
            switch (key)
            {
                case "installedApplications":
                    Logger.WriteLog("Collecting station experiences", Enums.LogLevel.Normal);
                    MainController.wrapperManager?.ActionHandler("CollectApplications");
                    break;

                case "volume":
                    string currentVolume = await AudioManager.GetVolume();
                    MessageController.SendResponse(source, "Station", "SetValue:" + key + ":" + currentVolume);
                    break;

                case "muted":
                    string isMuted = await AudioManager.GetMuted();
                    MessageController.SendResponse(source, "Station", "SetValue:" + key + ":" + isMuted);
                    break;

                case "devices":
                    //When a tablet connects/reconnects to the NUC, send through the current VR device statuses.
                    // Safe cast for potential vr profile
                    VrProfile? vrProfile = Profile.CastToType<VrProfile>(SessionController.StationProfile);
                    if (vrProfile?.VrHeadset == null) return;

                    vrProfile.VrHeadset?.GetStatusManager().QueryStatuses();
                    break;
            }
        }
        
        if (additionalData.StartsWith("SetValue"))
        {
            string[] keyValue = additionalData.Split(":", 3);
            string key = keyValue[1];
            string value = keyValue[2];
            
            switch (key)
            {
                case "idleMode":
                    ModeTracker.ToggleIdleMode(value);
                    break;
                
                case "volume":
                    AudioManager.SetVolume(value);
                    break;

                case "activeAudioDevice":
                    AudioManager.SetCurrentAudioDevice(value);
                    break;

                case "muted":
                    AudioManager.SetMuted(value);
                    break;

                case "steamCMD":
                    SteamScripts.ConfigureSteamCommand(value);
                    break;
            }
        }
        
        if (additionalData.StartsWith("AcceptEulas"))
        {
            await WrapperManager.AcceptUnacceptedEulas();
        }
    }
    
    // /// <summary>
    // /// Handle the action of an experience that has been sent from the Tablet -> NUC -> Station
    // /// </summary>
    // /// <param name="additionalData">A string of information separated by ':'</param>
    // public static async void HandleExperienceString(string additionalData)
    // {
    //     if (additionalData.StartsWith("Refresh"))
    //     {
    //         MainController.wrapperManager?.ActionHandler("CollectApplications");
    //     }
    //
    //     if (additionalData.StartsWith("Restart"))
    //     {
    //         MainController.wrapperManager?.ActionHandler("Restart");
    //     }
    //
    //     if (additionalData.StartsWith("Thumbnails"))
    //     {
    //         string[] split = additionalData.Split(":", 2);
    //         MainController.wrapperManager?.ActionHandler("CollectHeaderImages", split[1]);
    //     }
    //
    //     if (additionalData.StartsWith("Launch"))
    //     {
    //         string id = additionalData.Split(":")[1]; // todo - tidy this up
    //         MainController.wrapperManager?.ActionHandler("Stop");
    //         
    //         await Task.Delay(2000);
    //         
    //         MainController.wrapperManager?.ActionHandler("Start", id);
    //     }
    //
    //     if (additionalData.StartsWith("PassToExperience"))
    //     {
    //         string[] split = additionalData.Split(":", 2);
    //         MainController.wrapperManager?.ActionHandler("Message", split[1]);
    //     }
    // }
}
