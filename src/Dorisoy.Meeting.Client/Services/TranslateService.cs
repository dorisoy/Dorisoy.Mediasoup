using System.Net.Http;
using System.Text.Json;
using System.Web;
using Dorisoy.Meeting.Client.Models;
using Microsoft.Extensions.Logging;

namespace Dorisoy.Meeting.Client.Services;

/// <summary>
/// 翻译服务 - 使用免费的 MyMemory 翻译 API
/// </summary>
public class TranslateService : IDisposable
{
    private readonly ILogger<TranslateService>? _logger;
    private readonly HttpClient _httpClient;
    private const string API_URL = "https://api.mymemory.translated.net/get";
    
    /// <summary>
    /// 支持的语言列表
    /// </summary>
    public static List<LanguageInfo> SupportedLanguages { get; } = new()
    {
        new LanguageInfo { Code = "zh-CN", DisplayName = "中文（简体）", TranslateCode = "zh-CN", SpeechCode = "zh-CN" },
        new LanguageInfo { Code = "zh-TW", DisplayName = "中文（繁体）", TranslateCode = "zh-TW", SpeechCode = "zh-TW" },
        new LanguageInfo { Code = "en-US", DisplayName = "英语", TranslateCode = "en", SpeechCode = "en-US" },
        new LanguageInfo { Code = "ja-JP", DisplayName = "日语", TranslateCode = "ja", SpeechCode = "ja-JP" },
        new LanguageInfo { Code = "ko-KR", DisplayName = "韩语", TranslateCode = "ko", SpeechCode = "ko-KR" },
        new LanguageInfo { Code = "fr-FR", DisplayName = "法语", TranslateCode = "fr", SpeechCode = "fr-FR" },
        new LanguageInfo { Code = "de-DE", DisplayName = "德语", TranslateCode = "de", SpeechCode = "de-DE" },
        new LanguageInfo { Code = "es-ES", DisplayName = "西班牙语", TranslateCode = "es", SpeechCode = "es-ES" },
        new LanguageInfo { Code = "ru-RU", DisplayName = "俄语", TranslateCode = "ru", SpeechCode = "ru-RU" },
        new LanguageInfo { Code = "pt-BR", DisplayName = "葡萄牙语", TranslateCode = "pt", SpeechCode = "pt-BR" },
        new LanguageInfo { Code = "it-IT", DisplayName = "意大利语", TranslateCode = "it", SpeechCode = "it-IT" },
        new LanguageInfo { Code = "ar-SA", DisplayName = "阿拉伯语", TranslateCode = "ar", SpeechCode = "ar-SA" },
        new LanguageInfo { Code = "th-TH", DisplayName = "泰语", TranslateCode = "th", SpeechCode = "th-TH" },
        new LanguageInfo { Code = "vi-VN", DisplayName = "越南语", TranslateCode = "vi", SpeechCode = "vi-VN" },
    };

    public TranslateService(ILogger<TranslateService>? logger = null)
    {
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    /// <summary>
    /// 翻译文本
    /// </summary>
    /// <param name="text">要翻译的文本</param>
    /// <param name="sourceLanguage">源语言代码</param>
    /// <param name="targetLanguage">目标语言代码</param>
    /// <returns>翻译结果</returns>
    public async Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        // 如果源语言和目标语言相同，直接返回
        if (sourceLanguage == targetLanguage)
        {
            return text;
        }

        try
        {
            // 构建请求 URL
            var langPair = $"{sourceLanguage}|{targetLanguage}";
            var encodedText = HttpUtility.UrlEncode(text);
            var url = $"{API_URL}?q={encodedText}&langpair={langPair}";

            _logger?.LogDebug("翻译请求: {Url}", url);

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<MyMemoryResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result?.ResponseData?.TranslatedText != null)
            {
                _logger?.LogDebug("翻译结果: {Result}", result.ResponseData.TranslatedText);
                return result.ResponseData.TranslatedText;
            }

            _logger?.LogWarning("翻译返回空结果");
            return text;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "翻译请求失败");
            // 网络错误时返回原文
            return $"[翻译失败] {text}";
        }
        catch (TaskCanceledException)
        {
            _logger?.LogWarning("翻译请求超时");
            return $"[翻译超时] {text}";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "翻译出错");
            return $"[翻译错误] {text}";
        }
    }

    /// <summary>
    /// 批量翻译
    /// </summary>
    public async Task<List<string>> TranslateBatchAsync(
        List<string> texts, 
        string sourceLanguage, 
        string targetLanguage)
    {
        var results = new List<string>();
        
        foreach (var text in texts)
        {
            var translated = await TranslateAsync(text, sourceLanguage, targetLanguage);
            results.Add(translated);
            
            // 避免请求过快
            await Task.Delay(100);
        }
        
        return results;
    }

    /// <summary>
    /// 检测语言（简单实现）
    /// </summary>
    public string DetectLanguage(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "en";
        }

        // 简单的语言检测：检查是否包含中文字符
        foreach (var c in text)
        {
            if (c >= 0x4E00 && c <= 0x9FFF)
            {
                return "zh-CN";
            }
            if (c >= 0x3040 && c <= 0x30FF)
            {
                return "ja";
            }
            if (c >= 0xAC00 && c <= 0xD7AF)
            {
                return "ko";
            }
            if (c >= 0x0400 && c <= 0x04FF)
            {
                return "ru";
            }
            if (c >= 0x0600 && c <= 0x06FF)
            {
                return "ar";
            }
        }

        return "en";
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    #region MyMemory API Response Models

    private class MyMemoryResponse
    {
        public ResponseData? ResponseData { get; set; }
        public string? ResponseDetails { get; set; }
        public int ResponseStatus { get; set; }
    }

    private class ResponseData
    {
        public string? TranslatedText { get; set; }
        public double Match { get; set; }
    }

    #endregion
}
