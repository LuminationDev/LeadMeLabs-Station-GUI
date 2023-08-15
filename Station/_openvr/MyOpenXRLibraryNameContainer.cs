using Silk.NET.Core.Loader;

namespace Station
{
    /// <summary>Contains the library name of OpenXR.</summary>
    internal class MyOpenXRLibraryNameContainer : SearchPathContainer
    {
        /// <inheritdoc />
        public override string[] Linux => new string[1]
        {
            "libopenxr_loader.so.1"
        };

        /// <inheritdoc />
        public override string[] MacOS => new string[1]
        {
            "null"
        };

        /// <inheritdoc />
        public override string[] Android => new string[1]
        {
            "libopenxr_loader.so.1"
        };

        /// <inheritdoc />
        public override string[] IOS => new string[1]
        {
            "__Internal"
        };

        /// <inheritdoc />
        public override string[] Windows64 => new string[1]
        {
            "C:/Program Files (x86)/Steam/steamapps/common/SteamVR/bin/win64/openxr_loader.dll"
        };

        /// <inheritdoc />
        public override string[] Windows86 => new string[1]
        {
            "vrclient_x64.dll"
        };
    }
}