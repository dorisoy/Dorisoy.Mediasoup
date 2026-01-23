namespace Dorisoy.Meeting.Client.Models;

/// <summary>
/// 语言信息
/// </summary>
public class LanguageInfo
{
    /// <summary>
    /// 语言代码 (如 zh-CN, en-US)
    /// </summary>
    public string Code { get; set; } = string.Empty;
    
    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// 用于翻译API的代码
    /// </summary>
    public string TranslateCode { get; set; } = string.Empty;
    
    /// <summary>
    /// 语音识别语言代码
    /// </summary>
    public string SpeechCode { get; set; } = string.Empty;

    public override string ToString() => DisplayName;
}

/// <summary>
/// 转录条目
/// </summary>
public class TranscriptEntry
{
    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;
    
    /// <summary>
    /// 源文本
    /// </summary>
    public string SourceText { get; set; } = string.Empty;
    
    /// <summary>
    /// 翻译后文本
    /// </summary>
    public string TranslatedText { get; set; } = string.Empty;
    
    /// <summary>
    /// 源语言
    /// </summary>
    public string SourceLanguage { get; set; } = string.Empty;
    
    /// <summary>
    /// 目标语言
    /// </summary>
    public string TargetLanguage { get; set; } = string.Empty;
    
    /// <summary>
    /// 是否为最终结果（非临时识别结果）
    /// </summary>
    public bool IsFinal { get; set; }
}
