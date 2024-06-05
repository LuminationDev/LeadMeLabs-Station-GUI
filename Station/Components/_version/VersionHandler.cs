using System;
using System.Reflection;
using System.Threading.Tasks;
using Station.Components._utils;
using Station.MVC.Controller;
using Version = Station.Components._models.Version;

namespace Station.Components._version;

public static class VersionHandler
{
    public static LeadMeVersion NucVersion { get; private set; } = LeadMeVersion.Base;

    /// <summary>
    /// Send the version number to the NUC before anything else so the NUC knows how to handle the Station. Once the
    /// NUC sets the version, it will send back it's own version (if updated).
    /// </summary>
    public static void Connect()
    {
        MessageController.SendResponse("NUC", "Station", $"Version:{Updater.GetVersionNumber()}");
        Task.Delay(2000).Wait(); //Forced delay while waiting for the NUC response
    }
    
    /// <summary>
    /// Sets the NUC version to the nearest supported version based on the provided version string.
    /// </summary>
    /// <param name="nucVersion">The version string to be evaluated and matched to the nearest supported version.</param>
    public static void SetVersion(string nucVersion)
    {
        LeadMeVersion nearestVersion = GetNearestEnumValue(nucVersion);
        NucVersion = nearestVersion;
    }

    /// <summary>
    /// Determines the nearest supported enum value for the provided version string.
    /// The method rounds down to the nearest matching version.
    /// </summary>
    /// <param name="version">The version string to be evaluated and matched to the nearest supported version.</param>
    /// <returns>The nearest <see cref="LeadMeVersion"/> enum value that is less than or equal to the provided version.</returns>
    private static LeadMeVersion GetNearestEnumValue(string version)
    {
        Version inputVersion = new Version(version);
        LeadMeVersion nearestVersion = LeadMeVersion.Base; // Default to the earliest version
        Version nearestParsedVersion = new Version("1.0.0"); // Default version for comparison

        foreach (LeadMeVersion enumValue in Enum.GetValues(typeof(LeadMeVersion)))
        {
            string? value = GetEnumValue(enumValue);
            if (value == null) continue;
            
            Version enumParsedVersion = new Version(value);
            if (enumParsedVersion.CompareTo(inputVersion) > 0 || enumParsedVersion.CompareTo(nearestParsedVersion) <= 0) continue;
            nearestParsedVersion = enumParsedVersion;
            nearestVersion = enumValue;
        }

        return nearestVersion;
    }
    
    /// <summary>
    /// Retrieves the value attribute of an enum value.
    /// If the value attribute is not found, returns the enum value as a string.
    /// </summary>
    /// <param name="value">The enum value to retrieve the value for.</param>
    /// <returns>The value attribute of the enum value, or the enum value as a string if no value is found.</returns>
    private static string? GetEnumValue(Enum value)
    {
        FieldInfo? field = value.GetType().GetField(value.ToString());
        if (field == null) return value.ToString();
        ValueAttribute? attribute = (ValueAttribute?)Attribute.GetCustomAttribute(field, typeof(ValueAttribute));
        return attribute?.Value;
    }
        
    /// <summary>
    /// Retrieves the description attribute of an enum value.
    /// If the description attribute is not found, returns the enum value as a string.
    /// </summary>
    /// <param name="value">The enum value to retrieve the description for.</param>
    /// <returns>The description attribute of the enum value, or the enum value as a string if no description is found.</returns>
    public static string? GetEnumDescription(Enum value)
    {
        FieldInfo? field = value.GetType().GetField(value.ToString());
        if (field == null) return value.ToString();
        DescriptionAttribute? attribute = (DescriptionAttribute?)Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute));
        return attribute?.Description;
    }
}
