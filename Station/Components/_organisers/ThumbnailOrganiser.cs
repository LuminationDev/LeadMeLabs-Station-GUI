using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Station.Components._commandLine;
using Station.Components._notification;
using Station.Components._utils;

namespace Station.Components._organisers;

public static class ThumbnailOrganiser
{
    /// <summary>
    /// Hold a unique set of application images to retrieve, the key is the experience name
    /// and the value is the Station that contains the image. This is to ensure that the
    /// missing thumbnail is only retrieved once.
    /// </summary>
    private static readonly HashSet<string> ImagesToRetrieve = new();

    /// <summary>
    /// Hold a reference to all image names that are located within the cache, this allows for 
    /// quick read access for checking purposes instead of reading the directory every time.
    /// </summary>
    private static readonly HashSet<string> LocalImages = new();

    /// <summary>
    /// Load the Cache file names so that continuous reads are not necessary.
    /// </summary>
    public static void LoadCache()
    {
        Logger.WriteLog("Loading thumbnail cache.", MockConsole.LogLevel.Error);

        if (CommandLine.StationLocation == null)
        {
            Logger.WriteLog("Station location not found: LoadCache", MockConsole.LogLevel.Error);
            return;
        }

        if (!Directory.Exists(@$"{CommandLine.StationLocation}\_cache"))
        {
            Directory.CreateDirectory(@$"{CommandLine.StationLocation}\_cache");
        }

        string[] filePaths = Directory.GetFiles(@$"{CommandLine.StationLocation}\_cache", "*.jpg", SearchOption.TopDirectoryOnly);

        foreach (string file in filePaths)
        {
            LocalImages.Add(Path.GetFileName(file));
        }
    }

    /// <summary>
    /// Based on a supplied name check the local images hashset to see if it exists locally and return the uri of the
    /// location in string form.
    /// </summary>
    /// <param name="name">A string of the application name</param>
    /// <returns>A string of the header uri or null</returns>
    public static string? GetEntry(string name)
    {
        string header = $"{name.Replace(":", "")}_header.jpg";
        
        if (CommandLine.StationLocation == null)
        {
            Logger.WriteLog("Station location not found: GetEntry", MockConsole.LogLevel.Error);
            return null;
        }
        
        return LocalImages.Contains(header) ? @$"{CommandLine.StationLocation}\_cache\{header}" : null;
    }

    /// <summary>
    /// Search the local cache folder for the thumbnail that is associated with the supplied
    /// experience name. The method is synchronised so that the files are not accessed at the
    /// same time.
    /// </summary>
    /// <param name="list">A string list of the experiences to search for.</param>
    /// <returns>A bool if the thumbnail exists locally</returns>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public static void CheckCache(string list)
    {
        Logger.WriteLog("Checking Local Cache for files.", MockConsole.LogLevel.Error);

        List<string> localExperiences = GetExperiencesOfType(list, "Custom");
        List<string> reviveExperiences = GetExperiencesOfType(list, "Revive");
        List<string> steamExperiences = GetExperiencesOfType(list, "Steam");

        LocalThumbnail(localExperiences);
        ReviveThumbnail(reviveExperiences);
        SteamThumbnail(steamExperiences);
    }

    /// <summary>
    /// Retrieves the experiences of a specific type from the given list.
    /// </summary>
    /// <param name="list">The list of experiences.</param>
    /// <param name="type">The type of experiences to retrieve.</param>
    /// <returns>The experiences of the specified type.</returns>
    private static List<string> GetExperiencesOfType(string list, string type)
    {
        return list.Split('/')
            .Where(experience => experience.StartsWith(type + "|"))
            .ToList();
    }

    /// <summary>
    /// Finds the missing thumbnails in the given list of experiences.
    /// </summary>
    /// <param name="experiences">The list of experiences to check for missing thumbnails.</param>
    /// <returns>A list of missing thumbnails.</returns>
    private static List<string> FindMissingThumbnails(List<string> experiences)
    {
        List<string> missingThumbnails = new();

        foreach (string experience in experiences)
        {
            string[] appTokens = experience.Split('|');

            if (appTokens.Length < 3)
            {
                continue;
            }

            if (ImagesToRetrieve.Contains(appTokens[2]))
            {
                Logger.WriteLog($"Thumbnail for {appTokens[2]} already awaiting transfer.", MockConsole.LogLevel.Normal);
                continue;
            }

            string fileName = $"{appTokens[2].Replace(":", "")}_header.jpg";

            if (!LocalImages.Contains(fileName))
            {
                ImagesToRetrieve.Add(appTokens[2]);
                missingThumbnails.Add(experience);
            }
        }

        return missingThumbnails;
    }


    /// <summary>
    /// Checks for missing thumbnails associated with Steam experiences and attempts to download them in batch.
    /// </summary>
    /// <param name="experiences">A list of experiences to check for missing thumbnails.</param>
    private static void SteamThumbnail(List<string> experiences)
    {
        List<string> missingThumbnails = FindMissingThumbnails(experiences);

        // There are no missing thumbnails
        if (missingThumbnails.Count == 0) return;

        // Attempt to download missing headers
        _ = DownloadImagesInBatch(missingThumbnails);
    }
    
    /// <summary>
    /// Checks for missing thumbnails in the provided list of experiences and sends requests for the missing
    /// thumbnails. These thumbnails exist on the Station themselves but in the Revive directory.
    /// </summary>
    /// <param name="experiences">The list of experiences to process.</param>
    private static void ReviveThumbnail(List<string> experiences)
    {
        List<string> missingThumbnails = FindMissingThumbnails(experiences);

        // There are no missing thumbnails
        if (missingThumbnails.Count == 0) return;
        
        //TODO copy the local thumbnails into the cache folder
        Console.WriteLine(missingThumbnails.ToString());
    }

    /// <summary>
    /// Checks for missing thumbnails in the provided list of experiences and sends requests for the missing
    /// thumbnails. These thumbnails exist on the Station themselves hence 'local' thumbnails.
    /// </summary>
    /// <param name="experiences">The list of experiences to process.</param>
    private static void LocalThumbnail(List<string> experiences)
    {
        List<string> missingThumbnails = FindMissingThumbnails(experiences);

        // There are no missing thumbnails
        if (missingThumbnails.Count == 0) return;
        
        //TODO copy the local thumbnails into the cache folder
        Console.WriteLine(missingThumbnails.ToString());
    }

    /// <summary>
    /// Downloads multiple images in a batch from the specified URLs and saves them to the specified directory.
    /// </summary>
    /// <param name="experiences">The list of experience that require headers to be download.</param>
    /// <param name="concurrent">Determines whether the images will be downloaded concurrently (default: false).</param>
    private static async Task DownloadImagesInBatch(List<string> experiences, bool concurrent = false)
    {
        using HttpClient client = new HttpClient();

        List<Task> downloadTasks = new List<Task>();

        foreach (string experience in experiences)
        {
            //[0]-type, [1]-ID, [2]-name
            string[] appTokens = experience.Split('|');
            downloadTasks.Add(DownloadAndSaveImageAsync(appTokens[1], appTokens[2].Replace("\"", ""), client));
        }

        if (concurrent)
        {
            await Task.WhenAll(downloadTasks);
        }
        else
        {
            foreach (Task downloadTask in downloadTasks)
            {
                await downloadTask;
            }
        }

        Logger.WriteLog("Batch download completed.", MockConsole.LogLevel.Debug);
    }

    /// <summary>
    /// Downloads a single image from the specified URL and saves it to the specified file path.
    /// </summary>
    /// <param name="appId">The ID of the application to be added to the download URL.</param>
    /// <param name="appName">The file name for what the image will be saved as.</param>
    /// <param name="client"></param>
    private static async Task DownloadAndSaveImageAsync(string appId, string appName, HttpClient client)
    {
        string imageUrl = @$"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/header.jpg";
        string saveDirectory = $@"{CommandLine.StationLocation}\_cache\{appName.Replace(":", "")}_header.jpg";

        try
        {
            // Download the image data
            byte[] imageData = await client.GetByteArrayAsync(imageUrl);

            if (imageData.Length == 0)
            {
                // Handle empty file scenario
                MockConsole.WriteLine("Empty file detected.", MockConsole.LogLevel.Error);
                throw new Exception("Empty file detected, aborting download.");
            }

            await using (FileStream fileStream = new FileStream(saveDirectory, FileMode.Create, FileAccess.Write))
            {
                await fileStream.WriteAsync(imageData);
            }

            Logger.WriteLog($"Image downloaded and saved: {appName}", MockConsole.LogLevel.Debug);
            LocalImages.Add($"{appName.Replace(":", "")}_header.jpg");
            ImagesToRetrieve.Remove(appName);
        }
        catch (Exception ex)
        {
            Logger.WriteLog($"An error occurred while downloading the image for {appName}: {ex.Message}", MockConsole.LogLevel.Error);
            ImagesToRetrieve.Remove(appName);
        }
    }
}
