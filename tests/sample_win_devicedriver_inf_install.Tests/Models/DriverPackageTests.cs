using FluentAssertions;
using sample_win_devicedriver_inf_install.Core.Models;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace sample_win_devicedriver_inf_install.Tests.Models;

/// <summary>
/// DriverPackageクラスのテスト
/// </summary>
public class DriverPackageTests
{
    [Fact]
    public void Create_ValidDriverPackage_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var id = "test-driver-1";
        var infPath = "test.inf";
        var name = "Test Driver";

        // Act
        var package = new DriverPackage
        {
            Id = id,
            InfPath = infPath,
            Name = name
        };

        // Assert
        package.Id.Should().Be(id);
        package.InfPath.Should().Be(infPath);
        package.Name.Should().Be(name);
        package.HardwareIds.Should().NotBeNull().And.BeEmpty();
        package.CompatibleIds.Should().NotBeNull().And.BeEmpty();
        package.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        package.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ValidateInfFileExists_WithNonExistentFile_ShouldReturnFalse()
    {
        // Arrange
        var package = new DriverPackage
        {
            Id = "test",
            InfPath = "non-existent.inf"
        };

        // Act
        var result = package.ValidateInfFileExists();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithEmptyInfFilePath_ShouldReturnValidationError()
    {
        // Arrange
        var package = new DriverPackage
        {
            Id = "test",
            InfPath = ""
        };

        // Act
        var result = package.Validate();

        // Assert
        result.Should().NotBe(ValidationResult.Success);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateInfFileExists_WithInvalidPath_ShouldReturnFalse(string? invalidPath)
    {
        // Arrange
        var package = new DriverPackage
        {
            Id = "test",
            InfPath = invalidPath!
        };

        // Act
        var result = package.ValidateInfFileExists();

        // Assert
        result.Should().BeFalse();
    }
}