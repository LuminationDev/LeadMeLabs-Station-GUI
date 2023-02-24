using System;
using System.Linq;

namespace Station
{
    public class Helper
    {
        public const string STATION_MODE_VR = "vr";
        public const string STATION_MODE_APPLIANCE = "appliance";
        public const string STATION_MODE_CONTENT = "content";
        private static readonly string[] STATION_MODES = { STATION_MODE_VR, STATION_MODE_APPLIANCE, STATION_MODE_CONTENT };

        public static string GetStationMode()
        {
            string? mode = Environment.GetEnvironmentVariable("StationMode");
            if (mode == null)
            {
                Environment.SetEnvironmentVariable("StationMode", STATION_MODE_VR);
                mode = STATION_MODE_VR;
            }

            if (!STATION_MODES.Contains(mode))
            {
                throw new Exception("Station in unsupported mode");
            }

            return mode;
        }
    }
}
