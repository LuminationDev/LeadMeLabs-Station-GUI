namespace Station.Components._models;

/// <summary>
/// A class to hold the details about the local playback devices.
/// </summary>
public class LocalAudioDevice
{
    public string Name { get; }
    public string Id { get; }
    public string Volume { get; private set; } = "0";
    public bool Muted { get; private set; }

    public LocalAudioDevice(string name, string id)
    {
        Name = name;
        Id = id;
    }
    
    public void SetVolume(string volume)
    {
        Volume = volume;
    }

    public void SetMuted(bool isMuted)
    {
        Muted = isMuted;
    }
}
