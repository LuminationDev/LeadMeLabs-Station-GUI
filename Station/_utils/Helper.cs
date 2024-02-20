using System;
using System.Linq;
using System.Threading.Tasks;
using Station._utils;
using Station._wrapper;

namespace Station
{
    public class Helper
    {
        public const string STATION_MODE_VR = "VR";
        public const string STATION_MODE_APPLIANCE = "Appliance";
        public const string STATION_MODE_CONTENT = "Content";
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

            if (!STATION_MODES.Contains(mode))
            {
                Logger.WriteLog($"Station Mode is not set or supported: {mode}.", MockConsole.LogLevel.Error);
                throw new Exception("Station in unsupported mode");
            }

            return mode;
        }
        
        /// <summary>
        /// Monitors a specified condition using a loop that is delayed by 3 seconds each time the condition is not met, with
        /// optional timeout and attempt limits.
        /// </summary>
        /// <param name="conditionChecker">A delegate that returns a boolean value indicating whether the monitored condition is met.</param>
        /// <param name="attemptLimit">An int of the maximum amount of attempts the loop will wait for.</param>
        /// <returns>True if the condition was successfully met within the specified attempts; false otherwise.</returns>
        public static async Task<bool> MonitorLoop(Func<bool> conditionChecker, int attemptLimit)
        {
            //Track the attempts
            int monitorAttempts = 0;
            int delay = 3000;

            //Check the condition status (bail out after x amount)
            do
            {
                monitorAttempts++;
                await Task.Delay(delay);
            } while (conditionChecker.Invoke() && monitorAttempts < attemptLimit);

            return true;
        }
    }
}
