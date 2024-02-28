using System;
using System.Collections.Generic;
using Station._controllers;
using Station._profiles;
using Station._utils;
using Station._wrapper;

namespace Station._qa.checks;

public class ImvrChecks
{
    public List<QaCheck> RunQa(string expectedHeadset)
    {
        string condensedString = expectedHeadset.Replace(" ", "");
        
        List<QaCheck> qaChecks = new List<QaCheck>();
        
        QaCheck correctHeadset = new QaCheck("correct_headset");
        string headset = Environment.GetEnvironmentVariable("HeadsetType", EnvironmentVariableTarget.Process) ?? "Not found";

        if (!Helper.GetStationMode().Equals(Helper.STATION_MODE_VR))
        {
            correctHeadset.SetPassed("Station is a non-vr station");
        } 
        else if (headset.Equals("Not found"))
        {
            correctHeadset.SetFailed("HeadsetType environment variable not found");
        }
        else if (!condensedString.Equals(headset))
        {
            correctHeadset.SetFailed($"Headset set to {headset} not {condensedString}");
        }
        else
        {
            correctHeadset.SetPassed($"HeadsetType is {headset}");
        }
        
        qaChecks.Add(correctHeadset);
        
        // Safe cast and null checks
        VrProfile? vrProfile = Profile.CastToType<VrProfile>(SessionController.StationProfile);
        if (vrProfile?.VrHeadset == null) return qaChecks;

        qaChecks.AddRange(vrProfile.VrHeadset.GetStatusManager().VrQaChecks());
        
        return qaChecks;
    }
}
