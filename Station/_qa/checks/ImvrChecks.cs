using System;
using System.Collections.Generic;

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
        qaChecks.AddRange(SessionController.VrHeadset.GetStatusManager().VrQaChecks());
        
        return qaChecks;
    }
}
