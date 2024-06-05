using System;

namespace Station.Components._version;

/// <summary>
/// Represents a NUC update version, specifying the capabilities and format it supports.
/// This allows the station to determine the appropriate data it can send and in which format.
/// For example, version "1.2.4" refers to the StateHandler, which supports setting values using JObjects instead of strings.
/// </summary>
public enum LeadMeVersion
{
    [Description("The earliest possible version, the default.")]
    [Value("1.0.0")]
    Base,
        
    [Description("Use JObject instead of strings for set value.")]
    [Value("1.2.4")]
    StateHandler
}

[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
sealed class DescriptionAttribute : Attribute
{
    public string Description { get; }

    public DescriptionAttribute(string description)
    {
        Description = description;
    }
}
    
[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
sealed class ValueAttribute : Attribute
{
    public string Value { get; }

    public ValueAttribute(string value)
    {
        Value = value;
    }
}
