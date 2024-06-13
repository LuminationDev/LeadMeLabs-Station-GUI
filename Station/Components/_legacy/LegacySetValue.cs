using System;
using Station.Components._enums;
using Station.Components._managers;
using Station.Components._profiles;
using Station.MVC.Controller;

namespace Station.Components._legacy;

public static class LegacySetValue
{
    public static void InitialStartUp()
    {
        // Only send the headset if is a vr profile Station
        // Safe cast for potential vr profile
        VrProfile? vrProfile = Profile.CastToType<VrProfile>(SessionController.StationProfile);
        if (vrProfile?.VrHeadset != null)
        {
            MessageController.SendResponse("NUC", "Station", $"SetValue:headsetType:{Environment.GetEnvironmentVariable("HeadsetType", EnvironmentVariableTarget.Process)}");
        }
        
        MessageController.SendResponse("NUC", "Station", "SetValue:status:On");
        MessageController.SendResponse("NUC", "Station", "SetValue:gameName:");
        MessageController.SendResponse("Android", "Station", "SetValue:gameId:");
    }
    
    public static void HandleConnection(string source)
    {
        // Only send the headset if is a vr profile Station
        // Safe cast for potential vr profile
        VrProfile? vrProfile = Profile.CastToType<VrProfile>(SessionController.StationProfile);
        if (vrProfile?.VrHeadset != null)
        {
            MessageController.SendResponse(source, "Station", $"SetValue:headsetType:{Environment.GetEnvironmentVariable("HeadsetType", EnvironmentVariableTarget.Process)}");
        }
        
        MessageController.SendResponse(source, "Station", "SetValue:status:On");
        MessageController.SendResponse(source, "Station", $"SetValue:state:{Attributes.GetEnumValue(SessionController.CurrentState)}");
        MessageController.SendResponse(source, "Station", "SetValue:gameName:");
        MessageController.SendResponse("Android", "Station", "SetValue:gameId:");
        
        AudioManager.Initialise();
        VideoManager.Initialise();
        FileManager.Initialise();
    }

    public static void SimpleSetValue(string key, string? value)
    {
        MessageController.SendResponse("Android", "Station", $"SetValue:{key}:{value}");
    }
}
