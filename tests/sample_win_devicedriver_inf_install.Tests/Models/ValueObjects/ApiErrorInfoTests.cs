using FluentAssertions;
using sample_win_devicedriver_inf_install.Core.Models.ValueObjects;
using Xunit;

namespace sample_win_devicedriver_inf_install.Tests.Models.ValueObjects;

/// <summary>
/// ApiErrorInfoクラスのテスト
/// </summary>
public class ApiErrorInfoTests
{
    [Fact]
    public void Create_ValidParameters_ShouldCreateApiErrorInfo()
    {
        // Arrange
        uint errorCode = 5;
        string systemMessage = "Access is denied.";
        string technicalDetails = "Operation failed";
        string apiFunction = "TestFunction";

        // Act
        var errorInfo = ApiErrorInfo.Create(errorCode, systemMessage, technicalDetails, apiFunction);

        // Assert
        errorInfo.ErrorCode.Should().Be(errorCode);
        errorInfo.SystemMessage.Should().Be(systemMessage);
        errorInfo.TechnicalDetails.Should().Be(technicalDetails);
        errorInfo.ApiFunction.Should().Be(apiFunction);
        errorInfo.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetLogMessage_ShouldReturnFormattedMessage()
    {
        // Arrange
        var errorInfo = ApiErrorInfo.Create(5, "Access denied", "Test operation failed", "TestAPI");

        // Act
        var logMessage = errorInfo.GetLogMessage();

        // Assert
        logMessage.Should().Contain("ErrorCode: 5");
        logMessage.Should().Contain("Access denied");
        logMessage.Should().Contain("TestAPI");
    }

    [Fact]
    public void Equality_SameErrorCodes_ShouldBeEqual()
    {
        // Arrange
        var error1 = ApiErrorInfo.Create(5, "Message", "Details", "Function");
        var error2 = ApiErrorInfo.Create(5, "Message", "Details", "Function");
        var error3 = ApiErrorInfo.Create(6, "Message", "Details", "Function");

        // Act & Assert
        error1.Should().Be(error2);
        error1.Should().NotBe(error3);
    }
}