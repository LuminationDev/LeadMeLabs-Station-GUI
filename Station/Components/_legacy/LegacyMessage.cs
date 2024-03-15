using System.Threading.Tasks;
using Station.MVC.Controller;

namespace Station.Components._legacy;

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
            MainController.wrapperManager?.ActionHandler("CollectApplications");
        }

        if (additionalData.StartsWith("Restart"))
        {
            MainController.wrapperManager?.ActionHandler("Restart");
        }

        if (additionalData.StartsWith("Thumbnails"))
        {
            string[] split = additionalData.Split(":", 2);
            MainController.wrapperManager?.ActionHandler("CollectHeaderImages", split[1]);
        }

        if (additionalData.StartsWith("Launch"))
        {
            string id = additionalData.Split(":")[1]; // todo - tidy this up
            MainController.wrapperManager?.ActionHandler("Stop");
            
            await Task.Delay(2000);
            
            MainController.wrapperManager?.ActionHandler("Start", id);
        }

        if (additionalData.StartsWith("PassToExperience"))
        {
            string[] split = additionalData.Split(":", 2);
            MainController.wrapperManager?.ActionHandler("Message", split[1]);
        }
    }
}
