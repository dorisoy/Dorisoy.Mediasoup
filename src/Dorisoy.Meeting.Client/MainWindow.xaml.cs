using System.ComponentModel;
using System.Windows;
using Dorisoy.Meeting.Client.ViewModels;
using Dorisoy.Meeting.Client.Views;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Dorisoy.Meeting.Client;

/// <summary>
/// 主窗口 - 使用 Wpf.Ui FluentWindow 暗主题风格
/// </summary>
public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;
    private bool _isReturningToJoinRoom;
    private WindowState _previousWindowState;
    private WindowStyle _previousWindowStyle;
    private bool _previousTopmost;

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
        
        // 订阅返回加入房间事件
        _viewModel.ReturnToJoinRoomRequested += OnReturnToJoinRoomRequested;
        
        // 订阅全屏请求事件
        _viewModel.FullScreenRequested += OnFullScreenRequested;
        
        // 订阅打开分享房间窗口事件
        _viewModel.OpenShareRoomWindowRequested += OnOpenShareRoomWindowRequested;
        
        // 订阅窗口关闭事件
        Closed += OnWindowClosed;
        
        // 订阅键盘事件用于 Esc 退出全屏
        KeyDown += OnWindowKeyDown;
    }
    
    /// <summary>
    /// 全屏请求处理
    /// </summary>
    private void OnFullScreenRequested(bool isFullScreen)
    {
        if (isFullScreen)
        {
            // 保存当前状态
            _previousWindowState = WindowState;
            _previousWindowStyle = WindowStyle;
            _previousTopmost = Topmost;
            
            // 进入全屏
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            Topmost = true;
            
            // 隐藏标题栏
            TitleBar.Visibility = Visibility.Collapsed;
        }
        else
        {
            // 恢复之前的状态
            WindowStyle = _previousWindowStyle;
            WindowState = _previousWindowState;
            Topmost = _previousTopmost;
            
            // 显示标题栏
            TitleBar.Visibility = Visibility.Visible;
        }
    }
    
    /// <summary>
    /// 键盘事件 - Esc 退出全屏
    /// </summary>
    private void OnWindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape && _viewModel.IsFullScreen)
        {
            _viewModel.FullScreenCommand.Execute(null);
        }
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
    /// 打开分享房间窗口（二维码）
    /// </summary>
    private void OnOpenShareRoomWindowRequested()
    {
        var shareWindow = new ShareRoomWindow(_viewModel.RoomId, _viewModel.ServerUrl)
        {
            Owner = this
        };
        shareWindow.ShowDialog();
    }
    
    /// <summary>
    /// 返回加入房间窗口
    /// </summary>
    private void OnReturnToJoinRoomRequested()
    {
        _isReturningToJoinRoom = true;
        
        Application.Current.Dispatcher.Invoke(() =>
        {
            // 隐藏主窗口
            this.Hide();
            
            // 创建并显示 JoinRoomWindow
            var joinRoomViewModel = App.ServiceProvider?.GetRequiredService<JoinRoomViewModel>();
            if (joinRoomViewModel != null)
            {
                var joinRoomWindow = new JoinRoomWindow(joinRoomViewModel);
                joinRoomWindow.ShowDialog();
                
                if (joinRoomWindow.IsConfirmed && joinRoomWindow.JoinRoomInfo != null)
                {
                    // 用户确认加入，重新显示主窗口并加入房间
                    this.WindowState = WindowState.Maximized;
                    this.Show();
                    
                    // 自动加入房间
                    _ = _viewModel.AutoJoinAsync(joinRoomWindow.JoinRoomInfo);
                    _isReturningToJoinRoom = false;
                }
                else
                {
                    // 用户取消，关闭应用
                    Application.Current.Shutdown();
                }
            }
        });
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
            _viewModel.ReturnToJoinRoomRequested -= OnReturnToJoinRoomRequested;
            _viewModel.FullScreenRequested -= OnFullScreenRequested;
            _viewModel.OpenShareRoomWindowRequested -= OnOpenShareRoomWindowRequested;
            KeyDown -= OnWindowKeyDown;
            
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
        // 如果不是返回加入房间的情况，才退出应用
        if (!_isReturningToJoinRoom)
        {
            Application.Current.Shutdown();
        }
    }
}
