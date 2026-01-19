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
    /// 房间号码（用于加入房间）
    /// </summary>
    [ObservableProperty]
    private string _roomId = "0";

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
    /// 自我视图是否可见
    /// </summary>
    [ObservableProperty]
    private bool _isSelfViewVisible = true;

    /// <summary>
    /// 是否已举手
    /// </summary>
    [ObservableProperty]
    private bool _isHandRaised;

    /// <summary>
    /// 当前用户名
    /// </summary>
    [ObservableProperty]
    private string _currentUserName = "我";

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

    #region 聊天相关属性

    /// <summary>
    /// 聊天用户列表
    /// </summary>
    public ObservableCollection<ChatUser> ChatUsers { get; } = [];

    /// <summary>
    /// 选中的聊天用户
    /// </summary>
    [ObservableProperty]
    private ChatUser? _selectedChatUser;

    /// <summary>
    /// 当前消息列表
    /// </summary>
    public ObservableCollection<ChatMessage> CurrentMessages { get; } = [];

    /// <summary>
    /// 群聊消息列表
    /// </summary>
    private readonly ObservableCollection<ChatMessage> _groupMessages = [];

    /// <summary>
    /// 私聊消息字典
    /// </summary>
    private readonly Dictionary<string, ObservableCollection<ChatMessage>> _privateMessages = [];

    /// <summary>
    /// 聊天面板是否可见
    /// </summary>
    [ObservableProperty]
    private bool _isChatPanelVisible;

    /// <summary>
    /// 是否在群聊模式
    /// </summary>
    [ObservableProperty]
    private bool _isGroupChatMode = true;

    /// <summary>
    /// 当前显示的表情反应
    /// </summary>
    [ObservableProperty]
    private EmojiReaction? _currentEmojiReaction;

    /// <summary>
    /// 表情反应是否可见
    /// </summary>
    [ObservableProperty]
    private bool _isEmojiReactionVisible;

    #endregion

    #region 屏幕共享相关属性

    /// <summary>
    /// 是否正在共享屏幕
    /// </summary>
    [ObservableProperty]
    private bool _isScreenSharing;

    /// <summary>
    /// 是否有待处理的屏幕共享请求
    /// </summary>
    [ObservableProperty]
    private bool _hasPendingScreenShareRequest;

    /// <summary>
    /// 待处理请求的发起者名称
    /// </summary>
    [ObservableProperty]
    private string _pendingScreenShareRequesterName = "";

    /// <summary>
    /// 待处理的屏幕共享请求
    /// </summary>
    private ScreenShareRequestData? _pendingScreenShareRequest;

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
            _logger.LogInformation("切换房间状态: 当前IsJoinedRoom={IsJoinedRoom}", IsJoinedRoom);
            
            if (IsJoinedRoom)
            {
                _logger.LogInformation("开始离开房间...");
                await LeaveRoomAsync();
            }
            else
            {
                _logger.LogInformation("开始加入房间...");
                await JoinRoomAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换房间状态失败");
            StatusMessage = $"操作失败: {ex.Message}";
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

    #region 左侧工具栏命令

    /// <summary>
    /// 分享教室
    /// </summary>
    [RelayCommand]
    private void ShareRoom()
    {
        _logger.LogInformation("分享教室");
        StatusMessage = "分享教室功能待实现";
    }

    /// <summary>
    /// 切换自我视图可见性
    /// </summary>
    [RelayCommand]
    private void ToggleSelfView()
    {
        IsSelfViewVisible = !IsSelfViewVisible;
        StatusMessage = IsSelfViewVisible ? "已显示自我视图" : "已隐藏自我视图";
    }

    /// <summary>
    /// 录制
    /// </summary>
    [RelayCommand]
    private void Record()
    {
        _logger.LogInformation("录制");
        StatusMessage = "录制功能待实现";
    }

    /// <summary>
    /// 全屏
    /// </summary>
    [RelayCommand]
    private void FullScreen()
    {
        _logger.LogInformation("全屏");
        StatusMessage = "全屏功能待实现";
    }

    /// <summary>
    /// 表情
    /// </summary>
    [RelayCommand]
    private void Emoji()
    {
        _logger.LogInformation("表情");
        StatusMessage = "表情功能待实现";
    }

    /// <summary>
    /// 同步转译
    /// </summary>
    [RelayCommand]
    private void Translate()
    {
        _logger.LogInformation("同步转译");
        StatusMessage = "同步转译功能待实现";
    }

    /// <summary>
    /// 投票
    /// </summary>
    [RelayCommand]
    private void Poll()
    {
        _logger.LogInformation("投票");
        StatusMessage = "投票功能待实现";
    }

    /// <summary>
    /// 文本编辑器
    /// </summary>
    [RelayCommand]
    private void Editor()
    {
        _logger.LogInformation("文本编辑器");
        StatusMessage = "文本编辑器功能待实现";
    }

    /// <summary>
    /// 白板
    /// </summary>
    [RelayCommand]
    private void Whiteboard()
    {
        _logger.LogInformation("白板");
        StatusMessage = "白板功能待实现";
    }

    /// <summary>
    /// 画中画
    /// </summary>
    [RelayCommand]
    private void Pip()
    {
        _logger.LogInformation("画中画");
        StatusMessage = "画中画功能待实现";
    }

    /// <summary>
    /// 共享屏幕
    /// </summary>
    [RelayCommand]
    private async Task ShareScreenAsync()
    {
        _logger.LogInformation("屏幕共享按钮点击, IsJoinedRoom={IsJoinedRoom}, IsScreenSharing={IsScreenSharing}", IsJoinedRoom, IsScreenSharing);
        
        if (!IsJoinedRoom)
        {
            StatusMessage = "请先加入房间";
            _logger.LogWarning("尝试共享屏幕但未加入房间");
            return;
        }

        try
        {
            if (IsScreenSharing)
            {
                // 停止共享
                _logger.LogInformation("停止屏幕共享...");
                await _webRtcService.StopScreenShareAsync();
                IsScreenSharing = false;
                StatusMessage = "已停止屏幕共享";
                _logger.LogInformation("屏幕共享已停止");
            }
            else
            {
                // 开始共享 - 向所有用户发送共享请求
                _logger.LogInformation("开始屏幕共享...");
                var sessionId = Guid.NewGuid().ToString();
                
                // 通过SignalR广播屏幕共享请求
                await _signalRService.InvokeAsync("BroadcastMessage", new
                {
                    type = "screenShareRequest",
                    data = new
                    {
                        requesterId = SelectedPeerIndex.ToString(),
                        requesterName = CurrentUserName,
                        sessionId
                    }
                });
                _logger.LogInformation("已发送屏幕共享请求, sessionId={SessionId}", sessionId);

                // 开始屏幕捕获
                await _webRtcService.StartScreenShareAsync();
                IsScreenSharing = true;
                StatusMessage = "屏幕共享中...";
                _logger.LogInformation("屏幕共享已开始");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "屏幕共享失败");
            StatusMessage = $"屏幕共享失败: {ex.Message}";
            IsScreenSharing = false;
        }
    }

    /// <summary>
    /// 接受屏幕共享请求
    /// </summary>
    [RelayCommand]
    private async Task AcceptScreenShareAsync()
    {
        if (_pendingScreenShareRequest == null) return;

        try
        {
            await _signalRService.InvokeAsync("BroadcastMessage", new
            {
                type = "screenShareResponse",
                data = new
                {
                    responderId = SelectedPeerIndex.ToString(),
                    sessionId = _pendingScreenShareRequest.SessionId,
                    accepted = true
                }
            });

            HasPendingScreenShareRequest = false;
            _pendingScreenShareRequest = null;
            StatusMessage = "已接受屏幕共享";
            _logger.LogInformation("接受屏幕共享请求");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "接受屏幕共享失败");
        }
    }

    /// <summary>
    /// 拒绝屏幕共享请求
    /// </summary>
    [RelayCommand]
    private async Task RejectScreenShareAsync()
    {
        if (_pendingScreenShareRequest == null) return;

        try
        {
            await _signalRService.InvokeAsync("BroadcastMessage", new
            {
                type = "screenShareResponse",
                data = new
                {
                    responderId = SelectedPeerIndex.ToString(),
                    sessionId = _pendingScreenShareRequest.SessionId,
                    accepted = false
                }
            });

            HasPendingScreenShareRequest = false;
            _pendingScreenShareRequest = null;
            StatusMessage = "已拒绝屏幕共享";
            _logger.LogInformation("拒绝屏幕共享请求");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "拒绝屏幕共享失败");
        }
    }

    /// <summary>
    /// 屏幕截图
    /// </summary>
    [RelayCommand]
    private void Screenshot()
    {
        _logger.LogInformation("屏幕截图");
        StatusMessage = "屏幕截图功能待实现";
    }

    #endregion

    #region 底部控制栏命令

    /// <summary>
    /// 聊天
    /// </summary>
    [RelayCommand]
    private void Chat()
    {
        IsChatPanelVisible = !IsChatPanelVisible;
        _logger.LogInformation("聊天面板: {Visible}", IsChatPanelVisible);
        StatusMessage = IsChatPanelVisible ? "打开聊天" : "关闭聊天";
        
        // 切换到群聊
        if (IsChatPanelVisible)
        {
            SwitchToGroupChat();
        }
    }

    /// <summary>
    /// 举手/发送表情
    /// </summary>
    [RelayCommand]
    private void RaiseHand()
    {
        // 打开表情选择窗口
        OpenEmojiPickerRequested?.Invoke();
    }

    /// <summary>
    /// 请求打开表情选择器事件
    /// </summary>
    public event Action? OpenEmojiPickerRequested;

    /// <summary>
    /// 打开设置
    /// </summary>
    [RelayCommand]
    private void OpenSettings()
    {
        _logger.LogInformation("打开设置");
        // 通过事件通知视图打开设置窗口
        OpenSettingsRequested?.Invoke();
    }

    /// <summary>
    /// 请求打开设置窗口事件
    /// </summary>
    public event Action? OpenSettingsRequested;

    /// <summary>
    /// 自动加入房间（用于启动时自动连接并加入）
    /// </summary>
    /// <param name="joinInfo">加入信息</param>
    public async Task AutoJoinAsync(Models.JoinRoomInfo joinInfo)
    {
        if (IsBusy) return;

        IsBusy = true;
        try
        {
            // 应用加入信息
            ServerUrl = joinInfo.ServerUrl;
            CurrentUserName = joinInfo.UserName;
            RoomId = joinInfo.RoomId;

            // 设置选中的设备
            if (!string.IsNullOrEmpty(joinInfo.CameraDeviceId))
            {
                SelectedCamera = Cameras.FirstOrDefault(c => c.DeviceId == joinInfo.CameraDeviceId) ?? Cameras.FirstOrDefault();
            }
            if (!string.IsNullOrEmpty(joinInfo.MicrophoneDeviceId))
            {
                SelectedMicrophone = Microphones.FirstOrDefault(m => m.DeviceId == joinInfo.MicrophoneDeviceId) ?? Microphones.FirstOrDefault();
            }

            _logger.LogInformation("自动加入: ServerUrl={ServerUrl}, UserName={UserName}, RoomId={RoomId}", 
                ServerUrl, CurrentUserName, RoomId);

            // 连接服务器
            StatusMessage = "正在连接服务器...";
            await ConnectAsync();

            if (!IsConnected)
            {
                StatusMessage = "连接服务器失败";
                return;
            }

            // 加入房间
            StatusMessage = "正在加入房间...";
            await JoinRoomAsync();

            if (!IsJoinedRoom)
            {
                StatusMessage = "加入房间失败";
                return;
            }

            // 根据设置控制摄像头和麦克风
            if (!joinInfo.MuteCameraOnJoin && !IsCameraEnabled)
            {
                await ToggleCameraAsync();
            }
            if (!joinInfo.MuteMicrophoneOnJoin && !IsMicrophoneEnabled)
            {
                await ToggleMicrophoneAsync();
            }

            StatusMessage = $"已加入房间 {RoomId}";
            _logger.LogInformation("自动加入成功: RoomId={RoomId}", RoomId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "自动加入失败");
            StatusMessage = $"加入失败: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    #region 聊天和表情方法

    /// <summary>
    /// 发送表情广播
    /// </summary>
    public async Task SendEmojiReactionAsync(string emoji)
    {
        if (!IsJoinedRoom) return;

        try
        {
            var reaction = new
            {
                emoji,
                senderName = CurrentUserName,
                senderId = SelectedPeerIndex.ToString()
            };

            await _signalRService.InvokeAsync("BroadcastMessage", new
            {
                type = "emojiReaction",
                data = reaction
            });

            _logger.LogInformation("发送表情反应: {Emoji}", emoji);
            StatusMessage = $"发送表情: {emoji}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送表情失败");
        }
    }

    /// <summary>
    /// 显示表情反应
    /// </summary>
    public void ShowEmojiReaction(EmojiReaction reaction)
    {
        Application.Current?.Dispatcher.Invoke(async () =>
        {
            CurrentEmojiReaction = reaction;
            IsEmojiReactionVisible = true;

            // 3秒后隐藏
            await Task.Delay(3000);
            IsEmojiReactionVisible = false;
        });
    }

    /// <summary>
    /// 切换到群聊
    /// </summary>
    public void SwitchToGroupChat()
    {
        IsGroupChatMode = true;
        SelectedChatUser = null;
        
        CurrentMessages.Clear();
        foreach (var msg in _groupMessages)
        {
            CurrentMessages.Add(msg);
        }
    }

    /// <summary>
    /// 选中聊天用户变化
    /// </summary>
    partial void OnSelectedChatUserChanged(ChatUser? value)
    {
        if (value == null) return;

        IsGroupChatMode = false;
        
        // 切换到私聊消息
        if (!_privateMessages.TryGetValue(value.PeerId, out var messages))
        {
            messages = [];
            _privateMessages[value.PeerId] = messages;
        }

        CurrentMessages.Clear();
        foreach (var msg in messages)
        {
            CurrentMessages.Add(msg);
        }

        // 清除未读数
        value.UnreadCount = 0;
    }

    /// <summary>
    /// 发送文本消息
    /// </summary>
    public async void SendTextMessage(string content, string? receiverId)
    {
        if (!IsJoinedRoom) return;

        var message = new ChatMessage
        {
            SenderId = SelectedPeerIndex.ToString(),
            SenderName = CurrentUserName,
            ReceiverId = receiverId ?? "",
            Content = content,
            MessageType = ChatMessageType.Text,
            IsFromSelf = true
        };

        AddMessageToCollection(message);

        try
        {
            await _signalRService.InvokeAsync("BroadcastMessage", new
            {
                type = "chatMessage",
                data = new
                {
                    id = message.Id,
                    senderId = message.SenderId,
                    senderName = message.SenderName,
                    receiverId = message.ReceiverId,
                    content = message.Content,
                    messageType = (int)message.MessageType,
                    timestamp = message.Timestamp
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送消息失败");
        }
    }

    /// <summary>
    /// 发送图片消息
    /// </summary>
    public async void SendImageMessage(string filePath, string? receiverId)
    {
        if (!IsJoinedRoom) return;

        try
        {
            var fileInfo = new System.IO.FileInfo(filePath);
            var message = new ChatMessage
            {
                SenderId = SelectedPeerIndex.ToString(),
                SenderName = CurrentUserName,
                ReceiverId = receiverId ?? "",
                Content = $"[图片] {fileInfo.Name}",
                MessageType = ChatMessageType.Image,
                FileName = fileInfo.Name,
                FilePath = filePath,
                FileSize = fileInfo.Length,
                IsFromSelf = true
            };

            // 加载图片
            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(filePath);
            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            message.ImageSource = bitmap;

            AddMessageToCollection(message);

            // 发送消息通知（实际文件传输需要额外实现）
            await _signalRService.InvokeAsync("BroadcastMessage", new
            {
                type = "chatMessage",
                data = new
                {
                    id = message.Id,
                    senderId = message.SenderId,
                    senderName = message.SenderName,
                    receiverId = message.ReceiverId,
                    content = message.Content,
                    messageType = (int)message.MessageType,
                    fileName = message.FileName,
                    fileSize = message.FileSize,
                    timestamp = message.Timestamp
                }
            });

            StatusMessage = "图片已发送";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送图片失败");
            StatusMessage = $"发送图片失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 发送文件消息
    /// </summary>
    public async void SendFileMessage(string filePath, string? receiverId)
    {
        if (!IsJoinedRoom) return;

        try
        {
            var fileInfo = new System.IO.FileInfo(filePath);
            var message = new ChatMessage
            {
                SenderId = SelectedPeerIndex.ToString(),
                SenderName = CurrentUserName,
                ReceiverId = receiverId ?? "",
                Content = $"[文件] {fileInfo.Name}",
                MessageType = ChatMessageType.File,
                FileName = fileInfo.Name,
                FilePath = filePath,
                FileSize = fileInfo.Length,
                IsFromSelf = true
            };

            AddMessageToCollection(message);

            // 发送消息通知（实际文件传输需要额外实现）
            await _signalRService.InvokeAsync("BroadcastMessage", new
            {
                type = "chatMessage",
                data = new
                {
                    id = message.Id,
                    senderId = message.SenderId,
                    senderName = message.SenderName,
                    receiverId = message.ReceiverId,
                    content = message.Content,
                    messageType = (int)message.MessageType,
                    fileName = message.FileName,
                    fileSize = message.FileSize,
                    timestamp = message.Timestamp
                }
            });

            StatusMessage = "文件已发送";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送文件失败");
            StatusMessage = $"发送文件失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 添加消息到集合
    /// </summary>
    private void AddMessageToCollection(ChatMessage message)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            if (string.IsNullOrEmpty(message.ReceiverId))
            {
                // 群聊消息
                _groupMessages.Add(message);
                if (IsGroupChatMode)
                {
                    CurrentMessages.Add(message);
                }
            }
            else
            {
                // 私聊消息
                if (!_privateMessages.TryGetValue(message.ReceiverId, out var messages))
                {
                    messages = [];
                    _privateMessages[message.ReceiverId] = messages;
                }
                messages.Add(message);

                if (!IsGroupChatMode && SelectedChatUser?.PeerId == message.ReceiverId)
                {
                    CurrentMessages.Add(message);
                }
            }
        });
    }

    /// <summary>
    /// 处理接收到的消息
    /// </summary>
    private void HandleChatMessage(object? data)
    {
        if (data == null) return;

        try
        {
            var json = JsonSerializer.Serialize(data);
            var msgData = JsonSerializer.Deserialize<ChatMessageData>(json, JsonOptions);
            if (msgData == null) return;

            // 忽略自己发送的消息
            if (msgData.SenderId == SelectedPeerIndex.ToString()) return;

            var message = new ChatMessage
            {
                Id = msgData.Id ?? Guid.NewGuid().ToString(),
                SenderId = msgData.SenderId ?? "",
                SenderName = msgData.SenderName ?? "Unknown",
                ReceiverId = msgData.ReceiverId ?? "",
                Content = msgData.Content ?? "",
                MessageType = (ChatMessageType)(msgData.MessageType ?? 0),
                FileName = msgData.FileName,
                FileSize = msgData.FileSize ?? 0,
                Timestamp = msgData.Timestamp ?? DateTime.Now,
                IsFromSelf = false
            };

            AddMessageToCollection(message);

            // 如果不在当前聊天，增加未读数
            if (!IsChatPanelVisible || (!IsGroupChatMode && SelectedChatUser?.PeerId != message.SenderId))
            {
                var user = ChatUsers.FirstOrDefault(u => u.PeerId == message.SenderId);
                if (user != null)
                {
                    user.UnreadCount++;
                    user.LastMessage = message;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理消息失败");
        }
    }

    /// <summary>
    /// 处理接收到的表情反应
    /// </summary>
    private void HandleEmojiReaction(object? data)
    {
        if (data == null) return;

        try
        {
            var json = JsonSerializer.Serialize(data);
            var reactionData = JsonSerializer.Deserialize<EmojiReactionData>(json, JsonOptions);
            if (reactionData == null) return;

            // 忽略自己发送的
            if (reactionData.SenderId == SelectedPeerIndex.ToString()) return;

            var reaction = new EmojiReaction
            {
                SenderId = reactionData.SenderId ?? "",
                SenderName = reactionData.SenderName ?? "Unknown",
                Emoji = reactionData.Emoji ?? "👍"
            };

            ShowEmojiReaction(reaction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理表情反应失败");
        }
    }

    #endregion

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
                _logger.LogInformation("摄像头已关闭");
            }
            else
            {
                // 启动摄像头（使用选中的设备）
                var deviceId = SelectedCamera?.DeviceId;
                await _webRtcService.StartCameraAsync(deviceId);
                IsCameraEnabled = true;
                StatusMessage = "摄像头采集中...";
                _logger.LogInformation("摄像头已开启");

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
    }

    /// <summary>
    /// 切换麦克风
    /// </summary>
    [RelayCommand]
    private async Task ToggleMicrophoneAsync()
    {
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
                _logger.LogInformation("麦克风已关闭");
            }
            else
            {
                // 启动麦克风（使用选中的设备）
                var deviceId = SelectedMicrophone?.DeviceId;
                await _webRtcService.StartMicrophoneAsync(deviceId);
                IsMicrophoneEnabled = true;
                StatusMessage = "麦克风已开启";
                _logger.LogInformation("麦克风已开启");

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
        var roomIdToJoin = !string.IsNullOrEmpty(RoomId) ? RoomId : Rooms[SelectedRoomIndex];
        var joinRoomRequest = new
        {
            roomId = roomIdToJoin,
            role = isAdmin ? "admin" : "normal"
        };

        StatusMessage = "正在加入房间...";
        _logger.LogInformation("调用JoinRoom: RoomId={RoomId}, IsAdmin={IsAdmin}", roomIdToJoin, isAdmin);

        var result = await _signalRService.InvokeAsync<JoinRoomResponse>("JoinRoom", joinRoomRequest);
        if (!result.IsSuccess)
        {
            _logger.LogError("JoinRoom failed: {Message}", result.Message);
            
            // 检查是否是"已在房间中"的错误
            if (result.Message?.Contains("already") == true || result.Message?.Contains("已在") == true)
            {
                _logger.LogWarning("检测到已在房间中，同步状态为已加入");
                IsJoinedRoom = true;
                StatusMessage = $"已在房间 {roomIdToJoin} 中";
                return;
            }
            
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
        _logger.LogInformation("加入房间成功: RoomId={RoomId}, PeerCount={PeerCount}", roomIdToJoin, Peers.Count);

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

        StatusMessage = $"已加入房间 {roomIdToJoin}";
    }

    /// <summary>
    /// 离开房间
    /// </summary>
    private async Task LeaveRoomAsync()
    {
        _logger.LogInformation("开始离开房间...");
        
        await _webRtcService.CloseAsync();

        var result = await _signalRService.InvokeAsync("LeaveRoom");
        
        // 无论服务器返回成功还是失败，都重置客户端状态
        IsJoinedRoom = false;
        IsCameraEnabled = false;
        IsMicrophoneEnabled = false;
        Peers.Clear();
        RemoteVideos.Clear();
        HasNoRemoteVideos = true;
        LocalVideoFrame = null;
        
        // 清理 Transport ID
        _sendTransportId = null;
        _recvTransportId = null;
        _videoProducerId = null;
        _audioProducerId = null;
        
        if (result.IsSuccess)
        {
            StatusMessage = "已离开房间";
            _logger.LogInformation("离开房间成功");
        }
        else
        {
            StatusMessage = "已离开房间（本地）";
            _logger.LogWarning("服务器LeaveRoom返回失败，但已重置客户端状态: {Message}", result.Message);
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
                    case "chatMessage":
                        HandleChatMessage(notification.Data);
                        break;
                    case "emojiReaction":
                        HandleEmojiReaction(notification.Data);
                        break;
                    case "screenShareRequest":
                        HandleScreenShareRequest(notification.Data);
                        break;
                    case "screenShareResponse":
                        HandleScreenShareResponse(notification.Data);
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

            // 同步到聊天用户列表
            if (!ChatUsers.Any(u => u.PeerId == notification.Peer.PeerId))
            {
                ChatUsers.Add(new ChatUser
                {
                    PeerId = notification.Peer.PeerId ?? "",
                    DisplayName = notification.Peer.DisplayName ?? "Unknown",
                    IsOnline = true
                });
            }
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

            // 从聊天用户列表移除
            var chatUser = ChatUsers.FirstOrDefault(u => u.PeerId == notification.PeerId);
            if (chatUser != null)
            {
                ChatUsers.Remove(chatUser);
            }
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

    /// <summary>
    /// 处理屏幕共享请求
    /// </summary>
    private void HandleScreenShareRequest(object? data)
    {
        if (data == null) return;

        try
        {
            var json = JsonSerializer.Serialize(data);
            var requestData = JsonSerializer.Deserialize<ScreenShareRequestData>(json, JsonOptions);
            if (requestData == null) return;

            // 忽略自己的请求
            if (requestData.RequesterId == SelectedPeerIndex.ToString()) return;

            _logger.LogInformation("收到屏幕共享请求: {RequesterName}", requestData.RequesterName);

            // 保存当前请求
            _pendingScreenShareRequest = requestData;
            PendingScreenShareRequesterName = requestData.RequesterName ?? "Unknown";
            HasPendingScreenShareRequest = true;

            StatusMessage = $"{requestData.RequesterName} 请求共享屏幕";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理屏幕共享请求失败");
        }
    }

    /// <summary>
    /// 处理屏幕共享响应
    /// </summary>
    private void HandleScreenShareResponse(object? data)
    {
        if (data == null) return;

        try
        {
            var json = JsonSerializer.Serialize(data);
            var responseData = JsonSerializer.Deserialize<ScreenShareResponseData>(json, JsonOptions);
            if (responseData == null) return;

            // 忽略自己的响应
            if (responseData.ResponderId == SelectedPeerIndex.ToString()) return;

            if (responseData.Accepted)
            {
                _logger.LogInformation("屏幕共享被接受");
                StatusMessage = "对方接受了屏幕共享";
            }
            else
            {
                _logger.LogInformation("屏幕共享被拒绝");
                StatusMessage = "对方拒绝了屏幕共享";
                IsScreenSharing = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理屏幕共享响应失败");
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
