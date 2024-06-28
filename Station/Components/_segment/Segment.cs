using System;
using LeadMeLabsLibrary;
using Newtonsoft.Json.Linq;
using Station.Components._segment._classes;
using Station.Components._segment._interfaces;
using Station.Components._utils;
using Station.MVC.Controller;
using Segment;
using Segment.Model;
using Logger = Station.Components._utils.Logger;

namespace Station.Components._segment;

public static class Segment
{
    private static string? _userId;
    private static string? _sessionId = "Session not started";
    private static string? _sessionStart = "";

    public static string GetSessionId()
    {
        return _sessionId ?? "Session not started";
    }
    
    /// <summary>
    /// Set up the analytics configuration and set the user Id.
    /// </summary>
    public static void Initialise()
    {
        try
        {
            Analytics.Initialize("9sFLeH6haHjqv492AR6pHL2kxUCQZgrx");

            _userId = Environment.GetEnvironmentVariable("LabLocation", EnvironmentVariableTarget.Process);

            //No location is set, do not proceed
            if (_userId == null)
            {
                Logger.WriteLog("Segment - Initialise: No lab location set.", Enums.LogLevel.Error);
                return;
            }

            Analytics.Client.Identify(_userId,  new Traits {
                {"location", _userId},
                {"app_version", Updater.GetVersionNumber()}
            });
        }
        catch (Exception e)
        {
            Logger.WriteLog($"Segment - Initialise: Initialisation failed error: {e}", Enums.LogLevel.Error);
        }
    }

    /// <summary>
    /// Parse and incoming Segment request to a extract the action that needs to be performed.
    /// </summary>
    /// <param name="additionalData">A string containing the action and additional data if required</param>
    public static void HandleRequest(string additionalData)
    {
        JObject details = JObject.Parse(additionalData);
        
        if (!details.ContainsKey("Action")) return;

        string? action = (string?)details.GetValue("Action");
        if (action == null) return;
        
        HandleAction(action);
    }

    /// <summary>
    /// Handle a specific action related to Segment.
    /// </summary>
    /// <param name="action">A string of the action to perform</param>
    /// <param name="additionalData">A string containing additional data if required</param>
    public static void HandleAction(string action, string? additionalData = null)
    {
        switch (action)
        {
            case "Update":
                UpdateSessionId(additionalData);
                break;
        }
    }

    /// <summary>
    /// A tablet has started a new session (VR mode) or ended a current one (Classroom Mode) and has sent across the
    /// new Session ID to track all events within the same session. Update the local ID and pass the message on to all
    /// connected tablets.
    /// NOTE: Send this to any new tablets that connect as well, so they are not out of sync.
    /// </summary>
    /// <param name="data"></param>
    private static void UpdateSessionId(string? data)
    {
        if (data == null) return;
        
        JObject details = JObject.Parse(data);

        if (!details.ContainsKey("SessionId")) return;
        
        _sessionId = (string?)details.GetValue("SessionId");

        if (details.ContainsKey("SessionStart"))
        {
            _sessionStart = (string?)details.GetValue("SessionStart");
        }
        else
        {
            _sessionStart = "";
        }
    }
    
    /// <summary>
    /// Tracks an action/event with associated details using Segment analytics.
    /// </summary>
    /// <param name="details">details A SegmentEvent containing additional details or properties associated with the event.</param>
    /// <typeparam name="T">The type of values stored in the details hashtable.</typeparam>
    public static void TrackAction<T>(T details) where T : IEventDetails
    {
        if (_userId == null) return;
        
        Analytics.Client.Track(
            _userId,
            details.GetEvent(),
            details.ToPropertiesDictionary()
        );
    }
}
