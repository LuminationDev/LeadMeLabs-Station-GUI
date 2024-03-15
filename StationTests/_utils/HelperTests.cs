using Station;
using System;
using Station.Components._utils;
using Xunit;

namespace StationTests._utils
{
    public class HelperTests
    {
        /// <summary>
        /// Checks whether the method returns the default mode (VR) when the environment variable 
        /// is null.
        /// </summary>
        [Fact]
        public void GetStationMode_Should_Return_Default_Mode_When_Environment_Variable_Is_Null()
        {
            // Arrange
            var expectedMode = "VR";
            Environment.SetEnvironmentVariable("StationMode", null);

            // Act
            var actualMode = Helper.GetStationMode();

            // Assert
            Assert.Equal(expectedMode, actualMode);
        }

        /// <summary>
        /// Checks whether the method throws an exception when the environment variable is set to 
        /// an unsupported value.
        /// </summary>
        [Fact]
        public void GetStationMode_Should_Throw_Exception_When_Environment_Variable_Is_Not_Supported()
        {
            // Arrange
            Environment.SetEnvironmentVariable("StationMode", "invalid_mode");

            // Act and Assert
            Assert.Throws<Exception>(() => Helper.GetStationMode());
        }

        /// <summary>
        /// Checks whether the method returns the environment variable value when it is a supported 
        /// value.
        /// </summary>
        [Fact]
        public void GetStationMode_Should_Return_Environment_Variable_Value_When_It_Is_Supported()
        {
            // Arrange
            var expectedMode = "Appliance";
            Environment.SetEnvironmentVariable("StationMode", expectedMode);

            // Act
            var actualMode = Helper.GetStationMode();

            // Assert
            Assert.Equal(expectedMode, actualMode);
        }
    }
}
