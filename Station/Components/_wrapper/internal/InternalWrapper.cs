﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using leadme_api;
using Newtonsoft.Json.Linq;
using Station.Components._interfaces;
using Station.Components._managers;
using Station.Components._models;
using Station.Components._notification;
using Station.MVC.Controller;

namespace Station.Components._wrapper.@internal;

/// <summary>
/// A basic wrapper designed to deal with launching executables from scenes or background programs that do not
/// influence the running of the station.
/// </summary>
public class InternalWrapper : IWrapper
{
    public const string WrapperType = "Internal";
    private static Process? currentProcess;
    private static Experience lastExperience;
    //Track any internal executables in the dictionary to start/stop at will
    private readonly Dictionary<string, Process> _internalProcesses = new();

    public Experience? GetLastExperience()
    {
        return lastExperience;
    }
    
    public void SetLastExperience(Experience experience)
    {
        lastExperience = experience;
    }

    public string? GetCurrentExperienceName()
    {
        return lastExperience.Name;
    }

    public bool GetLaunchingExperience()
    {
        throw new NotImplementedException();
    }

    public void SetLaunchingExperience(bool isLaunching)
    {
        throw new NotImplementedException();
    }
    
    public List<T> CollectApplications<T>()
    {
        throw new NotImplementedException();
    }

    public void CollectHeaderImage(string experienceName)
    {
        throw new NotImplementedException();
    }

    public void PassMessageToProcess(string message)
    {
        PipeClient pipeClient = new(MockConsole.WriteLine, 5);

        Task.Factory.StartNew(() =>
        {
            pipeClient.Send(message);
        });
    }

    public void SetCurrentProcess(Process process)
    {
        throw new NotImplementedException();
    }

    public string WrapProcess(Experience experience)
    {
        return WrapProcess("hidden", experience);
    }

    public string WrapProcess(string launchType, Experience experience)
    {
        Task.Factory.StartNew(() =>
        {
            if (experience.Name == null || experience.AltPath == null) return;

            string processPath = experience.AltPath;

            if (!File.Exists(processPath))
            {
                JObject message = new JObject
                {
                    { "action", "StationError" },
                    { "value", $"File not found:{processPath}" }
                };
                SessionController.PassStationMessage(message);
                return;
            }

            //Check if the process is already running as to not double up
            _internalProcesses.TryGetValue(experience.Name, out var runningProcess);
            if (runningProcess != null)
            {
                MockConsole.WriteLine($"{experience.Name} is already running in the internal wrapper.");
                return;
            }
            
            Process newProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = processPath,
                    Arguments = experience.Parameters
                }
            };
            newProcess.Start();
            if (launchType.Equals("other")) return;
            
            _internalProcesses.Add(experience.Name, newProcess);
            if (launchType.Equals("hidden")) return;
        
            //Close any active experiences if the internal one is visible
            WrapperManager.currentWrapper?.StopCurrentProcess();
        
            currentProcess = newProcess;
            lastExperience = experience;
        
            //Update the UI, NUC and Tablet
            UiUpdater.UpdateProcess(lastExperience.Name ?? "Unknown");
            UiUpdater.UpdateStatus("Running...");

            JObject experienceInformation = new JObject
            {
                { "name", lastExperience.Name },
                { "appId", lastExperience.ID },
                { "wrapper", "Internal" }
            };
            
            JObject updateMessage = new JObject
            {
                { "action", "ApplicationUpdate" },
                { "info", experienceInformation }
            };
            SessionController.PassStationMessage(updateMessage);
            ListenForClose();
        });
        return "launching";
    }

    /// <summary>
    /// Begin a new task with the purpose of detecting if the current process has been exited.
    /// </summary>
    public void ListenForClose()
    {
        Task.Factory.StartNew(() =>
        {
            currentProcess?.WaitForExit();
            lastExperience.Name = null; //Reset for correct headset state
            
            JObject message = new JObject
            {
                { "action", "ApplicationClosed" },
            };
            SessionController.PassStationMessage(message);
            UiUpdater.ResetUiDisplay();
        });
    }

    public bool? CheckCurrentProcess()
    {
        return currentProcess?.Responding;
    }

    public void StopAProcess(Experience experience)
    {
        if (experience.Name == null) return;

        _internalProcesses.TryGetValue(experience.Name, out var runningProcess);

        if (runningProcess == null) return;

        //Kill the process and remove it from the list
        runningProcess.Kill(true);
        _internalProcesses.Remove(experience.Name);

        if (experience.Name != lastExperience.Name) return;

        lastExperience.Name = null; //Reset for correct headset state
        JObject message = new JObject
        {
            { "action", "ApplicationClosed" },
        };
        SessionController.PassStationMessage(message);
        UiUpdater.ResetUiDisplay();
    }

    public void StopCurrentProcess()
    {
        if (lastExperience.Name == null) return;

        _internalProcesses.TryGetValue(lastExperience.Name, out var runningProcess);

        if (runningProcess == null) return;

        //Kill the process and remove it from the list
        runningProcess.Kill(true);
        _internalProcesses.Remove(lastExperience.Name);

        lastExperience.Name = null; //Reset for correct headset state
        JObject message = new JObject
        {
            { "action", "ApplicationClosed" },
        };
        SessionController.PassStationMessage(message);
        UiUpdater.ResetUiDisplay();
    }

    public void RestartCurrentExperience()
    {
        if (lastExperience.Name == null) return;

        _internalProcesses.TryGetValue(lastExperience.Name, out var runningProcess);

        //Create a temp as the StopCurrenProcess alters the current experience
        Experience temp = lastExperience;
        if (runningProcess == null || lastExperience.IsNull()) return;

        StopCurrentProcess();
        Task.Delay(3000).Wait();
        WrapProcess("visible", temp);
    }
    
    public bool HasCurrentProcess()
    {
        return currentProcess != null;
    }
    
    public bool LaunchFailedFromOpenVrTimeout()
    {
        return false;
    }
}
