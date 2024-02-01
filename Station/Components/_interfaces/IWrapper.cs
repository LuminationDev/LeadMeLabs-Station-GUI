using System.Collections.Generic;
using System.Diagnostics;
using Station.Components._models;

namespace Station.Components._interfaces;

public interface IWrapper
{
    /// <summary>
    /// Return the last experience that was launched from the wrapper.
    /// </summary>
    /// <returns>An Experience object.</returns>
    Experience? GetLastExperience();

    /// <summary>
    /// Set the experience that a wrapper is monitoring.
    /// </summary>
    void SetLastExperience(Experience experience);

    /// <summary>
    /// Get the wrappers current launching status.
    /// </summary>
    /// <returns></returns>
    bool GetLaunchingExperience();

    /// <summary>
    /// Set whether the Station is actively trying to launch an experience.
    /// </summary>
    /// <returns></returns>
    void SetLaunchingExperience(bool isLaunching);
    
    /// <summary>
    /// Query the current experience for it's name.
    /// </summary>
    /// <returns>A string or null representing the current experiences name</returns>
    string? GetCurrentExperienceName();

    /// <summary>
    /// Collect all the applications associated with the type of wrapper.
    /// </summary>
    /// <returns>A list of all applications associated with the wrapper type.</returns>
    List<string>? CollectApplications();

    /// <summary>
    /// Search the experience folder for the supplied header image, transferring it across
    /// to the NUC.
    /// </summary>
    /// <param name="experienceName">A string representing the experience image to collect and send</param>
    void CollectHeaderImage(string experienceName);

    /// <summary>
    /// Pass a message into the running process through the use of a custom pipe.
    /// </summary>
    /// <param name="message">A string an action to pass into an experience.</param>
    void PassMessageToProcess(string message);

    /// <summary>
    /// Set the current process for the wrapper class, this is primarily used be OpenVR once it has collected information through
    /// the QueryApplications function.
    /// </summary>
    /// <param name="process">A process representing a running experience</param>
    void SetCurrentProcess(Process process);

    /// <summary>
    /// Start the supplied process and maintain a connection through the current process variable.
    /// </summary>
    /// <param name="experience">A custom struct that contains all the information of an experience.</param>
    string WrapProcess(Experience experience);

    /// <summary>
    /// Begin a new task with the purpose of detecting if the current process has been exited.
    /// </summary>
    void ListenForClose();

    /// <summary>
    /// Check if a process is currently running.
    /// </summary>
    bool? CheckCurrentProcess();

    /// <summary>
    /// Kill the currently running process, releasing all resources associated with it.
    /// </summary>
    void StopCurrentProcess();

    /// <summary>
    /// Restart the current experience without restarting any external software.
    /// </summary>
    void RestartCurrentExperience();

    bool HasCurrentProcess();

    /// <summary>
    /// Returns a boolean indicating if post launch request we had a process at any point, the boolean is reset on launch
    /// </summary>
    /// <returns></returns>
    bool LaunchFailedFromOpenVrTimeout();
}
