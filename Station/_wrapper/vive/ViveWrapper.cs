using System;
using System.Collections.Generic;
using System.Diagnostics;
using Station._interfaces;
using Station._models;

namespace Station._wrapper.vive;

internal class ViveWrapper : IWrapper
{
    public const string WrapperType = "Vive";

    public Experience? GetLastExperience()
    {
        return null;
    }
    
    public void SetLastExperience(Experience experience)
    {
        throw new NotImplementedException();
    }

    public bool GetLaunchingExperience()
    {
        throw new NotImplementedException();
    }

    public void SetLaunchingExperience(bool isLaunching)
    {
        throw new NotImplementedException();
    }

    public string? GetCurrentExperienceName()
    {
        return null;
    }

    public List<T>? CollectApplications<T>()
    {
        return null;
    }

    public void CollectHeaderImage(string experienceName)
    {
        throw new NotImplementedException();
    }

    public void PassMessageToProcess(string message)
    {
        throw new NotImplementedException();
    }

    public void SetCurrentProcess(Process process)
    {
        throw new NotImplementedException();
    }

    public string WrapProcess(Experience experience)
    {
        throw new NotImplementedException();
    }

    public void ListenForClose()
    {
        throw new NotImplementedException();
    }

    public bool? CheckCurrentProcess()
    {
        throw new NotImplementedException();
    }

    public void StopCurrentProcess()
    {
        throw new NotImplementedException();
    }

    public void RestartCurrentExperience()
    {
        throw new NotImplementedException();
    }
    
    public bool HasCurrentProcess()
    {
        throw new NotImplementedException();
    }
    
    public bool LaunchFailedFromOpenVrTimeout()
    {
        throw new NotImplementedException();
    }
}
