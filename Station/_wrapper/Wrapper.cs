using System.Collections.Generic;

namespace Station
{
    public struct Experience
    {
        public string? Type { get; set; }
        public string? ID { get; set; }
        public string? Name { get; set; }
        public string? Parameters { get; set; }
        public string? AltPath { get; set; }

        public Experience(string? Type, string? ID, string? Name, string? Parameters, string? AltPath)
        {
            this.Type = Type;
            this.ID = ID;
            this.Name = Name;
            this.Parameters = Parameters;
            this.AltPath = AltPath;
        }

        /// <summary>
        /// Determine if the experience instance is just a default constructor that contains all
        /// null values.
        /// </summary>
        /// <returns>A bool if any of the key variables are null.</returns>
        public bool IsNull()
        {
            return (Type == null || ID == null || Name == null);
        }
    }

    public interface Wrapper
    {
        /// <summary>
        /// Collect all the applications associated with the type of wrapper.
        /// </summary>
        /// <returns>A list of all applications associated with the wrapper type.</returns>
        List<string>? CollectApplications();

        /// <summary>
        /// Search the experience folder for the supplied header image, transfering it across
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
        /// Start the supplied process and maintain a connection through the current process variable.
        /// </summary>
        /// <param name="experience">A custom struct that contains all the information of an experience.</param>
        void WrapProcess(Experience experience);

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
        void RestartCurrentProcess();

        /// <summary>
        /// Restart the current session, this includes the current experience and any external applications that are required.
        /// </summary>
        void RestartCurrentSession();
    }
}
