using FluentAssertions;
using sample_win_devicedriver_inf_install.Models.ValueObjects;
using Xunit;

namespace sample_win_devicedriver_inf_install.Tests.Models.ValueObjects;

/// <summary>
/// ApiErrorInfoクラスのテスト
/// </summary>
public class ApiErrorInfoTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldCreateCorrectly()
    {
        // Arrange
        uint errorCode = 1234;
        var errorMessage = "テストエラーメッセージ";
        var technicalDetails = "Technical error details";
        var apiFunction = "TestApiFunction";

        // Act
        var errorInfo = ApiErrorInfo.Create(errorCode, errorMessage, technicalDetails, apiFunction);

        // Assert
        errorInfo.ErrorCode.Should().Be(errorCode);
        errorInfo.ErrorMessage.Should().Be(errorMessage);
        errorInfo.TechnicalDetails.Should().Be(technicalDetails);
        errorInfo.ApiFunction.Should().Be(apiFunction);
        errorInfo.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void CreateUnknownError_WithApiFunction_ShouldCreateDefaultError()
    {
        // Arrange
        var apiFunction = "TestApiFunction";

        // Act
        var errorInfo = ApiErrorInfo.CreateUnknownError(apiFunction);

        // Assert
        errorInfo.ErrorCode.Should().Be(0);
        errorInfo.ErrorMessage.Should().Be("不明なエラーが発生しました");
        errorInfo.TechnicalDetails.Should().Be("Unknown error occurred");
        errorInfo.ApiFunction.Should().Be(apiFunction);
        errorInfo.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ApiErrorInfo_ShouldBeValueObject()
    {
        // Arrange
        var errorInfo1 = ApiErrorInfo.Create(123, "message", "details", "function");
        var errorInfo2 = ApiErrorInfo.Create(123, "message", "details", "function");
        var errorInfo3 = ApiErrorInfo.Create(456, "message", "details", "function");

        // Act & Assert
        (errorInfo1 == errorInfo2).Should().BeFalse(); // Timestampが異なるため
        errorInfo1.ErrorCode.Should().Be(errorInfo2.ErrorCode);
        errorInfo1.ErrorMessage.Should().Be(errorInfo2.ErrorMessage);
        errorInfo1.TechnicalDetails.Should().Be(errorInfo2.TechnicalDetails);
        errorInfo1.ApiFunction.Should().Be(errorInfo2.ApiFunction);
        
        errorInfo1.ErrorCode.Should().NotBe(errorInfo3.ErrorCode);
    }
}