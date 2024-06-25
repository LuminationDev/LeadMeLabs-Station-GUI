using System;
using System.Reflection;

namespace Station.Components._enums;

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

public static class Attributes {
    /// <summary>
    /// Retrieves the value attribute of an enum value.
    /// If the value attribute is not found, returns the enum value as a string.
    /// </summary>
    /// <param name="value">The enum value to retrieve the value for.</param>
    /// <returns>The value attribute of the enum value, or the enum value as a string if no value is found.</returns>
    public static string GetEnumValue(Enum value)
    {
        FieldInfo? field = value.GetType().GetField(value.ToString());
        if (field == null) return value.ToString();
        ValueAttribute? attribute = (ValueAttribute?)Attribute.GetCustomAttribute(field, typeof(ValueAttribute));
        return attribute?.Value ?? "";
    }

    /// <summary>
    /// Retrieves the description attribute of an enum value.
    /// If the description attribute is not found, returns the enum value as a string.
    /// </summary>
    /// <param name="value">The enum value to retrieve the description for.</param>
    /// <returns>The description attribute of the enum value, or the enum value as a string if no description is found.</returns>
    public static string GetEnumDescription(Enum value)
    {
        FieldInfo? field = value.GetType().GetField(value.ToString());
        if (field == null) return value.ToString();
        DescriptionAttribute? attribute =
            (DescriptionAttribute?)Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute));
        return attribute?.Description ?? "";
    }
}
