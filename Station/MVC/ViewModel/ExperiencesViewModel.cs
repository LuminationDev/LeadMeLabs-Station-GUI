using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using LeadMeLabsLibrary;
using Station.Components._models;
using Station.Components._notification;
using Station.Components._utils;
using Station.Core;
using Station.MVC.Controller;

namespace Station.MVC.ViewModel;

//TODO finish this and make the view page look better/get the image for the tile
public class ExperiencesViewModel : ObservableObject
{
    private string _runningExperience = "";
    
    public RelayCommand ExperienceClickCommand { get; }
    
    public ExperiencesViewModel()
    {
        ExperienceClickCommand = new RelayCommand(ExperienceClick);
    }
    
    private ObservableCollection<Experience> _experiences = new();
    public ObservableCollection<Experience> Experiences
    {
        get => _experiences;
        set
        {
            _experiences = value;
            ExperienceCollectionView.Refresh();
            OnPropertyChanged();
        }
    }
    
    private ICollectionView? _experienceCollectionView;
    public ICollectionView ExperienceCollectionView
    {
        get
        {
            _experienceCollectionView = CollectionViewSource.GetDefaultView(Experiences);
            _experienceCollectionView.Filter = ExperienceFilter;
            return _experienceCollectionView;
        }
    }
    
    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            ExperienceCollectionView.Refresh();
            OnPropertyChanged();
        }
    }

    private bool ExperienceFilter(object item)
    {
        if (string.IsNullOrWhiteSpace(SearchText)) return true;

        if (item is Experience experience)
        {
            // Customize your filtering logic here
            return experience.Name != null && experience.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
    
    /**
     * Add a new Experience object to the Observable collection.
     */
    public void AddExperience(Experience newExperience)
    {
        //Do not double up on experiences on restart/recollection
        if (Experiences.Any(e => e.ID == newExperience.ID)) return;
        
        //Needs to be added using the main UI thread to reflect in real-time
        Application.Current.Dispatcher.Invoke(() =>
        {
            Experiences.Add(newExperience);
            ExperienceCollectionView.Refresh();
        });
    }
    
    /**
     * Update a field within the Station Observable collection.
     */
    public void UpdateExperience(string experienceId, string key, string value)
    {
        Experience targetExperience = Experiences.FirstOrDefault(experience => experience.ID == experienceId);
        switch (key)
        {
            case "status":
                targetExperience.Status = value;
                if (value.Equals("Running"))
                {
                    _runningExperience = experienceId;
                }
                break;
                
            default:
                MockConsole.WriteLine($"StationsViewModel - UpdateStation: Unknown key: {key}", Enums.LogLevel.Error);
                break;
        }
            
        Application.Current.Dispatcher.Invoke(() => { 
            // Find the index of the experience based on the id property
            var index = new List<Experience>(Experiences).FindIndex(experience => experience.ID == experienceId);
            if (index == -1) return;
            
            // Replace the existing experience in the ObservableCollection
            Experiences.RemoveAt(index);
            Experiences.Insert(index, targetExperience);
            ExperienceCollectionView.Refresh();
        }, DispatcherPriority.DataBind);
    }

    /**
     * An experience has been stopped, run through the list of experiences and change the running application to stopped.
     */
    public void ExperienceStopped()
    {
        var targetExperience = Experiences.FirstOrDefault(experience => experience.ID == _runningExperience);
        targetExperience.Status = "Stopped";

        Application.Current.Dispatcher.Invoke(() => { 
            // Find the index of the experience based on the id property
            var index = new List<Experience>(Experiences).FindIndex(experience => experience.ID == _runningExperience);
            if (index == -1) return;
            
            // Replace the existing experience in the ObservableCollection
            Experiences.RemoveAt(index);
            Experiences.Insert(index, targetExperience);
            ExperienceCollectionView.Refresh();
        }, DispatcherPriority.DataBind);
    }
    
    /// <summary>
    /// An experience tile has been selected, scrap the id and status before passing it on to be managed.
    /// </summary>
    /// <param name="parameters">An object containing the bound parameters of the experience card</param>
    private void ExperienceClick(object parameters)
    {
        if (parameters is not Tuple<string, string> tuple) return;
        
        // Access the parameters
        string experienceId = tuple.Item1;
        string status = tuple.Item2;
        Helper.FireAndForget(Task.Run(() => ManageExperience(status, experienceId)));
    }

    /// <summary>
    /// Based on the status stop or start an experience.
    /// </summary>
    /// <param name="status">The current status of the experience</param>
    /// <param name="experienceId">The Id of an individual experience</param>
    private void ManageExperience(string status, string experienceId)
    {
        //NOTE: The following will be stopped by the vive check etc..
        switch (status)
        {
            case "Running":
                UpdateExperience(_runningExperience, "status", "Closing");
                MainController.wrapperManager?.ActionHandler("Stop");
                break;
            
            case "Stopped":
                if (_runningExperience != "")
                {
                    UpdateExperience(_runningExperience, "status", "Stopped");
                    _runningExperience = "";
                }
                MainController.wrapperManager?.ActionHandler("Stop"); //Stop any running experience first
                MainController.wrapperManager?.ActionHandler("Start", experienceId);
                break;
            
            default:
                Console.WriteLine($"Experience already doing something");
                break;
        }
    }
}
