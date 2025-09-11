using System.Text.Json;
using Microsoft.Extensions.Logging;
using sample_win_devicedriver_inf_install.Contracts;
using sample_win_devicedriver_inf_install.Enums;
using sample_win_devicedriver_inf_install.Models;
using sample_win_devicedriver_inf_install.Services;

namespace sample_win_devicedriver_inf_install.CommandHandlers;

/// <summary>
/// インストールコマンドを処理するハンドラー
/// </summary>
public class InstallCommand
{
    private readonly IDriverInstallationService _driverInstallationService;
    private readonly IInstallationLogger _installationLogger;
    private readonly LocalizationService _localization;
    private readonly ILogger<InstallCommand> _logger;

    public InstallCommand(
        IDriverInstallationService driverInstallationService,
        IInstallationLogger installationLogger,
        LocalizationService localization,
        ILogger<InstallCommand> logger)
    {
        _driverInstallationService = driverInstallationService;
        _installationLogger = installationLogger;
        _localization = localization;
        _logger = logger;
    }

    /// <summary>
    /// インストールコマンドを実行する
    /// </summary>
    /// <param name="options">CLIオプション</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    /// <returns>実行結果（0: 成功、非0: エラー）</returns>
    public async Task<int> ExecuteAsync(CliOptions options, CancellationToken cancellationToken = default)
    {
        try
        {
            // ドライランモードのメッセージ表示
            if (options.DryRun)
            {
                Console.WriteLine(_localization.GetMessage("Cli_DryRunMode"));
            }

            // 詳細モードのメッセージ表示
            if (options.Verbose)
            {
                Console.WriteLine(_localization.GetMessage("Cli_VerboseMode"));
            }

            Console.WriteLine(_localization.GetMessage("Install_Starting"));

            // インストールの実行
            var result = await _driverInstallationService.InstallFromInfAsync(
                options.InfPath!,
                options.Section,
                0, // flags
                cancellationToken);

            // 結果の出力
            if (options.OutputFormat?.ToLowerInvariant() == "json")
            {
                await OutputJsonResultAsync(result);
            }
            else
            {
                OutputHumanReadableResult(result);
            }

            return result.Status == InstallationStatus.Completed ? 0 : 1;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine(_localization.GetMessage("App_Cancelled"));
            return 130; // Ctrl+C exit code
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception during installation");
            Console.WriteLine(_localization.GetMessage("App_Error", ex.Message));
            return 1;
        }
    }

    /// <summary>
    /// JSON形式で結果を出力する
    /// </summary>
    /// <param name="result">インストール結果</param>
    private async Task OutputJsonResultAsync(InstallationResult result)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var jsonOutput = new
        {
            status = result.Status.ToString(),
            success = result.Status == InstallationStatus.Completed,
            correlationId = result.CorrelationId,
            startTime = result.StartTime,
            endTime = result.EndTime,
            duration = result.Duration,
            infPath = result.InfPath,
            sectionName = result.SectionName,
            logEntries = result.LogEntries?.Select(entry => new
            {
                timestamp = entry.Timestamp,
                level = entry.Level.ToString(),
                message = entry.Message,
                details = entry.Details
            }),
            errors = result.Errors?.Select(error => new
            {
                errorCode = error.ErrorCode,
                systemMessage = error.SystemMessage,
                userMessage = error.UserMessage,
                technicalDetails = error.TechnicalDetails
            })
        };

        var json = JsonSerializer.Serialize(jsonOutput, jsonOptions);
        await Console.Out.WriteLineAsync(json);
    }

    /// <summary>
    /// 人間が読みやすい形式で結果を出力する
    /// </summary>
    /// <param name="result">インストール結果</param>
    private void OutputHumanReadableResult(InstallationResult result)
    {
        if (result.Status == InstallationStatus.Completed)
        {
            Console.WriteLine(_localization.GetMessage("Install_Success"));
        }
        else
        {
            Console.WriteLine(_localization.GetMessage("Install_Failed"));
            
            // エラー詳細の表示
            if (result.Errors != null && result.Errors.Any())
            {
                Console.WriteLine();
                Console.WriteLine("エラー詳細:");
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"- {error.UserMessage}");
                    if (!string.IsNullOrEmpty(error.TechnicalDetails))
                    {
                        Console.WriteLine($"  技術詳細: {error.TechnicalDetails}");
                    }
                }
            }
        }

        // 実行時間の表示
        Console.WriteLine($"実行時間: {result.Duration.TotalSeconds:F2}秒");
        
        // 相関IDの表示（詳細モード時）
        Console.WriteLine($"セッションID: {result.CorrelationId}");
    }
}