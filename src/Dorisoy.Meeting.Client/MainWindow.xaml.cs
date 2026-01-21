using System.ComponentModel;
using System.Windows;
using Dorisoy.Meeting.Client.ViewModels;
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
    }

    /// <summary>
    /// 窗口关闭事件处理 - 清理资源
    /// </summary>
    private async void Window_Closing(object sender, CancelEventArgs e)
    {
        try
        {
            // 异步清理资源
            await _viewModel.CleanupAsync();
        }
        catch (Exception)
        {
            // 忽略关闭时的异常，确保窗口能正常关闭
        }
    }
}
