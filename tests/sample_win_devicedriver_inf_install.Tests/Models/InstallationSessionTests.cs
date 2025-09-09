using FluentAssertions;
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
    public void Create_WithValidDriverPackage_ShouldCreateSessionCorrectly()
    {
        // Arrange
        var driverPackage = new DriverPackage
        {
            Id = "test-driver",
            InfFilePath = "test.inf"
        };

        // Act
        var session = InstallationSession.Create(driverPackage);

        // Assert
        session.SessionId.Should().NotBeNullOrEmpty();
        session.DriverPackage.Should().Be(driverPackage);
        session.Status.Should().Be(InstallationStatus.Pending);
        session.ProgressPercentage.Should().Be(0);
        session.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        session.CompletedAt.Should().BeNull();
        session.IsCompleted.Should().BeFalse();
        session.IsSuccessful.Should().BeFalse();
    }

    [Fact]
    public void UpdateProgress_WithValidValues_ShouldUpdateCorrectly()
    {
        // Arrange
        var driverPackage = new DriverPackage { Id = "test", InfFilePath = "test.inf" };
        var session = InstallationSession.Create(driverPackage);
        var progressPercentage = 50;
        var step = "テストステップ";

        // Act
        session.UpdateProgress(progressPercentage, step);

        // Assert
        session.ProgressPercentage.Should().Be(progressPercentage);
        session.CurrentStep.Should().Be(step);
        session.LogMessages.Should().Contain(msg => msg.Contains(step) && msg.Contains("50%"));
    }

    [Theory]
    [InlineData(-10, 0)]
    [InlineData(150, 100)]
    [InlineData(50, 50)]
    public void UpdateProgress_WithBoundaryValues_ShouldClampCorrectly(int input, int expected)
    {
        // Arrange
        var driverPackage = new DriverPackage { Id = "test", InfFilePath = "test.inf" };
        var session = InstallationSession.Create(driverPackage);

        // Act
        session.UpdateProgress(input);

        // Assert
        session.ProgressPercentage.Should().Be(expected);
    }

    [Fact]
    public void Complete_WithSuccessStatus_ShouldSetCompletedState()
    {
        // Arrange
        var driverPackage = new DriverPackage { Id = "test", InfFilePath = "test.inf" };
        var session = InstallationSession.Create(driverPackage);

        // Act
        session.Complete(InstallationStatus.Completed);

        // Assert
        session.Status.Should().Be(InstallationStatus.Completed);
        session.CompletedAt.Should().NotBeNull();
        session.IsCompleted.Should().BeTrue();
        session.IsSuccessful.Should().BeTrue();
        session.ProgressPercentage.Should().Be(100);
        session.LogMessages.Should().Contain(msg => msg.Contains("正常に完了"));
    }

    [Fact]
    public void Complete_WithFailureStatus_ShouldSetFailedState()
    {
        // Arrange
        var driverPackage = new DriverPackage { Id = "test", InfFilePath = "test.inf" };
        var session = InstallationSession.Create(driverPackage);
        var errorMessage = "テストエラー";

        // Act
        session.Complete(InstallationStatus.Failed, errorMessage);

        // Assert
        session.Status.Should().Be(InstallationStatus.Failed);
        session.CompletedAt.Should().NotBeNull();
        session.IsCompleted.Should().BeTrue();
        session.IsSuccessful.Should().BeFalse();
        session.ErrorMessage.Should().Be(errorMessage);
        session.LogMessages.Should().Contain(msg => msg.Contains("エラー") && msg.Contains(errorMessage));
    }

    [Fact]
    public void ElapsedTime_ShouldCalculateCorrectly()
    {
        // Arrange
        var driverPackage = new DriverPackage { Id = "test", InfFilePath = "test.inf" };
        var session = InstallationSession.Create(driverPackage);

        // Act
        Thread.Sleep(100); // 短時間待機
        var elapsedTime = session.ElapsedTime;

        // Assert
        elapsedTime.Should().BeGreaterThan(TimeSpan.FromMilliseconds(50));
        elapsedTime.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }
}