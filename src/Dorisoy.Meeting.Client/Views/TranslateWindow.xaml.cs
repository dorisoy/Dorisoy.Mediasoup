using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Speech.Recognition;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Dorisoy.Meeting.Client.Models;
using Dorisoy.Meeting.Client.Services;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Dorisoy.Meeting.Client.Views;

/// <summary>
/// 同步转译窗口
/// </summary>
public partial class TranslateWindow : FluentWindow, INotifyPropertyChanged
{
    private readonly SpeechRecognitionService _speechService;
    private readonly TranslateService _translateService;
    private readonly ObservableCollection<TranscriptEntry> _transcripts = new();
    
    private bool _isListening;
    private bool _isTranslating;
    private string _currentSourceText = string.Empty;
    private string _currentTargetText = string.Empty;
    private LanguageInfo? _selectedSourceLanguage;
    private LanguageInfo? _selectedTargetLanguage;
    private bool _autoScroll = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    #region Properties

    /// <summary>
    /// 可用语言列表
    /// </summary>
    public List<LanguageInfo> AvailableLanguages => TranslateService.SupportedLanguages;

    /// <summary>
    /// 是否正在监听
    /// </summary>
    public bool IsListening
    {
        get => _isListening;
        set
        {
            if (_isListening != value)
            {
                _isListening = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 是否正在翻译
    /// </summary>
    public bool IsTranslating
    {
        get => _isTranslating;
        set
        {
            if (_isTranslating != value)
            {
                _isTranslating = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 当前源文本（临时识别结果）
    /// </summary>
    public string CurrentSourceText
    {
        get => _currentSourceText;
        set
        {
            if (_currentSourceText != value)
            {
                _currentSourceText = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 当前目标文本（翻译结果）
    /// </summary>
    public string CurrentTargetText
    {
        get => _currentTargetText;
        set
        {
            if (_currentTargetText != value)
            {
                _currentTargetText = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 选中的源语言
    /// </summary>
    public LanguageInfo? SelectedSourceLanguage
    {
        get => _selectedSourceLanguage;
        set
        {
            if (_selectedSourceLanguage != value)
            {
                _selectedSourceLanguage = value;
                OnPropertyChanged();
                OnSourceLanguageChanged();
            }
        }
    }

    /// <summary>
    /// 选中的目标语言
    /// </summary>
    public LanguageInfo? SelectedTargetLanguage
    {
        get => _selectedTargetLanguage;
        set
        {
            if (_selectedTargetLanguage != value)
            {
                _selectedTargetLanguage = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 自动滚动
    /// </summary>
    public bool AutoScroll
    {
        get => _autoScroll;
        set
        {
            if (_autoScroll != value)
            {
                _autoScroll = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 转录记录列表
    /// </summary>
    public ObservableCollection<TranscriptEntry> Transcripts => _transcripts;

    #endregion

    public TranslateWindow()
    {
        _speechService = new SpeechRecognitionService();
        _translateService = new TranslateService();
        
        InitializeComponent();
        DataContext = this;
        
        // 设置默认语言
        _selectedSourceLanguage = AvailableLanguages.FirstOrDefault(l => l.Code == "zh-CN");
        _selectedTargetLanguage = AvailableLanguages.FirstOrDefault(l => l.Code == "en-US");
        
        // 绑定语音识别事件
        _speechService.SpeechRecognized += OnSpeechRecognized;
        _speechService.SpeechHypothesized += OnSpeechHypothesized;
        _speechService.ListeningStateChanged += OnListeningStateChanged;
        _speechService.RecognitionError += OnRecognitionError;
        
        Loaded += TranslateWindow_Loaded;
        Closing += TranslateWindow_Closing;
    }

    private void TranslateWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // 初始化语音识别
        if (SelectedSourceLanguage != null)
        {
            _speechService.Initialize(SelectedSourceLanguage.SpeechCode);
        }
    }

    private void TranslateWindow_Closing(object? sender, CancelEventArgs e)
    {
        // 停止并清理资源
        _speechService.StopListening();
        _speechService.Dispose();
        _translateService.Dispose();
    }

    #region Event Handlers

    /// <summary>
    /// 开始/停止按钮点击
    /// </summary>
    private void StartStop_Click(object sender, RoutedEventArgs e)
    {
        if (IsListening)
        {
            _speechService.StopListening();
        }
        else
        {
            if (SelectedSourceLanguage == null)
            {
                var tipBox = new Wpf.Ui.Controls.MessageBox
                {
                    Title = "提示",
                    Content = "请先选择源语言",
                    CloseButtonText = "确定"
                };
                _ = tipBox.ShowDialogAsync();
                return;
            }
            
            _speechService.StartListening();
        }
    }

    /// <summary>
    /// 交换语言
    /// </summary>
    private void SwapLanguages_Click(object sender, RoutedEventArgs e)
    {
        var temp = SelectedSourceLanguage;
        SelectedSourceLanguage = SelectedTargetLanguage;
        SelectedTargetLanguage = temp;
    }

    /// <summary>
    /// 清空记录
    /// </summary>
    private void ClearTranscripts_Click(object sender, RoutedEventArgs e)
    {
        _transcripts.Clear();
        SourceTextPanel.Children.Clear();
        TargetTextPanel.Children.Clear();
        
        // 重新添加当前文本显示
        SourceTextPanel.Children.Add(new TextBlock 
        { 
            Text = string.Empty, 
            Style = FindResource("TranscriptTextStyle") as Style,
            Opacity = 0.7,
            FontStyle = FontStyles.Italic
        });
        TargetTextPanel.Children.Add(new TextBlock 
        { 
            Text = string.Empty, 
            Style = FindResource("TranscriptTextStyle") as Style,
            Foreground = FindResource("AccentTextFillColorPrimaryBrush") as System.Windows.Media.Brush
        });
            
        CurrentSourceText = string.Empty;
        CurrentTargetText = string.Empty;
    }

    /// <summary>
    /// 保存记录
    /// </summary>
    private void SaveTranscripts_Click(object sender, RoutedEventArgs e)
    {
        if (_transcripts.Count == 0)
        {
            var tipBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = "提示",
                Content = "没有可导出的记录",
                CloseButtonText = "确定"
            };
            _ = tipBox.ShowDialogAsync();
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "导出转译记录",
            Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
            DefaultExt = ".txt",
            FileName = $"转译记录_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("========== 同步转译记录 ==========");
                sb.AppendLine($"导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"源语言: {SelectedSourceLanguage?.DisplayName ?? "未知"}");
                sb.AppendLine($"目标语言: {SelectedTargetLanguage?.DisplayName ?? "未知"}");
                sb.AppendLine("==================================");
                sb.AppendLine();

                foreach (var entry in _transcripts)
                {
                    sb.AppendLine($"[{entry.Timestamp:HH:mm:ss}]");
                    sb.AppendLine($"原文: {entry.SourceText}");
                    sb.AppendLine($"译文: {entry.TranslatedText}");
                    sb.AppendLine();
                }

                File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                var successBox = new Wpf.Ui.Controls.MessageBox
                {
                    Title = "成功",
                    Content = $"导出成功: {dialog.FileName}",
                    CloseButtonText = "确定"
                };
                _ = successBox.ShowDialogAsync();
            }
            catch (Exception ex)
            {
                var errorBox = new Wpf.Ui.Controls.MessageBox
                {
                    Title = "错误",
                    Content = $"导出失败: {ex.Message}",
                    CloseButtonText = "确定"
                };
                _ = errorBox.ShowDialogAsync();
            }
        }
    }

    /// <summary>
    /// 设置按钮点击
    /// </summary>
    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        // TODO: 打开设置对话框
        var tipBox = new Wpf.Ui.Controls.MessageBox
        {
            Title = "提示",
            Content = "设置功能开发中...",
            CloseButtonText = "确定"
        };
        _ = tipBox.ShowDialogAsync();
    }

    /// <summary>
    /// 关闭窗口
    /// </summary>
    private void CloseWindow_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// 源语言改变
    /// </summary>
    private void OnSourceLanguageChanged()
    {
        if (SelectedSourceLanguage != null && !IsListening)
        {
            _speechService.SwitchLanguage(SelectedSourceLanguage.SpeechCode);
        }
    }

    /// <summary>
    /// 语音识别结果（最终）
    /// </summary>
    private async void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
    {
        if (e.Result?.Text == null) return;

        var sourceText = e.Result.Text;
        
        await Dispatcher.InvokeAsync(async () =>
        {
            // 更新当前源文本
            CurrentSourceText = sourceText;
            
            // 翻译
            IsTranslating = true;
            try
            {
                var sourceCode = SelectedSourceLanguage?.TranslateCode ?? "zh-CN";
                var targetCode = SelectedTargetLanguage?.TranslateCode ?? "en";
                
                var translatedText = await _translateService.TranslateAsync(sourceText, sourceCode, targetCode);
                CurrentTargetText = translatedText;
                
                // 添加到记录
                var entry = new TranscriptEntry
                {
                    Timestamp = DateTime.Now,
                    SourceText = sourceText,
                    TranslatedText = translatedText,
                    SourceLanguage = sourceCode,
                    TargetLanguage = targetCode,
                    IsFinal = true
                };
                _transcripts.Add(entry);
                
                // 添加到UI
                AddTranscriptToUI(entry);
            }
            catch (Exception ex)
            {
                CurrentTargetText = $"[翻译错误: {ex.Message}]";
            }
            finally
            {
                IsTranslating = false;
            }
        });
    }

    /// <summary>
    /// 语音识别假设（临时结果）
    /// </summary>
    private void OnSpeechHypothesized(object? sender, SpeechHypothesizedEventArgs e)
    {
        if (e.Result?.Text == null) return;

        Dispatcher.Invoke(() =>
        {
            CurrentSourceText = e.Result.Text + "...";
        });
    }

    /// <summary>
    /// 监听状态改变
    /// </summary>
    private void OnListeningStateChanged(object? sender, bool isListening)
    {
        Dispatcher.Invoke(() =>
        {
            IsListening = isListening;
        });
    }

    /// <summary>
    /// 识别错误
    /// </summary>
    private void OnRecognitionError(object? sender, string errorMessage)
    {
        Dispatcher.Invoke(() =>
        {
            var errorBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = "语音识别错误",
                Content = errorMessage,
                CloseButtonText = "确定"
            };
            _ = errorBox.ShowDialogAsync();
        });
    }

    /// <summary>
    /// 添加转录条目到UI
    /// </summary>
    private void AddTranscriptToUI(TranscriptEntry entry)
    {
        // 源文本面板
        var sourcePanel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        sourcePanel.Children.Add(new TextBlock
        {
            Text = entry.Timestamp.ToString("HH:mm:ss"),
            Style = FindResource("TimestampStyle") as Style
        });
        sourcePanel.Children.Add(new TextBlock
        {
            Text = entry.SourceText,
            Style = FindResource("TranscriptTextStyle") as Style
        });
        
        // 在当前文本之前插入
        var sourceIndex = SourceTextPanel.Children.Count - 1;
        if (sourceIndex >= 0)
        {
            SourceTextPanel.Children.Insert(sourceIndex, sourcePanel);
        }
        else
        {
            SourceTextPanel.Children.Add(sourcePanel);
        }

        // 目标文本面板
        var targetPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        targetPanel.Children.Add(new TextBlock
        {
            Text = entry.Timestamp.ToString("HH:mm:ss"),
            Style = FindResource("TimestampStyle") as Style
        });
        targetPanel.Children.Add(new TextBlock
        {
            Text = entry.TranslatedText,
            Style = FindResource("TranscriptTextStyle") as Style,
            Foreground = FindResource("AccentTextFillColorPrimaryBrush") as System.Windows.Media.Brush
        });
        
        var targetIndex = TargetTextPanel.Children.Count - 1;
        if (targetIndex >= 0)
        {
            TargetTextPanel.Children.Insert(targetIndex, targetPanel);
        }
        else
        {
            TargetTextPanel.Children.Add(targetPanel);
        }

        // 自动滚动
        if (AutoScroll)
        {
            SourceScrollViewer.ScrollToEnd();
            TargetScrollViewer.ScrollToEnd();
        }
    }

    #endregion

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
