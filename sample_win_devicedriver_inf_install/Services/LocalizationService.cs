using System;
using System.Collections.Generic;
using System.Globalization;
using System.Resources;
using System.Reflection;

namespace sample_win_devicedriver_inf_install.Services;

/// <summary>
/// ローカライゼーションサービス
/// リソースファイルからメッセージを取得し、多言語対応を提供します
/// </summary>
public class LocalizationService
{
    private readonly ResourceManager _errorMessagesResourceManager;
    private readonly ResourceManager _messagesResourceManager;
    private readonly CultureInfo _culture;

    public LocalizationService() : this(CultureInfo.CurrentUICulture)
    {
    }

    public LocalizationService(CultureInfo culture)
    {
        _culture = culture ?? throw new ArgumentNullException(nameof(culture));
        
        // リソースマネージャーを初期化
        var assembly = Assembly.GetExecutingAssembly();
        _errorMessagesResourceManager = new ResourceManager("sample_win_devicedriver_inf_install.Resources.ErrorMessages", assembly);
        _messagesResourceManager = new ResourceManager("sample_win_devicedriver_inf_install.Resources.Messages", assembly);
    }

    /// <summary>
    /// エラーメッセージを取得します
    /// </summary>
    /// <param name="key">リソースキー</param>
    /// <param name="defaultMessage">デフォルトメッセージ</param>
    /// <returns>ローカライズされたエラーメッセージ</returns>
    public string GetErrorMessage(string key, string? defaultMessage = null)
    {
        try
        {
            var message = _errorMessagesResourceManager.GetString(key, _culture);
            return message ?? defaultMessage ?? $"Unknown error: {key}";
        }
        catch (Exception)
        {
            return defaultMessage ?? $"Unknown error: {key}";
        }
    }

    /// <summary>
    /// 一般メッセージを取得します
    /// </summary>
    /// <param name="key">リソースキー</param>
    /// <param name="defaultMessage">デフォルトメッセージ</param>
    /// <returns>ローカライズされたメッセージ</returns>
    public string GetMessage(string key, string? defaultMessage = null)
    {
        try
        {
            var message = _messagesResourceManager.GetString(key, _culture);
            return message ?? defaultMessage ?? $"Unknown message: {key}";
        }
        catch (Exception)
        {
            return defaultMessage ?? $"Unknown message: {key}";
        }
    }

    /// <summary>
    /// パラメータ付きメッセージを取得します
    /// </summary>
    /// <param name="key">リソースキー</param>
    /// <param name="args">フォーマット引数</param>
    /// <returns>フォーマット済みメッセージ</returns>
    public string GetFormattedMessage(string key, params object[] args)
    {
        var template = GetMessage(key);
        try
        {
            return string.Format(_culture, template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    /// <summary>
    /// パラメータ付きエラーメッセージを取得します
    /// </summary>
    /// <param name="key">リソースキー</param>
    /// <param name="args">フォーマット引数</param>
    /// <returns>フォーマット済みエラーメッセージ</returns>
    public string GetFormattedErrorMessage(string key, params object[] args)
    {
        var template = GetErrorMessage(key);
        try
        {
            return string.Format(_culture, template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    /// <summary>
    /// 現在のカルチャを取得します
    /// </summary>
    public CultureInfo Culture => _culture;

    /// <summary>
    /// 利用可能なすべてのエラーメッセージキーを取得します（デバッグ用）
    /// </summary>
    /// <returns>エラーメッセージキーのリスト</returns>
    public IEnumerable<string> GetAllErrorMessageKeys()
    {
        var keys = new List<string>();
        
        try
        {
            var resourceSet = _errorMessagesResourceManager.GetResourceSet(_culture, true, false);
            if (resourceSet != null)
            {
                foreach (System.Collections.DictionaryEntry entry in resourceSet)
                {
                    if (entry.Key is string key)
                    {
                        keys.Add(key);
                    }
                }
            }
        }
        catch (Exception)
        {
            // リソースセットの取得に失敗した場合は空のリストを返す
        }
        
        return keys;
    }

    /// <summary>
    /// 利用可能なすべてのメッセージキーを取得します（デバッグ用）
    /// </summary>
    /// <returns>メッセージキーのリスト</returns>
    public IEnumerable<string> GetAllMessageKeys()
    {
        var keys = new List<string>();
        
        try
        {
            var resourceSet = _messagesResourceManager.GetResourceSet(_culture, true, false);
            if (resourceSet != null)
            {
                foreach (System.Collections.DictionaryEntry entry in resourceSet)
                {
                    if (entry.Key is string key)
                    {
                        keys.Add(key);
                    }
                }
            }
        }
        catch (Exception)
        {
            // リソースセットの取得に失敗した場合は空のリストを返す
        }
        
        return keys;
    }
}