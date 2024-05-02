using System;
using System.Collections.Generic;
using System.IO;
using LeadMeLabsLibrary;
using Sentry;
using Station.Components._commandLine;
using Station.Components._notification;
using Station.Components._utils;
using Station.Components._utils._steamConfig;

namespace Station.QA.checks;

public class SteamConfigChecks
{
    private string? _steamId;
    private List<QaCheck> _qaChecks = new();

    private const string NotInitializedMessage =
        "Steam has not been initialized, please login to Steam client to continue checks.";

    public List<QaCheck> RunQa(string labType)
    {
        _qaChecks = new List<QaCheck>();
        QaCheck isSteamUserNameSet = IsSteamUserNameSet();
        QaCheck isSteamPasswordSet = IsSteamPasswordSet();
        
        _qaChecks.Add(isSteamUserNameSet);
        _qaChecks.Add(isSteamPasswordSet);

        if (isSteamUserNameSet.GetPassedCheck() && isSteamPasswordSet.GetPassedCheck())
        {
            _steamId = SteamConfig.GetSteamId();
        }

        _qaChecks.Add(IsSteamPasswordComplexEnough());
        _qaChecks.Add(IsSteamInitialized());
        _qaChecks.Add(IsFriendsSettingsDisabled());
        _qaChecks.Add(IsDownloadRegionSetCorrectly());
        _qaChecks.Add(IsCloudEnabledOff());
        _qaChecks.Add(IsDefaultPageSetToLibrary());
        _qaChecks.Add(IsSteamInstalledInTheCorrectLocation());
        _qaChecks.AddRange(IsLoginUsersCorrectlySet(labType.ToLower().Equals("online")));
        _qaChecks.AddRange(IsSteamVrSettingsCorrectlySet());
        return _qaChecks;
    }

    private QaCheck IsSteamUserNameSet()
    {
        string? username = Environment.GetEnvironmentVariable("SteamUserName", EnvironmentVariableTarget.Process);
        QaCheck qaCheck = new QaCheck("steam_username");
        if (string.IsNullOrEmpty(username))
        {
            qaCheck.SetFailed("SteamUserName was null or empty");
        }
        else
        {
            qaCheck.SetPassed(null);
        }

        return qaCheck;
    }
    
    private QaCheck IsSteamPasswordSet()
    {
        string? password = Environment.GetEnvironmentVariable("SteamPassword", EnvironmentVariableTarget.Process);
        QaCheck qaCheck = new QaCheck("steam_password");
        if (string.IsNullOrEmpty(password))
        {
            qaCheck.SetFailed("SteamPassword was null or empty");
        }
        else
        {
            qaCheck.SetPassed(null);
        }

        return qaCheck;
    }
    
    private QaCheck IsSteamPasswordComplexEnough()
    {
        string? password = Environment.GetEnvironmentVariable("SteamPassword", EnvironmentVariableTarget.Process);
        QaCheck qaCheck = new QaCheck("steam_password_complexity");
        if (string.IsNullOrEmpty(password))
        {
            qaCheck.SetFailed("SteamPassword is not set");
            return qaCheck;
        }

        bool isPasswordValid = Helpers.DoesPasswordMeetRequirements(password);
        if (isPasswordValid)
        {
            qaCheck.SetPassed(null);
        }
        else
        {
            List<string> passwordValidityMessages = Helpers.GetPasswordValidityMessages(password);
            qaCheck.SetFailed(string.Join(';', passwordValidityMessages));
        }
        
        return qaCheck;
    }

    private QaCheck IsSteamInitialized()
    {
        if (_steamId == null)
        {
            _steamId = SteamConfig.GetSteamId();
        }
        
        QaCheck qaCheck = new QaCheck("steam_initialized");
        if (string.IsNullOrEmpty(_steamId))
        {
            qaCheck.SetFailed(NotInitializedMessage);
        }
        else
        {
            qaCheck.SetPassed(null);
        }

        return qaCheck;
    }
    
    private QaCheck IsSteamInstalledInTheCorrectLocation()
    {
        QaCheck qaCheck = new QaCheck("steam_install_location");
        const string powershellCommand = "Get-ItemProperty -Path HKCU:\\SOFTWARE\\Valve\\Steam -Name SteamPath | grep SteamPath";

        string? output = CommandLine.RunProgramWithOutput("powershell.exe", $"-NoProfile -ExecutionPolicy unrestricted -Command \"{powershellCommand}\"");

        if (string.IsNullOrWhiteSpace(output))
        {
            qaCheck.SetFailed("Could not find steam install location in registry");
            return qaCheck;
        }
        if (output.Contains("SteamPath    : c:/program files (x86)/steam"))
        {
            qaCheck.SetPassed(null);
            return qaCheck;
        }
        else
        {
            qaCheck.SetFailed("Steam install location is not correct. Installed at: " + output.Split("SteamPath")[1].Trim());
            return qaCheck;
        }
    }

    private QaCheck IsFriendsSettingsDisabled()
    {
        QaCheck qaCheck = new QaCheck("friends_setting");
        if (!IsSteamInitialized().GetPassedCheck())
        {
            qaCheck.SetFailed(NotInitializedMessage);
            return qaCheck;
        }
        
        string fileLocation = $"C:\\Program Files (x86)\\Steam\\userdata\\{_steamId}\\config\\sharedconfig.vdf";
        if (!File.Exists(fileLocation))
        {
            fileLocation = $"C:\\Program Files (x86)\\Steam\\userdata\\{_steamId}\\7\\remote\\sharedconfig.vdf";
        }

        if (File.Exists(fileLocation))
        {
            try
            {
                string[] lines = File.ReadAllLines(fileLocation);
                foreach (var line in lines)
                {
                    if (line.Contains("FriendsUIJSON"))
                    {
                        if (line.Contains("\"bSignIntoFriends\\\":false"))
                        {
                            qaCheck.SetPassed(null);
                        }
                        else
                        {
                            qaCheck.SetFailed("Setting is set to false in file: " + fileLocation);
                        }

                        return qaCheck;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.WriteLog($"IsFriendsSettingsDisabled - Sentry Exception: {e}", Enums.LogLevel.Error);
                SentrySdk.CaptureException(e);
            }
        }
        
        string secondFileLocation = $"C:\\Program Files (x86)\\Steam\\userdata\\{_steamId}\\config\\localconfig.vdf";
        if (File.Exists(secondFileLocation))
        {
            try
            {
                string[] lines = File.ReadAllLines(secondFileLocation);
                foreach (var line in lines)
                {
                    if (line.Contains("SignIntoFriends"))
                    {
                        if (line.Contains("0"))
                        {
                            qaCheck.SetPassed(null);
                        }
                        else
                        {
                            qaCheck.SetFailed("Setting is set to false in file: " + secondFileLocation);
                        }

                        return qaCheck;
                    }
                }
                qaCheck.SetFailed("Could not find setting for disabling friends. It is likely still on.");
            }
            catch (Exception e)
            {
                Logger.WriteLog($"IsFriendsSettingsDisabled - Sentry Exception: {e}", Enums.LogLevel.Error);
                SentrySdk.CaptureException(e);
            }
        }
        else
        {
            qaCheck.SetFailed("Could not find settings file at either of " + fileLocation + "," + secondFileLocation);
        }

        if (qaCheck.PassedStatusNotSet())
        {
            qaCheck.SetFailed("Unknown failure.");
        }
        
        return qaCheck;
    }
    
    private QaCheck IsDownloadRegionSetCorrectly()
    {
        QaCheck qaCheck = new QaCheck("download_region");
        if (!IsSteamInitialized().GetPassedCheck())
        {
            qaCheck.SetFailed(NotInitializedMessage);
            return qaCheck;
        }
        
        string fileLocation = $"C:\\Program Files (x86)\\Steam\\config\\config.vdf";

        if (File.Exists(fileLocation))
        {
            try
            {
                string[] lines = File.ReadAllLines(fileLocation);
                foreach (var line in lines)
                {
                    if (line.Contains("CellIDServerOverride"))
                    {
                        /*
                         * 51 - Brisbane
                         * 52 - Sydney
                         * 53 - Melbourne
                         * 54 - Adelaide
                         * 55 - Perth
                         */
                        if (line.Contains("51"))
                        {
                            qaCheck.SetPassed("Download region set to Australia/Brisbane");
                            return qaCheck;
                        }
                        if (line.Contains("52"))
                        {
                            qaCheck.SetPassed("Download region set to Australia/Sydney");
                            return qaCheck;
                        }
                        if (line.Contains("53"))
                        {
                            qaCheck.SetPassed("Download region set to Australia/Melbourne");
                            return qaCheck;
                        }
                        if (line.Contains("54"))
                        {
                            qaCheck.SetPassed("Download region set to Australia/Adelaide");
                            return qaCheck;
                        }
                        if (line.Contains("55"))
                        {
                            qaCheck.SetPassed("Download region set to Australia/Perth");
                            return qaCheck;
                        }
                        qaCheck.SetFailed("Download region is not set to an Australian Region");
                        return qaCheck;
                    }
                }
                qaCheck.SetFailed("Could not find download region setting.");
                return qaCheck;
            }
            catch (Exception e)
            {
                Logger.WriteLog($"IsDownloadRegionSetCorrectly - Sentry Exception: {e}", Enums.LogLevel.Error);
                SentrySdk.CaptureException(e);
            }
        }
        else
        {
            qaCheck.SetFailed("Could not find settings file: " + fileLocation);
            return qaCheck;
        }

        qaCheck.SetFailed("Unknown failure.");
        return qaCheck;
    }
    
    private QaCheck IsCloudEnabledOff()
    {
        QaCheck qaCheck = new QaCheck("cloud_disabled");
        if (!IsSteamInitialized().GetPassedCheck())
        {
            qaCheck.SetFailed(NotInitializedMessage);
            return qaCheck;
        }
        
        string fileLocation = $"C:\\Program Files (x86)\\Steam\\userdata\\{_steamId}\\config\\sharedconfig.vdf";

        if (File.Exists(fileLocation))
        {
            try
            {
                string[] lines = File.ReadAllLines(fileLocation);
                foreach (var line in lines)
                {
                    if (line.Contains("CloudEnabled"))
                    {
                        if (line.Contains("0"))
                        {
                            qaCheck.SetPassed("Passed check in file: " + fileLocation);
                            return qaCheck;
                        }
                        else
                        {
                            qaCheck.SetFailed("Setting failed in file: " + fileLocation);
                            return qaCheck;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.WriteLog($"IsCloudEnabledOff - Sentry Exception: {e}", Enums.LogLevel.Error);
                SentrySdk.CaptureException(e);
            }
        }
        
        string secondaryFileLocation = $"C:\\Program Files (x86)\\Steam\\userdata\\{_steamId}\\7\\remote\\sharedconfig.vdf";
        if (File.Exists(secondaryFileLocation))
        {
            try
            {
                string[] lines = File.ReadAllLines(secondaryFileLocation);
                foreach (var line in lines)
                {
                    if (line.Contains("CloudEnabled"))
                    {
                        if (line.Contains("0"))
                        {
                            qaCheck.SetPassed("Passed check in file: " + secondaryFileLocation);
                            return qaCheck;
                        }
                        else
                        {
                            qaCheck.SetFailed("Setting failed in file: " + secondaryFileLocation);
                            return qaCheck;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.WriteLog($"IsCloudEnabledOff - Sentry Exception: {e}", Enums.LogLevel.Error);
                SentrySdk.CaptureException(e);
            }
        }

        qaCheck.SetFailed("Unknown failure.");
        return qaCheck;
    }

    private QaCheck IsDefaultPageSetToLibrary()
    {
        QaCheck qaCheck = new QaCheck("default_page_library");
        if (!IsSteamInitialized().GetPassedCheck())
        {
            qaCheck.SetFailed(NotInitializedMessage);
            return qaCheck;
        }
        
        string fileLocation = $"C:\\Program Files (x86)\\Steam\\userdata\\{_steamId}\\7\\remote\\sharedconfig.vdf";
        if (File.Exists(fileLocation))
        {
            try
            {
                string[] lines = File.ReadAllLines(fileLocation);
                foreach (var line in lines)
                {
                    if (line.Contains("SteamDefaultDialog"))
                    {
                        if (line.Contains("#app_games"))
                        {
                            qaCheck.SetPassed(null);
                        }
                        else
                        {
                            qaCheck.SetFailed("Default page set to: " + line);
                        }

                        return qaCheck;
                    }
                }
                qaCheck.SetFailed("Could not find setting for default page in file: " + fileLocation);
                return qaCheck;
            }
            catch (Exception e)
            {
                Logger.WriteLog($"IsDefaultPageSetToLibrary - Sentry Exception: {e}", Enums.LogLevel.Error);
                SentrySdk.CaptureException(e);
            }
        }
        else
        {
            qaCheck.SetFailed("Could not find settings file: " + fileLocation);
            return qaCheck;
        }

        qaCheck.SetFailed("Unknown failure.");
        return qaCheck;
    }

    private List<QaCheck> IsLoginUsersCorrectlySet(bool isOnline)
    {
        QaCheck skipOfflineWarning = new QaCheck("skip_offline_warning");
        QaCheck allowAutoLogin = new QaCheck("allow_auto_login");
        QaCheck wantsOfflineMode = new QaCheck("wants_offline_mode");
        List<QaCheck> qaChecks;
        if (!IsSteamInitialized().GetPassedCheck())
        {
            skipOfflineWarning.SetFailed(NotInitializedMessage);
            allowAutoLogin.SetFailed(NotInitializedMessage);
            wantsOfflineMode.SetFailed(NotInitializedMessage);
            qaChecks = new List<QaCheck> { skipOfflineWarning, allowAutoLogin, wantsOfflineMode };
            return qaChecks;
        }
        
        string fileLocation = "C:\\Program Files (x86)\\Steam\\config\\loginusers.vdf";
        if (File.Exists(fileLocation))
        {
            try
            {
                string[] lines = File.ReadAllLines(fileLocation);
                string latestAccountName = "";
                string expectedAccountName = Environment.GetEnvironmentVariable("SteamUserName", EnvironmentVariableTarget.Process) ?? "";
                foreach (var line in lines)
                {
                    if (line.Contains("AccountName"))
                    {
                        string[] split = line.Trim('\t').Trim('\"').Split('\t');
                        latestAccountName = split[split.Length - 1].Trim('\"').Trim('\t');
                    }

                    if (!latestAccountName.ToLower().Equals(expectedAccountName.ToLower()))
                    {
                        continue;
                    }
                    if (line.Contains("SkipOfflineModeWarning"))
                    {
                        if (line.Contains("1"))
                        {
                            skipOfflineWarning.SetPassed(null);
                        }
                        else
                        {
                            skipOfflineWarning.SetFailed("Skip offline mode warning is set to false in file: " + fileLocation);
                        }
                    }
                    
                    if (line.Contains("AllowAutoLogin"))
                    {
                        if (line.Contains("1"))
                        {
                            allowAutoLogin.SetPassed(null);
                        }
                        else
                        {
                            allowAutoLogin.SetFailed("Allow auto login is set to false in file: " + fileLocation);
                        }
                    }
                    
                    if (line.Contains("WantsOfflineMode"))
                    {
                        if (isOnline)
                        {
                            if (line.Contains("0"))
                            {
                                wantsOfflineMode.SetPassed("Set to off as we are in an online lab");
                            }
                            else
                            {
                                wantsOfflineMode.SetFailed("Set to on, but we are in an online lab");
                            }
                        }
                        else
                        {
                            if (line.Contains("0"))
                            {
                                wantsOfflineMode.SetFailed("Set to off, but we are in an offline lab");
                            }
                            else
                            {
                                wantsOfflineMode.SetPassed("Set to on as we are in an offline lab");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.WriteLog($"IsLoginUsersCorrectlySet - Sentry Exception: {e}", Enums.LogLevel.Error);
                SentrySdk.CaptureException(e);
            }
        }

        qaChecks = new List<QaCheck> { skipOfflineWarning, allowAutoLogin, wantsOfflineMode };
        return qaChecks;
    }

    private List<QaCheck> IsSteamVrSettingsCorrectlySet()
    {
        QaCheck homeAppDisabled = new QaCheck("home_app_disabled");
        QaCheck controllerTimeoutSetToZero = new QaCheck("controller_timeout_set_to_zero");
        QaCheck screenTimeoutSetTo1800 = new QaCheck("screen_timeout_set_to_1800");
        QaCheck pauseCompositorSetToFalse = new QaCheck("pause_compositor_set_to_false");
        QaCheck steamVrDashboardDisabled = new QaCheck("steamvr_dashboard_disabled");
        QaCheck steamVrStatusNotOnTop = new QaCheck("steamvr_status_not_on_top");
        QaCheck fenceCorrectColour = new QaCheck("fence_correct_colour");
        List<QaCheck> qaChecks;
        if (!IsSteamInitialized().GetPassedCheck())
        {
            homeAppDisabled.SetFailed(NotInitializedMessage);
            controllerTimeoutSetToZero.SetFailed(NotInitializedMessage);
            screenTimeoutSetTo1800.SetFailed(NotInitializedMessage);
            pauseCompositorSetToFalse.SetFailed(NotInitializedMessage);
            steamVrDashboardDisabled.SetFailed(NotInitializedMessage);
            steamVrStatusNotOnTop.SetFailed(NotInitializedMessage);
            fenceCorrectColour.SetFailed(NotInitializedMessage);
            qaChecks = new List<QaCheck> { homeAppDisabled, controllerTimeoutSetToZero, screenTimeoutSetTo1800, pauseCompositorSetToFalse, steamVrDashboardDisabled, steamVrStatusNotOnTop };
            return qaChecks;
        }
        
        if (!Helper.GetStationMode().Equals(Helper.STATION_MODE_VR))
        {
            homeAppDisabled.SetPassed("Station is a non-vr station");
            controllerTimeoutSetToZero.SetPassed("Station is a non-vr station");
            screenTimeoutSetTo1800.SetPassed("Station is a non-vr station");
            pauseCompositorSetToFalse.SetPassed("Station is a non-vr station");
            steamVrDashboardDisabled.SetPassed("Station is a non-vr station");
            steamVrStatusNotOnTop.SetPassed("Station is a non-vr station");
            fenceCorrectColour.SetPassed("Station is a non-vr station");
            qaChecks = new List<QaCheck> { homeAppDisabled, controllerTimeoutSetToZero, screenTimeoutSetTo1800, pauseCompositorSetToFalse, steamVrDashboardDisabled, steamVrStatusNotOnTop };
            return qaChecks;
        }
        
        string fileLocation = "C:\\Program Files (x86)\\Steam\\config\\steamvr.vrsettings";
        if (File.Exists(fileLocation))
        {
            try
            {
                string[] lines = File.ReadAllLines(fileLocation);
                
                // initialize as failed to find settings
                string message = "Could not find setting in file: " + fileLocation;
                homeAppDisabled.SetFailed(message);
                controllerTimeoutSetToZero.SetFailed(message);
                screenTimeoutSetTo1800.SetFailed(message);
                pauseCompositorSetToFalse.SetFailed(message);
                steamVrDashboardDisabled.SetFailed(message);
                steamVrStatusNotOnTop.SetFailed(message);
                
                foreach (var line in lines)
                {
                    if (line.Contains("CollisionBoundsColorGammaA"))
                    {
                        if (!line.Contains("255"))
                        {
                            fenceCorrectColour.SetFailed("Fence colour is not red");
                        }
                    }
                    if (line.Contains("CollisionBoundsColorGammaR"))
                    {
                        if (!line.Contains("255"))
                        {
                            fenceCorrectColour.SetFailed("Fence colour is not red");
                        }
                    }
                    if (line.Contains("CollisionBoundsColorGammaG"))
                    {
                        if (!line.Contains("0"))
                        {
                            fenceCorrectColour.SetFailed("Fence colour is not red");
                        }
                    }
                    if (line.Contains("CollisionBoundsColorGammaB"))
                    {
                        if (!line.Contains("0"))
                        {
                            fenceCorrectColour.SetFailed("Fence colour is not red");
                        }
                    }
                    
                    
                    if (line.Contains("enableHomeApp"))
                    {
                        if (line.Contains("false"))
                        {
                            homeAppDisabled.SetPassed(null);
                        }
                        else
                        {
                            homeAppDisabled.SetFailed("Setting incorrect in file: " + fileLocation);
                        }
                    }

                    if (line.Contains("turnOffScreensTimeout"))
                    {
                        if (line.Contains("1800"))
                        {
                            screenTimeoutSetTo1800.SetPassed(null);
                        }
                        else
                        {
                            screenTimeoutSetTo1800.SetFailed("Setting incorrect in file: " + fileLocation);
                        }
                    }
                    
                    if (line.Contains("turnOffControllersTimeout"))
                    {
                        if (line.Contains("0"))
                        {
                            controllerTimeoutSetToZero.SetPassed(null);
                        }
                        else
                        {
                            controllerTimeoutSetToZero.SetFailed("Setting incorrect in file: " + fileLocation);
                        }
                    }
                    
                    if (line.Contains("pauseCompositorOnStandby"))
                    {
                        if (line.Contains("false"))
                        {
                            pauseCompositorSetToFalse.SetPassed(null);
                        }
                        else
                        {
                            pauseCompositorSetToFalse.SetFailed("Setting incorrect in file: " + fileLocation);
                        }
                    }
                    
                    if (line.Contains("enableDashboard"))
                    {
                        if (line.Contains("false"))
                        {
                            steamVrDashboardDisabled.SetPassed(null);
                        }
                        else
                        {
                            steamVrDashboardDisabled.SetFailed("Setting incorrect in file: " + fileLocation);
                        }
                    }
                    
                    if (line.Contains("StatusAlwaysOnTop"))
                    {
                        if (line.Contains("false"))
                        {
                            steamVrStatusNotOnTop.SetPassed(null);
                        }
                        else
                        {
                            steamVrStatusNotOnTop.SetFailed("Setting incorrect in file: " + fileLocation);
                        }
                    }
                }
                if (fenceCorrectColour.PassedStatusNotSet())
                {
                    fenceCorrectColour.SetPassed(null);
                }
            }
            catch (Exception e)
            {
                Logger.WriteLog($"IsSteamVrSettingsCorrectlySet - Sentry Exception: {e}", Enums.LogLevel.Error);
                SentrySdk.CaptureException(e);
            }
        }
        else
        {
            string message = "Could not find settings file: " + fileLocation;
            homeAppDisabled.SetFailed(message);
            controllerTimeoutSetToZero.SetFailed(message);
            screenTimeoutSetTo1800.SetFailed(message);
            pauseCompositorSetToFalse.SetFailed(message);
            steamVrDashboardDisabled.SetFailed(message);
            steamVrStatusNotOnTop.SetFailed(message);
            fenceCorrectColour.SetFailed(message);
            qaChecks = new List<QaCheck> { homeAppDisabled, controllerTimeoutSetToZero, screenTimeoutSetTo1800, pauseCompositorSetToFalse, steamVrDashboardDisabled, steamVrStatusNotOnTop, fenceCorrectColour };
            return qaChecks;
        }

        qaChecks = new List<QaCheck> { homeAppDisabled, controllerTimeoutSetToZero, screenTimeoutSetTo1800, pauseCompositorSetToFalse, steamVrDashboardDisabled, steamVrStatusNotOnTop, fenceCorrectColour };
        return qaChecks;
    }
}
