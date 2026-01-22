using System.ComponentModel;
using System.Windows;
using Dorisoy.Meeting.Client.Models;
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
    private VoteWindow? _currentVoteWindow; // 当前打开的投票窗口引用

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
        
        // 订阅打开同步转译窗口事件
        _viewModel.OpenTranslateWindowRequested += OnOpenTranslateWindowRequested;
        
        // 订阅打开投票窗口事件
        _viewModel.OpenPollRequested += OnOpenPollRequested;
        _viewModel.VoteCreatedReceived += OnVoteCreatedReceived;
        
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
    /// 打开同步转译窗口
    /// </summary>
    private void OnOpenTranslateWindowRequested()
    {
        var translateWindow = new TranslateWindow
        {
            Owner = this
        };
        
        // 窗口关闭时重置转译状态
        translateWindow.Closed += (s, e) =>
        {
            _viewModel.IsTranslateEnabled = false;
        };
        
        translateWindow.Show();
    }

    /// <summary>
    /// 主持人打开投票窗口
    /// </summary>
    private void OnOpenPollRequested()
    {
        // 如果已有投票窗口打开，则激活它
        if (_currentVoteWindow != null && _currentVoteWindow.IsLoaded)
        {
            _currentVoteWindow.Activate();
            return;
        }
        
        OpenVoteWindow(null);
    }

    /// <summary>
    /// 参与者收到投票创建通知
    /// </summary>
    private void OnVoteCreatedReceived(Vote vote)
    {
        Dispatcher.Invoke(() =>
        {
            // 如果是主持人且已有投票窗口打开，直接更新现有窗口内容
            if (_viewModel.IsHost && _currentVoteWindow != null && _currentVoteWindow.IsLoaded)
            {
                // 更新现有窗口的投票内容
                _currentVoteWindow.SetVote(vote);
                return;
            }
            
            // 非主持人或者没有投票窗口打开，则新建窗口
            OpenVoteWindow(vote);
        });
    }

    /// <summary>
    /// 打开投票窗口
    /// </summary>
    private void OpenVoteWindow(Vote? existingVote)
    {
        // 如果已有窗口打开，先关闭
        if (_currentVoteWindow != null && _currentVoteWindow.IsLoaded)
        {
            // 先取消事件绑定
            _currentVoteWindow.Close();
        }
        
        var voteWindow = new VoteWindow(
            _viewModel.CurrentPeerId,
            _viewModel.CurrentUserName,
            _viewModel.IsHost,
            existingVote
        )
        {
            Owner = this
        };
        
        // 保存窗口引用
        _currentVoteWindow = voteWindow;
        
        // 窗口关闭时清理引用
        voteWindow.Closed += (s, e) =>
        {
            if (_currentVoteWindow == voteWindow)
            {
                _currentVoteWindow = null;
            }
        };
        
        // 绑定投票事件
        voteWindow.VoteCreated += async (vote) =>
        {
            await _viewModel.CreateVoteAsync(vote);
        };
        
        voteWindow.VoteSubmitted += async (voteId, optionIndex) =>
        {
            await _viewModel.SubmitVoteAsync(voteId, optionIndex);
        };
        
        voteWindow.VoteDeleted += async (voteId) =>
        {
            await _viewModel.DeleteVoteAsync(voteId);
        };
        
        // 绑定投票结果更新事件
        _viewModel.VoteResultUpdated += (submitData) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (voteWindow.CurrentVote?.Id == submitData.VoteId)
                {
                    // 窗口内部已经通过数据绑定更新，无需额外处理
                }
            });
        };
        
        // 绑定投票删除事件
        _viewModel.VoteDeletedReceived += (voteId) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (voteWindow.CurrentVote?.Id == voteId)
                {
                    voteWindow.Close();
                }
            });
        };
        
        voteWindow.Show();
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
            _viewModel.OpenTranslateWindowRequested -= OnOpenTranslateWindowRequested;
            _viewModel.OpenPollRequested -= OnOpenPollRequested;
            _viewModel.VoteCreatedReceived -= OnVoteCreatedReceived;
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
