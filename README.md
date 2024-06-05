# LeadMeLabs-Station-GUI

### Path Overhaul
Debug build uses set paths for both SteamCMD and SetVol, positioned as the previous LeadMeLabs-Station.
    - i.e "C:\Users\{Environment.GetEnvironmentVariable("Directory")}\steamcmd\steamcmd.exe"

Production uses the working directory of the running executable, with SteamCMD and SetVol being packaged inside the Station folder by the new Launcher.
    - i.e stationLocation + @"\external\steamcmd\steamcmd.exe";

These variables are automatically switched depending on the build type selected.


### ENV Variable Overhaul
Instead of relying on the System Environment variables, the software now uses an internal config.env file which upon start up is read into the local environment variables.
    - A base version is saved in the software that can be filled out for debug.
    - The new launcher creates the config.env file when installing the application.

### Framework type Overhaul
The software is now using a WPF framework. This allows the software to be confined the the icon tray, a new 'Mock' console has be designed to mimic a console application for logging purposes.
