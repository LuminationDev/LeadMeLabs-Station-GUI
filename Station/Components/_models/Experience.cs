namespace Station.Components._models;

/// <summary>
/// Encapsulate an experience regardless of wrapper type to hold all relative information in
/// a singular object.
/// </summary>
public struct Experience
{
    public string? Type { get; set; }
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? ExeName { get; set; }
    public string? Parameters { get; set; }
    public string? AltPath { get; set; }
    public string? Status { get; set; } = "Stopped";

    public Experience(string? type, string? id, string? name, string? exeName, string? parameters, string? altPath)
    {
        this.Type = type;
        this.Id = id;
        this.Name = name;
        this.ExeName = exeName;
        this.Parameters = parameters;
        this.AltPath = altPath;
    }

    /// <summary>
    /// Determine if the experience instance is just a default constructor that contains all
    /// null values.
    /// </summary>
    /// <returns>A bool if any of the key variables are null.</returns>
    public bool IsNull()
    {
        return (Type == null || Id == null || Name == null);
    }
}
