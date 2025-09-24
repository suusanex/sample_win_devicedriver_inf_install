using sample_win_devicedriver_inf_install.Services;
using sample_win_devicedriver_inf_install.Tests.Stubs;
using System.Globalization;

namespace sample_win_devicedriver_inf_install.Tests.Services;

/// <summary>
/// WindowsApiErrorHandlerのUnitTest
/// 実際のWindows APIに依存せずにテストを実行
/// </summary>
public class WindowsApiErrorHandlerTests : IDisposable
{
    private readonly LocalizationService _localizationService;
    private readonly SetupApiStub _setupApiStub;
    private readonly WindowsApiErrorHandler _errorHandler;

    public WindowsApiErrorHandlerTests()
    {
        _localizationService = new LocalizationService(CultureInfo.GetCultureInfo("ja-JP"));
        _setupApiStub = new SetupApiStub();
        _errorHandler = new WindowsApiErrorHandler(_localizationService, _setupApiStub);
    }

    /// <summary>
    /// ファイルが見つからないエラーのテスト
    /// </summary>
    [Fact]
    public void GetLastError_WithFileNotFoundError_ShouldReturnCorrectErrorInfo()
    {
        // Arrange
        _setupApiStub.SetupError(2); // ERROR_FILE_NOT_FOUND
        var operationName = "SetupOpenInfFile";

        // Act
        var errorInfo = _errorHandler.GetLastError(operationName);

        // Assert
        Assert.NotNull(errorInfo);
        Assert.Equal(2u, errorInfo.ErrorCode);
        Assert.Equal(operationName, errorInfo.ApiFunction);
        Assert.Contains("file specified", errorInfo.SystemMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ファイルが見つかりません", errorInfo.LocalizedMessage);
        Assert.Contains(operationName, errorInfo.TechnicalDetails);
    }

    /// <summary>
    /// アクセス拒否エラーのテスト
    /// </summary>
    [Fact]
    public void GetLastError_WithAccessDeniedError_ShouldReturnCorrectErrorInfo()
    {
        // Arrange
        _setupApiStub.SetupError(5); // ERROR_ACCESS_DENIED
        var operationName = "SetupInstallFromInfSection";

        // Act
        var errorInfo = _errorHandler.GetLastError(operationName);

        // Assert
        Assert.NotNull(errorInfo);
        Assert.Equal(5u, errorInfo.ErrorCode);
        Assert.Equal(operationName, errorInfo.ApiFunction);
        Assert.Contains("Access is denied", errorInfo.SystemMessage);
        Assert.Contains("管理者権限", errorInfo.LocalizedMessage);
    }

    /// <summary>
    /// SetupAPIの特定エラーのテスト
    /// </summary>
    [Fact]
    public void GetLastError_WithSetupApiSectionNotFoundError_ShouldReturnCorrectErrorInfo()
    {
        // Arrange
        _setupApiStub.SetupError(0xE0000101u); // SPAPI_E_SECTION_NOT_FOUND
        var operationName = "SetupFindFirstLine";
        var context = "Section: DefaultInstall";

        // Act
        var errorInfo = _errorHandler.GetLastError(operationName, context);

        // Assert
        Assert.NotNull(errorInfo);
        Assert.Equal(0xE0000101u, errorInfo.ErrorCode);
        Assert.Equal(operationName, errorInfo.ApiFunction);
        Assert.Contains("section was not found", errorInfo.SystemMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("セクションが", errorInfo.LocalizedMessage);
        Assert.Contains(context, errorInfo.TechnicalDetails);
    }

    /// <summary>
    /// 不明なエラーコードのテスト
    /// </summary>
    [Fact]
    public void GetLastError_WithUnknownError_ShouldReturnGenericErrorInfo()
    {
        // Arrange
        _setupApiStub.SetupError(0x12345678u); // 不明なエラーコード
        var operationName = "UnknownOperation";

        // Act
        var errorInfo = _errorHandler.GetLastError(operationName);

        // Assert
        Assert.NotNull(errorInfo);
        Assert.Equal(0x12345678u, errorInfo.ErrorCode);
        Assert.Equal(operationName, errorInfo.ApiFunction);
        Assert.Contains("0x12345678", errorInfo.SystemMessage);
        Assert.Contains("エラーコード", errorInfo.LocalizedMessage);
    }

    /// <summary>
    /// FileNotFoundExceptionからのApiErrorInfo作成テスト
    /// </summary>
    [Fact]
    public void CreateFromException_WithFileNotFoundException_ShouldReturnCorrectErrorInfo()
    {
        // Arrange
        var exception = new FileNotFoundException("The specified INF file was not found.", "test.inf");
        var operationName = "LoadInfFile";

        // Act
        var errorInfo = _errorHandler.CreateFromException(exception, operationName);

        // Assert
        Assert.NotNull(errorInfo);
        Assert.Equal(2u, errorInfo.ErrorCode); // ERROR_FILE_NOT_FOUND
        Assert.Equal(operationName, errorInfo.ApiFunction);
        Assert.Contains("not found", errorInfo.SystemMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("INFファイルが見つかりません", errorInfo.LocalizedMessage);
        Assert.Contains("FileNotFoundException", errorInfo.TechnicalDetails);
    }

    /// <summary>
    /// InvalidOperationExceptionからのApiErrorInfo作成テスト
    /// </summary>
    [Fact]
    public void CreateFromException_WithInvalidOperationException_ShouldReturnCorrectErrorInfo()
    {
        // Arrange
        var exception = new InvalidOperationException("Failed to install from INF section 'DefaultInstall': Section not found");
        var operationName = "InstallFromSection";

        // Act
        var errorInfo = _errorHandler.CreateFromException(exception, operationName);

        // Assert
        Assert.NotNull(errorInfo);
        Assert.Equal(operationName, errorInfo.ApiFunction);
        Assert.Contains("Section not found", errorInfo.SystemMessage);
        Assert.Contains("セクションが", errorInfo.LocalizedMessage);
        Assert.Contains("InvalidOperationException", errorInfo.TechnicalDetails);
    }

    /// <summary>
    /// 追加コンテキスト情報が含まれることのテスト
    /// </summary>
    [Fact]
    public void CreateApiErrorInfo_WithAdditionalContext_ShouldIncludeContext()
    {
        // Arrange
        var errorCode = 87u; // ERROR_INVALID_PARAMETER
        var operationName = "SetupInstallFromInfSection";
        var additionalContext = "Section: DefaultInstall, Flags: 0x000000FF";

        // Act
        var errorInfo = _errorHandler.CreateApiErrorInfo(errorCode, operationName, additionalContext);

        // Assert
        Assert.NotNull(errorInfo);
        Assert.Equal(errorCode, errorInfo.ErrorCode);
        Assert.Contains(additionalContext, errorInfo.TechnicalDetails);
        Assert.Contains("Section: DefaultInstall", errorInfo.TechnicalDetails);
        Assert.Contains("Flags: 0x000000FF", errorInfo.TechnicalDetails);
    }

    /// <summary>
    /// レジストリ操作エラーのテスト
    /// </summary>
    [Fact]
    public void GetLastError_WithRegistryError_ShouldReturnCorrectErrorInfo()
    {
        // Arrange
        _setupApiStub.SetupError(0xE0000104u); // Registry operation failed
        var operationName = "SetupInstallFromInfSection";

        // Act
        var errorInfo = _errorHandler.GetLastError(operationName);

        // Assert
        Assert.NotNull(errorInfo);
        Assert.Equal(0xE0000104u, errorInfo.ErrorCode);
        Assert.Contains("Registry", errorInfo.SystemMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("レジストリ", errorInfo.LocalizedMessage);
    }

    /// <summary>
    /// エラーコードゼロの場合のテスト
    /// </summary>
    [Fact]
    public void GetLastError_WithNoError_ShouldReturnZeroErrorCode()
    {
        // Arrange
        _setupApiStub.Reset(); // エラーなし
        var operationName = "SuccessfulOperation";

        // Act
        var errorInfo = _errorHandler.GetLastError(operationName);

        // Assert
        Assert.NotNull(errorInfo);
        Assert.Equal(0u, errorInfo.ErrorCode);
    }

    public void Dispose()
    {
        _setupApiStub?.Reset();
    }
}