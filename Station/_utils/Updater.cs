using System.Reflection;
using System.Diagnostics;
using System.IO;
using System;
using System.Threading.Tasks;
using Sentry;

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
		public async static Task UpdateLauncher()
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

            MockConsole.WriteLine("Waiting 15 seconds for Launcher to close.", MockConsole.LogLevel.Normal);

			//Make sure the launcher has closed before trying to extract
			for(int i = 15; i > 0; i--)
            {
				await Task.Delay(1000);
				MockConsole.WriteLine($"{i}....", MockConsole.LogLevel.Normal);
			}
			
			MockConsole.WriteLine("Clearing old folder - ", MockConsole.LogLevel.Normal);

			try
			{
				//Delete the Launcher folder and replace it with the Temp one
				ClearFolder(destinationPath);

				await Task.Delay(2000);

			}
			catch (Exception e)
			{
				MockConsole.WriteLine("Clearing folder error: ", MockConsole.LogLevel.Error);
				MockConsole.WriteLine(e.ToString(), MockConsole.LogLevel.Error);
			}

			MockConsole.WriteLine("Moving archive - ", MockConsole.LogLevel.Normal);

			try
			{
				// Create the final destination's parent file it is does not exist
				Directory.CreateDirectory(destinationPath);

				MockConsole.WriteLine("Creating necessary subfolders.", MockConsole.LogLevel.Normal);

				//Create any necessary subfolders.
				foreach (string newFolder in Directory.GetDirectories(extractTarget))
				{
					MockConsole.WriteLine(newFolder.Replace(extractTarget, destinationPath), MockConsole.LogLevel.Error);
					Directory.CreateDirectory(newFolder.Replace(extractTarget, destinationPath));
				}

				MockConsole.WriteLine("Moving necessary files.", MockConsole.LogLevel.Normal);

				// Copy the extracted files and replace everything in the current directory to finish the update
				// C# doesn't easily let us extract & replace at the same time
				// From http://stackoverflow.com/a/3822913/1460422
				foreach (string newPath in Directory.GetFiles(extractTarget, "*.*", SearchOption.AllDirectories))
				{
					MockConsole.WriteLine(newPath.Replace(extractTarget, destinationPath), MockConsole.LogLevel.Error);
					File.Copy(newPath, newPath.Replace(extractTarget, destinationPath), true);
				}

				await Task.Delay(2000);

				MockConsole.WriteLine("done.", MockConsole.LogLevel.Error);
			}
			catch (Exception e)
			{
				MockConsole.WriteLine("Moving folder error: ", MockConsole.LogLevel.Error);
				MockConsole.WriteLine(e.ToString(), MockConsole.LogLevel.Error);
			}
			finally
			{
				// Clean up the temporary files
				MockConsole.WriteLine("Cleaning up - ", MockConsole.LogLevel.Error);
				Directory.Delete(extractTarget, true);
				MockConsole.WriteLine("done.", MockConsole.LogLevel.Error);
			}

			SentrySdk.CaptureMessage("Migration Complete at: " +
										(Environment.GetEnvironmentVariable("LabLocation", EnvironmentVariableTarget.User) ?? "Unknown") + 
										"for Station " +
										(Environment.GetEnvironmentVariable("LabLocation", EnvironmentVariableTarget.User) ?? "Unknown"));

			MockConsole.WriteLine("Migration Complete, exiting Station and opening Launcher", MockConsole.LogLevel.Normal);

            //Needs to exit the current application and start the 'new' launcher with a command line argument
            //Open launcher with command line
            string launcher = $@"C:\Users\{Environment.GetEnvironmentVariable("UserDirectory", EnvironmentVariableTarget.User)}\Launcher\LeadMe.exe";
            string arguments = $" --software=Station --directory={Environment.GetEnvironmentVariable("UserDirectory", EnvironmentVariableTarget.User)}";

            Process temp = new();
            temp.StartInfo.FileName = launcher;
            temp.StartInfo.Arguments = arguments;
            temp.StartInfo.UseShellExecute = true;
            temp.Start();
            temp.Close();

            //Immediately close the current application
            Environment.Exit(0);
        }

		/// <summary>
		/// Delete the contents of a folder.
		/// </summary>
		/// <param name="FolderName">A string of the path to the folder to clear.</param>
		private static void ClearFolder(string FolderName)
		{
			DirectoryInfo dir = new(FolderName);

			foreach (FileInfo fi in dir.GetFiles())
			{
				fi.Delete();
			}

			foreach (DirectoryInfo di in dir.GetDirectories())
			{
				ClearFolder(di.FullName);
				di.Delete();
			}
		}

		/// <summary>
		/// Move a directory to another.
		/// </summary>
		public static void Move(string source, string destination)
		{
			try
			{
				Directory.Move(source, destination);
			}
			catch (Exception e)
			{
				MockConsole.WriteLine(e.Message, MockConsole.LogLevel.Normal);
			}
		}
	}
}
