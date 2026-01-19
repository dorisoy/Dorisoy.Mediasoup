using System.Windows;
using Dorisoy.Meeting.Client.Models;
using Dorisoy.Meeting.Client.ViewModels;

namespace Dorisoy.Meeting.Client.Views;

/// <summary>
/// JoinRoomWindow.xaml 的交互逻辑
/// </summary>
public partial class JoinRoomWindow
{
    private readonly JoinRoomViewModel _viewModel;
    private bool _isConfirmed;
    private JoinRoomInfo? _joinRoomInfo;

    public JoinRoomWindow(JoinRoomViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        
        InitializeComponent();

        // 订阅关闭请求事件
        _viewModel.RequestClose += OnRequestClose;
        
        // 窗口关闭时清理资源
        Closed += async (s, e) =>
        {
            _viewModel.RequestClose -= OnRequestClose;
            await _viewModel.CleanupAsync();
        };
    }

    /// <summary>
    /// 是否确认加入
    /// </summary>
    public bool IsConfirmed => _isConfirmed;

    /// <summary>
    /// 获取加入房间信息
    /// </summary>
    public JoinRoomInfo? JoinRoomInfo => _joinRoomInfo;

    /// <summary>
    /// 处理关闭请求
    /// </summary>
    private void OnRequestClose(bool result)
    {
        _isConfirmed = result;
        
        // 在关闭窗口之前获取加入信息，确保数据不会丢失
        if (_isConfirmed)
        {
            _joinRoomInfo = _viewModel.GetJoinRoomInfo();
        }
        
        // 对于 FluentWindow，直接关闭窗口，不使用 DialogResult
        Close();
    }
}
