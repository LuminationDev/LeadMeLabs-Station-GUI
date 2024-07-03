using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using Station.Components._commandLine;
using Station.Core;
using Station.Extensions;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace Station.MVC.ViewModel;

public class LogsViewModel : ObservableObject
{
    private readonly List<string> _initialMarkers = new() { "E", "I", "N", "D", "V", "U" };
    private string? _currentFilePath;
    
    public RelayCommand LoadMostRecentCommand { get; }
    public RelayCommand ReloadCurrentFileCommand { get; }
    public RelayCommand LoadFileCommand { get; }
    public RelayCommand CheckBoxCommand { get; }
    public RelayCommand ResetFiltersCommand { get; }
    
    public LogsViewModel()
    {
        _checkedMarkers = new ObservableCollection<string>(_initialMarkers); 
        _checkedMarkers.CollectionChanged += CheckedMarkers_CollectionChanged;
        
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
            FilterFileText();
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
    /// Find and load the most recent log file.
    /// </summary>
    private void LoadLatestLogFile()
    {
        LoadFile(GetLatestLogFile());
    }
    
    /// <summary>
    /// Retrieves the path of the latest log file within the log directory specified by the NucLocation property from the CommandLine.
    /// If NucLocation is null or no log files are found, returns null.
    /// If today's log file exists, returns its path.
    /// If today's log file doesn't exist, finds the most recent log file before today and returns its path.
    /// </summary>
    /// <returns>The path of the latest log file, or null if no log files are found.</returns>
    private string? GetLatestLogFile()
    {
        if (StationCommandLine.StationLocation == null) return null;
        string logDirectory = Path.GetFullPath(Path.Combine(StationCommandLine.StationLocation, "_logs"));
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
    /// Opens a file dialog to allow the user to select a text file.
    /// If a file is selected, its contents are loaded into the FileText property using the LoadFile method.
    /// The initial directory for the file dialog is set to the log directory specified by the NucLocation property from the CommandLine.
    /// If NucLocation is null, or if the user cancels the file dialog, no action is taken.
    /// </summary>
    private void LoadFileContentsAsync()
    {
        if (StationCommandLine.StationLocation == null) return;
        string logDirectory = Path.GetFullPath(Path.Combine(StationCommandLine.StationLocation, "_logs"));
        
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

    /// <summary>
    /// Asynchronously loads the content of a file specified by the file path into the FileText property.
    /// If the file path is null, the method returns without performing any action.
    /// </summary>
    /// <param name="filePath">The path to the file to be loaded. If null, no action is taken.</param>
    private async void LoadFile(string? filePath)
    {
        if (filePath == null) return;
        
        _currentFilePath = filePath;
        using StreamReader reader = new StreamReader(_currentFilePath);
        FileText = await reader.ReadToEndAsync();
    }

    /// <summary>
    /// Splits the text from FileText into segments based on markers.
    /// A marker is defined as a line that starts with '[' and ends with ']' at the second character.
    /// Each segment is separated by these markers and added to an ObservableCollection.
    /// </summary>
    /// <returns>An <see cref="ObservableCollection{T}"/> containing the segmented lines from FileText.</returns>
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
    private ObservableCollection<string> _checkedMarkers;
    public ObservableCollection<string> CheckedMarkers
    {
        get => _checkedMarkers;
        set
        {
            if (_checkedMarkers == value) return;
            _checkedMarkers = value;
            OnPropertyChanged();
        }
    }
    
    private void CheckedMarkers_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CheckedMarkers));
    }
    
    /// <summary>
    /// Resets the CheckedMarkers collection to its initial values and applies the filter.
    /// This method clears the current CheckedMarkers collection and repopulates it with the initial markers.
    /// After resetting the collection, the FilterFileText method is called to apply the filter.
    /// </summary>
    private void ResetFilters()
    {
        CheckedMarkers.Reset(_initialMarkers);
        FilterFileText();
    }
    
    /// <summary>
    /// Handles the event when a checkbox is checked by calling the ToggleMarker method.
    /// The checkbox content, if it is a string, is passed to the ToggleMarker method to toggle its presence in the CheckedMarkers collection.
    /// </summary>
    /// <param name="parameter">The content of the checkbox, expected to be of type <see cref="string"/>.</param>
    private void OnCheckBoxChecked(object parameter)
    {
        if (parameter is not string checkBoxContent) return;
        ToggleMarker(checkBoxContent);
    }
    
    /// <summary>
    /// Toggles the presence of a specified marker in the CheckedMarkers collection.
    /// If the marker is present, it is removed; otherwise, it is added.
    /// After updating the collection, the FilterFileText method is called to apply the filter.
    /// </summary>
    /// <param name="parameter">The marker to be toggled, expected to be of type <see cref="string"/>.</param>
    private void ToggleMarker(object parameter)
    {
        if (parameter is not string marker) return;
        
        if (CheckedMarkers.Contains(marker))
        {
            CheckedMarkers.Remove(marker);
        }
        else
        {
            CheckedMarkers.Add(marker);
        }

        FilterFileText();
    }
    #endregion
}
