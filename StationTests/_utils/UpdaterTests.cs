using Station;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Xunit;
using LeadMeLabsLibrary;

namespace StationTests._utils
{
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
            bool result = SystemInformation.GenerateVersion();

            // Assert
            Assert.True(result);
            string actualVersion = File.ReadAllText($"{CommandLine.stationLocation}\\_logs\\version.txt");
            Assert.Equal(expectedVersion, actualVersion);
        }
    }
}
