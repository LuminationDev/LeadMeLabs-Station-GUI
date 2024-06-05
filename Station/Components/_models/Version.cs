using System;

namespace Station.Components._models;

public class Version : IComparable<Version>
{
    private int Major { get; }
    private int Minor { get; }
    private int Patch { get; }

    public Version(string version)
    {
        var parts = version.Split('.');
        Major = int.Parse(parts[0]);
        Minor = int.Parse(parts[1]);
        Patch = int.Parse(parts[2]);
    }

    public int CompareTo(Version other)
    {
        if (Major != other.Major) return Major.CompareTo(other.Major);
        if (Minor != other.Minor) return Minor.CompareTo(other.Minor);
        return Patch.CompareTo(other.Patch);
    }
}
