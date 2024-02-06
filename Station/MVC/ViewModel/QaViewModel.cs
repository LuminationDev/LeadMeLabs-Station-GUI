using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using Station.Components._notification;
using Station.Core;
using Station.QA;
using System.Threading.Tasks;

namespace Station.MVC.ViewModel;

public class QaViewModel : ObservableObject
{
    public RelayCommand SortCommand { get; }
    public RelayCommand RefreshCommand { get; }
    
    public QaViewModel()
    {
        SortCommand = new RelayCommand(Sort);
        RefreshCommand = new RelayCommand(Refresh);
    }
    
    /// <summary>
    /// Sorts the items in the ListView based on the specified property name.
    /// </summary>
    /// <param name="parameter">The name of the property by which to sort the items.</param>
    private void Sort(object? parameter)
    {
        if (parameter is not string propertyName) return;
        
        ICollectionView view = CollectionViewSource.GetDefaultView(QaCheckCollectionView);

        // Toggle sorting direction
        ListSortDirection direction = ListSortDirection.Ascending;
        if (view.SortDescriptions.Count > 0 && view.SortDescriptions[0].PropertyName == propertyName)
        {
            direction = view.SortDescriptions[0].Direction == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        }

        // Apply sorting
        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(new SortDescription(propertyName, direction));
    }

    /// <summary>
    /// Redo the local quality assurance checks.
    /// </summary>
    private void Refresh(object? parameter)
    {
        new Task(() => QualityManager.HandleLocalQualityAssurance(false)).Start();
    }
    
    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            OnPropertyChanged();
        }
    }
    
    private ObservableCollection<QaCheck> _qaChecks = new();
    public ObservableCollection<QaCheck> QaChecks
    {
        get => _qaChecks;
        set
        {
            _qaChecks = value;
            QaCheckCollectionView.Refresh();
            OnPropertyChanged();
        }
    }
    
    private ICollectionView? _qaCheckCollectionView;
    public ICollectionView QaCheckCollectionView
    {
        get
        {
            _qaCheckCollectionView = CollectionViewSource.GetDefaultView(QaChecks);
            _qaCheckCollectionView.Filter = QaCheckFilter;
            return _qaCheckCollectionView;
        }
    }
    
    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            QaCheckCollectionView.Refresh();
            OnPropertyChanged();
        }
    }

    private bool QaCheckFilter(object item)
    {
        if (string.IsNullOrWhiteSpace(SearchText)) return true;

        if (item is QaCheck qaCheck)
        {
            // Customize the filtering logic here
            return qaCheck.Id.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
    
    /**
     * Add a new Qa Check object to the Observable collection.
     */
    public void AddQaCheck(QaCheck newCheck)
    {
        //Do not double up on qa checks on restart/recollection
        if (QaChecks.Any(e => e.Id == newCheck.Id)) return;
        
        //Needs to be added using the main UI thread to reflect in real-time
        Application.Current.Dispatcher.Invoke(() =>
        {
            QaChecks.Add(newCheck);
            QaCheckCollectionView.Refresh();
        });
    }
    
    /**
     * Update a field within the Station Observable collection.
     */
    public void UpdateQaCheck(string qaCheckId, string key, string value)
    {
        QaCheck? targetExperience = QaChecks.FirstOrDefault(experience => experience.Id == qaCheckId);
        if (targetExperience == null) return;
        
        switch (key)
        {
            // case "status":
            //     targetExperience.Status = value;
            //     if (value.Equals("Running"))
            //     {
            //         _runningExperience = experienceId;
            //     }
            //     break;
                
            default:
                MockConsole.WriteLine($"StationsViewModel - UpdateStation: Unknown key: {key}", MockConsole.LogLevel.Error);
                break;
        }
            
        Application.Current.Dispatcher.Invoke(() => { 
            // Find the index of the experience based on the id property
            var index = new List<QaCheck>(QaChecks).FindIndex(experience => experience.Id == qaCheckId);
            if (index == -1) return;
            
            // Replace the existing experience in the ObservableCollection
            QaChecks.RemoveAt(index);
            QaChecks.Insert(index, targetExperience);
            QaCheckCollectionView.Refresh();
        }, DispatcherPriority.DataBind);
    }
}
