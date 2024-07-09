using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using LeadMeLabsLibrary;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Station.Components._commandLine;
using Station.Components._models;
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
        Logger.WriteLog("Loading thumbnail cache.", Enums.LogLevel.Error);

        if (StationCommandLine.StationLocation == null)
        {
            Logger.WriteLog("Station location not found: LoadCache", Enums.LogLevel.Error);
            return;
        }

        if (!Directory.Exists(@$"{StationCommandLine.StationLocation}\_cache"))
        {
            Directory.CreateDirectory(@$"{StationCommandLine.StationLocation}\_cache");
        }

        string[] filePaths = Directory.GetFiles(@$"{StationCommandLine.StationLocation}\_cache", "*.jpg", SearchOption.TopDirectoryOnly);

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
        
        if (StationCommandLine.StationLocation == null)
        {
            Logger.WriteLog("Station location not found: GetEntry", Enums.LogLevel.Error);
            return null;
        }
        
        return LocalImages.Contains(header) ? @$"{StationCommandLine.StationLocation}\_cache\{header}" : null;
    }

    /// <summary>
    /// Checks the local cache for files of a specified type and initiates thumbnail retrieval processes.
    /// </summary>
    /// <typeparam name="T">The type of experiences in the cache.</typeparam>
    /// <param name="list">The string representation of the list of experiences.</param>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public static void CheckCache<T>(string? list)
    {
        if (list == null) return;
        
        Logger.WriteLog($"Checking Local Cache for files - {typeof(T).Name} method.", Enums.LogLevel.Normal);

        List<T>? objects;
        if (typeof(T) == typeof(string))
        {
            objects = list.Split('/').Cast<T>().ToList();
        }
        else if (typeof(T) == typeof(ExperienceDetails))
        {
            objects = JsonConvert.DeserializeObject<List<ExperienceDetails>>(list)?.Cast<T>().ToList();
        }
        else if (typeof(T) == typeof(Video))
        {
            objects = JsonConvert.DeserializeObject<List<Video>>(list)?.Cast<T>().ToList();
        }
        else
        {
            Logger.WriteLog($"Thumbnail conversion for type: {typeof(T)} is unsupported.", Enums.LogLevel.Normal);
            return;
        }

        if (objects == null) return;
        
        List<T> localExperiences = GetExperiencesOfType(objects, "Custom");
        localExperiences.AddRange(GetExperiencesOfType(objects, "Revive"));
        List<T> embeddedExperiences = GetExperiencesOfType(objects, "Embedded");
        List<T> steamExperiences = GetExperiencesOfType(objects, "Steam");

        LocalThumbnail(localExperiences);
        EmbeddedThumbnail(embeddedExperiences);
        SteamThumbnail(steamExperiences);
        
        // upload any missing images to cloud
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(10);
        httpClient.DefaultRequestHeaders.Add("site", Environment.GetEnvironmentVariable("LabLocation", EnvironmentVariableTarget.Process) ?? "Unknown");
        httpClient.DefaultRequestHeaders.Add("device", "Station" + Environment.GetEnvironmentVariable("StationId", EnvironmentVariableTarget.Process) ?? "0");
        JArray names = new JArray(LocalImages);
        JObject body = new JObject();
        body.Add("names", names);
        StringContent objData = new StringContent(body.ToString(), Encoding.UTF8, "application/json");
        var response = httpClient.PostAsync("https://us-central1-leadme-labs.cloudfunctions.net/checkForCachedImages", objData).GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            return;
        }

        var missingImages = JArray.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        foreach (JToken missingImage in missingImages)
        {
            var name = missingImage.ToString();
            byte[] imageData = File.ReadAllBytes(@$"{StationCommandLine.StationLocation}\_cache\{name}");
            var byteArrayContent = new ByteArrayContent(imageData);

            byteArrayContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");

            httpClient.DefaultRequestHeaders.Remove("filename");
            httpClient.DefaultRequestHeaders.Add("filename", name);

            HttpResponseMessage imageResponse = httpClient.PostAsync("https://us-central1-leadme-labs.cloudfunctions.net/uploadApplicationImage", byteArrayContent).GetAwaiter().GetResult();
            
            if (!imageResponse.IsSuccessStatusCode)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Retrieves the experiences of a specific type from the given list.
    /// </summary>
    /// <typeparam name="T">The type of experiences in the list.</typeparam>
    /// <param name="list">The list of experiences.</param>
    /// <param name="type">The type of experiences to retrieve.</param>
    /// <returns>The experiences of the specified type.</returns>
    private static List<T> GetExperiencesOfType<T>(List<T> list, string type)
    {
        if (typeof(T) == typeof(string))
        {
            return list
                .Where(experience => experience != null && ((string)(object)experience).StartsWith(type + "|"))
                .ToList();
        }

        if (typeof(T) == typeof(ExperienceDetails))
        {
            return list
                .Where(experience => experience != null && ((ExperienceDetails)(object)experience).WrapperType.Equals(type))
                .ToList();
        }

        Logger.WriteLog($"Thumbnail for type: {type} is unsupported.", Enums.LogLevel.Normal);
        return new List<T>();
    }

    /// <summary>
    /// Finds missing thumbnails for a list of experiences and adds them to the list of images to retrieve.
    /// </summary>
    /// <typeparam name="T">The type of experiences in the list.</typeparam>
    /// <param name="experiences">The list of experiences to check for missing thumbnails.</param>
    /// <returns>A list of missing thumbnails.</returns>
    private static List<string> FindMissingThumbnails<T>(List<T> experiences)
    {
        List<string> missingThumbnails = new();

        foreach (var experience in experiences)
        {
            if (experience == null) continue;

            string? id = GetIdFromExperience(experience);

            if (id == null) continue;

            if (ImagesToRetrieve.Contains(id))
            {
                Logger.WriteLog($"Thumbnail for {id} already awaiting transfer.", Enums.LogLevel.Normal);
                continue;
            }

            string fileName = $"{id.Replace(":", "")}_header.jpg";

            if (LocalImages.Contains(fileName)) continue;

            ImagesToRetrieve.Add(id);

            if (typeof(T) == typeof(string))
            {
                missingThumbnails.Add((string)(object)experience);
            }
            else if (typeof(T) == typeof(ExperienceDetails))
            {
                ExperienceDetails temp = (ExperienceDetails)(object)experience;
                missingThumbnails.Add($"{temp.WrapperType}|{temp.Id}|{temp.Name}");
            }
        }

        return missingThumbnails;
    }
    
    /// <summary>
    /// Extracts the ID from an experience object of type T.
    /// </summary>
    /// <typeparam name="T">The type of the experience object.</typeparam>
    /// <param name="experience">The experience object.</param>
    /// <returns>The ID of the experience.</returns>
    private static string? GetIdFromExperience<T>(T experience)
    {
        if (typeof(T) == typeof(string))
        {
            if (experience == null) return null;

            string[] appTokens = ((string)(object)experience).Split('|');
            if (appTokens.Length >= 3) return appTokens[2];
        }
        else if (typeof(T) == typeof(ExperienceDetails))
        {
            if (experience != null) return ((ExperienceDetails)(object)experience).Id;
        }
        else
        {
            Logger.WriteLog($"Thumbnail conversion for type: {typeof(T)} is unsupported.", Enums.LogLevel.Normal);
        }

        return null;
    }
    
    /// <summary>
    /// Checks for missing thumbnails for a list of experiences and attempts to download them in batch.
    /// </summary>
    /// <typeparam name="T">The type of experiences in the list.</typeparam>
    /// <param name="experiences">The list of experiences to check for missing thumbnails.</param>
    private static void SteamThumbnail<T>(List<T> experiences)
    {
        List<string> missingThumbnails = FindMissingThumbnails(experiences);

        // There are no missing thumbnails
        if (missingThumbnails.Count == 0) return;

        // Attempt to download missing headers
        _ = DownloadImagesInBatch(missingThumbnails);
    }

    /// <summary>
    /// Checks for missing thumbnails in the provided list of experiences and sends requests for the missing
    /// thumbnails. These thumbnails exist on the Station themselves hence 'local' thumbnails.
    /// </summary>
    /// <param name="experiences">The list of experiences to process.</param>
    private static void LocalThumbnail<T>(List<T> experiences)
    {
        List<string> missingThumbnails = FindMissingThumbnails(experiences);

        // There are no missing thumbnails
        if (missingThumbnails.Count == 0) return;
        
        //TODO copy the local thumbnails into the cache folder
        foreach (var variable in missingThumbnails)
        {
            Console.WriteLine(variable);
        }
    }
    
    /// <summary>
    /// Checks for missing thumbnails in the provided list of experiences and sends requests for the missing
    /// thumbnails. These thumbnails exist on the Station within the leadme_apps hence 'embedded' thumbnails.
    /// </summary>
    /// <param name="experiences">The list of experiences to process.</param>
    private static void EmbeddedThumbnail<T>(List<T> experiences)
    {
        List<string> missingThumbnails = FindMissingThumbnails(experiences);

        // There are no missing thumbnails
        if (missingThumbnails.Count == 0) return;
        
        //TODO copy the local thumbnails into the cache folder
        foreach (var variable in missingThumbnails)
        {
            Console.WriteLine(variable);
        }
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

        Logger.WriteLog("Batch download completed.", Enums.LogLevel.Debug);
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
        string saveDirectory = $@"{StationCommandLine.StationLocation}\_cache\{appName.Replace(":", "")}_header.jpg";

        try
        {
            // Download the image data
            byte[] imageData = await client.GetByteArrayAsync(imageUrl);

            if (imageData.Length == 0)
            {
                // Handle empty file scenario
                MockConsole.WriteLine("Empty file detected.", Enums.LogLevel.Error);
                throw new Exception("Empty file detected, aborting download.");
            }

            await using (FileStream fileStream = new FileStream(saveDirectory, FileMode.Create, FileAccess.Write))
            {
                await fileStream.WriteAsync(imageData);
            }

            Logger.WriteLog($"Image downloaded and saved: {appName}", Enums.LogLevel.Debug);
            LocalImages.Add($"{appName.Replace(":", "")}_header.jpg");
            ImagesToRetrieve.Remove(appName);
        }
        catch (Exception ex)
        {
            Logger.WriteLog($"An error occurred while downloading the image for {appName}: {ex.Message}", Enums.LogLevel.Error);
            ImagesToRetrieve.Remove(appName);
        }
    }
}
