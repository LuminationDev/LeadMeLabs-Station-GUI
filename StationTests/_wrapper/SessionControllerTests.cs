using Station;
using Xunit;
using System;
using System.Threading.Tasks;
using System.Diagnostics;
using Station._profiles;
using Station._profiles._headsets;
using Station._wrapper;

namespace StationTests._wrapper
{
    public class SessionControllerTests
    {
        /// <summary>
        /// Ensures that the SetupHeadsetType() method sets the correct vrHeadset
        /// for VivePro1.
        /// </summary>
        [Fact]
        public void SetupHeadsetType_Should_Set_VrHeadset()
        {
            // Arrange
            Environment.SetEnvironmentVariable("HeadsetType", "VivePro1");

            // Act
            SessionController.SetupStationProfile("Vr");

            // Assert
            // Safe cast for potential vr profile
            VrProfile? vrProfile = Profile.CastToType<VrProfile>(SessionController.StationProfile);
            Assert.IsType<VivePro1>(vrProfile?.VrHeadset);
        }

        /// <summary>
        /// Ensures that the SetupHeadsetType() method sets the correct vrHeadset
        /// for VivePro2.
        /// </summary>
        [Fact]
        public void SetupHeadsetType_Should_Set_VivePro2_Headset()
        {
            // Arrange
            Environment.SetEnvironmentVariable("HeadsetType", "VivePro2");

            // Act
            SessionController.SetupStationProfile("Vr");

            // Assert
            // Safe cast for potential vr profile
            VrProfile? vrProfile = Profile.CastToType<VrProfile>(SessionController.StationProfile);
            Assert.IsType<VivePro2>(vrProfile?.VrHeadset);
        }

        /// <summary>
        /// Ensure that the experience type is set to the correct type upon starting
        /// a VR session.
        /// </summary>
        /// <param name="experienceType"></param>
        [Theory]
        [InlineData("Custom")]
        [InlineData("Steam")]
        [InlineData("Vive")]
        public void StartVRSession_Should_Start_Session(string experienceType)
        {
            // Arrange
            SessionController.SetupStationProfile("Vr");
            // Safe cast for potential vr profile
            VrProfile? vrProfile = Profile.CastToType<VrProfile>(SessionController.StationProfile);
            if (vrProfile == null) return;
            vrProfile.VrHeadset = new VivePro1();

            // Act
            SessionController.StartSession(experienceType);

            // Assert
            Assert.Equal(experienceType, SessionController.ExperienceType);
        }

        ///// <summary>
        ///// Ensure that after restarting a VR session with no active experience, the experience
        ///// type remains null.
        ///// </summary>
        //[Fact]
        //public void RestartVRSession_Should_Return_No_Experience_Is_Currently_Running()
        //{
        //    // Arrange
        //    string expectedMessage = "No experience is currently running.";
        //    SessionController.experienceType = null;

        //    // Act
        //    SessionController.RestartVRSession();

        //    // Assert
        //    // Only one instance should be inside the Queue
        //    foreach (var element in MockConsole._textQueue)
        //    {
        //        if (element.Contains(expectedMessage))
        //        {
        //            Assert.Contains(expectedMessage, element);
        //        }
        //    }
        //}

        /// <summary>
        /// Test that ending an experience will return the type back to null.
        /// </summary>
        [Theory]
        [InlineData("Custom")]
        [InlineData("Steam")]
        [InlineData("Vive")]
        public void EndVRSession_Should_Set_ExperienceType_To_Null(string experienceType)
        {
            // Arrange
            SessionController.ExperienceType = experienceType;

            // Act
            SessionController.EndVrSession();

            // Assert
            Assert.Null(SessionController.ExperienceType);
        }

        /// <summary>
        /// Test that the task delay function waits for the appropriate time.
        /// </summary>
        [Fact]
        public async Task PutTaskDelay_Should_Delay_Task()
        {
            // Arrange
            int delay = 1000;

            // Act
            var stopwatch = Stopwatch.StartNew();
            await SessionController.PutTaskDelay(delay);
            stopwatch.Stop();

            // Assert
            Assert.True(stopwatch.ElapsedMilliseconds >= delay);
        }
    }
}
