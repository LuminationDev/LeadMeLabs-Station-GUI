using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using Station.Components._commandLine;
using Station.Core;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace Station.MVC.ViewModel;

public class LogsViewModel : ObservableObject
{
    private string? _currentFilePath;
    
    public RelayCommand LoadMostRecentCommand { get; }
    public RelayCommand ReloadCurrentFileCommand { get; }
    public RelayCommand LoadFileCommand { get; }
    public RelayCommand CheckBoxCommand { get; }
    public RelayCommand ResetFiltersCommand { get; }
    
    public LogsViewModel()
    {
        LoadMostRecentCommand = new RelayCommand(_ => LoadLatestLogFile());
        ReloadCurrentFileCommand = new RelayCommand(_ => LoadFile(_currentFilePath));
        LoadFileCommand = new RelayCommand(_ => LoadFileContentsAsync());
        CheckBoxCommand = new RelayCommand(OnCheckBoxChecked);
        ResetFiltersCommand = new RelayCommand(_ => ResetFilters());
        
        //Show the no file selected text as default
        FilterFileText();
    }
    
    #region Searching
    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            FilterFileText();
            OnPropertyChanged();
        }
    }
    
    /// <summary>
    /// 
    /// </summary>
    private void FilterFileText()
    {
        if (string.IsNullOrEmpty(SearchText) && _checkedMarkers.Count == MaxMarkers)// No filters
        {
            FilteredLines = new ObservableCollection<string>(SplitLinesByMarkers()); 
        }
        else if (string.IsNullOrEmpty(SearchText) && _checkedMarkers.Count < MaxMarkers)// Marker filters
        {
            FilteredLines = new ObservableCollection<string>(
                SplitLinesByMarkers().Where(line =>
                {
                    return _checkedMarkers.Any(marker => line.Contains($"[{marker}]"));
                }));
        }
        else // String and/or Marker filters
        {
            FilteredLines = new ObservableCollection<string>(
                SplitLinesByMarkers().Where(line =>
                {
                    bool containsSearchText = line.ToLower().Contains(SearchText.ToLower());
                    bool containsMarker = _checkedMarkers.Any(marker => line.Contains($"[{marker}]"));

                    return containsSearchText && containsMarker;
                }));
        }
    }
    #endregion
    
    //TODO does not filter on load
    #region File Loading
    private string _fileText = "No file selected...";
    private string FileText
    {
        get => _fileText;
        set
        {
            _fileText = value;
            OnPropertyChanged();
            FilteredLines = SplitLinesByMarkers(); // Update filtered lines whenever FileText changes
        }
    }
    
    private ObservableCollection<string>? _filteredLines;
    public ObservableCollection<string>? FilteredLines
    {
        get => _filteredLines;
        set
        {
            _filteredLines = value;
            OnPropertyChanged();
        }
    }
    
    /// <summary>
    /// 
    /// </summary>
    private void LoadLatestLogFile()
    {
        LoadFile(GetLatestLogFile());
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    private string? GetLatestLogFile()
    {
        if (CommandLine.StationLocation == null) return null;
        string logDirectory = Path.GetFullPath(Path.Combine(CommandLine.StationLocation, "_logs"));
        string todayFileNamePattern = $"{DateTime.Today:yyyy_MM_dd}_log";

        // Check if today's log file exists
        string todayLogFilePath = Path.Combine(logDirectory, todayFileNamePattern + ".txt");
        if (File.Exists(todayLogFilePath))
        {
            // Today's log file exists, return its path
            return todayLogFilePath;
        }

        // Today's log file doesn't exist, find the most recent log file
        var logFiles = Directory.GetFiles(logDirectory, "*.txt")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(fileName => fileName != null && fileName.StartsWith(DateTime.Today.Year.ToString()))
            .OrderByDescending(fileName => fileName)
            .ToList();

        foreach (var logFile in logFiles)
        {
            if (!DateTime.TryParseExact(logFile, "yyyy_MM_dd_log", null, System.Globalization.DateTimeStyles.None,
                    out DateTime logDate)) continue;
            
            // Check if the log date is before today
            if (logDate < DateTime.Today)
            {
                return Path.Combine(logDirectory, logFile + ".txt");
            }
        }

        // No log file found
        return null;
    }

    /// <summary>
    /// 
    /// </summary>
    private void LoadFileContentsAsync()
    {
        if (CommandLine.StationLocation == null) return;
        string logDirectory = Path.GetFullPath(Path.Combine(CommandLine.StationLocation, "_logs"));
        
        OpenFileDialog openFileDialog = new OpenFileDialog
        {
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            InitialDirectory = logDirectory
        };

        if (openFileDialog.ShowDialog() == true)
        {
            LoadFile(openFileDialog.FileName);
        }
    }

    private async void LoadFile(string? filePath)
    {
        if (filePath == null) return;
        
        _currentFilePath = filePath;
        using StreamReader reader = new StreamReader(_currentFilePath);
        FileText = await reader.ReadToEndAsync();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    private ObservableCollection<string> SplitLinesByMarkers()
    {
        ObservableCollection<string> segments = new ObservableCollection<string>();
        StringBuilder currentSegment = new StringBuilder();

        foreach (string line in FileText.Split(new[] { Environment.NewLine }, StringSplitOptions.None))
        {
            if (line.Length >= 3 && line[0] == '[' && line[2] == ']')
            {
                if (currentSegment.Length > 0)
                {
                    segments.Add(currentSegment.ToString().Trim());
                    currentSegment.Clear();
                }
            }
            currentSegment.AppendLine(line);
        }

        if (currentSegment.Length > 0)
        {
            segments.Add(currentSegment.ToString().Trim());
        }

        return segments;
    }
    #endregion
    
    #region Checkbox Controls
    private const int MaxMarkers = 6;
    private HashSet<string> _checkedMarkers = new() {"E", "I", "N", "D", "V", "U"};
    public HashSet<string> CheckedMarkers
    {
        get => _checkedMarkers;
        set
        {
            Console.WriteLine("CHANGING");
            if (_checkedMarkers == value) return;
            Console.WriteLine("CHANGED");
            _checkedMarkers = value;
            OnPropertyChanged();
        }
    }
    
    //TODO unable to reset?
    /// <summary>
    /// 
    /// </summary>
    private void ResetFilters()
    {
        Console.WriteLine("RESET");
        //CheckedMarkers = new() {"E", "I", "N", "D", "V", "U"};
        
        ToggleMarker("E");
        
        FilterFileText();
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="parameter"></param>
    private void OnCheckBoxChecked(object parameter)
    {
        if (parameter is not string checkBoxContent) return;
        ToggleMarker(checkBoxContent);
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="parameter"></param>
    private void ToggleMarker(object parameter)
    {
        if (parameter is not string marker) return;

        //Temporary shallow copy
        HashSet<string> temp = new HashSet<string>(CheckedMarkers);
        
        if (temp.Contains(marker))
        {
            temp.Remove(marker);
        }
        else
        {
            temp.Add(marker);
        }
        
        CheckedMarkers = temp;

        FilterFileText();
    }
    #endregion
}
