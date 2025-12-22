using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Dorisoy.Meeting.Client.Views;

/// <summary>
/// 视频面板控件 - 显示本地或远端视频
/// </summary>
public partial class VideoPanel : UserControl
{
    /// <summary>
    /// 视频源依赖属性
    /// </summary>
    public static readonly DependencyProperty VideoSourceProperty =
        DependencyProperty.Register(
            nameof(VideoSource),
            typeof(WriteableBitmap),
            typeof(VideoPanel),
            new PropertyMetadata(null, OnVideoSourceChanged));

    /// <summary>
    /// 用户名依赖属性
    /// </summary>
    public static readonly DependencyProperty UserNameProperty =
        DependencyProperty.Register(
            nameof(UserName),
            typeof(string),
            typeof(VideoPanel),
            new PropertyMetadata("User", OnUserNameChanged));

    /// <summary>
    /// 占位符文本依赖属性
    /// </summary>
    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.Register(
            nameof(Placeholder),
            typeof(string),
            typeof(VideoPanel),
            new PropertyMetadata("无视频", OnPlaceholderChanged));

    /// <summary>
    /// 视频源
    /// </summary>
    public WriteableBitmap? VideoSource
    {
        get => (WriteableBitmap?)GetValue(VideoSourceProperty);
        set => SetValue(VideoSourceProperty, value);
    }

    /// <summary>
    /// 用户名
    /// </summary>
    public string UserName
    {
        get => (string)GetValue(UserNameProperty);
        set => SetValue(UserNameProperty, value);
    }

    /// <summary>
    /// 占位符文本
    /// </summary>
    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public VideoPanel()
    {
        InitializeComponent();
    }

    private static void OnVideoSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VideoPanel panel)
        {
            panel.VideoImage.Source = e.NewValue as WriteableBitmap;
            panel.PlaceholderText.Visibility = e.NewValue == null 
                ? Visibility.Visible 
                : Visibility.Collapsed;
        }
    }

    private static void OnUserNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VideoPanel panel)
        {
            panel.UserNameText.Text = e.NewValue?.ToString() ?? "User";
        }
    }

    private static void OnPlaceholderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VideoPanel panel)
        {
            panel.PlaceholderText.Text = e.NewValue?.ToString() ?? "无视频";
        }
    }
}
