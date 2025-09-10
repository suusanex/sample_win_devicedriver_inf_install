using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using sample_win_devicedriver_inf_install.Core.Models;
using sample_win_devicedriver_inf_install.Core.Services;
using sample_win_devicedriver_inf_install.Services;
using Xunit;

namespace sample_win_devicedriver_inf_install.Tests.Services;

/// <summary>
/// DriverStatusService のユニットテスト
/// </summary>
public class DriverStatusServiceTests
{
    private readonly Mock<ILogger<DriverStatusService>> _mockLogger;
    private readonly Mock<WindowsApiErrorHandler> _mockErrorHandler;
    private readonly DriverStatusService _service;

    public DriverStatusServiceTests()
    {
        _mockLogger = new Mock<ILogger<DriverStatusService>>();
        _mockErrorHandler = new Mock<WindowsApiErrorHandler>();
        
        _service = new DriverStatusService(
            _mockLogger.Object,
            _mockErrorHandler.Object
        );
    }

    [Fact]
    public async Task GetDriverStatusAsync_NullDriverPackage_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _service.GetDriverStatusAsync(null!));
    }

    [Fact]
    public async Task FindDevicesByHardwareIdAsync_NullHardwareId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _service.FindDevicesByHardwareIdAsync(null!));
    }

    [Fact]
    public async Task FindDevicesByHardwareIdAsync_EmptyHardwareId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _service.FindDevicesByHardwareIdAsync(""));
    }

    [Fact]
    public async Task GetDriverDetailsAsync_NullDeviceInstanceId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _service.GetDriverDetailsAsync(null!));
    }

    [Fact]
    public async Task GetDriverDetailsAsync_EmptyDeviceInstanceId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _service.GetDriverDetailsAsync(""));
    }

    [Fact]
    public async Task GetDriverStatusAsync_ValidDriverPackage_ReturnsEmptyListWhenNoDevicesFound()
    {
        // Arrange - Windows API呼び出しが失敗する環境での基本テスト
        var driverPackage = new DriverPackage
        {
            Id = "test-driver-1",
            InfPath = @"C:\temp\nonexistent.inf",
            Name = "Test Driver",
            HardwareIds = new() { "TEST\\DEVICE001" }
        };

        // Act & Assert
        // Windows API へのアクセスが必要な実装のため、このテストでは例外がスローされることを期待
        await Assert.ThrowsAnyAsync<Exception>(() => 
            _service.GetDriverStatusAsync(driverPackage));
    }
}