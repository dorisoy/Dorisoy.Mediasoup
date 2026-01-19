using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using Dorisoy.Meeting.Client.Models;
using Dorisoy.Meeting.Client.Services;

namespace Dorisoy.Meeting.Client.ViewModels;

/// <summary>
/// 加入房间视图模型
/// </summary>
public partial class JoinRoomViewModel : ObservableObject
{
    private readonly ILogger<JoinRoomViewModel> _logger;
    private readonly IWebRtcService _webRtcService;
    private System.Threading.CancellationTokenSource? _previewCts;

    #region 可观察属性

    /// <summary>
    /// 房间号码 (6位数字)
    /// </summary>
    [ObservableProperty]
    private string _roomId = string.Empty;

    /// <summary>
    /// 用户名
    /// </summary>
    [ObservableProperty]
    private string _userName = string.Empty;

    /// <summary>
    /// 服务器地址
    /// </summary>
    [ObservableProperty]
    private string _serverUrl = "http://192.168.0.3:9000";

    /// <summary>
    /// 摄像头预览帧
    /// </summary>
    [ObservableProperty]
    private WriteableBitmap? _cameraPreviewFrame;

    /// <summary>
    /// 加入后是否禁用麦克风
    /// </summary>
    [ObservableProperty]
    private bool _muteMicrophoneOnJoin;

    /// <summary>
    /// 加入后是否禁用摄像头
    /// </summary>
    [ObservableProperty]
    private bool _muteCameraOnJoin;

    /// <summary>
    /// 是否正在预览
    /// </summary>
    [ObservableProperty]
    private bool _isPreviewing;

    /// <summary>
    /// 是否正在加载设备
    /// </summary>
    [ObservableProperty]
    private bool _isLoadingDevices;

    /// <summary>
    /// 可加入 - 房间号和用户名都有效
    /// </summary>
    public bool CanJoin => !string.IsNullOrWhiteSpace(RoomId) && 
                           RoomId.Length == 6 && 
                           !string.IsNullOrWhiteSpace(UserName);

    /// <summary>
    /// 对话结果 - 是否确认加入
    /// </summary>
    [ObservableProperty]
    private bool _dialogResult;

    #endregion

    #region 集合属性

    /// <summary>
    /// 可用摄像头列表
    /// </summary>
    public ObservableCollection<MediaDeviceInfo> Cameras { get; } = [];

    /// <summary>
    /// 可用麦克风列表
    /// </summary>
    public ObservableCollection<MediaDeviceInfo> Microphones { get; } = [];

    /// <summary>
    /// 选中的摄像头
    /// </summary>
    [ObservableProperty]
    private MediaDeviceInfo? _selectedCamera;

    /// <summary>
    /// 选中的麦克风
    /// </summary>
    [ObservableProperty]
    private MediaDeviceInfo? _selectedMicrophone;

    #endregion

    #region 事件

    /// <summary>
    /// 请求关闭窗口事件
    /// </summary>
    public event Action<bool>? RequestClose;

    #endregion

    #region 构造函数

    public JoinRoomViewModel(
        ILogger<JoinRoomViewModel> logger,
        IWebRtcService webRtcService)
    {
        _logger = logger;
        _webRtcService = webRtcService;

        // 生成随机6位数字房间号
        GenerateRandomRoomId();

        // 订阅视频帧事件
        _webRtcService.OnLocalVideoFrame += OnLocalVideoFrameReceived;

        // 初始化加载设备
        _ = LoadDevicesAsync();
    }

    #endregion

    #region 命令

    /// <summary>
    /// 生成随机房间号
    /// </summary>
    [RelayCommand]
    private void GenerateRandomRoomId()
    {
        var random = new Random();
        RoomId = random.Next(100000, 999999).ToString();
    }

    /// <summary>
    /// 刷新设备列表
    /// </summary>
    [RelayCommand]
    private async Task RefreshDevicesAsync()
    {
        await LoadDevicesAsync();
    }

    /// <summary>
    /// 开始/停止预览
    /// </summary>
    [RelayCommand]
    private async Task TogglePreviewAsync()
    {
        if (IsPreviewing)
        {
            await StopPreviewAsync();
        }
        else
        {
            await StartPreviewAsync();
        }
    }

    /// <summary>
    /// 加入房间
    /// </summary>
    [RelayCommand]
    private async Task JoinAsync()
    {
        if (!CanJoin) return;

        // 停止预览
        await StopPreviewAsync();

        DialogResult = true;
        RequestClose?.Invoke(true);
    }

    /// <summary>
    /// 取消
    /// </summary>
    [RelayCommand]
    private async Task CancelAsync()
    {
        await StopPreviewAsync();
        DialogResult = false;
        RequestClose?.Invoke(false);
    }

    #endregion

    #region 属性变更处理

    partial void OnRoomIdChanged(string value)
    {
        OnPropertyChanged(nameof(CanJoin));
    }

    partial void OnUserNameChanged(string value)
    {
        OnPropertyChanged(nameof(CanJoin));
    }

    partial void OnSelectedCameraChanged(MediaDeviceInfo? value)
    {
        if (value != null && IsPreviewing)
        {
            _ = SwitchCameraAsync(value.DeviceId);
        }
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 加载设备列表
    /// </summary>
    private async Task LoadDevicesAsync()
    {
        if (IsLoadingDevices) return;

        IsLoadingDevices = true;
        try
        {
            _logger.LogDebug("加载媒体设备...");

            // 获取摄像头列表
            var cameras = await _webRtcService.GetCamerasAsync();
            Cameras.Clear();
            foreach (var camera in cameras)
            {
                Cameras.Add(camera);
            }
            if (Cameras.Count > 0 && SelectedCamera == null)
            {
                SelectedCamera = Cameras[0];
            }

            // 获取麦克风列表
            var microphones = await _webRtcService.GetMicrophonesAsync();
            Microphones.Clear();
            foreach (var mic in microphones)
            {
                Microphones.Add(mic);
            }
            if (Microphones.Count > 0 && SelectedMicrophone == null)
            {
                SelectedMicrophone = Microphones[0];
            }

            _logger.LogInformation("加载了 {CameraCount} 个摄像头, {MicCount} 个麦克风",
                Cameras.Count, Microphones.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载媒体设备失败");
        }
        finally
        {
            IsLoadingDevices = false;
        }
    }

    /// <summary>
    /// 开始摄像头预览
    /// </summary>
    private async Task StartPreviewAsync()
    {
        if (IsPreviewing || SelectedCamera == null) return;

        try
        {
            _logger.LogInformation("开始摄像头预览: {DeviceId}", SelectedCamera.DeviceId);
            
            _previewCts = new System.Threading.CancellationTokenSource();
            await _webRtcService.StartCameraAsync(SelectedCamera.DeviceId);
            IsPreviewing = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动摄像头预览失败");
            IsPreviewing = false;
        }
    }

    /// <summary>
    /// 停止摄像头预览
    /// </summary>
    private async Task StopPreviewAsync()
    {
        if (!IsPreviewing) return;

        try
        {
            _previewCts?.Cancel();
            await _webRtcService.StopCameraAsync();
            IsPreviewing = false;
            CameraPreviewFrame = null;
            _logger.LogInformation("摄像头预览已停止");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "停止摄像头预览时出错");
        }
    }

    /// <summary>
    /// 切换摄像头设备
    /// </summary>
    private async Task SwitchCameraAsync(string deviceId)
    {
        try
        {
            await _webRtcService.StopCameraAsync();
            await _webRtcService.StartCameraAsync(deviceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换摄像头失败");
        }
    }

    /// <summary>
    /// 收到本地视频帧
    /// </summary>
    private void OnLocalVideoFrameReceived(WriteableBitmap frame)
    {
        if (IsPreviewing)
        {
            CameraPreviewFrame = frame;
        }
    }

    /// <summary>
    /// 获取加入房间信息
    /// </summary>
    public JoinRoomInfo GetJoinRoomInfo()
    {
        return new JoinRoomInfo
        {
            RoomId = RoomId,
            UserName = UserName,
            ServerUrl = ServerUrl,
            CameraDeviceId = SelectedCamera?.DeviceId,
            MicrophoneDeviceId = SelectedMicrophone?.DeviceId,
            MuteMicrophoneOnJoin = MuteMicrophoneOnJoin,
            MuteCameraOnJoin = MuteCameraOnJoin
        };
    }

    /// <summary>
    /// 清理资源
    /// </summary>
    public async Task CleanupAsync()
    {
        _webRtcService.OnLocalVideoFrame -= OnLocalVideoFrameReceived;
        await StopPreviewAsync();
        _previewCts?.Dispose();
    }

    #endregion
}
