using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Dorisoy.Meeting.Client.Models;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace Dorisoy.Meeting.Client.Views
{
    /// <summary>
    /// 协同编辑器窗口
    /// </summary>
    public partial class CollaborativeEditorWindow : FluentWindow
    {
        private readonly string _peerId;
        private readonly string _peerName;
        private readonly string _sessionId;
        private readonly string _hostId;
        private readonly bool _isHost;
        private bool _isUpdatingFromRemote;
        private bool _isForceClosing; // 标记是否是强制关闭（主持人关闭通知）
        private Timer? _debounceTimer;
        private const int DebounceDelay = 300; // 300ms 防抖

        /// <summary>
        /// 内容变化事件（用于广播给其他用户）
        /// </summary>
        public event Action<EditorContentUpdate>? ContentChanged;

        /// <summary>
        /// 窗口关闭事件（主持人关闭时触发）
        /// </summary>
        public event Action<CloseEditorRequest>? EditorClosed;

        public CollaborativeEditorWindow(string peerId, string peerName, string sessionId, string hostId)
        {
            InitializeComponent();
            _peerId = peerId;
            _peerName = peerName;
            _sessionId = sessionId;
            _hostId = hostId;
            _isHost = peerId == hostId;

            TxtSessionId.Text = $"会话: {sessionId.Substring(0, 8)}...";
            UpdateTimeDisplay();

            // 如果不是主持人，隐藏关闭菜单项
            if (!_isHost)
            {
                MenuClose.Visibility = Visibility.Collapsed;
            }

            Closing += OnWindowClosing;
            Closed += OnWindowClosed;
        }

        /// <summary>
        /// 更新编辑器内容（接收远程更新）
        /// </summary>
        public void UpdateContent(EditorContentUpdate update)
        {
            if (update.SessionId != _sessionId) return;
            if (update.EditorId == _peerId) return; // 忽略自己的更新

            Dispatcher.Invoke(() =>
            {
                _isUpdatingFromRemote = true;
                try
                {
                    // 保存当前光标位置
                    var caretOffset = RootTextBox.CaretPosition.GetOffsetToPosition(
                        RootTextBox.Document.ContentStart);

                    // 更新 RTF 内容
                    if (!string.IsNullOrEmpty(update.RtfContent))
                    {
                        var range = new TextRange(
                            RootTextBox.Document.ContentStart,
                            RootTextBox.Document.ContentEnd);
                        
                        using var stream = new MemoryStream(
                            Convert.FromBase64String(update.RtfContent));
                        range.Load(stream, DataFormats.Rtf);
                    }
                    else if (!string.IsNullOrEmpty(update.Content))
                    {
                        // 如果没有 RTF，使用纯文本
                        var range = new TextRange(
                            RootTextBox.Document.ContentStart,
                            RootTextBox.Document.ContentEnd);
                        range.Text = update.Content;
                    }

                    // 更新状态栏
                    TxtLastEditor.Text = $"最后编辑: {update.EditorName}";
                    TxtUpdateTime.Text = update.UpdateTime.ToString("HH:mm:ss");
                }
                finally
                {
                    _isUpdatingFromRemote = false;
                }
            });
        }

        /// <summary>
        /// 文本变化事件
        /// </summary>
        private void RootTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_isUpdatingFromRemote) return;

            // 防抖：延迟发送，避免频繁广播
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(OnDebounceElapsed, null, DebounceDelay, Timeout.Infinite);
        }

        /// <summary>
        /// 防抖结束，发送内容更新
        /// </summary>
        private void OnDebounceElapsed(object? state)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // 获取纯文本
                    var range = new TextRange(
                        RootTextBox.Document.ContentStart,
                        RootTextBox.Document.ContentEnd);
                    var plainText = range.Text;

                    // 获取 RTF
                    string rtfContent = string.Empty;
                    using (var stream = new MemoryStream())
                    {
                        range.Save(stream, DataFormats.Rtf);
                        rtfContent = Convert.ToBase64String(stream.ToArray());
                    }

                    // 获取光标位置
                    var caretPosition = RootTextBox.CaretPosition.GetOffsetToPosition(
                        RootTextBox.Document.ContentStart);

                    var update = new EditorContentUpdate
                    {
                        SessionId = _sessionId,
                        Content = plainText,
                        RtfContent = rtfContent,
                        EditorId = _peerId,
                        EditorName = _peerName,
                        UpdateTime = DateTime.Now,
                        CursorPosition = Math.Abs(caretPosition)
                    };

                    ContentChanged?.Invoke(update);
                    UpdateTimeDisplay();
                }
                catch
                {
                    // 忽略序列化错误
                }
            });
        }

        /// <summary>
        /// 选择变化事件
        /// </summary>
        private void RootTextBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            // 可以在这里更新光标位置信息给其他用户
        }

        /// <summary>
        /// 更新时间显示
        /// </summary>
        private void UpdateTimeDisplay()
        {
            TxtUpdateTime.Text = DateTime.Now.ToString("HH:mm:ss");
        }

        #region 菜单命令

        private void MenuSave_Click(object sender, RoutedEventArgs e)
        {
            SaveFile(false);
        }

        private void MenuSaveAs_Click(object sender, RoutedEventArgs e)
        {
            SaveFile(true);
        }

        private void SaveFile(bool saveAs)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "RTF 文件 (*.rtf)|*.rtf|文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
                DefaultExt = ".rtf",
                FileName = $"协同文档_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var range = new TextRange(
                        RootTextBox.Document.ContentStart,
                        RootTextBox.Document.ContentEnd);

                    using var stream = new FileStream(dialog.FileName, FileMode.Create);
                    var format = dialog.FileName.EndsWith(".txt") ? DataFormats.Text : DataFormats.Rtf;
                    range.Save(stream, format);

                    System.Windows.MessageBox.Show("文件保存成功！", "保存", System.Windows.MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"保存失败: {ex.Message}", "错误", System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void MenuClose_Click(object sender, RoutedEventArgs e)
        {
            if (_isHost)
            {
                // 主持人关闭：发送关闭通知并关闭窗口
                NotifyEditorClosed();
                _isForceClosing = true;
                Close();
            }
        }

        /// <summary>
        /// 窗口关闭前事件 - 非主持人不能自行关闭
        /// </summary>
        private void OnWindowClosing(object? sender, CancelEventArgs e)
        {
            // 如果是强制关闭（主持人通知），直接允许
            if (_isForceClosing)
            {
                return;
            }

            // 非主持人不能自行关闭窗口
            if (!_isHost)
            {
                e.Cancel = true;
                System.Windows.MessageBox.Show(
                    "在主持人结束协同编辑前，您不能关闭此窗口。",
                    "提示",
                    System.Windows.MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            // 主持人关闭窗口时，确认后发送关闭通知
            var result = System.Windows.MessageBox.Show(
                "关闭编辑器将结束所有用户的协同编辑会话。确定关闭吗？",
                "确认关闭",
                System.Windows.MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                NotifyEditorClosed();
                _isForceClosing = true;
            }
            else
            {
                e.Cancel = true;
            }
        }

        /// <summary>
        /// 发送编辑器关闭通知
        /// </summary>
        private void NotifyEditorClosed()
        {
            EditorClosed?.Invoke(new CloseEditorRequest
            {
                SessionId = _sessionId,
                CloserId = _peerId,
                CloserName = _peerName
            });
        }

        /// <summary>
        /// 强制关闭窗口（由主持人远程通知触发）
        /// </summary>
        public void ForceClose()
        {
            Dispatcher.Invoke(() =>
            {
                _isForceClosing = true;
                Close();
            });
        }

        private void MenuUndo_Click(object sender, RoutedEventArgs e)
        {
            RootTextBox.Undo();
        }

        private void MenuRedo_Click(object sender, RoutedEventArgs e)
        {
            RootTextBox.Redo();
        }

        private void MenuCut_Click(object sender, RoutedEventArgs e)
        {
            RootTextBox.Cut();
        }

        private void MenuCopy_Click(object sender, RoutedEventArgs e)
        {
            RootTextBox.Copy();
        }

        private void MenuPaste_Click(object sender, RoutedEventArgs e)
        {
            RootTextBox.Paste();
        }

        private void MenuSelectAll_Click(object sender, RoutedEventArgs e)
        {
            RootTextBox.SelectAll();
        }

        #endregion

        #region 格式化按钮

        private void BtnBold_Click(object sender, RoutedEventArgs e)
        {
            var current = RootTextBox.Selection.GetPropertyValue(TextElement.FontWeightProperty);
            var newWeight = (current is FontWeight weight && weight == FontWeights.Bold)
                ? FontWeights.Normal
                : FontWeights.Bold;
            RootTextBox.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, newWeight);
        }

        private void BtnItalic_Click(object sender, RoutedEventArgs e)
        {
            var current = RootTextBox.Selection.GetPropertyValue(TextElement.FontStyleProperty);
            var newStyle = (current is FontStyle style && style == FontStyles.Italic)
                ? FontStyles.Normal
                : FontStyles.Italic;
            RootTextBox.Selection.ApplyPropertyValue(TextElement.FontStyleProperty, newStyle);
        }

        private void BtnUnderline_Click(object sender, RoutedEventArgs e)
        {
            var current = RootTextBox.Selection.GetPropertyValue(Inline.TextDecorationsProperty);
            var newDecoration = (current is TextDecorationCollection decorations && decorations.Count > 0)
                ? null
                : TextDecorations.Underline;
            RootTextBox.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, newDecoration);
        }

        private void BtnFont_Click(object sender, RoutedEventArgs e)
        {
            // 简单实现：切换字体
            var fonts = new[] { "Microsoft YaHei", "SimSun", "SimHei", "KaiTi", "Consolas" };
            var current = RootTextBox.Selection.GetPropertyValue(TextElement.FontFamilyProperty) as FontFamily;
            var currentIndex = Array.FindIndex(fonts, f => f == current?.Source);
            var nextIndex = (currentIndex + 1) % fonts.Length;
            RootTextBox.Selection.ApplyPropertyValue(TextElement.FontFamilyProperty, new FontFamily(fonts[nextIndex]));
        }

        private void FontSize_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem menuItem && menuItem.Tag is string sizeStr)
            {
                if (double.TryParse(sizeStr, out var size))
                {
                    RootTextBox.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, size);
                }
            }
        }

        private void BtnAlignLeft_Click(object sender, RoutedEventArgs e)
        {
            RootTextBox.Selection.ApplyPropertyValue(Paragraph.TextAlignmentProperty, TextAlignment.Left);
        }

        private void BtnAlignCenter_Click(object sender, RoutedEventArgs e)
        {
            RootTextBox.Selection.ApplyPropertyValue(Paragraph.TextAlignmentProperty, TextAlignment.Center);
        }

        private void BtnAlignRight_Click(object sender, RoutedEventArgs e)
        {
            RootTextBox.Selection.ApplyPropertyValue(Paragraph.TextAlignmentProperty, TextAlignment.Right);
        }

        private void MenuWordWrap_Click(object sender, RoutedEventArgs e)
        {
            // RichTextBox 默认自动换行，此处可以控制页面宽度
        }

        #endregion

        /// <summary>
        /// 窗口关闭
        /// </summary>
        private void OnWindowClosed(object? sender, EventArgs e)
        {
            _debounceTimer?.Dispose();
        }
    }
}
