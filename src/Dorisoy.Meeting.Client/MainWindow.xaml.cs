using System.ComponentModel;
using System.Windows;
using Dorisoy.Meeting.Client.ViewModels;
using Dorisoy.Meeting.Client.Views;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Dorisoy.Meeting.Client;

/// <summary>
/// 主窗口 - 使用 Wpf.Ui FluentWindow 暗主题风格
/// </summary>
public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        // 监听系统主题变化
        ApplicationThemeManager.Apply(ApplicationTheme.Dark);

        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        // 订阅打开设置请求事件
        _viewModel.OpenSettingsRequested += OnOpenSettingsRequested;
        
        // 订阅打开表情选择器事件
        _viewModel.OpenEmojiPickerRequested += OnOpenEmojiPickerRequested;
        
        // 订阅窗口关闭事件
        Closed += OnWindowClosed;
    }

    /// <summary>
    /// 打开设置窗口
    /// </summary>
    private void OnOpenSettingsRequested()
    {
        var settingPage = new SettingPage(_viewModel);
        settingPage.Owner = this;
        settingPage.ShowDialog();
    }

    /// <summary>
    /// 打开表情选择窗口
    /// </summary>
    private async void OnOpenEmojiPickerRequested()
    {
        var picker = new EmojiPickerWindow { Owner = this };
        if (picker.ShowDialog() == true && !string.IsNullOrEmpty(picker.SelectedEmoji))
        {
            // 发送表情广播
            await _viewModel.SendEmojiReactionAsync(picker.SelectedEmoji);
        }
    }

    /// <summary>
    /// 窗口关闭事件处理 - 清理资源
    /// </summary>
    private async void Window_Closing(object sender, CancelEventArgs e)
    {
        try
        {
            // 取消事件订阅
            _viewModel.OpenSettingsRequested -= OnOpenSettingsRequested;
            _viewModel.OpenEmojiPickerRequested -= OnOpenEmojiPickerRequested;
            
            // 异步清理资源
            await _viewModel.CleanupAsync();
        }
        catch (Exception)
        {
            // 忽略关闭时的异常，确保窗口能正常关闭
        }
    }
    
    /// <summary>
    /// 窗口已关闭事件处理 - 退出应用
    /// </summary>
    private void OnWindowClosed(object? sender, EventArgs e)
    {
        // 主窗口关闭时退出应用
        Application.Current.Shutdown();
    }
}
