using System.Diagnostics;
using System.IO;
using System.Reflection;
using Station.Components._commandLine;
using Station.Components._utils;
using Xunit;

namespace StationTests._utils;

public class UpdaterTests
{
    /// <summary>
    /// Checks that the function generates a version file with the correct version number 
    /// and returns true.
    /// </summary>
    [Fact]
    public void TestGenerateVersion()
    {
        // Arrange
        Assembly assembly = Assembly.GetExecutingAssembly();
        FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
        string? expectedVersion = fileVersionInfo.ProductVersion;

        Assert.NotNull(expectedVersion);

        // Act
        bool result = Updater.GenerateVersion();

        // Assert
        Assert.True(result);
        string actualVersion = File.ReadAllText($"{StationCommandLine.StationLocation}\\_logs\\version.txt");
        Assert.Equal(expectedVersion, actualVersion);
    }
}
