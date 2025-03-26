using System.Windows;
using System.Windows.Controls;
using LeadMeLabsLibrary;
using Station.Components._notification;
using Station.MVC.ViewModel;

namespace Station.MVC.View;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    //Reference to the main logging console
    public static TextBox? console;

    //Control the different log levels
    public static Button? logLevel;
    
    //Image for Third party connection
    public static Image? headsetVrConnection;
    
    //Image for OpenVR connection
    public static Image? openVrConnection;

    //Labels for the current process
    public static Label? processConsole;
    public static Label? statusConsole;
    
    //Labels for OpenVR device connections
    public static Image? headsetConnection;
    public static Label? headsetDescription;

    public static Image? leftControllerConnection;
    public static Label? leftControllerBattery;
    
    public static Image? rightControllerConnection;
    public static Label? rightControllerBattery;
    
    public static Label? baseStationActive;
    public static Label? baseStationAmount;
    
    public static Label? trackerActive;
    public static Label? trackerAmount;
    public static WrapPanel? trackerSection;

    public MainWindow()
    {
        InitializeComponent();

        var viewModel = new MainWindowViewModel();
        this.DataContext = viewModel;
        MockConsole.SetViewModel(viewModel);

        console = this.ConsoleWindow;
        logLevel = this.LoggingLevel;

        headsetVrConnection = this.HeadsetVrConnection;
        openVrConnection = this.OpenVrConnection;
        processConsole = this.ProcessConsole;
        statusConsole = this.StatusConsole;

        headsetDescription = this.HeadsetDescription;
        headsetConnection = this.HeadsetConnection;

        leftControllerConnection = this.LeftControllerConnection;
        leftControllerBattery = this.LeftControllerBattery;
        
        rightControllerConnection = this.RightControllerConnection;
        rightControllerBattery = this.RightControllerBattery;

        baseStationActive = this.BaseStationActive;
        baseStationAmount = this.BaseStationAmount;
        
        trackerActive = this.TrackerActive;
        trackerAmount = this.TrackerAmount;
        this.TrackerSection.Visibility = Visibility.Collapsed;
        trackerSection = this.TrackerSection;
        
        MockConsole.WriteLine("Program Started", Enums.LogLevel.Error);
    }
}
