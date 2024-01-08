using System;
using System.Linq;
using Station.Components._notification;

namespace Station.Components._utils;

public class Helper
{
    public const string STATION_MODE_APPLIANCE = "Appliance";
    private const string STATION_MODE_VR = "VR";
    private const string STATION_MODE_CONTENT = "Content";
    private static readonly string[] STATION_MODES = { STATION_MODE_VR, STATION_MODE_APPLIANCE, STATION_MODE_CONTENT };

    public static string GetStationMode()
    {
        string? mode = Environment.GetEnvironmentVariable("StationMode", EnvironmentVariableTarget.Process);
        if (mode == null)
        {
            Environment.SetEnvironmentVariable("StationMode", STATION_MODE_VR);
            mode = STATION_MODE_VR;
        }
        if (mode.Equals("vr"))
        {
            Environment.SetEnvironmentVariable("StationMode", STATION_MODE_VR);
            mode = STATION_MODE_VR;
        }

        if (STATION_MODES.Contains(mode)) return mode;
        
        Logger.WriteLog($"Station Mode is not set or supported: {mode}.", MockConsole.LogLevel.Error);
        throw new Exception("Station in unsupported mode");
    }
}
