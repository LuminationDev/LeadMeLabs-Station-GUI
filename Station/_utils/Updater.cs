using System.Reflection;
using System.Diagnostics;
using System.IO;

namespace Station
{
    public static class Updater
    {
        /// <summary>
        /// Print out the current production version of the application to a text file.
        /// </summary>
        public static bool generateVersion()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            string? version = fileVersionInfo.ProductVersion;
            string? stationLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            File.WriteAllText($"{stationLocation}\\_logs\\version.txt", version);

            return version != null;
        }
    }
}
