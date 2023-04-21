using System;
using System.Collections.Generic;

namespace Station
{
    internal class ViveWrapper : Wrapper
    {
        public static string wrapperType = "Vive";

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

        public void WrapProcess(Experience experience)
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
