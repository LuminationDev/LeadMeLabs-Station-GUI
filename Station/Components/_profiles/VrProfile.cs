using System;
using System.Collections.Generic;
using LeadMeLabsLibrary;
using Newtonsoft.Json.Linq;
using Station.Components._interfaces;
using Station.Components._profiles._headsets;
using Station.Components._utils;
using Station.Components._wrapper.vive;
using Station.MVC.Controller;

namespace Station.Components._profiles;

public class VrProfile: Profile, IProfile
{
    private string? _headsetType;

    /// <summary>
    /// Get the Profiles variant type
    /// </summary>
    public Variant GetVariant()
    {
        return Variant.Vr;
    }

    /// <summary>
    /// Store the HeadSet type that is linked to the current station.
    /// </summary>
    private IVrHeadset? _vrHeadset;
    public IVrHeadset? VrHeadset
    {
        get => _vrHeadset;
        set
        {
            if (_vrHeadset != null && _vrHeadset == value) return;

            _vrHeadset = value;
        }
    }

    public VrProfile()
    {
        SetupHeadsetType();
    }

    /// <summary>
    /// Read the store headset type from the config.env file and create an instance that 
    /// can be accessed from this class.
    /// </summary>
    private void SetupHeadsetType()
    {
        _headsetType = Environment.GetEnvironmentVariable("HeadsetType", EnvironmentVariableTarget.Process);

        //Read from env file
        switch (_headsetType)
        {
            case "VivePro1":
                VrHeadset = new VivePro1();
                break;
            case "VivePro2":
                VrHeadset = new VivePro2();
                break;
            case "ViveFocus3": //Backwards compatability
            case "ViveBusinessStreaming":
                VrHeadset = new ViveBusinessStreaming();
                break;
            default:
                Logger.WriteLog("No headset type specified.", Enums.LogLevel.Error);
                break;
        }
    }

    /// <summary>
    /// Collect the specific processes a headset requires to work.
    /// </summary>
    /// <returns>A list of headset specific processes.</returns>
    public List<string> GetProcessesToQuery()
    {
        return VrHeadset == null ? new List<string>() : VrHeadset.GetProcesses(ProcessListType.Query);
    }

    /// <summary>
    /// Minimise the software that handles the headset.
    /// </summary>
    /// <param name="attemptLimit"></param>
    public void MinimizeSoftware(int attemptLimit = 6)
    {
        if (VrHeadset == null)
        {
            return;
        }
        Minimize(VrHeadset.GetProcesses(ProcessListType.Minimize), attemptLimit);
    }

    public void StartSession()
    {
        //Bail out if session processes are already running
        if (QueryMonitorProcesses(GetProcessesToQuery()))
        {
            return;
        }

        JObject message = new JObject
        {
            { "action", "SoftwareState" },
            { "value", "Starting VR processes" }
        };
        ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage(message), TimeSpan.FromSeconds(0));

        VrHeadset?.StartVrSession(false);
        MinimizeSoftware(2);
    }
    
    public void StartDevToolsSession()
    {
        JObject message = new JObject
        {
            { "action", "SoftwareState" },
            { "value", "Restarting Processes" }
        };
        ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage(message), TimeSpan.FromSeconds(0));
        VrHeadset?.StartVrSession(true);
    }

    /// <summary>
    /// Collect the connection status of the headset from a headset's specific management software.
    /// </summary>
    /// <param name="wrapperType">A string of the Wrapper type that is being launched, required if the process
    /// needs to restart/start the VR session.</param>
    /// <returns>A bool representing the connection status.</returns>
    public bool WaitForConnection(string wrapperType)
    {
        switch (_headsetType)
        {
            case "VivePro1":
            case "VivePro2":
            case "ViveFocus3": //Backwards compatability
            case "ViveBusinessStreaming":
                return ViveScripts.WaitForVive(wrapperType).Result;
            default:
                Logger.WriteLog("WaitForConnection - No headset type specified.", Enums.LogLevel.Error);
                return false;
        }
    }
}