using System;
using System.IO;
using System.Reflection;

namespace Station.Components._utils;

public static class Updater
{
	/// <summary>
	/// Query the program to get the current version number of the software that is running.
	/// </summary>
	/// <returns>The current Version object or null</returns>
	private static Version? GetVersion()
	{
		Assembly? assembly = Assembly.GetExecutingAssembly();
		if (assembly == null) return null;

		Version? version = assembly.GetName().Version;
		if (version == null) return null;

		return version;
	}
	
	/// <summary>
	/// Query the program to get the current version number of the software that is running.
	/// </summary>
	/// <returns>A string of the version number in the format X.X.X</returns>
	public static string GetVersionNumber()
	{
		Version? version = GetVersion();
		
		// Format the version number as Major.Minor.Build
		return version == null ? "Unknown" : $"{version.Major}.{version.Minor}.{version.Build}";
	}
	
	/// <summary>
	/// Query the program to get the current version number of the software that is running.
	/// </summary>
	/// <returns>A string of the version number in the format X-X-X</returns>
	public static string GetVersionNumberHyphen()
	{
		Version? version = GetVersion();
		// Format the version number as Major.Minor.Build
		return version == null ? "Unknown" : $"{version.Major}-{version.Minor}-{version.Build}";
	}

	/// <summary>
	/// Print out the currently running software version to a text file at 'programLocation\_logs\version.txt'. 
	/// If the version number or program directory cannot be found the function returns false, bailing out before
	/// writing the version. 
	/// </summary>
	public static bool GenerateVersion()
	{
		string? programLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		if (programLocation == null)
		{
			return false;
		}

		Assembly? assembly = Assembly.GetExecutingAssembly();
		if (assembly == null)
		{
			WriteFile(programLocation, "0.0.0");
			return false;
		}

		Version? version = assembly.GetName().Version;
		if (version == null)
		{
			WriteFile(programLocation, "0.0.0");
			return false;
		}

		string? formattedVersion = $"{version.Major}.{version.Minor}.{version.Build}";
		WriteFile(programLocation, formattedVersion);

		return version != null;
	}

	private static void WriteFile(string location, string version)
	{
		File.WriteAllText($"{location}\\_logs\\version.txt", version);
	}
}
