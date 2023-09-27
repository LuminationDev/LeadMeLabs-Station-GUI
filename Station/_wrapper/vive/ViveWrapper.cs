using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Station
{
    internal class ViveWrapper : Wrapper
    {
        public static string wrapperType = "Vive";

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

        public List<string>? CollectApplications()
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
    }
}
