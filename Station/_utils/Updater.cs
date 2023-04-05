using System.Reflection;
using System.Diagnostics;
using System.IO;
using System;
using System.Threading.Tasks;

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


		/// THIS IS ONLY HERE FOR PREVIOUS LEADMELABS VERSIONS
		/// <summary>
		/// After being launched by the launcher check if there was an update. The launcher automatically
		/// downloads any updates for itself but cannot install them so trigger it here from a separate 
		/// program. Next time the launcher runs it will be the updated version.
		/// </summary>
		public async static void UpdateLauncher()
		{
			string directory = Environment.GetEnvironmentVariable("UserDirectory", EnvironmentVariableTarget.User);
			string destinationPath = $"C:\\Users\\{directory}\\Launcher";
			string extractTarget = $"C:\\Users\\{directory}\\LauncherTemp";

			if (!Directory.Exists(extractTarget))
			{
				//There has been no update, launcher the program as normal
				MockConsole.WriteLine("No launcher update - continue as normal", MockConsole.LogLevel.Normal);
				return;
			}

			//Make sure the launcher has closed before trying to extract
			await Task.Delay(1000);

			MockConsole.WriteLine("Moving archive - ", MockConsole.LogLevel.Normal);

			try
			{
				// Create the final destination's parent file it is does not exist
				Directory.CreateDirectory(destinationPath);

				//Create any necessary subfolders.
				foreach (string newFolder in Directory.GetDirectories(extractTarget))
				{
					MockConsole.WriteLine(newFolder.Replace(extractTarget, destinationPath), MockConsole.LogLevel.Normal);
					Directory.CreateDirectory(newFolder.Replace(extractTarget, destinationPath));
				}

				// Copy the extracted files and replace everything in the current directory to finish the update
				// C# doesn't easily let us extract & replace at the same time
				foreach (string newPath in Directory.GetFiles(extractTarget, "*.*", SearchOption.AllDirectories))
				{
					MockConsole.WriteLine(newPath.Replace(extractTarget, destinationPath), MockConsole.LogLevel.Normal);
					File.Copy(newPath, newPath.Replace(extractTarget, destinationPath), true);
				}

				MockConsole.WriteLine("done.", MockConsole.LogLevel.Normal);
			}
			catch (Exception e)
			{
				MockConsole.WriteLine(e.ToString());
			}
			finally
			{
				// Clean up the temporary files
				MockConsole.WriteLine("Cleaning up - ", MockConsole.LogLevel.Normal);
				Directory.Delete(extractTarget, true);
				MockConsole.WriteLine("done.", MockConsole.LogLevel.Normal);
			}
		}
	}
}
