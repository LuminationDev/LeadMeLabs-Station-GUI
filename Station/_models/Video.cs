namespace Station._models;

public enum VideoType
{
    Normal,
    Vr,
    Backdrop
}

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
    /// Does the video have a SRT file associated with it.
    /// </summary>
    public readonly bool hasSubtitles;

    /// <summary>
    /// The type of video (Normal, VR, Backdrop)
    /// </summary>
    public readonly VideoType videoType;
    
    /// <summary>
    /// Constructs a Video object with the specified name, source, playback state, length, and playback time.
    /// </summary>
    /// <param name="id">The unique Id of the video.</param>
    /// <param name="name">The name of the video.</param>
    /// <param name="source">The source of the video (e.g., URL, file path).</param>
    /// <param name="length">The total length of the video in seconds.</param>
    /// <param name="hasSubtitles">If the video has a subtitles file associated with it.</param>
    /// <param name="videoType">The type of video it is (Normal, VR, Backdrop)</param>
    public Video(string id, string name, string source, int length, bool hasSubtitles, VideoType videoType = VideoType.Normal)
    {
        this.id = id;
        this.name = name;
        this.source = source;
        this.length = length;
        this.hasSubtitles = hasSubtitles;
        this.videoType = videoType;
    }
}
