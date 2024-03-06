using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sentry;
using Station._controllers;
using Station._models;

namespace Station._managers;

/// <summary>
/// Provides methods to control audio devices using PowerShell commands.
/// </summary>
public static class AudioManager
{
    //Load the audio dll through a path as it needs runs through powershell module commands
    private static readonly string ModulePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AudioDeviceCmdlets.dll");
    private static readonly Dictionary<string, LocalAudioDevice> AudioDevices = new();
    private static readonly object AudioDevicesLock = new();

    #region Observers
    private static string activeAudioDevice = "";
    private static string ActiveAudioDevice
    {
        set
        {
            if (ActiveAudioDevice == value) return;

            activeAudioDevice = value;
            UpdateActiveDevice();
        }
        get => activeAudioDevice;
    }
    #endregion

    /// <summary>
    /// Update the currently active audio device model with the latest volume.
    /// </summary>
    /// <param name="volume">A string of the float value at which the volume is set.</param>
    private static void UpdateActiveAudioDevice(string volume)
    {
        foreach (var device in AudioDevices.Select(kvp => kvp.Value))
        {
            if (device.Name == ActiveAudioDevice)
            {
                device.SetVolume(volume);
            }
        }
    }

    /// <summary>
    /// Collect and store the list of audio devices then check which is one is currently active.
    /// </summary>
    public static void Initialise()
    {
        async void Collect()
        {
            await GetAudioDevices();

            LocalAudioDevice[] audioArray = AudioDevices.Values.ToArray();
            string json = JsonConvert.SerializeObject(audioArray);
            JArray jsonObject = JArray.Parse(json);
            string additionalData = $"SetValue:audioDevices:{jsonObject}";
            MessageController.SendResponse("NUC", "Station", additionalData);

            await GetCurrentAudioDevice();
        }

        new Thread(Collect).Start();
    }

    /// <summary>
    /// Gets information about the current audio playback device.
    /// </summary>
    private static async Task GetCurrentAudioDevice()
    {
        await Task.Run(() =>
        {
            using PowerShell powerShellInstance = PowerShell.Create();
            powerShellInstance.AddScript(@$"Import-Module {ModulePath}");
            powerShellInstance.AddScript("Get-AudioDevice -Playback");
            PSObject? obj = ExecuteAndReturnFirstPowerShellScriptResult(powerShellInstance);
            if (obj == null) return;
            
            //Collect the currently active audio device and send to the NUC
            var result = obj.Properties["Name"]?.Value.ToString() ?? "";
            string additionalData = $"SetValue:activeAudioDevice:{result}";
            MessageController.SendResponse("NUC", "Station", additionalData);
            UpdateActiveDevice();
        });
    }

    /// <summary>
    /// Check the newly selected source for volume level and muted status, sending the results to the NUC
    /// </summary>
    private static void UpdateActiveDevice()
    {
        //Collect the current volume and send to the NUC
        string currentVolume = GetVolume().Result;
        MessageController.SendResponse("NUC", "Station", "SetValue:volume:" + currentVolume);

        //Collect the current muted value and send to the NUC
        string isCurrentMuted = GetMuted().Result;
        MessageController.SendResponse("NUC", "Station", "SetValue:muted:" + isCurrentMuted);
    }

    /// <summary>
    /// Sets the volume of the default audio playback device.
    /// </summary>
    /// <param name="volume">A string representing the level at which to set the volume.</param>
    public static void SetVolume(string volume)
    {
        try
        {
            //Attempt to parse the parameter to a float
            if (float.TryParse(volume, out _))
            {
                new Task(() =>
                {
                    using PowerShell powerShellInstance = PowerShell.Create();
                    powerShellInstance.AddScript(@$"Import-Module {ModulePath}");
                    powerShellInstance.AddScript($"Set-AudioDevice -PlaybackVolume {volume}");
                    ExecutePowerShellScript(powerShellInstance);
                    UpdateActiveAudioDevice(volume);
                }).Start();
            }
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
        }
    }

    /// <summary>
    /// Gets the volume of the default audio playback device.
    /// </summary>
    public static async Task<string> GetVolume()
    {
        string result = "0";

        await Task.Run(() =>
        {
            using PowerShell powerShellInstance = PowerShell.Create();
            powerShellInstance.AddScript(@$"Import-Module {ModulePath}");
            powerShellInstance.AddScript("Get-AudioDevice -PlaybackVolume");
            result = ExecuteAndReturnFirstPowerShellScriptResult(powerShellInstance)?.ToString() ?? "0";
            result = result.Replace("%", "");
            UpdateActiveAudioDevice(result);
        });

        return result;
    }

    /// <summary>
    /// Sets the muted value of the default audio playback device.
    /// </summary>
    public static void SetMuted(string mute)
    {
        try
        {
            new Task(() =>
            {
                using PowerShell powerShellInstance = PowerShell.Create();
                powerShellInstance.AddScript(@$"Import-Module {ModulePath}");
                powerShellInstance.AddScript($"Set-AudioDevice -PlaybackMute ${mute}");
                ExecutePowerShellScript(powerShellInstance);
            }).Start();
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
        }
    }

    /// <summary>
    /// Gets the muted value of the default audio playback device.
    /// </summary>
    public static async Task<string> GetMuted()
    {
        string result = "false";

        await Task.Run(() =>
        {
            using PowerShell powerShellInstance = PowerShell.Create();
            powerShellInstance.AddScript(@$"Import-Module {ModulePath}");
            powerShellInstance.AddScript($"Get-AudioDevice -PlaybackMute");
            result = ExecuteAndReturnFirstPowerShellScriptResult(powerShellInstance)?.ToString() ?? "false";
        });

        return result;
    }

    /// <summary>
    /// Sets the audio device specified by its ID as the default playback device.
    /// </summary>
    public static void SetCurrentAudioDevice(string name)
    {
        new Task(() =>
        {
            if (!AudioDevices.TryGetValue(name, out LocalAudioDevice? device)) return;
            
            using PowerShell powerShellInstance = PowerShell.Create();
            powerShellInstance.AddScript(@$"Import-Module {ModulePath}");
            powerShellInstance.AddScript($"Set-AudioDevice \"{device.Id}\"");
            ExecutePowerShellScript(powerShellInstance);
            ActiveAudioDevice = name;
        }).Start();
    }

    /// <summary>
    /// Collects and stores the list of available audio playback devices.
    /// </summary>
    private static async Task GetAudioDevices()
    {
        await Task.Run(() =>
        {
            using PowerShell powerShellInstance = PowerShell.Create();
            powerShellInstance.AddScript(@$"Import-Module {ModulePath}");
            powerShellInstance.AddScript("Get-AudioDevice -List");
            CollectAudioDeviceInformation(powerShellInstance);
        });
    }

    private static void ExecutePowerShellScript(PowerShell powerShellInstance)
    {
        powerShellInstance.Invoke();

        if (powerShellInstance.HadErrors)
        {
            HandlePowerShellErrors(powerShellInstance);
        }
    }

    private static PSObject? ExecuteAndReturnFirstPowerShellScriptResult(PowerShell powerShellInstance)
    {
        Collection<PSObject> psOutput = powerShellInstance.Invoke();

        if (psOutput.Count > 0)
        {
            // Return the first PSObject
            return psOutput[0];
        }

        if (powerShellInstance.HadErrors)
        {
            HandlePowerShellErrors(powerShellInstance);
        }

        // Return null if no output or errors occurred
        return null;
    }

    private static void CollectAudioDeviceInformation(PowerShell powerShellInstance)
    {
        Collection<PSObject> psOutput = powerShellInstance.Invoke();

        foreach (PSObject outputItem in psOutput)
        {
            string? deviceType = outputItem.Properties["Type"]?.Value?.ToString();
            if (!deviceType?.Equals("Playback") ?? true) continue;

            string? deviceName = outputItem.Properties["Name"]?.Value?.ToString();
            string? deviceId = outputItem.Properties["Id"]?.Value?.ToString();

            // Do not proceed if one of the values is null
            if (deviceName == null || deviceId == null) continue;

            // Lock the AudioDevices dictionary to avoid duplicate entry race conditions
            lock (AudioDevicesLock)
            {
                if (AudioDevices.ContainsKey(deviceName)) continue;
                
                try
                {
                    AudioDevices.Add(deviceName, new LocalAudioDevice(deviceName, deviceId));
                }
                catch (Exception e)
                {
                    SentrySdk.CaptureException(e);
                }
            }
        }

        if (powerShellInstance.HadErrors)
        {
            HandlePowerShellErrors(powerShellInstance);
        }
    }

    private static void HandlePowerShellErrors(PowerShell powerShellInstance)
    {
        foreach (ErrorRecord error in powerShellInstance.Streams.Error)
        {
            Console.WriteLine(error.Exception.Message);
        }
    }
}