using Microsoft.Win32;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dorisoy.Meeting.Client.ViewModels;
using Dorisoy.Meeting.Client.Models;

namespace Dorisoy.Meeting.Client.Views;

/// <summary>
/// ChatPanel.xaml 的交互逻辑
/// </summary>
public partial class ChatPanel : UserControl
{
    private MainViewModel? _viewModel;
    private bool _isGroupChat = true;

    public ChatPanel()
    {
        InitializeComponent();
        
        Loaded += ChatPanel_Loaded;
    }

    private void ChatPanel_Loaded(object sender, RoutedEventArgs e)
    {
        _viewModel = DataContext as MainViewModel;
        UpdateChatTarget();
    }

    /// <summary>
    /// 更新聊天对象显示
    /// </summary>
    private void UpdateChatTarget()
    {
        if (_isGroupChat)
        {
            ChatTargetName.Text = "群聊";
            ChatTargetStatus.Text = $"({_viewModel?.Peers.Count ?? 0} 人在线)";
        }
        else if (_viewModel?.SelectedChatUser != null)
        {
            ChatTargetName.Text = _viewModel.SelectedChatUser.DisplayName;
            ChatTargetStatus.Text = _viewModel.SelectedChatUser.IsOnline ? "在线" : "离线";
        }
    }

    /// <summary>
    /// 群聊选项点击
    /// </summary>
    private void GroupChatItem_Click(object sender, MouseButtonEventArgs e)
    {
        _isGroupChat = true;
        UserListBox.SelectedItem = null;
        UpdateChatTarget();
        _viewModel?.SwitchToGroupChat();
    }

    /// <summary>
    /// 用户列表选择变化
    /// </summary>
    private void UserListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (UserListBox.SelectedItem != null)
        {
            _isGroupChat = false;
            UpdateChatTarget();
        }
    }

    /// <summary>
    /// 表情按钮点击
    /// </summary>
    private void EmojiButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new EmojiPickerWindow { Owner = Window.GetWindow(this) };
        if (picker.ShowDialog() == true && !string.IsNullOrEmpty(picker.SelectedEmoji))
        {
            // 将表情添加到输入框
            MessageInput.Text += picker.SelectedEmoji;
            MessageInput.Focus();
            MessageInput.CaretIndex = MessageInput.Text.Length;
        }
    }

    /// <summary>
    /// 图片按钮点击
    /// </summary>
    private void ImageButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择图片",
            Filter = "图片文件|*.jpg;*.jpeg;*.png;*.gif;*.bmp|所有文件|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            _viewModel?.SendImageMessage(dialog.FileName, _isGroupChat ? null : _viewModel.SelectedChatUser?.PeerId);
        }
    }

    /// <summary>
    /// 文件按钮点击
    /// </summary>
    private void FileButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择文件",
            Filter = "所有文件|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            _viewModel?.SendFileMessage(dialog.FileName, _isGroupChat ? null : _viewModel.SelectedChatUser?.PeerId);
        }
    }

    /// <summary>
    /// 文件消息点击 - 保存文件
    /// </summary>
    private async void FileMessage_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ChatMessage message)
        {
            // 优先使用下载 URL（大文件）
            if (!string.IsNullOrEmpty(message.DownloadUrl))
            {
                await DownloadFileFromUrlAsync(message.DownloadUrl, message.FileName ?? "file");
            }
            // 如果有 Base64 数据，允许保存
            else if (!string.IsNullOrEmpty(message.FileData))
            {
                var dialog = new SaveFileDialog
                {
                    Title = "保存文件",
                    FileName = message.FileName ?? "file",
                    Filter = "所有文件|*.*"
                };

                if (dialog.ShowDialog() == true)
                {
                    try
                    {
                        var fileBytes = Convert.FromBase64String(message.FileData);
                        System.IO.File.WriteAllBytes(dialog.FileName, fileBytes);
                        MessageBox.Show($"文件已保存到:\n{dialog.FileName}", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"保存文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            // 如果是自己发送的，使用本地文件路径
            else if (!string.IsNullOrEmpty(message.FilePath) && System.IO.File.Exists(message.FilePath))
            {
                try
                {
                    // 打开文件所在文件夹
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{message.FilePath}\"");
                }
                catch
                {
                    // 忽略打开失败
                }
            }
        }
    }

    /// <summary>
    /// 从 URL 下载文件
    /// </summary>
    private async Task DownloadFileFromUrlAsync(string url, string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "保存文件",
            FileName = suggestedFileName,
            Filter = "所有文件|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                // 显示下载进度（简单提示）
                if (_viewModel != null)
                {
                    _viewModel.StatusMessage = $"正在下载文件: {suggestedFileName}...";
                }

                using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
                var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                await using var contentStream = await response.Content.ReadAsStreamAsync();
                await using var fileStream = new System.IO.FileStream(dialog.FileName, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None);
                
                // 复制数据（带进度报告）
                var buffer = new byte[81920]; // 80KB buffer
                var totalBytesRead = 0L;
                var contentLength = response.Content.Headers.ContentLength ?? -1L;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalBytesRead += bytesRead;

                    // 更新进度
                    if (contentLength > 0 && _viewModel != null)
                    {
                        var progress = (double)totalBytesRead / contentLength * 100;
                        _viewModel.StatusMessage = $"正在下载文件: {suggestedFileName} ({progress:F0}%)";
                    }
                }

                if (_viewModel != null)
                {
                    _viewModel.StatusMessage = $"文件已保存: {suggestedFileName}";
                }

                MessageBox.Show($"文件已保存到:\n{dialog.FileName}", "下载完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                if (_viewModel != null)
                {
                    _viewModel.StatusMessage = $"下载失败: {ex.Message}";
                }
                MessageBox.Show($"下载文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// 发送按钮点击
    /// </summary>
    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        SendMessage();
    }

    /// <summary>
    /// 输入框按键事件
    /// </summary>
    private void MessageInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            SendMessage();
            e.Handled = true;
        }
    }

    /// <summary>
    /// 发送消息
    /// </summary>
    private void SendMessage()
    {
        var text = MessageInput.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        var receiverId = _isGroupChat ? null : _viewModel?.SelectedChatUser?.PeerId;
        _viewModel?.SendTextMessage(text, receiverId);

        MessageInput.Text = string.Empty;
        MessageInput.Focus();

        // 滚动到底部
        MessagesScrollViewer.ScrollToEnd();
    }
}
