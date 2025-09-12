using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using sample_win_devicedriver_inf_install.CommandHandlers;
using sample_win_devicedriver_inf_install.Models;
using sample_win_devicedriver_inf_install.Services;

namespace sample_win_devicedriver_inf_install.Services;

/// <summary>
/// GenericHostでコマンドを実行するサービス
/// </summary>
public class CommandExecutorService : IHostedService
{
    private readonly CliOptions _options;
    private readonly InstallCommand _installCommand;
    private readonly HelpCommand _helpCommand;
    private readonly LocalizationService _localization;
    private readonly IHostApplicationLifetime _hostLifetime;
    private readonly ILogger<CommandExecutorService> _logger;
    private int _exitCode = 0;

    public CommandExecutorService(
        CliOptions options,
        InstallCommand installCommand,
        HelpCommand helpCommand,
        LocalizationService localization,
        IHostApplicationLifetime hostLifetime,
        ILogger<CommandExecutorService> logger)
    {
        _options = options;
        _installCommand = installCommand;
        _helpCommand = helpCommand;
        _localization = localization;
        _hostLifetime = hostLifetime;
        _logger = logger;
    }

    /// <summary>
    /// 実行終了コード
    /// </summary>
    public int ExitCode => _exitCode;

    /// <summary>
    /// サービス開始時にコマンドを実行する
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // バックグラウンドで実行することで、ホストの初期化を妨げない
        _ = Task.Run(async () =>
        {
            try
            {
                Console.WriteLine(_localization.GetMessage("App_Starting"));

                if (_options.Help)
                {
                    _exitCode = await _helpCommand.ExecuteAsync();
                }
                else if (_options.Install)
                {
                    _exitCode = await _installCommand.ExecuteAsync(_options, cancellationToken);
                }
                else
                {
                    Console.WriteLine(_localization.GetMessage("Cli_InvalidArguments"));
                    _exitCode = 1;
                }

                if (_exitCode == 0)
                {
                    Console.WriteLine(_localization.GetMessage("App_Completed"));
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine(_localization.GetMessage("App_Cancelled"));
                _exitCode = 130;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in command executor");
                Console.WriteLine(_localization.GetMessage("App_Error", ex.Message));
                _exitCode = 1;
            }
            finally
            {
                // コマンド処理が完了したのでアプリケーションを終了させる
                _hostLifetime.StopApplication();
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    /// <summary>
    /// サービス停止処理（何もしない）
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}