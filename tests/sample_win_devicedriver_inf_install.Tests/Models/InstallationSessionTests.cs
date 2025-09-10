using FluentAssertions;
using sample_win_devicedriver_inf_install.Core.Models;
using sample_win_devicedriver_inf_install.Enums;
using sample_win_devicedriver_inf_install.Models;
using Xunit;

namespace sample_win_devicedriver_inf_install.Tests.Models;

/// <summary>
/// InstallationSessionクラスのテスト
/// </summary>
public class InstallationSessionTests
{
    [Fact]
    public void Create_ValidDriverPackage_ShouldCreateSession()
    {
        // Arrange
        var driverPackage = new DriverPackage
        {
            Id = "test-driver-1",
            InfPath = "test.inf",
            Name = "Test Driver"
        };

        // Act
        var session = InstallationSession.Create(driverPackage);

        // Assert
        session.Should().NotBeNull();
        session.SessionId.Should().NotBeNullOrEmpty();
        session.DriverPackage.Should().Be(driverPackage);
        session.Status.Should().Be(InstallationStatus.Pending);
        session.ProgressPercentage.Should().Be(0);
        session.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        session.LogMessages.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void UpdateProgress_ValidPercentage_ShouldUpdateProgress()
    {
        // Arrange
        var driverPackage = new DriverPackage
        {
            Id = "test-driver-1",
            InfPath = "test.inf"
        };
        var session = InstallationSession.Create(driverPackage);

        // Act
        session.UpdateProgress(50, "Half complete");

        // Assert
        session.ProgressPercentage.Should().Be(50);
        session.CurrentStep.Should().Be("Half complete");
        session.LogMessages.Should().ContainSingle();
    }

    [Fact]
    public void UpdateProgress_PercentageOver100_ShouldClampTo100()
    {
        // Arrange
        var driverPackage = new DriverPackage
        {
            Id = "test-driver-1", 
            InfPath = "test.inf"
        };
        var session = InstallationSession.Create(driverPackage);

        // Act
        session.UpdateProgress(150);

        // Assert
        session.ProgressPercentage.Should().Be(100);
    }

    [Fact]
    public void Complete_WithSuccessStatus_ShouldCompleteSession()
    {
        // Arrange
        var driverPackage = new DriverPackage
        {
            Id = "test-driver-1",
            InfPath = "test.inf"
        };
        var session = InstallationSession.Create(driverPackage);

        // Act
        session.Complete(InstallationStatus.Completed);

        // Assert
        session.Status.Should().Be(InstallationStatus.Completed);
        session.CompletedAt.Should().NotBeNull();
        session.IsCompleted.Should().BeTrue();
        session.IsSuccessful.Should().BeTrue();
        session.ProgressPercentage.Should().Be(100);
    }

    [Fact]
    public void Complete_WithFailureStatus_ShouldCompleteSessionWithError()
    {
        // Arrange
        var driverPackage = new DriverPackage
        {
            Id = "test-driver-1",
            InfPath = "test.inf"
        };
        var session = InstallationSession.Create(driverPackage);
        var errorMessage = "Installation failed";

        // Act
        session.Complete(InstallationStatus.Failed, errorMessage);

        // Assert
        session.Status.Should().Be(InstallationStatus.Failed);
        session.CompletedAt.Should().NotBeNull();
        session.ErrorMessage.Should().Be(errorMessage);
        session.IsCompleted.Should().BeTrue();
        session.IsSuccessful.Should().BeFalse();
    }
}