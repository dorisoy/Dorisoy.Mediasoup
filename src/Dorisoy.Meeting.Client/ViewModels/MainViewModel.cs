using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using Dorisoy.Meeting.Client.Models;
using Dorisoy.Meeting.Client.Models.Notifications;
using Dorisoy.Meeting.Client.Services;

namespace Dorisoy.Meeting.Client.ViewModels;

/// <summary>
/// 主视图模型 - 处理会议的核心逻辑
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ILogger<MainViewModel> _logger;
    private readonly ISignalRService _signalRService;
    private readonly IWebRtcService _webRtcService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    #region 可观察属性

    /// <summary>
    /// 是否已连接
    /// </summary>
    [ObservableProperty]
    private bool _isConnected;

    /// <summary>
    /// 是否已加入房间
    /// </summary>
    [ObservableProperty]
    private bool _isJoinedRoom;

    /// <summary>
    /// 服务器地址
    /// </summary>
    [ObservableProperty]
    private string _serverUrl = "http://192.168.30.8:9000";

    /// <summary>
    /// 选中的 Peer 索引
    /// </summary>
    [ObservableProperty]
    private int _selectedPeerIndex;

    /// <summary>
    /// 选中的房间索引
    /// </summary>
    [ObservableProperty]
    private int _selectedRoomIndex;

    /// <summary>
    /// 服务模式
    /// </summary>
    [ObservableProperty]
    private string _serveMode = "Open";

    /// <summary>
    /// 状态消息
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = "未连接";

    /// <summary>
    /// 是否开启摄像头
    /// </summary>
    [ObservableProperty]
    private bool _isCameraEnabled;

    /// <summary>
    /// 是否开启麦克风
    /// </summary>
    [ObservableProperty]
    private bool _isMicrophoneEnabled;

    /// <summary>
    /// 本地视频帧
    /// </summary>
    [ObservableProperty]
    private WriteableBitmap? _localVideoFrame;

    /// <summary>
    /// 是否正在处理中
    /// </summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// 是否可以加入房间 - 已连接且不在处理中
    /// </summary>
    public bool CanJoinRoom => IsConnected && !IsBusy;

    /// <summary>
    /// 是否可以切换媒体 - 已加入房间且不在处理中
    /// </summary>
    public bool CanToggleMedia => IsJoinedRoom && !IsBusy;

    #endregion

    #region 集合属性

    /// <summary>
    /// 房间内 Peer 列表
    /// </summary>
    public ObservableCollection<PeerInfo> Peers { get; } = [];

    /// <summary>
    /// 房间列表
    /// </summary>
    public ObservableCollection<string> Rooms { get; } =
        ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9"];

    /// <summary>
    /// 远端视频流字典
    /// </summary>
    public ObservableCollection<RemoteVideoItem> RemoteVideos { get; } = [];

    /// <summary>
    /// 是否没有远端视频
    /// </summary>
    [ObservableProperty]
    private bool _hasNoRemoteVideos = true;

    /// <summary>
    /// 侧边栏是否可见
    /// </summary>
    [ObservableProperty]
    private bool _isSidebarVisible = true;

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

    /// <summary>
    /// 可用的视频质量预设列表
    /// </summary>
    public VideoQualitySettings[] VideoQualityPresets { get; } = VideoQualitySettings.Presets;

    /// <summary>
    /// 选中的视频质量配置
    /// </summary>
    [ObservableProperty]
    private VideoQualitySettings _selectedVideoQuality = VideoQualitySettings.GetPreset(VideoQualityPreset.High);
    
    /// <summary>
    /// 可用的视频编解码器列表
    /// </summary>
    public VideoCodecInfo[] VideoCodecs { get; } = VideoCodecInfo.AvailableCodecs;
    
    /// <summary>
    /// 选中的视频编解码器
    /// </summary>
    [ObservableProperty]
    private VideoCodecInfo _selectedVideoCodec = VideoCodecInfo.AvailableCodecs[0]; // 默认 VP8

    #endregion

    #region 私有字段

    /// <summary>
    /// 预设的测试 Token - 2024-12-22 生成，有效期 300 天
    /// </summary>
    private readonly string[] _accessTokens =
    [
        // Peer 0
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1lIjoiMCIsIm5iZiI6MTc2NjM3MDM0MiwiZXhwIjoxNzkyMjkwMzQyLCJpc3MiOiJpc3N1ZXIiLCJhdWQiOiJhdWRpZW5jZSJ9.jOYQxKv8b_dQ04HlaOWE_wKEPyD6cjqHbY315q6vbt8",
        // Peer 1
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1lIjoiMSIsIm5iZiI6MTc2NjM3MDM0MiwiZXhwIjoxNzkyMjkwMzQyLCJpc3MiOiJpc3N1ZXIiLCJhdWQiOiJhdWRpZW5jZSJ9.ebWA7vkeQZyw3r6EpkL9gcrcO5hvfNPVWNdgY8FDBmM",
        // Peer 2
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1lIjoiMiIsIm5iZiI6MTc2NjM3MDM0MiwiZXhwIjoxNzkyMjkwMzQyLCJpc3MiOiJpc3N1ZXIiLCJhdWQiOiJhdWRpZW5jZSJ9.9kDOHUQ981zO_NvEG0OHvXS1g4id-DdPyQhtDhgGoEg",
        // Peer 3
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1lIjoiMyIsIm5iZiI6MTc2NjM3MDM0MiwiZXhwIjoxNzkyMjkwMzQyLCJpc3MiOiJpc3N1ZXIiLCJhdWQiOiJhdWRpZW5jZSJ9.lP0Ip4UjLd5YkDgFCV1hEHCbP4M2QvsTL4FcpICqP-k",
        // Peer 4
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1lIjoiNCIsIm5iZiI6MTc2NjM3MDM0MiwiZXhwIjoxNzkyMjkwMzQyLCJpc3MiOiJpc3N1ZXIiLCJhdWQiOiJhdWRpZW5jZSJ9.8PoprZl9sbL9GNqnnq1m9PoNyGZdPUN0vZRlvKGvGMg",
        // Peer 5
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1lIjoiNSIsIm5iZiI6MTc2NjM3MDM0MiwiZXhwIjoxNzkyMjkwMzQyLCJpc3MiOiJpc3N1ZXIiLCJhdWQiOiJhdWRpZW5jZSJ9.RJwY5X-6UROHy-nnkXPMJjGT4cgJxnMshxAvNnevvk8",
        // Peer 6
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1lIjoiNiIsIm5iZiI6MTc2NjM3MDM0MiwiZXhwIjoxNzkyMjkwMzQyLCJpc3MiOiJpc3N1ZXIiLCJhdWQiOiJhdWRpZW5jZSJ9.9BvxRplgwzfCCSabCszQ_Jmu9sxzKWpeA0CYtR1HmmM",
        // Peer 7
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1lIjoiNyIsIm5iZiI6MTc2NjM3MDM0MiwiZXhwIjoxNzkyMjkwMzQyLCJpc3MiOiJpc3N1ZXIiLCJhdWQiOiJhdWRpZW5jZSJ9.3hVVcQ4o5_iR-mhdwjOldCheO2ib8_YC7kbIzfyhuSg",
        // Peer 8 (Admin)
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1lIjoiOCIsIm5iZiI6MTc2NjM3MDM0MiwiZXhwIjoxNzkyMjkwMzQyLCJpc3MiOiJpc3N1ZXIiLCJhdWQiOiJhdWRpZW5jZSJ9.zYmqT6G87Ucpegewrr9HPqCrnyAwk3-7iSXW81_Jkls",
        // Peer 9 (Admin)
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1lIjoiOSIsIm5iZiI6MTc2NjM3MDM0MiwiZXhwIjoxNzkyMjkwMzQyLCJpc3MiOiJpc3N1ZXIiLCJhdWQiOiJhdWRpZW5jZSJ9.hGMms3iLeuabuPp6RBWsAOWmnxJ3s_ltC2z2CR-W69g"
    ];

    private object? _routerRtpCapabilities;
    private string? _sendTransportId;
    private string? _recvTransportId;
    private string? _videoProducerId;
    private string? _audioProducerId;

    /// <summary>
    /// 待恢复的 Consumer ID 列表 - 等待 Transport DTLS 连接后再 Resume
    /// </summary>
    private readonly List<string> _pendingResumeConsumers = new();

    #endregion

    #region 构造函数

    public MainViewModel(
        ILogger<MainViewModel> logger,
        ISignalRService signalRService,
        IWebRtcService webRtcService)
    {
        _logger = logger;
        _signalRService = signalRService;
        _webRtcService = webRtcService;

        // 订阅事件
        _signalRService.OnNotification += HandleNotification;
        _signalRService.OnConnected += OnSignalRConnected;
        _signalRService.OnDisconnected += OnSignalRDisconnected;

        _webRtcService.OnLocalVideoFrame += OnLocalVideoFrameReceived;
        _webRtcService.OnRemoteVideoFrame += OnRemoteVideoFrameReceived;
        _webRtcService.OnConnectionStateChanged += OnWebRtcStateChanged;

        // 订阅 recv transport DTLS 连接完成事件 - 在这之后才能 Resume Consumer
        _webRtcService.OnRecvTransportDtlsConnected += OnRecvTransportDtlsConnected;

        // 初始化视频质量配置
        _webRtcService.VideoQuality = SelectedVideoQuality;

        // 初始化时加载设备列表
        _ = LoadDevicesAsync();
    }

    #endregion

    #region 命令

    /// <summary>
    /// 清理资源 - 窗口关闭时调用
    /// </summary>
    public async Task CleanupAsync()
    {
        _logger.LogInformation("Cleaning up resources...");

        try
        {
            // 取消事件订阅
            _signalRService.OnNotification -= HandleNotification;
            _signalRService.OnConnected -= OnSignalRConnected;
            _signalRService.OnDisconnected -= OnSignalRDisconnected;

            _webRtcService.OnLocalVideoFrame -= OnLocalVideoFrameReceived;
            _webRtcService.OnRemoteVideoFrame -= OnRemoteVideoFrameReceived;
            _webRtcService.OnConnectionStateChanged -= OnWebRtcStateChanged;

            // 关闭 WebRTC 服务
            await _webRtcService.CloseAsync();

            // 断开 SignalR 连接
            await _signalRService.DisconnectAsync();

            _logger.LogInformation("Resources cleaned up successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cleanup");
        }
    }

    /// <summary>
    /// 连接/断开服务器
    /// </summary>
    [RelayCommand]
    private async Task ToggleConnectionAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        try
        {
            if (IsConnected)
            {
                await DisconnectAsync();
            }
            else
            {
                await ConnectAsync();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 加入/离开房间
    /// </summary>
    [RelayCommand]
    private async Task ToggleRoomAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        try
        {
            if (IsJoinedRoom)
            {
                await LeaveRoomAsync();
            }
            else
            {
                await JoinRoomAsync();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 加载设备列表
    /// </summary>
    private async Task LoadDevicesAsync()
    {
        try
        {
            _logger.LogDebug("Loading media devices...");

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

            _logger.LogInformation("Loaded {CameraCount} cameras, {MicCount} microphones",
                Cameras.Count, Microphones.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load media devices");
        }
    }

    /// <summary>
    /// 刷新设备列表命令
    /// </summary>
    [RelayCommand]
    private async Task RefreshDevicesAsync()
    {
        await LoadDevicesAsync();
        StatusMessage = "设备列表已刷新";
    }

    /// <summary>
    /// 切换侧边栏可见性
    /// </summary>
    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarVisible = !IsSidebarVisible;
    }

    /// <summary>
    /// 切换摄像头设备
    /// </summary>
    partial void OnSelectedCameraChanged(MediaDeviceInfo? value)
    {
        if (value == null || !IsCameraEnabled) return;

        // 如果摄像头正在运行，切换到新设备
        _ = SwitchCameraAsync(value.DeviceId);
    }

    /// <summary>
    /// 切换麦克风设备
    /// </summary>
    partial void OnSelectedMicrophoneChanged(MediaDeviceInfo? value)
    {
        if (value == null || !IsMicrophoneEnabled) return;

        // 如果麦克风正在运行，切换到新设备
        _ = SwitchMicrophoneAsync(value.DeviceId);
    }

    /// <summary>
    /// IsBusy 属性变化时通知相关计算属性
    /// </summary>
    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanJoinRoom));
        OnPropertyChanged(nameof(CanToggleMedia));
    }

    /// <summary>
    /// IsConnected 属性变化时通知相关计算属性
    /// </summary>
    partial void OnIsConnectedChanged(bool value)
    {
        OnPropertyChanged(nameof(CanJoinRoom));
    }

    /// <summary>
    /// IsJoinedRoom 属性变化时通知相关计算属性
    /// </summary>
    partial void OnIsJoinedRoomChanged(bool value)
    {
        OnPropertyChanged(nameof(CanToggleMedia));
    }

    /// <summary>
    /// 视频质量变化时应用到 WebRTC 服务
    /// </summary>
    partial void OnSelectedVideoQualityChanged(VideoQualitySettings value)
    {
        if (value != null)
        {
            _webRtcService.VideoQuality = value;
            _logger.LogInformation("视频质量已更改: {Quality} - {Resolution} @ {Bitrate}", 
                value.DisplayName, value.Resolution, value.BitrateDescription);
            StatusMessage = $"视频质量: {value.DisplayName} ({value.Resolution})";
        }
    }
    
    /// <summary>
    /// 视频编解码器变化时应用到 WebRTC 服务
    /// </summary>
    partial void OnSelectedVideoCodecChanged(VideoCodecInfo value)
    {
        if (value != null)
        {
            _webRtcService.CurrentVideoCodec = value.CodecType;
            _logger.LogInformation("视频编解码器已更改: {Codec} - {Description}", 
                value.DisplayName, value.Description);
            StatusMessage = $"编解码器: {value.DisplayName}";
        }
    }

    /// <summary>
    /// 切换摄像头到指定设备
    /// </summary>
    private async Task SwitchCameraAsync(string deviceId)
    {
        try
        {
            _logger.LogInformation("Switching camera to device: {DeviceId}", deviceId);
            StatusMessage = "正在切换摄像头...";

            // 先停止当前摄像头
            await _webRtcService.StopCameraAsync();

            // 启动新摄像头
            await _webRtcService.StartCameraAsync(deviceId);

            StatusMessage = "摄像头已切换";
            _logger.LogInformation("Camera switched to device: {DeviceId}", deviceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch camera to device: {DeviceId}", deviceId);
            StatusMessage = $"切换摄像头失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 切换麦克风到指定设备
    /// </summary>
    private async Task SwitchMicrophoneAsync(string deviceId)
    {
        try
        {
            _logger.LogInformation("Switching microphone to device: {DeviceId}", deviceId);
            StatusMessage = "正在切换麦克风...";

            // 先停止当前麦克风
            await _webRtcService.StopMicrophoneAsync();

            // 启动新麦克风
            await _webRtcService.StartMicrophoneAsync(deviceId);

            StatusMessage = "麦克风已切换";
            _logger.LogInformation("Microphone switched to device: {DeviceId}", deviceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch microphone to device: {DeviceId}", deviceId);
            StatusMessage = $"切换麦克风失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 切换摄像头
    /// </summary>
    [RelayCommand]
    private async Task ToggleCameraAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        try
        {
            if (IsCameraEnabled)
            {
                // 关闭摄像头
                await _webRtcService.StopCameraAsync();

                // 关闭 Producer
                if (!string.IsNullOrEmpty(_videoProducerId))
                {
                    await _signalRService.InvokeAsync("CloseProducer", _videoProducerId);
                    _videoProducerId = null;
                }

                IsCameraEnabled = false;
                LocalVideoFrame = null;
                StatusMessage = "摄像头已关闭";
            }
            else
            {
                // 启动摄像头（使用选中的设备）
                var deviceId = SelectedCamera?.DeviceId;
                await _webRtcService.StartCameraAsync(deviceId);
                IsCameraEnabled = true;
                StatusMessage = "摄像头采集中...";

                // 如果已加入房间，调用 Produce 推送视频
                if (IsJoinedRoom && !string.IsNullOrEmpty(_sendTransportId))
                {
                    await ProduceVideoAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle camera");
            StatusMessage = $"摄像头操作失败: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 切换麦克风
    /// </summary>
    [RelayCommand]
    private async Task ToggleMicrophoneAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        try
        {
            if (IsMicrophoneEnabled)
            {
                // 关闭麦克风
                await _webRtcService.StopMicrophoneAsync();

                // 关闭 Producer
                if (!string.IsNullOrEmpty(_audioProducerId))
                {
                    await _signalRService.InvokeAsync("CloseProducer", _audioProducerId);
                    _audioProducerId = null;
                }

                IsMicrophoneEnabled = false;
                StatusMessage = "麦克风已关闭";
            }
            else
            {
                // 启动麦克风（使用选中的设备）
                var deviceId = SelectedMicrophone?.DeviceId;
                await _webRtcService.StartMicrophoneAsync(deviceId);
                IsMicrophoneEnabled = true;
                StatusMessage = "麦克风已开启";

                // 如果已加入房间，调用 Produce 推送音频
                if (IsJoinedRoom && !string.IsNullOrEmpty(_sendTransportId))
                {
                    await ProduceAudioAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle microphone");
            StatusMessage = $"麦克风操作失败: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 连接服务器
    /// </summary>
    private async Task ConnectAsync()
    {
        try
        {
            StatusMessage = "正在连接...";
            var token = _accessTokens[SelectedPeerIndex];
            await _signalRService.ConnectAsync(ServerUrl, token);
            await StartMeetingAsync();
            StatusMessage = "已连接";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection failed");
            StatusMessage = $"连接失败: {ex.Message}";
            IsConnected = false;
        }
    }

    /// <summary>
    /// 断开连接
    /// </summary>
    private async Task DisconnectAsync()
    {
        await _webRtcService.CloseAsync();
        await _signalRService.DisconnectAsync();

        IsConnected = false;
        IsJoinedRoom = false;
        IsCameraEnabled = false;
        IsMicrophoneEnabled = false;
        Peers.Clear();
        RemoteVideos.Clear();
        HasNoRemoteVideos = true;
        LocalVideoFrame = null;
        StatusMessage = "已断开连接";
    }

    /// <summary>
    /// 初始化会议
    /// </summary>
    private async Task StartMeetingAsync()
    {
        // 1. 获取服务模式
        var serveModeResult = await _signalRService.InvokeAsync<ServeModeResponse>("GetServeMode");
        if (!serveModeResult.IsSuccess)
        {
            _logger.LogError("GetServeMode failed: {Message}", serveModeResult.Message);
            return;
        }
        ServeMode = serveModeResult.Data?.ServeMode ?? "Open";

        // 2. 获取 Router RTP Capabilities
        var rtpCapResult = await _signalRService.InvokeAsync<object>("GetRouterRtpCapabilities");
        if (!rtpCapResult.IsSuccess)
        {
            _logger.LogError("GetRouterRtpCapabilities failed: {Message}", rtpCapResult.Message);
            return;
        }
        _routerRtpCapabilities = rtpCapResult.Data;

        // 3. 加载 Mediasoup 设备
        if (_routerRtpCapabilities != null)
        {
            try
            {
                _webRtcService.LoadDevice(_routerRtpCapabilities);
                _logger.LogInformation("Mediasoup device loaded");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load mediasoup device");
            }
        }

        // 4. 加入会议
        var joinRequest = new
        {
            rtpCapabilities = _routerRtpCapabilities,
            sctpCapabilities = (object?)null,
            displayName = $"Peer {SelectedPeerIndex}",
            sources = new[] { "audio:mic", "video:cam" },
            appData = new Dictionary<string, object>()
        };

        var joinResult = await _signalRService.InvokeAsync("Join", joinRequest);
        if (!joinResult.IsSuccess)
        {
            _logger.LogError("Join failed: {Message}", joinResult.Message);
            return;
        }

        _logger.LogInformation("Joined meeting successfully, ServeMode: {ServeMode}", ServeMode);
    }

    /// <summary>
    /// 加入房间
    /// </summary>
    private async Task JoinRoomAsync()
    {
        var isAdmin = SelectedPeerIndex >= 8;
        var joinRoomRequest = new
        {
            roomId = Rooms[SelectedRoomIndex],
            role = isAdmin ? "admin" : "normal"
        };

        StatusMessage = "正在加入房间...";

        var result = await _signalRService.InvokeAsync<JoinRoomResponse>("JoinRoom", joinRoomRequest);
        if (!result.IsSuccess)
        {
            _logger.LogError("JoinRoom failed: {Message}", result.Message);
            StatusMessage = $"加入房间失败: {result.Message}";
            return;
        }

        // 更新 Peer 列表
        Peers.Clear();
        if (result.Data?.Peers != null)
        {
            foreach (var peer in result.Data.Peers)
            {
                Peers.Add(peer);
            }
        }

        IsJoinedRoom = true;

        // 创建 WebRTC Transport
        await CreateTransportsAsync();

        // 如果是 Open 模式，自动开始生产
        if (ServeMode == "Open")
        {
            await EnableMediaAsync();
        }

        // 通知服务器准备就绪
        if (ServeMode != "Pull")
        {
            await _signalRService.InvokeAsync("Ready");
        }

        StatusMessage = $"已加入房间 {Rooms[SelectedRoomIndex]}";
    }

    /// <summary>
    /// 离开房间
    /// </summary>
    private async Task LeaveRoomAsync()
    {
        await _webRtcService.CloseAsync();

        var result = await _signalRService.InvokeAsync("LeaveRoom");
        if (result.IsSuccess)
        {
            IsJoinedRoom = false;
            IsCameraEnabled = false;
            IsMicrophoneEnabled = false;
            Peers.Clear();
            RemoteVideos.Clear();
            HasNoRemoteVideos = true;
            LocalVideoFrame = null;
            StatusMessage = "已离开房间";
        }
    }

    /// <summary>
    /// 创建 WebRTC Transport
    /// </summary>
    private async Task CreateTransportsAsync()
    {
        // 创建发送 Transport
        var sendTransportResult = await _signalRService.InvokeAsync<CreateTransportResponse>(
            "CreateSendWebRtcTransport",
            new { forceTcp = false, sctpCapabilities = (object?)null });

        if (sendTransportResult.IsSuccess && sendTransportResult.Data != null)
        {
            var data = sendTransportResult.Data;
            _sendTransportId = data.TransportId;
            _logger.LogInformation("Created send transport: {TransportId}", _sendTransportId);

            // 创建 WebRTC Send Transport
            if (data.IceParameters != null && data.IceCandidates != null && data.DtlsParameters != null)
            {
                try
                {
                    _webRtcService.CreateSendTransport(
                        data.TransportId,
                        data.IceParameters,
                        data.IceCandidates,
                        data.DtlsParameters);
                    _logger.LogInformation("WebRTC send transport created");

                    // 连接 Send Transport - DTLS 握手
                    await _webRtcService.ConnectSendTransportAsync(async (transportId, dtlsParams) =>
                    {
                        var connectResult = await _signalRService.InvokeAsync(
                            "ConnectWebRtcTransport",
                            new { transportId, dtlsParameters = dtlsParams });
                        if (!connectResult.IsSuccess)
                        {
                            _logger.LogWarning("Failed to connect send transport: {Message}", connectResult.Message);
                        }
                    });
                    _logger.LogInformation("Send transport connected");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create/connect WebRTC send transport");
                }
            }
        }

        // 创建接收 Transport
        var recvTransportResult = await _signalRService.InvokeAsync<CreateTransportResponse>(
            "CreateRecvWebRtcTransport",
            new { forceTcp = false, sctpCapabilities = (object?)null });

        if (recvTransportResult.IsSuccess && recvTransportResult.Data != null)
        {
            var data = recvTransportResult.Data;
            _recvTransportId = data.TransportId;
            _logger.LogInformation("Created recv transport: {TransportId}", _recvTransportId);

            // 创建 WebRTC Recv Transport
            if (data.IceParameters != null && data.IceCandidates != null && data.DtlsParameters != null)
            {
                try
                {
                    // 创建 Recv Transport
                    _webRtcService.CreateRecvTransport(
                        data.TransportId,
                        data.IceParameters,
                        data.IceCandidates,
                        data.DtlsParameters);
                    _logger.LogInformation("Recv transport created: {TransportId}", data.TransportId);

                    // 设置 SDP 协商完成回调 - 在 SDP 协商完成后才调用 ConnectWebRtcTransport
                    // 这是 mediasoup-client 的正确流程，确保服务器在 DTLS 连接后能正确发送 RTP
                    _webRtcService.SetupRecvTransportNegotiationCallback(async (transportId, dtlsParams) =>
                    {
                        var connectResult = await _signalRService.InvokeAsync(
                            "ConnectWebRtcTransport",
                            new { transportId, dtlsParameters = dtlsParams });
                        if (!connectResult.IsSuccess)
                        {
                            _logger.LogWarning("Failed to connect recv transport: {Message}", connectResult.Message);
                        }
                    });
                    _logger.LogInformation("Recv transport negotiation callback setup, will connect after SDP negotiation");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create recv transport");
                }
            }
        }
    }

    /// <summary>
    /// 启用媒体
    /// </summary>
    private async Task EnableMediaAsync()
    {
        try
        {
            // 使用选中的设备
            var cameraDeviceId = SelectedCamera?.DeviceId;
            var micDeviceId = SelectedMicrophone?.DeviceId;

            await _webRtcService.StartCameraAsync(cameraDeviceId);
            IsCameraEnabled = true;
            StatusMessage = "摄像头采集中...";

            // 调用 Produce 推送视频
            if (!string.IsNullOrEmpty(_sendTransportId))
            {
                await ProduceVideoAsync();
            }

            await _webRtcService.StartMicrophoneAsync(micDeviceId);
            IsMicrophoneEnabled = true;

            // 调用 Produce 推送音频
            if (!string.IsNullOrEmpty(_sendTransportId))
            {
                await ProduceAudioAsync();
            }

            _logger.LogInformation("Media enabled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable media");
        }
    }

    /// <summary>
    /// 生产视频流
    /// </summary>
    private async Task ProduceVideoAsync()
    {
        try
        {
            // 从 SendTransport 获取实际使用的 SSRC，确保与 RTP 发送一致
            var videoSsrc = _webRtcService.SendTransport?.VideoSsrc ?? 0;
            var currentCodec = _webRtcService.CurrentVideoCodec;
            var produceRequest = RtpParametersFactory.CreateVideoProduceRequest(videoSsrc, currentCodec);
            _logger.LogInformation("创建视频 Producer: SSRC={Ssrc}, Codec={Codec}", videoSsrc, currentCodec);

            var result = await _signalRService.InvokeAsync<ProduceResponse>("Produce", produceRequest);
            if (result.IsSuccess && result.Data != null)
            {
                _videoProducerId = result.Data.Id;
                _logger.LogInformation("Video producer created: {ProducerId}, SSRC: {Ssrc}", _videoProducerId, videoSsrc);
                StatusMessage = "视频推送中";
            }
            else
            {
                _logger.LogWarning("Failed to produce video: {Message}", result.Message);
                StatusMessage = $"视频推送失败: {result.Message}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to produce video");
            StatusMessage = $"视频推送异常: {ex.Message}";
        }
    }

    /// <summary>
    /// 生产音频流
    /// </summary>
    private async Task ProduceAudioAsync()
    {
        try
        {
            // 从 SendTransport 获取实际使用的 SSRC，确保与 RTP 发送一致
            var audioSsrc = _webRtcService.SendTransport?.AudioSsrc ?? 0;
            var produceRequest = RtpParametersFactory.CreateAudioProduceRequest(audioSsrc);

            var result = await _signalRService.InvokeAsync<ProduceResponse>("Produce", produceRequest);
            if (result.IsSuccess && result.Data != null)
            {
                _audioProducerId = result.Data.Id;
                _logger.LogInformation("Audio producer created: {ProducerId}, SSRC: {Ssrc}", _audioProducerId, audioSsrc);
            }
            else
            {
                _logger.LogWarning("Failed to produce audio: {Message}", result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to produce audio");
        }
    }

    #endregion

    #region 事件处理

    private void OnSignalRConnected()
    {
        IsConnected = true;
    }

    private void OnSignalRDisconnected(Exception? error)
    {
        IsConnected = false;
        IsJoinedRoom = false;
        Peers.Clear();
        RemoteVideos.Clear();
        HasNoRemoteVideos = true;
    }

    private void OnLocalVideoFrameReceived(WriteableBitmap frame)
    {
        LocalVideoFrame = frame;
    }

    /// <summary>
    /// 处理远端视频帧
    /// </summary>
    private void OnRemoteVideoFrameReceived(string consumerId, WriteableBitmap frame)
    {
        try
        {
            // 查找或创建对应的远端视频项
            var existingVideo = RemoteVideos.FirstOrDefault(v => v.ConsumerId == consumerId);
            if (existingVideo != null)
            {
                existingVideo.VideoFrame = frame;
            }
            else
            {
                // 创建新的远端视频项
                var remoteVideo = new RemoteVideoItem
                {
                    ConsumerId = consumerId,
                    VideoFrame = frame,
                    DisplayName = $"远端用户_{consumerId.Substring(0, Math.Min(8, consumerId.Length))}"
                };

                RemoteVideos.Add(remoteVideo);
                HasNoRemoteVideos = false;
                _logger.LogInformation("添加远端视频: {ConsumerId}", consumerId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理远端视频帧失败");
        }
    }

    private void OnWebRtcStateChanged(string state)
    {
        _logger.LogDebug("WebRTC state changed: {State}", state);
    }

    /// <summary>
    /// Recv Transport DTLS 连接完成后，恢复所有待恢复的 Consumer
    /// </summary>
    private async void OnRecvTransportDtlsConnected()
    {
        _logger.LogInformation("Recv transport DTLS connected, resuming {Count} pending consumers", _pendingResumeConsumers.Count);

        // 复制列表并清空，避免并发问题
        List<string> consumersToResume;
        lock (_pendingResumeConsumers)
        {
            consumersToResume = new List<string>(_pendingResumeConsumers);
            _pendingResumeConsumers.Clear();
        }

        foreach (var consumerId in consumersToResume)
        {
            try
            {
                _logger.LogDebug("Resuming consumer after DTLS: {ConsumerId}", consumerId);
                await _signalRService.InvokeAsync("ResumeConsumer", consumerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resume consumer {ConsumerId}", consumerId);
            }
        }
    }

    /// <summary>
    /// 处理服务器通知
    /// </summary>
    private void HandleNotification(MeetingNotification notification)
    {
        _logger.LogDebug("Handling notification: {Type}", notification.Type);

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            try
            {
                switch (notification.Type)
                {
                    case "peerJoinRoom":
                        HandlePeerJoinRoom(notification.Data);
                        break;
                    case "peerLeaveRoom":
                        HandlePeerLeaveRoom(notification.Data);
                        break;
                    case "newConsumer":
                        HandleNewConsumer(notification.Data);
                        break;
                    case "consumerClosed":
                        HandleConsumerClosed(notification.Data);
                        break;
                    case "produceSources":
                        HandleProduceSources(notification.Data);
                        break;
                    case "producerClosed":
                        HandleProducerClosed(notification.Data);
                        break;
                    default:
                        _logger.LogDebug("Unhandled notification: {Type}", notification.Type);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling notification: {Type}", notification.Type);
            }
        });
    }

    private void HandlePeerJoinRoom(object? data)
    {
        if (data == null) return;

        var json = JsonSerializer.Serialize(data);
        var notification = JsonSerializer.Deserialize<PeerJoinRoomData>(json, JsonOptions);
        if (notification?.Peer != null)
        {
            Peers.Add(notification.Peer);
            _logger.LogInformation("Peer joined: {PeerId}", notification.Peer.PeerId);
            StatusMessage = $"用户 {notification.Peer.DisplayName} 加入房间";
        }
    }

    private void HandlePeerLeaveRoom(object? data)
    {
        if (data == null) return;

        var json = JsonSerializer.Serialize(data);
        var notification = JsonSerializer.Deserialize<PeerLeaveRoomData>(json, JsonOptions);
        if (notification?.PeerId != null)
        {
            var peer = Peers.FirstOrDefault(p => p.PeerId == notification.PeerId);
            if (peer != null)
            {
                Peers.Remove(peer);
                _logger.LogInformation("Peer left: {PeerId}", notification.PeerId);
                StatusMessage = $"用户 {peer.DisplayName} 离开房间";
            }

            // 移除该 peer 对应的所有远端视频
            var videosToRemove = RemoteVideos.Where(v => v.PeerId == notification.PeerId).ToList();
            foreach (var video in videosToRemove)
            {
                RemoteVideos.Remove(video);
                _logger.LogInformation("移除远端视频: ConsumerId={ConsumerId}, PeerId={PeerId}", video.ConsumerId, notification.PeerId);
            }

            // 更新无远端视频状态
            HasNoRemoteVideos = RemoteVideos.Count == 0;
        }
    }

    private async void HandleNewConsumer(object? data)
    {
        if (data == null) return;

        var json = JsonSerializer.Serialize(data);
        var notification = JsonSerializer.Deserialize<NewConsumerData>(json, JsonOptions);
        if (notification != null)
        {
            _logger.LogInformation("New consumer: {ConsumerId}, Kind: {Kind}",
                notification.ConsumerId, notification.Kind);

            // 如果是视频 Consumer，立即在 UI 中添加占位符
            if (notification.Kind == "video")
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    // 检查是否已存在
                    var existing = RemoteVideos.FirstOrDefault(v => v.ConsumerId == notification.ConsumerId);
                    if (existing == null)
                    {
                        var peerName = notification.ProducerPeerId ?? "remote";
                        var remoteVideo = new RemoteVideoItem
                        {
                            ConsumerId = notification.ConsumerId,
                            PeerId = notification.ProducerPeerId ?? "",
                            DisplayName = $"远端用户_{peerName.Substring(0, Math.Min(8, peerName.Length))}",
                            VideoFrame = null // 占位符，等待视频帧
                        };
                        RemoteVideos.Add(remoteVideo);
                        HasNoRemoteVideos = false;
                        _logger.LogInformation("添加远端视频占位符: {ConsumerId}", notification.ConsumerId);
                    }
                });
            }

            await _webRtcService.AddConsumerAsync(
                notification.ConsumerId,
                notification.Kind,
                notification.RtpParameters);

            // 判断 recv transport 是否已完成 DTLS 连接
            // 如果已连接，立即恢复 Consumer
            // 如果未连接，将 Consumer ID 添加到待恢复列表，等待 DTLS 连接后再恢复
            if (_webRtcService.IsRecvTransportDtlsConnected)
            {
                _logger.LogDebug("Recv transport already connected, resuming consumer immediately: {ConsumerId}", notification.ConsumerId);
                await _signalRService.InvokeAsync("ResumeConsumer", notification.ConsumerId);
            }
            else
            {
                _logger.LogDebug("Recv transport not yet connected, adding consumer to pending resume list: {ConsumerId}", notification.ConsumerId);
                lock (_pendingResumeConsumers)
                {
                    _pendingResumeConsumers.Add(notification.ConsumerId);
                }
            }
        }
    }

    private async void HandleConsumerClosed(object? data)
    {
        if (data == null) return;

        var json = JsonSerializer.Serialize(data);
        var notification = JsonSerializer.Deserialize<ConsumerClosedData>(json, JsonOptions);
        if (notification != null)
        {
            _logger.LogInformation("Consumer closed: {ConsumerId}", notification.ConsumerId);
            
            // 从 RemoteVideos 集合中移除对应的视频项
            var videoToRemove = RemoteVideos.FirstOrDefault(v => v.ConsumerId == notification.ConsumerId);
            if (videoToRemove != null)
            {
                RemoteVideos.Remove(videoToRemove);
                _logger.LogInformation("移除远端视频: {ConsumerId}", notification.ConsumerId);
                
                // 更新无远端视频状态
                HasNoRemoteVideos = RemoteVideos.Count == 0;
            }
            
            await _webRtcService.RemoveConsumerAsync(notification.ConsumerId);
        }
    }

    private async void HandleProduceSources(object? data)
    {
        if (data == null) return;

        var json = JsonSerializer.Serialize(data);
        var notification = JsonSerializer.Deserialize<ProduceSourcesData>(json, JsonOptions);
        if (notification?.Sources != null)
        {
            _logger.LogInformation("Produce sources requested: {Sources}",
                string.Join(", ", notification.Sources));

            foreach (var source in notification.Sources)
            {
                if (source == "audio:mic" && !IsMicrophoneEnabled)
                {
                    await _webRtcService.StartMicrophoneAsync();
                    IsMicrophoneEnabled = true;
                }
                else if (source == "video:cam" && !IsCameraEnabled)
                {
                    await _webRtcService.StartCameraAsync();
                    IsCameraEnabled = true;
                }
            }
        }
    }

    private void HandleProducerClosed(object? data)
    {
        if (data == null) return;

        var json = JsonSerializer.Serialize(data);
        var notification = JsonSerializer.Deserialize<ProducerClosedData>(json, JsonOptions);
        if (notification != null)
        {
            _logger.LogInformation("Producer closed: {ProducerId}", notification.ProducerId);
        }
    }

    #endregion
}

/// <summary>
/// 远端视频项
/// </summary>
public class RemoteVideoItem : ObservableObject
{
    private string _consumerId = string.Empty;
    private string _peerId = string.Empty;
    private string _displayName = string.Empty;
    private WriteableBitmap? _videoFrame;

    public string ConsumerId
    {
        get => _consumerId;
        set => SetProperty(ref _consumerId, value);
    }

    public string PeerId
    {
        get => _peerId;
        set => SetProperty(ref _peerId, value);
    }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    /// <summary>
    /// 兼容旧版本的 PeerName 属性
    /// </summary>
    public string PeerName
    {
        get => _displayName;
        set => DisplayName = value;
    }

    public WriteableBitmap? VideoFrame
    {
        get => _videoFrame;
        set => SetProperty(ref _videoFrame, value);
    }
}
