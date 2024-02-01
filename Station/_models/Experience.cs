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
    public string? Parameters { get; set; }
    public string? AltPath { get; set; }

    public bool IsVr { get; set; }

    public Experience(string? Type, string? ID, string? Name, string? ExeName, string? Parameters, string? AltPath, bool isVr)
    {
        this.Type = Type;
        this.ID = ID;
        this.Name = Name;
        this.ExeName = ExeName;
        this.Parameters = Parameters;
        this.AltPath = AltPath;
        this.IsVr = isVr;
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
}
