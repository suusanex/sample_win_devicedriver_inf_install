using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using sample_win_devicedriver_inf_install.Core.Contracts;
using sample_win_devicedriver_inf_install.Core.Models;
using sample_win_devicedriver_inf_install.Core.Services;
using sample_win_devicedriver_inf_install.Enums;
using sample_win_devicedriver_inf_install.Models;
using sample_win_devicedriver_inf_install.Services;
using Xunit;

namespace sample_win_devicedriver_inf_install.Tests.Services;

/// <summary>
/// DriverInstallationService のユニットテスト
/// </summary>
public class DriverInstallationServiceTests
{
    private readonly Mock<ILogger<DriverInstallationService>> _mockLogger;
    private readonly Mock<WindowsApiErrorHandler> _mockErrorHandler;
    private readonly Mock<DriverStatusService> _mockStatusService;
    private readonly DriverInstallationService _service;

    public DriverInstallationServiceTests()
    {
        _mockLogger = new Mock<ILogger<DriverInstallationService>>();
        _mockErrorHandler = new Mock<WindowsApiErrorHandler>();
        _mockStatusService = new Mock<DriverStatusService>(
            Mock.Of<ILogger<DriverStatusService>>(),
            Mock.Of<WindowsApiErrorHandler>()
        );
        
        _service = new DriverInstallationService(
            _mockLogger.Object,
            _mockErrorHandler.Object,
            _mockStatusService.Object
        );
    }

    [Fact]
    public void CreateSession_ValidDriverPackage_ReturnsSession()
    {
        // Arrange
        var driverPackage = new DriverPackage
        {
            Id = "test-driver-1",
            InfPath = @"C:\temp\test.inf",
            Name = "Test Driver",
            Version = "1.0.0"
        };

        // Act
        var session = _service.CreateSession(driverPackage);

        // Assert
        Assert.NotNull(session);
        Assert.NotEmpty(session.SessionId);
        Assert.Equal(driverPackage, session.DriverPackage);
        Assert.Equal(InstallationStatus.Pending, session.Status);
    }

    [Fact]
    public void CreateSession_NullDriverPackage_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _service.CreateSession(null!));
    }

    [Fact]
    public void GetActiveSessions_AfterCreatingSession_ReturnsSession()
    {
        // Arrange
        var driverPackage = new DriverPackage
        {
            Id = "test-driver-1",
            InfPath = @"C:\temp\test.inf",
            Name = "Test Driver"
        };

        // Act
        var session = _service.CreateSession(driverPackage);
        var activeSessions = _service.GetActiveSessions();

        // Assert
        Assert.Contains(session, activeSessions);
    }

    [Fact]
    public void GetSession_ValidSessionId_ReturnsSession()
    {
        // Arrange
        var driverPackage = new DriverPackage
        {
            Id = "test-driver-1",
            InfPath = @"C:\temp\test.inf",
            Name = "Test Driver"
        };
        var session = _service.CreateSession(driverPackage);

        // Act
        var retrievedSession = _service.GetSession(session.SessionId);

        // Assert
        Assert.Equal(session, retrievedSession);
    }

    [Fact]
    public void GetSession_InvalidSessionId_ReturnsNull()
    {
        // Act
        var retrievedSession = _service.GetSession("non-existent-session-id");

        // Assert
        Assert.Null(retrievedSession);
    }

    [Fact]
    public async Task GetDriverStatusAsync_ValidDriverPackage_CallsStatusService()
    {
        // Arrange
        var driverPackage = new DriverPackage
        {
            Id = "test-driver-1",
            InfPath = @"C:\temp\test.inf",
            Name = "Test Driver"
        };

        var expectedDevices = new List<DeviceInstance>
        {
            DeviceInstance.Create("test-instance-1", "Test Device 1")
        };

        _mockStatusService
            .Setup(x => x.GetDriverStatusAsync(driverPackage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDevices);

        // Act
        var result = await _service.GetDriverStatusAsync(driverPackage);

        // Assert
        Assert.Equal(expectedDevices, result);
        _mockStatusService.Verify(
            x => x.GetDriverStatusAsync(driverPackage, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InstallDriverAsync_NullDriverPackage_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _service.InstallDriverAsync(null!));
    }

    [Fact]
    public async Task UninstallDriverAsync_NullDriverPackage_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _service.UninstallDriverAsync(null!));
    }
}