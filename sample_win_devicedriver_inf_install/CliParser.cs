using sample_win_devicedriver_inf_install.Models;
using sample_win_devicedriver_inf_install.Services;

namespace sample_win_devicedriver_inf_install;

/// <summary>
/// コマンドライン引数を解析するクラス
/// </summary>
public class CliParser
{
    private readonly LocalizationService _localization;

    public CliParser(LocalizationService localization)
    {
        _localization = localization;
    }

    /// <summary>
    /// コマンドライン引数を解析してCliOptionsを作成する
    /// </summary>
    /// <param name="args">コマンドライン引数</param>
    /// <returns>解析結果とエラーメッセージ</returns>
    public (CliOptions? Options, string? ErrorMessage) Parse(string[] args)
    {
        var options = new CliOptions();
        
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            switch (arg.ToLowerInvariant())
            {
                case "--install":
                    options.Install = true;
                    break;

                case "--inf":
                    if (i + 1 >= args.Length)
                    {
                        return (null, _localization.GetMessage("Cli_MissingInfPath"));
                    }
                    options.InfPath = args[++i];
                    break;

                case "--section":
                    if (i + 1 >= args.Length)
                    {
                        return (null, "セクション名を指定してください。");
                    }
                    options.Section = args[++i];
                    break;

                case "--verbose":
                    options.Verbose = true;
                    break;

                case "--output":
                    if (i + 1 >= args.Length)
                    {
                        return (null, "出力形式を指定してください。");
                    }
                    options.OutputFormat = args[++i];
                    break;

                case "--dry-run":
                    options.DryRun = true;
                    break;

                case "--help":
                case "-h":
                case "/?":
                    options.Help = true;
                    break;

                default:
                    return (null, _localization.GetMessage("Cli_UnknownOption", arg));
            }
        }

        var (isValid, errorMessage) = options.Validate();
        if (!isValid)
        {
            return (null, errorMessage);
        }

        return (options, null);
    }

    /// <summary>
    /// ヘルプメッセージを生成する
    /// </summary>
    /// <returns>ヘルプメッセージ</returns>
    public string GenerateHelpMessage()
    {
        var help = new System.Text.StringBuilder();
        
        help.AppendLine(_localization.GetMessage("Cli_HelpHeader"));
        help.AppendLine();
        help.AppendLine(_localization.GetMessage("Cli_Usage"));
        help.AppendLine();
        help.AppendLine(_localization.GetMessage("Cli_OptionsHeader"));
        help.AppendLine(_localization.GetMessage("Cli_InstallHelp"));
        help.AppendLine(_localization.GetMessage("Cli_InfHelp"));
        help.AppendLine(_localization.GetMessage("Cli_SectionHelp"));
        help.AppendLine(_localization.GetMessage("Cli_VerboseHelp"));
        help.AppendLine(_localization.GetMessage("Cli_OutputHelp"));
        help.AppendLine(_localization.GetMessage("Cli_DryRunHelp"));
        help.AppendLine(_localization.GetMessage("Cli_HelpHelp"));
        help.AppendLine();
        help.AppendLine(_localization.GetMessage("Cli_ExampleHeader"));
        help.AppendLine(_localization.GetMessage("Cli_Example1"));
        help.AppendLine(_localization.GetMessage("Cli_Example2"));  
        help.AppendLine(_localization.GetMessage("Cli_Example3"));

        return help.ToString();
    }
}