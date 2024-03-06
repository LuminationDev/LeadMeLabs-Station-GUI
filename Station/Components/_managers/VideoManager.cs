using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using MediaInfo;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sentry;
using Station.Components._models;
using Station.Components._notification;
using Station.MVC.Controller;
using InternalLogger = Station.Components._utils.Logger;

namespace Station.Components._managers;

/// <summary>
/// This class manages video information for playback control. It maintains a list of known videos stored locally
/// in specific folders, distinct for regular and VR videos to facilitate easy identification. It also tracks details
/// of the current video being played, including its name, source, length, playback state, and playback time.
/// </summary>
public static class VideoManager
{
    // List of the valid file types to try and load
    private static readonly List<string> ValidFileTypes = new() { ".mp4" };
    private static readonly object VideoFilesLock = new();

    private static readonly string BaseFolderPath = GetVideoFolder();
    private static readonly string VrFolderPath = Path.Join(BaseFolderPath, "VR");
    private static readonly string RegularFolderPath = Path.Join(BaseFolderPath, "Regular");

    // Hold the different video types
    private static readonly Dictionary<string, Video> VideoFiles = new();

    private static string GetVideoFolder()
    {
        string videosFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        return videosFolderPath;
    }

    #region Observers
    /**
     * The current settings on the active video player.
     */
    private static JObject? videoPlayerDetails;
    public static JObject? VideoPlayerDetails
    {
        private set
        {
            if (videoPlayerDetails == value) return;

            videoPlayerDetails = value;

            // Send a message to the tablet
            string additionalData = $"SetValue:videoPlayerDetails:{videoPlayerDetails}";
            MessageController.SendResponse("Android", "Station", additionalData);
        }
        get => videoPlayerDetails;
    }

    public static void UpdateVideoPlayerDetails(string details)
    {
        try
        {
            VideoPlayerDetails = JObject.Parse(details);
        }
        catch (Exception e)
        {
            InternalLogger.WriteLog($"VideoManager - UpdateVideoPlayerDetails: Unexpected value: {details}. ERROR {e}", MockConsole.LogLevel.Error);
        }
    }

    /**
     * The current playback time of the video.
     */
    private static int? playbackTime;
    public static int? PlaybackTime
    {
        private set
        {
            if (playbackTime == value) return;

            playbackTime = value;

            // Send a message to the tablet
            string additionalData = $"SetValue:activeVideoPlaybackTime:{playbackTime}";
            MessageController.SendResponse("Android", "Station", additionalData);
        }
        get => playbackTime;
    }

    public static void UpdatePlaybackTime(string timeString)
    {
        if (int.TryParse(timeString, out var time))
        {
            // Conversion successful, 'time' contains the integer value
            PlaybackTime = time;
        }
        else
        {
            MockConsole.WriteLine($"VideoManager - UpdatePlaybackTime: {timeString} is not of type int", MockConsole.LogLevel.Normal);
        }
    }

    /**
     * The active video, this takes in a video source, locates it in the VideoFiles dictionary to collect it's unique
     * Id which is then used as the activeVideo identifier. If the Id is a different value to what is already saved the
     * Station will send a message to the NUC.
     */
    private static string? activeVideo = "";
    public static string? ActiveVideo
    {
        private set
        {
            // Find the video in the VideoFiles dictionary
            Video? video = FindVideoBySource(value);
            string additionalData;

            // Reset the video on the tablet
            if (video == null || activeVideo == video.id)
            {
                activeVideo = "";
                additionalData = $"SetValue:activeVideoFile:";
                MessageController.SendResponse("NUC", "Station", additionalData);
                return;
            }

            activeVideo = video.id;

            // Send a message to the tablet
            additionalData = $"SetValue:activeVideoFile:{activeVideo}";
            MessageController.SendResponse("NUC", "Station", additionalData);
        }
        get => activeVideo;
    }

    public static void UpdateActiveVideo(string video)
    {
        ActiveVideo = video;
    }
    #endregion
    /// <summary>
    /// Collect and store the list of videos.
    /// </summary>
    public static void Initialise()
    {
        void Collect()
        {
            LoadLocalVideoFiles(VrFolderPath, true);
            LoadLocalVideoFiles(RegularFolderPath, false);

            Video[] videoArray = VideoFiles.Values.ToArray();
            string json = JsonConvert.SerializeObject(videoArray);
            JArray jsonObject = JArray.Parse(json);
            string additionalData = $"SetValue:videoFiles:{jsonObject}";
            MessageController.SendResponse("NUC", "Station", additionalData);
        }

        new Thread(Collect).Start();
    }

    /// <summary>
    /// Load any video files that are in the local Video folder, adding these to the details object
    /// before sending the details object to LeadMe Labs.
    /// </summary>
    private static void LoadLocalVideoFiles(string folderPath, bool isVr)
    {
        // Bail out early if folder does not exist
        if (!Directory.Exists(folderPath))
        {
            InternalLogger.WriteLog($"VideoManager - Folder does not exist {folderPath}", MockConsole.LogLevel.Normal);
        }

        string[] files = Directory.GetFiles(folderPath);

        foreach (string filePath in files)
        {
            string fileName = Path.GetFileName(filePath);
            string extension = Path.GetExtension(filePath);
            if (!ValidFileTypes.Contains(extension)) continue;

            // Calculate video duration
            int duration = GetVideoDuration(filePath);

            //TODO look into if we can create a thumbnails of the first frame?
            string id = GenerateUniqueId(fileName, filePath);

            // Lock the VideoFilesLock dictionary to avoid duplicate entry race conditions
            lock (VideoFilesLock)
            {
                if (VideoFiles.ContainsKey(id)) continue;

                try
                {
                    Video video = new Video(id, fileName, filePath, duration, isVr);
                    VideoFiles.Add(id ,video);
                }
                catch (Exception e)
                {
                    SentrySdk.CaptureException(e);
                }
            }
        }
    }

    /// <summary>
    /// Gets the duration of a video file using the MediaInfo library.
    /// </summary>
    /// <param name="filePath">The path to the video file.</param>
    /// <returns>The duration of the video in seconds, or 0 if unsuccessful.</returns>
    private static int GetVideoDuration(string filePath)
    {
        try
        {
            // Create a logger instance for logging purposes
            ILogger logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<MediaInfoWrapper>();

            // Use MediaInfoWrapper to obtain video duration
            var media = new MediaInfoWrapper(filePath, logger);
            return media.Success ? (media.Duration / 1000) : 0;
        }
        catch (Exception ex)
        {
            // Log the exception using Sentry for monitoring purposes
            SentrySdk.CaptureMessage($"Unable to calculate duration from ({filePath}), Error: {ex}");
        }

        return 0;
    }

    /// <summary>
    /// Generates a unique identifier (ID) based on the combination of a file name and its corresponding file path.
    /// The unique ID is created by concatenating the file name and file path, computing the MD5 hash of the combined string,
    /// and converting the resulting hash to a hexadecimal string.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="filePath">The path of the file.</param>
    /// <returns>A unique identifier (ID) representing the file, generated based on its name and path.</returns>
    private static string GenerateUniqueId(string fileName, string filePath)
    {
        // Concatenate file name and file path
        string combinedString = fileName + filePath;

        // Compute hash
        using MD5 md5Hash = MD5.Create();

        // Convert the input string to a byte array and compute the hash.
        byte[] data = md5Hash.ComputeHash(Encoding.Unicode.GetBytes(combinedString));

        // Create a new StringBuilder to collect the bytes
        // and create a string.
        StringBuilder stringBuilder = new StringBuilder();

        // Loop through each byte of the hashed data 
        // and format each one as a hexadecimal string.
        foreach (var t in data)
        {
            stringBuilder.Append(t.ToString("x2"));
        }

        // Return the hexadecimal string.
        return stringBuilder.ToString();
    }

    /// <summary>
    /// Finds a video in the VideoFiles dictionary by its source.
    /// </summary>
    /// <param name="source">The source of the video to find.</param>
    /// <returns>The video with the specified source if found; otherwise, returns null.</returns>
    private static Video? FindVideoBySource(string? source)
    {
        if (source == null) return null;

        foreach (var kvp in VideoFiles)
        {
            string filePath = source;
            // Check if the string starts with "file://"
            if (filePath.StartsWith("file://"))
            {
                // Remove "file://" prefix
                filePath = filePath.Substring(7); // Remove the first 7 characters
            }

            if (kvp.Value.source == filePath)
            {
                return kvp.Value;
            }
        }
        return null; // Return null if no matching video is found
    }

    /// <summary>
    /// A video player has closed, reset all the information for the next time it is used.
    /// </summary>
    public static void ClearVideoInformation()
    {
        PlaybackTime = null;
        ActiveVideo = null;
        VideoPlayerDetails = new JObject();
    }
}
