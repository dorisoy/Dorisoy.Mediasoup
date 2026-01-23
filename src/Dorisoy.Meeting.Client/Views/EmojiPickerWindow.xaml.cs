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
        
        // 默认显示举手类别
        EmojisControl.ItemsSource = CommonEmojis.HandEmojis;
    }

    /// <summary>
    /// 类别按钮切换
    /// </summary>
    private void CategoryButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton radioButton) return;
        if (EmojisControl == null) return;

        // 根据选中的按钮切换表情列表
        if (radioButton == BtnHand)
        {
            EmojisControl.ItemsSource = CommonEmojis.HandEmojis;
        }
        else if (radioButton == BtnFace)
        {
            EmojisControl.ItemsSource = CommonEmojis.FaceEmojis;
        }
        else if (radioButton == BtnGesture)
        {
            EmojisControl.ItemsSource = CommonEmojis.GestureEmojis;
        }
        else if (radioButton == BtnHeart)
        {
            EmojisControl.ItemsSource = CommonEmojis.HeartEmojis;
        }
        else if (radioButton == BtnSound)
        {
            EmojisControl.ItemsSource = CommonEmojis.SoundEmojis;
        }
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
