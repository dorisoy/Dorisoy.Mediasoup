using System.Globalization;
using System.Speech.Recognition;
using Dorisoy.Meeting.Client.Models;
using Microsoft.Extensions.Logging;

namespace Dorisoy.Meeting.Client.Services;

/// <summary>
/// 语音识别服务 - 使用 Windows 内置语音识别
/// </summary>
public class SpeechRecognitionService : IDisposable
{
    private readonly ILogger<SpeechRecognitionService>? _logger;
    private SpeechRecognitionEngine? _recognizer;
    private bool _isListening;
    private string _currentLanguage = "zh-CN";
    
    /// <summary>
    /// 语音识别结果事件（实时识别）
    /// </summary>
    public event EventHandler<SpeechRecognizedEventArgs>? SpeechRecognized;
    
    /// <summary>
    /// 语音识别假设事件（临时结果）
    /// </summary>
    public event EventHandler<SpeechHypothesizedEventArgs>? SpeechHypothesized;
    
    /// <summary>
    /// 识别状态改变事件
    /// </summary>
    public event EventHandler<bool>? ListeningStateChanged;
    
    /// <summary>
    /// 错误事件
    /// </summary>
    public event EventHandler<string>? RecognitionError;

    /// <summary>
    /// 是否正在监听
    /// </summary>
    public bool IsListening => _isListening;
    
    /// <summary>
    /// 当前语言
    /// </summary>
    public string CurrentLanguage => _currentLanguage;

    public SpeechRecognitionService(ILogger<SpeechRecognitionService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// 初始化语音识别引擎
    /// </summary>
    /// <param name="language">语言代码</param>
    public bool Initialize(string language = "zh-CN")
    {
        try
        {
            _currentLanguage = language;
            
            // 检查是否有可用的识别器
            var installedRecognizers = SpeechRecognitionEngine.InstalledRecognizers();
            _logger?.LogInformation("已安装的语音识别器: {Count}", installedRecognizers.Count);
            
            foreach (var recognizer in installedRecognizers)
            {
                _logger?.LogInformation("识别器: {Name}, 语言: {Culture}", 
                    recognizer.Description, recognizer.Culture.Name);
            }

            // 尝试找到匹配的语言识别器
            RecognizerInfo? targetRecognizer = null;
            
            // 首先尝试精确匹配
            targetRecognizer = installedRecognizers
                .FirstOrDefault(r => r.Culture.Name.Equals(language, StringComparison.OrdinalIgnoreCase));
            
            // 如果没有精确匹配，尝试匹配语言前缀
            if (targetRecognizer == null)
            {
                var langPrefix = language.Split('-')[0];
                targetRecognizer = installedRecognizers
                    .FirstOrDefault(r => r.Culture.TwoLetterISOLanguageName.Equals(langPrefix, StringComparison.OrdinalIgnoreCase));
            }
            
            // 如果还是没有，使用默认识别器
            if (targetRecognizer == null && installedRecognizers.Count > 0)
            {
                targetRecognizer = installedRecognizers[0];
                _logger?.LogWarning("未找到语言 {Language} 的识别器，使用默认: {Default}", 
                    language, targetRecognizer.Culture.Name);
            }

            if (targetRecognizer == null)
            {
                _logger?.LogError("没有可用的语音识别器");
                RecognitionError?.Invoke(this, "没有可用的语音识别器，请安装语音识别组件");
                return false;
            }

            // 创建识别引擎
            _recognizer?.Dispose();
            _recognizer = new SpeechRecognitionEngine(targetRecognizer);

            // 加载听写语法（自由说话模式）
            var dictationGrammar = new DictationGrammar
            {
                Name = "Dictation"
            };
            _recognizer.LoadGrammar(dictationGrammar);

            // 设置输入为默认音频设备
            _recognizer.SetInputToDefaultAudioDevice();

            // 绑定事件
            _recognizer.SpeechRecognized += OnSpeechRecognized;
            _recognizer.SpeechHypothesized += OnSpeechHypothesized;
            _recognizer.RecognizeCompleted += OnRecognizeCompleted;
            _recognizer.SpeechRecognitionRejected += OnSpeechRejected;

            _logger?.LogInformation("语音识别引擎初始化成功，语言: {Language}", targetRecognizer.Culture.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "初始化语音识别引擎失败");
            RecognitionError?.Invoke(this, $"初始化失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 开始语音识别
    /// </summary>
    public void StartListening()
    {
        if (_recognizer == null)
        {
            _logger?.LogWarning("语音识别引擎未初始化");
            RecognitionError?.Invoke(this, "语音识别引擎未初始化");
            return;
        }

        if (_isListening)
        {
            return;
        }

        try
        {
            // 开始连续识别
            _recognizer.RecognizeAsync(RecognizeMode.Multiple);
            _isListening = true;
            ListeningStateChanged?.Invoke(this, true);
            _logger?.LogInformation("开始语音识别");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "开始语音识别失败");
            RecognitionError?.Invoke(this, $"开始识别失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 停止语音识别
    /// </summary>
    public void StopListening()
    {
        if (_recognizer == null || !_isListening)
        {
            return;
        }

        try
        {
            _recognizer.RecognizeAsyncStop();
            _isListening = false;
            ListeningStateChanged?.Invoke(this, false);
            _logger?.LogInformation("停止语音识别");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "停止语音识别失败");
        }
    }

    /// <summary>
    /// 切换识别语言
    /// </summary>
    public bool SwitchLanguage(string language)
    {
        var wasListening = _isListening;
        
        if (wasListening)
        {
            StopListening();
        }

        var result = Initialize(language);

        if (result && wasListening)
        {
            StartListening();
        }

        return result;
    }

    /// <summary>
    /// 获取已安装的语音识别器
    /// </summary>
    public static List<LanguageInfo> GetInstalledLanguages()
    {
        var languages = new List<LanguageInfo>();
        
        try
        {
            var recognizers = SpeechRecognitionEngine.InstalledRecognizers();
            
            foreach (var recognizer in recognizers)
            {
                languages.Add(new LanguageInfo
                {
                    Code = recognizer.Culture.Name,
                    DisplayName = recognizer.Culture.DisplayName,
                    SpeechCode = recognizer.Culture.Name,
                    TranslateCode = recognizer.Culture.TwoLetterISOLanguageName
                });
            }
        }
        catch
        {
            // 忽略错误
        }

        return languages;
    }

    #region Event Handlers

    private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
    {
        if (e.Result?.Text != null && e.Result.Confidence > 0.3)
        {
            _logger?.LogDebug("识别结果: {Text}, 置信度: {Confidence}", 
                e.Result.Text, e.Result.Confidence);
            SpeechRecognized?.Invoke(this, e);
        }
    }

    private void OnSpeechHypothesized(object? sender, SpeechHypothesizedEventArgs e)
    {
        if (e.Result?.Text != null)
        {
            _logger?.LogDebug("临时识别: {Text}", e.Result.Text);
            SpeechHypothesized?.Invoke(this, e);
        }
    }

    private void OnRecognizeCompleted(object? sender, RecognizeCompletedEventArgs e)
    {
        if (e.Error != null)
        {
            _logger?.LogError(e.Error, "识别完成时出错");
            RecognitionError?.Invoke(this, e.Error.Message);
        }
        
        if (e.Cancelled)
        {
            _logger?.LogInformation("识别被取消");
        }
    }

    private void OnSpeechRejected(object? sender, SpeechRecognitionRejectedEventArgs e)
    {
        _logger?.LogDebug("语音被拒绝，置信度过低");
    }

    #endregion

    public void Dispose()
    {
        StopListening();
        
        if (_recognizer != null)
        {
            _recognizer.SpeechRecognized -= OnSpeechRecognized;
            _recognizer.SpeechHypothesized -= OnSpeechHypothesized;
            _recognizer.RecognizeCompleted -= OnRecognizeCompleted;
            _recognizer.SpeechRecognitionRejected -= OnSpeechRejected;
            _recognizer.Dispose();
            _recognizer = null;
        }
    }
}
