using System.Windows;
using System.Windows.Controls;
using Dorisoy.Meeting.Client.Models;

namespace Dorisoy.Meeting.Client.Views;

/// <summary>
/// EmojiPickerWindow.xaml 的交互逻辑
/// </summary>
public partial class EmojiPickerWindow
{
    /// <summary>
    /// 选中的表情
    /// </summary>
    public string? SelectedEmoji { get; private set; }

    public EmojiPickerWindow()
    {
        InitializeComponent();
        LoadEmojis();
    }

    /// <summary>
    /// 加载表情
    /// </summary>
    private void LoadEmojis()
    {
        HandEmojisControl.ItemsSource = CommonEmojis.HandEmojis;
        FaceEmojisControl.ItemsSource = CommonEmojis.FaceEmojis;
        GestureEmojisControl.ItemsSource = CommonEmojis.GestureEmojis;
        HeartEmojisControl.ItemsSource = CommonEmojis.HeartEmojis;
    }

    /// <summary>
    /// 表情按钮点击
    /// </summary>
    private void EmojiButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Content is string emoji)
        {
            SelectedEmoji = emoji;
            DialogResult = true;
            Close();
        }
    }
}
