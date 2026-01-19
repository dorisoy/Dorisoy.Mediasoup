using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dorisoy.Meeting.Client.ViewModels;

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
