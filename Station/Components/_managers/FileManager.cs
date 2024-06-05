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
                
                StateController.UpdateListsValue("localFiles", jsonObject.ToString());
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

    /// <summary>
    /// A file action has been requested, determine what the action is and triage it accordingly.
    /// </summary>
    /// <param name="data">A stringify JObject</param>
    public static void HandleFileAction(string data)
    {
        JObject fileInformation = JObject.Parse(data);
        if (!fileInformation.ContainsKey("Action")) return;

        string? action = fileInformation.GetValue("Action")?.ToString();
        if (action == null) return;
        
        switch (action)
        {
            case "delete":
                DeleteFile(data);
                break;
            
            default:
                return;
        }
    }

    /// <summary>
    /// A delete action has been requested, get the file path and name from the supplied data. Only delete the file
    /// located at the supplied path if it exists and matches the supplied name as well.
    /// </summary>
    /// <param name="data">A stringify JObject</param>
    private static void DeleteFile(string data)
    {
        JObject fileInformation = JObject.Parse(data);
        if (!fileInformation.ContainsKey("fileName") || !fileInformation.ContainsKey("filePath")) return;

        string? fileNameWithoutExtension = fileInformation.GetValue("fileName")?.ToString();
        string? filePath = fileInformation.GetValue("filePath")?.ToString();
        
        //Delete the file at the supplied path
        try
        {
            // Check if the file exists
            if (File.Exists(filePath))
            {
                // Extract the file name without extension from the file path
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                
                // Check if the extracted file name matches the provided file name
                if (fileName.Equals(fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase))
                {
                    // Delete the file
                    File.Delete(filePath);
                    Logger.WriteLog("File deleted successfully.", Enums.LogLevel.Info);
                }
                else
                {
                    Logger.WriteLog($"File does not match the provided file name: supplied {fileNameWithoutExtension}, found: {fileName}", Enums.LogLevel.Error);
                }
            }
            else
            {
                Logger.WriteLog($"File does not exist. {filePath}", Enums.LogLevel.Error);
            }
        }
        catch (Exception e)
        {
            Logger.WriteLog($"An error occurred: {e.Message}", Enums.LogLevel.Error);
        }
        
        //Refresh the file list
        Initialise();
    }
}
