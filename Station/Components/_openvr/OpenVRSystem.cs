using System;
using Station.Components._notification;
using Valve.VR;

namespace Station.Components._openvr;

/// <summary>
/// Represents a wrapper for the OpenVR API, providing access to virtual reality (VR) hardware and functionality.
/// This class facilitates the initialization and management of VR applications with different application types.
/// </summary>
public class OpenVrSystem
{
    /// <summary>
    /// Enumerates the types of VR applications that can be created using OpenVR.
    /// Each value corresponds to a specific type of application with distinct behaviors when interacting with the OpenVR runtime.
    /// </summary>
    public enum ApplicationType
    {
        /// <summary>
        /// A 3D application that will be drawing an environment.
        /// </summary>
        Scene = EVRApplicationType.VRApplication_Scene,

        /// <summary>
        /// An application that only interacts with overlays or the dashboard.
        /// </summary>
        Overlay = EVRApplicationType.VRApplication_Overlay,

        /// <summary>
        /// The application will not start SteamVR. If it is not already running
        /// the call with VR_Init will fail with <see cref="EVRInitError.Init_NoServerForBackgroundApp"/>.
        /// </summary>
        Background = EVRApplicationType.VRApplication_Background,

        /// <summary>
        /// The application will start up even if no hardware is present. Only the IVRSettings
        /// and IVRApplications interfaces are guaranteed to work. This application type is
        /// appropriate for things like installers.
        /// </summary>
        Utility = EVRApplicationType.VRApplication_Utility,

        Other = EVRApplicationType.VRApplication_Other
    }

    /// <summary>
    /// Gets the type of the VR application represented by this instance of OpenVRSystem.
    /// </summary>
    public readonly ApplicationType Type;

    /// <summary>
    /// Gets the handle to the OpenVR system, providing access to core functionality of the OpenVR API.
    /// This allows interaction with VR devices and managing VR sessions.
    /// </summary>
    public CVRSystem? OVRSystem { get; private set; }

    /// <summary>
    /// Instantiate and initialize a new <see cref="OpenVrManager"/>.
    /// Internally, this will initialize the OpenVR API with the specified
    /// <paramref name="type"/> and <paramref name="startupInfo"/>.
    /// </summary>
    /// 
    /// <param name="type"></param>
    /// <param name="startupInfo"></param>
    public OpenVrSystem(ApplicationType type, string startupInfo = "")
    {
        Type = type;

        // Attempt to initialize a new OpenVR context
        var err = EVRInitError.None;
        OVRSystem = OpenVR.Init(ref err, (EVRApplicationType)type, startupInfo);

        if (err != EVRInitError.None)
        {
            try
            {
                throw new OpenVrSystemException<EVRInitError>(
                    "An error occurred while initializing the OpenVR runtime.", err);
            }
            catch (Exception error)
            {
                MockConsole.WriteLine(error.ToString(), MockConsole.LogLevel.Debug);
                OVRSystem = null;
            }
        }
    }

    /// <summary>
    /// Shuts down the OpenVR system and releases its resources.
    /// </summary>
    public void Shutdown()
    {
        OpenVR.Shutdown();
        OVRSystem = null;
    }
}
