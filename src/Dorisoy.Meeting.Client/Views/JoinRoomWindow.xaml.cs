using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dorisoy.Meeting.Client.Models;
using Dorisoy.Meeting.Client.ViewModels;
using Wpf.Ui.Controls;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace Dorisoy.Meeting.Client.Views;

/// <summary>
/// JoinRoomWindow.xaml 的交互逻辑
/// </summary>
public partial class JoinRoomWindow
{
    private readonly JoinRoomViewModel _viewModel;
    private bool _isConfirmed;
    private JoinRoomInfo? _joinRoomInfo;
    private TextBox[] _roomDigitBoxes = null!;

    public JoinRoomWindow(JoinRoomViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        
        InitializeComponent();
        
        // 初始化输入框数组
        _roomDigitBoxes = new[] { RoomDigit1, RoomDigit2, RoomDigit3, RoomDigit4, RoomDigit5 };

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
    
    /// <summary>
    /// 房间号输入框预览输入 - 只允许数字
    /// </summary>
    private void RoomDigit_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // 只允许数字
        e.Handled = !char.IsDigit(e.Text, 0);
    }
    
    /// <summary>
    /// 房间号输入框获得焦点 - 自动选择文本
    /// </summary>
    private void RoomDigit_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            // 选择所有文本，方便直接输入覆盖
            textBox.SelectAll();
        }
    }
    
    /// <summary>
    /// 房间号输入框文本变化 - 自动跳转到下一个
    /// </summary>
    private void RoomDigit_TextChanged(object sender, TextChangedEventArgs e)
    {
        // 防止在初始化期间触发
        if (_roomDigitBoxes == null) return;
        if (sender is not TextBox currentBox) return;
        
        // 如果输入了一个数字，自动跳转到下一个输入框
        if (currentBox.Text.Length == 1)
        {
            var currentIndex = Array.IndexOf(_roomDigitBoxes, currentBox);
            if (currentIndex >= 0 && currentIndex < _roomDigitBoxes.Length - 1)
            {
                _roomDigitBoxes[currentIndex + 1].Focus();
            }
        }
    }
}
