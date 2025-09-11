using sample_win_devicedriver_inf_install.Services;

namespace sample_win_devicedriver_inf_install.CommandHandlers;

/// <summary>
/// ヘルプコマンドを処理するハンドラー
/// </summary>
public class HelpCommand
{
    private readonly CliParser _cliParser;

    public HelpCommand(CliParser cliParser)
    {
        _cliParser = cliParser;
    }

    /// <summary>
    /// ヘルプコマンドを実行する
    /// </summary>
    /// <returns>常に0（成功）</returns>
    public Task<int> ExecuteAsync()
    {
        var helpMessage = _cliParser.GenerateHelpMessage();
        Console.WriteLine(helpMessage);
        return Task.FromResult(0);
    }
}