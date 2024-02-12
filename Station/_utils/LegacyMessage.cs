using System.Threading.Tasks;
using Station._manager;

namespace Station._utils;

/// <summary>
/// A dedicated class to hold functions that interpret the legacy string messages. This will condense all functions
/// into a single file that in future can be removed.
/// </summary>
public static class LegacyMessage
{
    /// <summary>
    /// Handle the action of an experience that has been sent from the Tablet -> NUC -> Station
    /// </summary>
    /// <param name="additionalData">A string of information separated by ':'</param>
    public static async void HandleExperienceString(string additionalData)
    {
        if (additionalData.StartsWith("Refresh"))
        {
            Manager.wrapperManager?.ActionHandler("CollectApplications");
        }

        if (additionalData.StartsWith("Restart"))
        {
            Manager.wrapperManager?.ActionHandler("Restart");
        }

        if (additionalData.StartsWith("Thumbnails"))
        {
            string[] split = additionalData.Split(":", 2);
            Manager.wrapperManager?.ActionHandler("CollectHeaderImages", split[1]);
        }

        if (additionalData.StartsWith("Launch"))
        {
            string id = additionalData.Split(":")[1]; // todo - tidy this up
            Manager.wrapperManager?.ActionHandler("Stop");
            
            await Task.Delay(2000);
            
            Manager.wrapperManager?.ActionHandler("Start", id);
        }

        if (additionalData.StartsWith("PassToExperience"))
        {
            string[] split = additionalData.Split(":", 2);
            Manager.wrapperManager?.ActionHandler("Message", split[1]);
        }
    }
}
