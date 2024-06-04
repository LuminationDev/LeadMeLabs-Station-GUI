using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using LeadMeLabsLibrary;
using Newtonsoft.Json.Linq;
using Station.Components._utils;

namespace Station.MVC.Controller;

/// <summary>
/// Represents a container for holding the current values of individual statuses and their associated values.
/// </summary>
public static class StateController
{
    //STATES NOT MAINTAINED
    // details - sent directly to the tablet (no temp data on NUC)
    // steamCMD - 
    
    //STATES TO MAINTAIN
    //VALUES
    //Headset stuff
    // headsetType
    // deviceStatus
    // deviceStatus:Headset:OpenVR:tracking
    // deviceStatus:Headset:Vive:tracking
    // deviceStatus:Controller:{role}:tracking
    // deviceStatus:Controller:{role}:battery
    // deviceStatus:BaseStation
    
    //Station stuff
    // status
    // state
    // session
    
    //Experience stuff
    // gameName
    // gameId
    // steamCMD
    
    //Audio stuff
    // activeAudioDevices
    // volume
    // muted
    
    //Video stuff
    // activeVideoPlaybackTime
    // activeVideoFile
    
    //LISTS
    // audioDevices
    // videoFiles
    // videoPlayerDetails
    // installedJsonApplications
    // blockedApplications

    #region State Values
    /// <summary>
    /// Gets the dictionary that holds the current state off all values on the Station.
    /// </summary>
    private static ConcurrentDictionary<string, object> StateValues { get; } = new();

    /// <summary>
    /// Adds a new key and value to the container.
    /// </summary>
    /// <param name="key">The key to be added.</param>
    /// <param name="value">The value associated with the status.</param>
    /// <returns>True if the value was added successfully; otherwise, false.</returns>
    private static bool AddStateValue(string key, object value)
    {
        return StateValues.TryAdd(key, value);
    }
    
    /// <summary>
    /// Adds multiple keys and values to the container at once.
    /// </summary>
    /// <param name="values">A dictionary containing statuses and their corresponding values to be added.</param>
    public static void UpdateStatusBunch(Dictionary<string, object> values)
    {
        foreach (var kvp in values)
        {
            if (!StateValues.ContainsKey(kvp.Key))
            {
                AddStateValue(kvp.Key, kvp.Value);
            }
        
            //Value is already set
            if (StateValues[kvp.Key] == kvp.Value) continue;
            StateValues[kvp.Key] = kvp.Value;
        }
        
        SendStateValues();
    }

    /// <summary>
    /// Updates the value of an existing status in the container.
    /// </summary>
    /// <param name="key">The key to be updated.</param>
    /// <param name="value">The new value for the status.</param>
    /// <returns>True if the status value was updated successfully; otherwise, false.</returns>
    public static bool UpdateStateValue(string key, object value)
    {
        if (!StateValues.ContainsKey(key))
        {
            AddStateValue(key, value);
        }
        
        StateValues[key] = value;
        SendStateValues();
        return true;
    }

    /// <summary>
    /// Retrieves the value associated with a given status.
    /// </summary>
    /// <param name="key">The key whose value is to be retrieved.</param>
    /// <returns>The value associated with the specified status.</returns>
    public static object GetStateValue(string key)
    {
        if (!StateValues.TryGetValue(key, out object? value))
        {
            Logger.WriteLog("GetStatusValue - The status does not exist in the container.", Enums.LogLevel.Error);
            return false;
        }
        
        return value;
    }

    /// <summary>
    /// Removes a status and its value from the container.
    /// </summary>
    /// <param name="key">The key to be removed.</param>
    /// <returns>True if the value was removed successfully; otherwise, false.</returns>
    public static bool RemoveStateValue(string key)
    {
        if (!StateValues.TryRemove(key, out _))
        {
            Logger.WriteLog("RemoveStatusValue - The status does not exist in the container.", Enums.LogLevel.Error);
        }
        SendStateValues();
        return true;
    }

    /// <summary>
    /// Clears all statuses and their values from the container.
    /// </summary>
    public static void ClearStateValues()
    {
        StateValues.Clear();
    }
    
    /// <summary>
    /// Logs the current status values as a JObject.
    /// </summary>
    private static void SendStateValues()
    {
        JObject stateValuesJson = ToJObject(StateValues);
        MessageController.SendResponse("NUC", "Station", $"CurrentState:{stateValuesJson}");
    }
    #endregion

    #region File State
    private static ConcurrentDictionary<string, object> ListValues { get; } = new();
    
    /// <summary>
    /// Adds a new key and value to the container.
    /// </summary>
    /// <param name="key">The key to be added.</param>
    /// <param name="value">The value associated with the status.</param>
    /// <returns>True if the value was added successfully; otherwise, false.</returns>
    private static bool AddListValue(string key, object value)
    {
        return ListValues.TryAdd(key, value);
    }
    
    /// <summary>
    /// Adds multiple keys and values to the container at once.
    /// </summary>
    /// <param name="values">A dictionary containing statuses and their corresponding values to be added.</param>
    public static void UpdateListBunch(Dictionary<string, object> values)
    {
        foreach (var kvp in values)
        {
            if (!ListValues.ContainsKey(kvp.Key))
            {
                AddListValue(kvp.Key, kvp.Value);
            }
        
            //Value is already set
            if (ListValues[kvp.Key] == kvp.Value) continue;
            ListValues[kvp.Key] = kvp.Value;
        }
        
        SendListValues();
    }
    
    /// <summary>
    /// Updates the value of an existing files in the container.
    /// </summary>
    /// <param name="key">The key to be updated.</param>
    /// <param name="value">The new value for the status.</param>
    /// <returns>True if the status value was updated successfully; otherwise, false.</returns>
    public static bool UpdateListsValue(string key, object value)
    {
        if (!ListValues.ContainsKey(key))
        {
            AddListValue(key, value);
        }
        
        ListValues[key] = value;
        SendListValues();
        return true;
    }
    
    /// <summary>
    /// Logs the current status values as a JObject.
    /// </summary>
    private static void SendListValues()
    {
        JObject listsValuesJson = ToJObject(ListValues);
        MessageController.SendResponse("NUC", "Station", $"CurrentLists:{listsValuesJson}");
    }
    #endregion
    
    /// <summary>
    /// Converts the concurrent dictionary to a JObject.
    /// </summary>
    /// <returns>A JObject representing the current state values.</returns>
    private static JObject ToJObject(object values)
    {
        return JObject.FromObject(values);
    }
}
