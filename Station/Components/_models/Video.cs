namespace Station.Components._models;

public class Video
{
    /// <summary>
    /// The unique Id of the video.
    /// </summary>
    public readonly string id;

    /// <summary>
    /// The name of the video.
    /// </summary>
    public readonly string name;

    /// <summary>
    /// The source of the video (e.g., URL, file path).
    /// </summary>
    public readonly string source;

    /// <summary>
    /// The total length of the video in seconds.
    /// </summary>
    public readonly int length;

    /// <summary>
    /// Is the video VR or a regular format.
    /// </summary>
    public readonly bool isVr;

    /// <summary>
    /// Constructs a Video object with the specified name, source, playback state, length, and playback time.
    /// </summary>
    /// <param name="id">The unique Id of the video.</param>
    /// <param name="name">The name of the video.</param>
    /// <param name="source">The source of the video (e.g., URL, file path).</param>
    /// <param name="length">The total length of the video in seconds.</param>
    /// <param name="isVr">If the video is VR (true) format of regular (false).</param>
    public Video(string id, string name, string source, int length, bool isVr)
    {
        this.id = id;
        this.name = name;
        this.source = source;
        this.length = length;
        this.isVr = isVr;
    }
}
