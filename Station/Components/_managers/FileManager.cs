using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using LeadMeLabsLibrary;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sentry;
using Station.Components._models;
using Station.Components._utils;
using Station.Converters;
using Station.MVC.Controller;

namespace Station.Components._managers;

/// <summary>
/// A class for managing the different saved files of applications on a Station.
/// </summary>
public static class FileManager
{
    private static readonly object LocalFilesLock = new();
    private static readonly Dictionary<string, List<LocalFile>> LocalFiles = new();
    
    private static readonly string BaseFolderPath = GetDocumentsFolder();
    private static readonly string OpenBrushFolderPath = Path.Join(BaseFolderPath, "Open Brush", "Sketches");
    private static string GetDocumentsFolder()
    {
        string videosFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return videosFolderPath;
    }

    /// <summary>
    /// 
    /// </summary>
    public static void Initialise()
    {
        void Collect()
        {
            LoadLocalFiles(OpenBrushFolderPath, FileType.OpenBrush);
            
            // Use the custom settings to convert any enums to strings
            var settings = new JsonSerializerSettings
            {
                Converters = { new CustomEnumConverter() }
            };
            
            lock (LocalFilesLock)
            {
                string json = JsonConvert.SerializeObject(LocalFiles, settings);
                JObject jsonObject = JObject.Parse(json);
                string additionalData = $"SetValue:localFiles:{jsonObject}";
                MessageController.SendResponse("NUC", "Station", additionalData);
            }
        }
        
        new Thread(Collect).Start();
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="folderPath">A string of the absolute folder path of where to look for files</param>
    /// <param name="fileType"></param>
    private static void LoadLocalFiles(string folderPath, FileType fileType)
    {
        // Bail out early if folder does not exist
        if (!Directory.Exists(folderPath))
        {
            Logger.WriteLog($"FileManager - Folder does not exist {folderPath}", Enums.LogLevel.Normal);
            return;
        }
        
        // Collect any videos at root level
        CollectFiles(folderPath, fileType);
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="folderPath"></param>
    /// <param name="fileType"></param>
    private static void CollectFiles(string folderPath, FileType fileType)
    {
        string[] files = Directory.GetFiles(folderPath);
        foreach (string filePath in files)
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string extension = Path.GetExtension(filePath);

            string? validExtension = fileType.GetFileExtension();
            if (validExtension != null && !validExtension.Equals(extension)) continue;
            
            // Lock the VideoFilesLock dictionary to avoid duplicate entry race conditions
            lock (LocalFilesLock)
            {
                try
                {
                    string fileCategory = fileType.ToString();
                    LocalFile localFile = new(fileType, fileName, filePath);
                    
                    if (LocalFiles.TryGetValue(fileCategory, out List<LocalFile>? fileList))
                    {
                        // Key exists, add to the existing list
                        fileList.Add(localFile);
                    }
                    else
                    {
                        // Key does not exist, create a new list and add it to the dictionary
                        fileList = new List<LocalFile> { localFile };
                        LocalFiles[fileCategory] = fileList;
                    }
                }
                catch (Exception e)
                {
                    Logger.WriteLog($"LoadLocalVideoFiles - Sentry Exception: {e}", Enums.LogLevel.Error);
                    SentrySdk.CaptureException(e);
                }
            }
        }
    }
}
