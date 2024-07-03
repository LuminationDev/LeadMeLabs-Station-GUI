using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LeadMeLabsLibrary;
using Newtonsoft.Json.Linq;
using Station.Components._enums;
using Station.Components._interfaces;
using Station.Components._managers;
using Station.Components._notification;
using Station.Components._profiles;
using Station.Components._scripts;
using Station.Components._segment;
using Station.Components._segment._classes;
using Station.Components._utils;
using Station.Components._wrapper.vive;
using Station.MVC.ViewModel;

namespace Station.MVC.Controller;

public static class SessionController
{
    /// <summary>
    /// The absolute path of the steam executable on the local machine.
    /// </summary>
    public const string Steam = "C:/Program Files (x86)/Steam/steam.exe";

    /// <summary>
    /// Store the profile that is linked to the current station.
    /// </summary>
    public static IProfile? StationProfile { private set; get; }

    /// <summary>
    /// Store the current experience type that is running.
    /// </summary>
    public static string? ExperienceType { set; get; }

    /// <summary>
    /// Track the current state of the Station software.
    /// </summary>
    private static State currentState = State.Base;

    public static State CurrentState
    {
        get => currentState;
        set
        {
            currentState = value;
            StateController.UpdateStateValue("state", Attributes.GetEnumValue(currentState));
            UiController.UpdateCurrentState(Attributes.GetEnumValue(currentState)); //Update the home page UI
        }
    }
    
    private static DateTime gameLaunchTime = DateTime.Now;
    private static String gameName = "";
    private static String gameId = "";
    private static String gameType = "";

    /// <summary>
    /// Setup the Station profiles using the supplied .config information. The profile determines what processes are
    /// started and monitored.
    /// </summary>
    public static void SetupStationProfile(string profile)
    {
        switch (profile.ToLower())
        {
            case "vr":
                StationProfile = new VrProfile();
                break;
            case "content":
                StationProfile = new ContentProfile();
                break;
            default:
                Logger.WriteLog($"SessionController - SetupStationProfile: Unknown profile selected: {profile}", Enums.LogLevel.Error);
                break;
        }
    }

    /// <summary>
    /// Start up a VR session on the local machine, this may include starting up Steam, steamVR and/or ViveWireless. The
    /// applications that will be started/required depend on the supplied type.
    /// </summary>
    /// <param name="type">A string of what type of experience is being loaded [Custom, Steam, Vive, etc]</param>
    public static void StartSession(string type)
    {
        if (!InternalDebugger.GetAutoStart()) return;
        
        ExperienceType = type;
        switch (ExperienceType)
        {
            case "Custom":
            case "Embedded":
            case "Steam":
            case "Revive":
                StationProfile?.StartSession();
                break;
            case "Vive":
                MockConsole.WriteLine("startVRSession not implemented for type: Vive.", Enums.LogLevel.Error);
                break;
        }
        
        //Attempt to minimise other applications (mostly Steam)
        StationProfile?.MinimizeSoftware(2);
    }

    /// <summary>
    /// Stop all processes that are associated with a VR session.
    /// </summary>
    public static void RestartVrSession()
    {
        //Reset the idle timer and current mode type
        if (InternalDebugger.GetIdleModeActive())
        {
            ModeTracker.ResetMode();
        }
        
        ScheduledTaskQueue.EnqueueTask(() => UpdateState(State.StopVrProcess), TimeSpan.FromSeconds(1));
        _ = WrapperManager.RestartVrProcesses();

        if (ExperienceType == null)
        {
            MockConsole.WriteLine("No experience is currently running.", Enums.LogLevel.Normal);
            return;
        }

        switch (ExperienceType)
        {
            case "Custom":
            case "Embedded":
                MockConsole.WriteLine("restartVRSession not implemented for type: Custom.", Enums.LogLevel.Error);
                break;
            case "Revive":
            case "Steam":
                ViveScripts.StopMonitoring();
                break;
            case "Vive":
                MockConsole.WriteLine("restartVRSession not implemented for type: Vive.", Enums.LogLevel.Error);
                break;
            default:
                MockConsole.WriteLine("Wrapper: No experience type set.", Enums.LogLevel.Error);
                break;
        }
        
        //Attempt to minimise other applications (mostly Steam)
        StationProfile?.MinimizeSoftware(2);
    }

    /// <summary>
    /// Stop all processes that are associated with a VR session.
    /// </summary>
    public static void EndVrSession()
    {
        switch (ExperienceType)
        {
            case "Custom":
            case "Embedded":
                MockConsole.WriteLine("endVRSession not implemented for type: Custom.", Enums.LogLevel.Error);
                break;
            case "Revive":
            case "Steam":
                ViveScripts.StopMonitoring();
                break;
            case "Vive":
                MockConsole.WriteLine("endVRSession not implemented for type: Vive.", Enums.LogLevel.Error);
                break;
            default:
                MockConsole.WriteLine("Wrapper: No experience type set.", Enums.LogLevel.Error);
                break;
        }

        ExperienceType = null;
        
        //Attempt to minimise other applications (mostly Steam)
        StationProfile?.MinimizeSoftware(2);
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
    /// Update the current state enum of the Station.
    /// </summary>
    /// <param name="state"></param>
    public static void UpdateState(State state)
    {
        CurrentState = state;
    }

    /// <summary>
    /// Take an action message from the wrapper and pass the response onto the NUC or handle it internally.
    /// </summary>
    /// <param name="message">A string representing the message, different actions are separated by a ','</param>
    public static void PassStationMessage(JObject message)
    {
        new Thread(() => {
            MockConsole.WriteLine("Action: " + message, Enums.LogLevel.Debug);
            
            string? action = (string?)message.GetValue("action");
            if (action == null) return;
            
            string? value = (string?)message.GetValue("value");

            switch (action)
            {
                case "MessageToAndroid":
                    MessageController.SendResponse("Android", "Station", value);
                    break;

                case "Processing":
                    if (value == null) return;
                    StationScripts.processing = bool.Parse(value);
                    break;

                case "ApplicationUpdate":
                    JObject? info = (JObject?)message.GetValue("info");
                    if (info == null) return;

                    string? name = (string?)info.GetValue("name");
                    string? appId = (string?)info.GetValue("appId");
                    string? wrapper = (string?)info.GetValue("wrapper");
                    
                    {
                        Dictionary<string, object?> stateValues = new()
                        {
                            { "gameName", name ?? "" }
                        };
                        
                        if (appId != null && wrapper != null)
                        {
                            stateValues.Add("gameId", appId);
                            stateValues.Add("gameType", wrapper);
                            //Update the ExperienceView UI
                            MainViewModel.ViewModelManager.ExperiencesViewModel.UpdateExperience(appId, "status", "Running");
                        }
                        else
                        {
                            stateValues.Add("gameId", "");
                            stateValues.Add("gameType", "");
                        }

                        StateController.UpdateStatusBunch(stateValues);
                    }
                    
                    gameLaunchTime = DateTime.Now;
                    gameName = name ?? "";
                    gameId = appId ?? "";
                    gameType = wrapper ?? "";
                    break;

                case "ApplicationClosed":
                    {
                        Dictionary<string, object?> stateValues = new()
                        {
                            { "gameName", "" },
                            { "gameId", "" },
                            { "gameType", "" }
                        };
                        StateController.UpdateStatusBunch(stateValues);
                        
                        //Update the ExperienceView UI
                        MainViewModel.ViewModelManager.ExperiencesViewModel.ExperienceStopped();

                        // segment tracking
                        TimeSpan difference = DateTime.Now - gameLaunchTime;
                        int timeInMinutes = Convert.ToInt32(difference.TotalMinutes);
                        
                        if (timeInMinutes < 0) break;
        
                        SegmentExperienceEvent experienceEvent = new SegmentExperienceEvent(
                            SegmentConstants.EventExperienceDuration,
                            gameName,
                            gameId,
                            gameType
                        );
                        experienceEvent.SetRuntime(timeInMinutes);
                        Components._segment.Segment.TrackAction(experienceEvent);
                    }
                    break;

                case "StationError":
                    //Just print to the Console for now but send message to the NUC/Tablet in the future
                    break;

                default:
                    MockConsole.WriteLine("Non-primary command", Enums.LogLevel.Debug);
                    break;
            }
        }).Start();
    }
}
