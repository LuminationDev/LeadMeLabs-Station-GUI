using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Station._utils;

namespace Station._models;

/// <summary>
/// Encapsulate an experience regardless of wrapper type to hold all relative information in
/// a singular object.
/// </summary>
public struct Experience
{
    public string? Type { get; set; }
    public string? ID { get; set; }
    public string? Name { get; set; }
    public string? ExeName { get; set; }
    public string? Parameters { get; private set; }
    public string? AltPath { get; set; }
    public bool IsVr { get; set; }
    public string? HeaderPath { get; set; }
    
    /// <summary>
    /// Contains specific information about the Experience's category and how the Tablet should handle it's launching
    /// and operation.
    /// </summary>
    public JObject? Subtype { get; }

    public Experience(string? type, string? id, string? name, string? exeName, string? parameters, string? altPath, bool isVr, JObject? subtype = null, string? headerPath = null)
    {
        this.Type = type;
        this.ID = id;
        this.Name = name;
        this.ExeName = exeName;
        this.Parameters = parameters;
        this.AltPath = altPath;
        this.IsVr = isVr;
        this.HeaderPath = headerPath;
        this.Subtype = subtype;
    }

    /// <summary>
    /// Determine if the experience instance is just a default constructor that contains all
    /// null values.
    /// </summary>
    /// <returns>A bool if any of the key variables are null.</returns>
    public bool IsNull()
    {
        return (Type == null || ID == null || Name == null);
    }

    public void UpdateParameters(string arguments)
    {
        //Only configured to handle shareCode experiences at the moment
        string? category = Subtype?.GetValue("category")?.ToString();
        if (category == null) return;

        switch (category)
        {
            case "shareCode":
                //[0] -app
                //[1] (typeValue)
                //[2] -code
                //[3] (codeValue)
                List<string>? split = new List<string>(Parameters?.Split(" ") ?? Array.Empty<string>());
                switch (split.Count)
                {
                    case 0:
                        return;
                    case >= 4:
                        split[3] = arguments;
                        break;
                    default:
                        split.Add(arguments);
                        break;
                }

                // Join the arguments with a space between
                Parameters = string.Join(" ", split);
                break;
            
            default:
                Logger.WriteLog($"Subtype not configured for update: {category}", MockConsole.LogLevel.Normal);
                break;
        }
    }
}
