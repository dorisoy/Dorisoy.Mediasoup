using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.IO;
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
    private readonly SoundService _soundService;

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
    /// 是否在大厅（已连接但未加入房间）
    /// </summary>
    [ObservableProperty]
    private bool _isInLobby;

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
    /// 在线人数 - 直接等于 Peers 集合的数量（包含自己）
    /// 注意：Peers 集合包含房间内所有用户，包括自己
    /// </summary>
    public int OnlinePeerCount => Peers.Count;

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
    /// 群聊未读消息计数
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGroupUnread))]
    private int _groupUnreadCount;

    /// <summary>
    /// 是否有群聊未读消息
    /// </summary>
    public bool HasGroupUnread => GroupUnreadCount > 0;

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

    #region 主持人相关属性

    /// <summary>
    /// 主持人 PeerId
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHost))]
    private string? _hostPeerId;

    /// <summary>
    /// 当前用户的 PeerId（SignalR 连接 ID）
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHost))]
    private string? _currentPeerId;

    /// <summary>
    /// 当前用户是否是主持人
    /// </summary>
    public bool IsHost => !string.IsNullOrEmpty(HostPeerId) && !string.IsNullOrEmpty(CurrentPeerId) && HostPeerId == CurrentPeerId;

    #endregion

    #region 远程视频控制属性

    /// <summary>
    /// 当前选中的远程视频
    /// </summary>
    [ObservableProperty]
    private RemoteVideoItem? _selectedRemoteVideo;

    /// <summary>
    /// 最大化的远程视频
    /// </summary>
    [ObservableProperty]
    private RemoteVideoItem? _maximizedRemoteVideo;

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

    #region 断线重连相关属性

    /// <summary>
    /// 是否正在重连
    /// </summary>
    [ObservableProperty]
    private bool _isReconnecting;

    /// <summary>
    /// 重连倒计时秒数
    /// </summary>
    [ObservableProperty]
    private int _reconnectCountdown;

    /// <summary>
    /// 重连提示消息
    /// </summary>
    [ObservableProperty]
    private string _reconnectMessage = "";

    /// <summary>
    /// 重连失败后是否显示手动重连按钮
    /// </summary>
    [ObservableProperty]
    private bool _showManualReconnect;

    #endregion

    #region 私有字段

    /// <summary>
    /// 当前用户的访问令牌 - 从 JoinRoomInfo 传入
    /// </summary>
    private string _currentAccessToken = string.Empty;

    /// <summary>
    /// 文件上传服务 - 支持大文件分片上传
    /// </summary>
    private FileUploadService? _fileUploadService;

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
        IWebRtcService webRtcService,
        SoundService soundService)
    {
        _logger = logger;
        _signalRService = signalRService;
        _webRtcService = webRtcService;
        _soundService = soundService;

        // 订阅事件
        _signalRService.OnNotification += HandleNotification;
        _signalRService.OnConnected += OnSignalRConnected;
        _signalRService.OnDisconnected += OnSignalRDisconnected;

        _webRtcService.OnLocalVideoFrame += OnLocalVideoFrameReceived;
        _webRtcService.OnRemoteVideoFrame += OnRemoteVideoFrameReceived;
        _webRtcService.OnConnectionStateChanged += OnWebRtcStateChanged;

        // 订阅 recv transport DTLS 连接完成事件 - 在这之后才能 Resume Consumer
        _webRtcService.OnRecvTransportDtlsConnected += OnRecvTransportDtlsConnected;

        // 订阅 Peers 集合变化事件 - 同步更新在线人数
        Peers.CollectionChanged += (s, e) => OnPropertyChanged(nameof(OnlinePeerCount));

        // 订阅重连事件
        _signalRService.OnReconnecting += OnSignalRReconnecting;
        _signalRService.OnReconnected += OnSignalRReconnected;
        
        // 订阅分块消息重组完成事件
        _signalRService.OnChunkedMessageReceived += OnChunkedMessageReceived;

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
            _signalRService.OnReconnecting -= OnSignalRReconnecting;
            _signalRService.OnReconnected -= OnSignalRReconnected;
            _signalRService.OnChunkedMessageReceived -= OnChunkedMessageReceived;

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

    #region 断线重连处理

    /// <summary>
    /// SignalR 正在重连事件处理
    /// </summary>
    private async void OnSignalRReconnecting(int attempt)
    {
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            IsReconnecting = true;
            ShowManualReconnect = false;
            ReconnectCountdown = 30;
            ReconnectMessage = $"网络连接中断，正在尝试重连 ({attempt})...";
            StatusMessage = "正在重连...";
        });

        // 启动倒计时
        while (IsReconnecting && ReconnectCountdown > 0)
        {
            await Task.Delay(1000);
            
            // 如果已经重连成功，退出循环
            if (_signalRService.IsConnected)
            {
                return;
            }
            
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ReconnectCountdown--;
            });
        }

        // 倒计时结束，检查连接状态
        if (IsReconnecting && !_signalRService.IsConnected)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ReconnectMessage = "重连失败，请检查网络后重试";
                ShowManualReconnect = true;
            });
        }
    }

    /// <summary>
    /// SignalR 重连成功事件处理
    /// </summary>
    private void OnSignalRReconnected()
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            IsReconnecting = false;
            ShowManualReconnect = false;
            ReconnectCountdown = 0;
            ReconnectMessage = "";
            StatusMessage = "重连成功";
            _logger.LogInformation("重连成功");
        });
    }

    /// <summary>
    /// 手动重连命令
    /// </summary>
    [RelayCommand]
    private async Task ManualReconnectAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        try
        {
            ShowManualReconnect = false;
            IsReconnecting = true;
            ReconnectMessage = "正在手动重连...";

            // 先断开现有连接
            await _signalRService.DisconnectAsync();

            // 重新连接
            await _signalRService.ConnectAsync(ServerUrl, _currentAccessToken);

            IsReconnecting = false;
            ReconnectMessage = "";
            StatusMessage = "手动重连成功";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "手动重连失败");
            ReconnectMessage = $"重连失败: {ex.Message}";
            ShowManualReconnect = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

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

    #region 主持人命令

    /// <summary>
    /// 踢出用户
    /// </summary>
    [RelayCommand]
    private async Task KickUser(ChatUser? user)
    {
        if (user == null || !IsHost)
        {
            _logger.LogWarning("踢出用户失败: 用户为空或非主持人");
            return;
        }

        try
        {
            _logger.LogInformation("主持人踢出用户: {PeerId}, {DisplayName}", user.PeerId, user.DisplayName);
            
            var result = await _signalRService.InvokeAsync("KickPeer", new { targetPeerId = user.PeerId });
            if (result.IsSuccess)
            {
                StatusMessage = $"已踢出用户 {user.DisplayName}";
            }
            else
            {
                _logger.LogError("踢出用户失败: {Message}", result.Message);
                StatusMessage = $"踢出失败: {result.Message}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "踢出用户异常");
            StatusMessage = $"踢出失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 远程静音用户
    /// </summary>
    [RelayCommand]
    private async Task MuteUser(ChatUser? user)
    {
        if (user == null || !IsHost)
        {
            _logger.LogWarning("静音用户失败: 用户为空或非主持人");
            return;
        }

        try
        {
            var shouldMute = !user.IsMutedByHost;
            _logger.LogInformation("主持人{Action}用户: {PeerId}, {DisplayName}", 
                shouldMute ? "静音" : "取消静音", user.PeerId, user.DisplayName);
            
            var result = await _signalRService.InvokeAsync("RemoteMutePeer", new 
            { 
                targetPeerId = user.PeerId,
                isMuted = shouldMute
            });
            
            if (result.IsSuccess)
            {
                user.IsMutedByHost = shouldMute;
                StatusMessage = shouldMute ? $"已静音 {user.DisplayName}" : $"已取消静音 {user.DisplayName}";
            }
            else
            {
                _logger.LogError("静音用户失败: {Message}", result.Message);
                StatusMessage = $"操作失败: {result.Message}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "静音用户异常");
            StatusMessage = $"操作失败: {ex.Message}";
        }
    }

    #endregion

    #region 远程视频控制命令

    /// <summary>
    /// 选中远程视频
    /// </summary>
    [RelayCommand]
    private void SelectRemoteVideo(RemoteVideoItem? video)
    {
        if (video == null) return;
        
        // 取消之前选中的视频
        if (SelectedRemoteVideo != null && SelectedRemoteVideo != video)
        {
            SelectedRemoteVideo.IsSelected = false;
        }
        
        // 切换选中状态
        video.IsSelected = !video.IsSelected;
        SelectedRemoteVideo = video.IsSelected ? video : null;
        
        _logger.LogInformation("选中远程视频: {DisplayName}, IsSelected={IsSelected}", 
            video.DisplayName, video.IsSelected);
    }

    /// <summary>
    /// 最大化/还原远程视频
    /// </summary>
    [RelayCommand]
    private void ToggleMaximizeRemoteVideo(RemoteVideoItem? video)
    {
        if (video == null) return;
        
        if (MaximizedRemoteVideo == video)
        {
            // 还原
            video.IsMaximized = false;
            MaximizedRemoteVideo = null;
            _logger.LogInformation("还原远程视频: {DisplayName}", video.DisplayName);
        }
        else
        {
            // 取消之前的最大化
            if (MaximizedRemoteVideo != null)
            {
                MaximizedRemoteVideo.IsMaximized = false;
            }
            // 最大化当前视频
            video.IsMaximized = true;
            MaximizedRemoteVideo = video;
            _logger.LogInformation("最大化远程视频: {DisplayName}", video.DisplayName);
        }
    }

    /// <summary>
    /// 切换远程视频水平镜像
    /// </summary>
    [RelayCommand]
    private void ToggleHorizontalMirror(RemoteVideoItem? video)
    {
        if (video == null) return;
        video.IsHorizontallyMirrored = !video.IsHorizontallyMirrored;
        _logger.LogInformation("切换远程视频水平镜像: {DisplayName}, IsMirrored={IsMirrored}", 
            video.DisplayName, video.IsHorizontallyMirrored);
    }

    /// <summary>
    /// 切换远程视频垂直镜像
    /// </summary>
    [RelayCommand]
    private void ToggleVerticalMirror(RemoteVideoItem? video)
    {
        if (video == null) return;
        video.IsVerticallyMirrored = !video.IsVerticallyMirrored;
        _logger.LogInformation("切换远程视频垂直镜像: {DisplayName}, IsMirrored={IsMirrored}", 
            video.DisplayName, video.IsVerticallyMirrored);
    }

    /// <summary>
    /// 截取远程视频截图
    /// </summary>
    [RelayCommand]
    private async Task ScreenshotRemoteVideo(RemoteVideoItem? video)
    {
        if (video?.VideoFrame == null)
        {
            StatusMessage = "没有可截取的视频帧";
            return;
        }

        try
        {
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PNG 图片|*.png|JPEG 图片|*.jpg",
                DefaultExt = ".png",
                FileName = $"{video.DisplayName}_截图_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (saveDialog.ShowDialog() == true)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    using var fileStream = new FileStream(saveDialog.FileName, FileMode.Create);
                    BitmapEncoder encoder = saveDialog.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                        ? new JpegBitmapEncoder()
                        : new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(video.VideoFrame));
                    encoder.Save(fileStream);
                });
                
                StatusMessage = $"截图已保存: {saveDialog.FileName}";
                _logger.LogInformation("远程视频截图已保存: {Path}", saveDialog.FileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "截图保存失败");
            StatusMessage = $"截图失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 切换远程视频静音
    /// </summary>
    [RelayCommand]
    private void ToggleRemoteVideoMute(RemoteVideoItem? video)
    {
        if (video == null) return;
        video.IsMuted = !video.IsMuted;
        _logger.LogInformation("切换远程视频静音: {DisplayName}, IsMuted={IsMuted}", 
            video.DisplayName, video.IsMuted);
        StatusMessage = video.IsMuted ? $"已静音 {video.DisplayName}" : $"已取消静音 {video.DisplayName}";
    }

    /// <summary>
    /// 取消选中远程视频（点击空白区域）
    /// </summary>
    [RelayCommand]
    private void DeselectRemoteVideo()
    {
        if (SelectedRemoteVideo != null)
        {
            SelectedRemoteVideo.IsSelected = false;
            SelectedRemoteVideo = null;
        }
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
    /// 清空聊天记录
    /// </summary>
    [RelayCommand]
    private async Task ClearChatHistoryAsync()
    {
        // 确认对话框
        var messageBox = new Wpf.Ui.Controls.MessageBox
        {
            Title = "清空聊天记录",
            Content = "确定要清空当前聊天记录吗？此操作不可恢复。",
            PrimaryButtonText = "清空",
            CloseButtonText = "取消",
            PrimaryButtonAppearance = Wpf.Ui.Controls.ControlAppearance.Danger,
        };
        
        var result = await messageBox.ShowDialogAsync();
        if (result != Wpf.Ui.Controls.MessageBoxResult.Primary)
        {
            return;
        }
        
        // 清空当前显示的消息
        CurrentMessages.Clear();
        
        // 清空对应的消息存储
        if (IsGroupChatMode)
        {
            _groupMessages.Clear();
            _logger.LogInformation("已清空群聊记录");
        }
        else if (SelectedChatUser != null)
        {
            var peerId = SelectedChatUser.PeerId;
            if (_privateMessages.ContainsKey(peerId))
            {
                _privateMessages[peerId].Clear();
            }
            _logger.LogInformation("已清空与 {DisplayName} 的私聊记录", SelectedChatUser.DisplayName);
        }
        
        StatusMessage = "聊天记录已清空";
    }

    /// <summary>
    /// 导出聊天记录
    /// </summary>
    [RelayCommand]
    private async Task ExportChatHistoryAsync()
    {
        if (CurrentMessages.Count == 0)
        {
            StatusMessage = "没有聊天记录可导出";
            return;
        }
        
        try
        {
            var chatName = IsGroupChatMode ? "群聊" : SelectedChatUser?.DisplayName ?? "聊天";
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "文本文件|*.txt|HTML 文件|*.html",
                DefaultExt = ".txt",
                FileName = $"聊天记录_{chatName}_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (saveDialog.ShowDialog() == true)
            {
                var isHtml = saveDialog.FileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase);
                var content = isHtml ? BuildHtmlChatHistory(chatName) : BuildTextChatHistory(chatName);
                
                await File.WriteAllTextAsync(saveDialog.FileName, content);
                
                StatusMessage = $"聊天记录已导出: {saveDialog.FileName}";
                _logger.LogInformation("聊天记录已导出: {Path}", saveDialog.FileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出聊天记录失败");
            StatusMessage = $"导出失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 构建文本格式的聊天记录
    /// </summary>
    private string BuildTextChatHistory(string chatName)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"========== {chatName} 聊天记录 ==========");
        sb.AppendLine($"导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"房间号: {RoomId}");
        sb.AppendLine(new string('=', 40));
        sb.AppendLine();
        
        foreach (var msg in CurrentMessages)
        {
            var sender = msg.IsFromSelf ? $"{CurrentUserName}(我)" : msg.SenderName;
            var time = msg.Timestamp.ToString("HH:mm:ss");
            
            switch (msg.MessageType)
            {
                case ChatMessageType.Text:
                    sb.AppendLine($"[{time}] {sender}:");
                    sb.AppendLine($"  {msg.Content}");
                    break;
                case ChatMessageType.Image:
                    sb.AppendLine($"[{time}] {sender}:");
                    sb.AppendLine($"  [图片] {msg.FileName}");
                    break;
                case ChatMessageType.File:
                    sb.AppendLine($"[{time}] {sender}:");
                    sb.AppendLine($"  [文件] {msg.FileName} ({msg.FormattedFileSize})");
                    break;
            }
            sb.AppendLine();
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// 构建 HTML 格式的聊天记录
    /// </summary>
    private string BuildHtmlChatHistory(string chatName)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html><head><meta charset='utf-8'>");
        sb.AppendLine($"<title>{chatName} 聊天记录</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: 'Segoe UI', Arial, sans-serif; max-width: 800px; margin: 0 auto; padding: 20px; background: #f5f5f5; }");
        sb.AppendLine("h1 { color: #333; border-bottom: 2px solid #0078D4; padding-bottom: 10px; }");
        sb.AppendLine(".info { color: #666; margin-bottom: 20px; }");
        sb.AppendLine(".message { margin: 10px 0; padding: 10px; border-radius: 8px; }");
        sb.AppendLine(".message.self { background: #0078D4; color: white; margin-left: 20%; text-align: right; }");
        sb.AppendLine(".message.other { background: white; margin-right: 20%; }");
        sb.AppendLine(".sender { font-weight: bold; font-size: 12px; opacity: 0.8; }");
        sb.AppendLine(".time { font-size: 11px; opacity: 0.6; }");
        sb.AppendLine(".content { margin-top: 5px; }");
        sb.AppendLine(".file-info { font-style: italic; }");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine($"<h1>{chatName} 聊天记录</h1>");
        sb.AppendLine($"<div class='info'>导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss} | 房间号: {RoomId}</div>");
        
        foreach (var msg in CurrentMessages)
        {
            var cssClass = msg.IsFromSelf ? "self" : "other";
            var sender = msg.IsFromSelf ? $"{CurrentUserName}(我)" : msg.SenderName;
            var time = msg.Timestamp.ToString("HH:mm:ss");
            
            sb.AppendLine($"<div class='message {cssClass}'>");
            sb.AppendLine($"<div class='sender'>{sender} <span class='time'>{time}</span></div>");
            
            switch (msg.MessageType)
            {
                case ChatMessageType.Text:
                    sb.AppendLine($"<div class='content'>{System.Web.HttpUtility.HtmlEncode(msg.Content)}</div>");
                    break;
                case ChatMessageType.Image:
                    sb.AppendLine($"<div class='content file-info'>[图片] {msg.FileName}</div>");
                    break;
                case ChatMessageType.File:
                    sb.AppendLine($"<div class='content file-info'>[文件] {msg.FileName} ({msg.FormattedFileSize})</div>");
                    break;
            }
            sb.AppendLine("</div>");
        }
        
        sb.AppendLine("</body></html>");
        return sb.ToString();
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
    /// 请求返回加入房间窗口事件（离开房间后）
    /// </summary>
    public event Action? ReturnToJoinRoomRequested;

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
            // 1. 立即应用加入信息并同步到 UI - 这样窗口显示时就能看到正确的房间号
            ServerUrl = joinInfo.ServerUrl;
            CurrentUserName = joinInfo.UserName;
            _currentAccessToken = joinInfo.AccessToken;
            
            // 设置 RoomId 并强制触发 UI 更新
            RoomId = joinInfo.RoomId;
            OnPropertyChanged(nameof(RoomId));
            
            // 设置初始媒体状态（根据用户在 JoinRoomWindow 中的选择）
            // 注意：这里设置的是"previewState"，实际开启在后面处理
            var shouldEnableCamera = !joinInfo.MuteCameraOnJoin;
            var shouldEnableMic = !joinInfo.MuteMicrophoneOnJoin;
            
            _logger.LogInformation("自动加入开始: ServerUrl={ServerUrl}, UserName={UserName}, RoomId={RoomId}, EnableCamera={EnableCamera}, EnableMic={EnableMic}", 
                ServerUrl, CurrentUserName, RoomId, shouldEnableCamera, shouldEnableMic);

            // 2. 设置选中的设备
            if (!string.IsNullOrEmpty(joinInfo.CameraDeviceId))
            {
                SelectedCamera = Cameras.FirstOrDefault(c => c.DeviceId == joinInfo.CameraDeviceId) ?? Cameras.FirstOrDefault();
            }
            if (!string.IsNullOrEmpty(joinInfo.MicrophoneDeviceId))
            {
                SelectedMicrophone = Microphones.FirstOrDefault(m => m.DeviceId == joinInfo.MicrophoneDeviceId) ?? Microphones.FirstOrDefault();
            }

            // 3. 连接服务器
            StatusMessage = "正在连接服务器...";
            await ConnectAsync();

            if (!IsConnected)
            {
                StatusMessage = "连接服务器失败";
                return;
            }

            // 4. 加入房间
            StatusMessage = "正在加入房间...";
            await JoinRoomAsync();

            if (!IsJoinedRoom)
            {
                StatusMessage = "加入房间失败";
                return;
            }

            // 5. 根据设置控制摄像头和麦克风 - 在加入房间成功后处理
            // EnableMediaAsync 已经在 JoinRoomAsync -> CreateTransportsAsync 后调用
            // 这里根据用户设置决定是否需要关闭某个媒体
            if (joinInfo.MuteCameraOnJoin && IsCameraEnabled)
            {
                await ToggleCameraAsync(); // 关闭摄像头
            }
            if (joinInfo.MuteMicrophoneOnJoin && IsMicrophoneEnabled)
            {
                await ToggleMicrophoneAsync(); // 关闭麦克风
            }

            // 6. 最终状态同步
            SyncAllStates();
            
            StatusMessage = $"已加入房间 {RoomId}";
            _logger.LogInformation("自动加入成功: RoomId={RoomId}, IsJoinedRoom={IsJoinedRoom}, OnlinePeerCount={OnlinePeerCount}", 
                RoomId, IsJoinedRoom, OnlinePeerCount);
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
    
    /// <summary>
    /// 同步所有 UI 状态 - 强制触发所有相关属性的通知（强制在 UI 线程上执行）
    /// </summary>
    private void SyncAllStates()
    {
        _logger.LogInformation("SyncAllStates 开始: IsJoinedRoom={IsJoinedRoom}, IsCameraEnabled={IsCameraEnabled}, IsMicrophoneEnabled={IsMicrophoneEnabled}, RoomId={RoomId}",
            IsJoinedRoom, IsCameraEnabled, IsMicrophoneEnabled, RoomId);
        
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            _logger.LogWarning("SyncAllStates: Dispatcher 为 null");
            DoSyncAllStates();
            return;
        }
        
        // 使用同步 Invoke，确保 UI 更新完成后再返回
        if (!dispatcher.CheckAccess())
        {
            dispatcher.Invoke(DoSyncAllStates);
        }
        else
        {
            DoSyncAllStates();
        }
    }
    
    /// <summary>
    /// 实际执行所有状态同步
    /// </summary>
    private void DoSyncAllStates()
    {
        _logger.LogDebug("DoSyncAllStates 执行");
        
        OnPropertyChanged(nameof(RoomId));
        OnPropertyChanged(nameof(IsJoinedRoom));
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(IsCameraEnabled));
        OnPropertyChanged(nameof(IsMicrophoneEnabled));
        OnPropertyChanged(nameof(OnlinePeerCount));
        OnPropertyChanged(nameof(CanJoinRoom));
        OnPropertyChanged(nameof(CanToggleMedia));
        
        _logger.LogInformation("SyncAllStates 完成: IsJoinedRoom={IsJoinedRoom}, IsCameraEnabled={IsCameraEnabled}, IsMicrophoneEnabled={IsMicrophoneEnabled}, OnlinePeerCount={OnlinePeerCount}",
            IsJoinedRoom, IsCameraEnabled, IsMicrophoneEnabled, OnlinePeerCount);
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

            // 播放表情对应的音效
            _soundService.PlayEmojiSoundByEmoji(reaction.Emoji);

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
        
        // 清除群聊未读计数
        GroupUnreadCount = 0;
        
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
    /// 发送图片消息 - 通过文件上传服务传输 URL
    /// 避免 Base64 超过 SignalR 消息大小限制 (32KB)
    /// </summary>
    public async void SendImageMessage(string filePath, string? receiverId)
    {
        if (!IsJoinedRoom) return;

        try
        {
            var fileInfo = new System.IO.FileInfo(filePath);
            
            // 限制图片大小为 50MB
            const long maxImageSize = 50 * 1024 * 1024;
            if (fileInfo.Length > maxImageSize)
            {
                StatusMessage = $"图片大小超过 50MB 限制，无法发送";
                _logger.LogWarning("图片太大: {Size} bytes, 限制: {Limit} bytes", fileInfo.Length, maxImageSize);
                return;
            }

            // 确保文件上传服务已初始化
            EnsureFileUploadServiceInitialized();
            if (_fileUploadService == null)
            {
                StatusMessage = "文件上传服务未初始化";
                return;
            }

            StatusMessage = $"正在上传图片: {fileInfo.Name} (0%)";
            _logger.LogInformation("开始上传图片: {FileName}, 大小: {Size} bytes", fileInfo.Name, fileInfo.Length);

            // 上传图片到服务器
            var result = await _fileUploadService.UploadFileAsync(filePath, progress =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"正在上传图片: {fileInfo.Name} ({progress:F0}%)";
                });
            });

            if (!result.Success)
            {
                StatusMessage = $"图片上传失败: {result.Message}";
                _logger.LogError("图片上传失败: {FileName}, 错误: {Error}", fileInfo.Name, result.Message);
                return;
            }

            // 获取完整的下载 URL
            var downloadUrl = _fileUploadService.GetFullDownloadUrl(result.DownloadUrl!);

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
                DownloadUrl = downloadUrl,
                IsFromSelf = true
            };

            // 加载图片用于本地显示
            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(filePath);
            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            message.ImageSource = bitmap;

            AddMessageToCollection(message);

            // 发送包含下载 URL 的消息（不再发送 Base64）
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
                    downloadUrl = downloadUrl, // 使用 URL 而非 Base64
                    timestamp = message.Timestamp
                }
            });

            StatusMessage = "图片已发送";
            _logger.LogInformation("图片发送成功: {FileName}, 大小: {Size} bytes, URL: {Url}", fileInfo.Name, fileInfo.Length, downloadUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送图片失败");
            StatusMessage = $"发送图片失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 发送文件消息 - 通过文件上传服务传输 URL
    /// 最大支持 500MB
    /// </summary>
    public async void SendFileMessage(string filePath, string? receiverId)
    {
        if (!IsJoinedRoom) return;

        try
        {
            var fileInfo = new System.IO.FileInfo(filePath);
            
            // 最大限制：500MB
            const long maxFileSize = 500L * 1024 * 1024;

            if (fileInfo.Length > maxFileSize)
            {
                StatusMessage = $"文件大小超过 500MB 限制，无法发送";
                _logger.LogWarning("文件太大: {Size} bytes, 限制: {Limit} bytes", fileInfo.Length, maxFileSize);
                return;
            }

            // 统一使用文件上传服务（避免 SignalR 消息大小限制）
            await SendFileViaUploadServiceAsync(filePath, fileInfo, receiverId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送文件失败");
            StatusMessage = $"发送文件失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 通过文件上传服务发送文件
    /// </summary>
    private async Task SendFileViaUploadServiceAsync(string filePath, System.IO.FileInfo fileInfo, string? receiverId)
    {
        // 确保 FileUploadService 已初始化
        EnsureFileUploadServiceInitialized();

        if (_fileUploadService == null)
        {
            StatusMessage = "文件上传服务未初始化";
            return;
        }

        StatusMessage = $"正在上传文件: {fileInfo.Name} (0%)";
        _logger.LogInformation("开始分片上传大文件: {FileName}, 大小: {Size} bytes", fileInfo.Name, fileInfo.Length);

        // 上传文件
        var result = await _fileUploadService.UploadFileAsync(filePath, progress =>
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                StatusMessage = $"正在上传文件: {fileInfo.Name} ({progress:F0}%)";
            });
        });

        if (!result.Success)
        {
            StatusMessage = $"文件上传失败: {result.Message}";
            _logger.LogError("文件上传失败: {FileName}, 错误: {Error}", fileInfo.Name, result.Message);
            return;
        }

        // 获取完整的下载 URL
        var downloadUrl = _fileUploadService.GetFullDownloadUrl(result.DownloadUrl!);

        var message = new ChatMessage
        {
            SenderId = SelectedPeerIndex.ToString(),
            SenderName = CurrentUserName,
            ReceiverId = receiverId ?? "",
            Content = $"[文件] {fileInfo.Name}",
            MessageType = ChatMessageType.File,
            FileName = fileInfo.Name,
            FilePath = downloadUrl, // 使用下载 URL 作为文件路径
            FileSize = fileInfo.Length,
            DownloadUrl = downloadUrl,
            IsFromSelf = true
        };

        AddMessageToCollection(message);

        // 发送包含下载 URL 的消息
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
                downloadUrl = downloadUrl,
                timestamp = message.Timestamp
            }
        });

        StatusMessage = $"文件已发送: {fileInfo.Name}";
        _logger.LogInformation("大文件发送成功 (分片上传): {FileName}, 大小: {Size} bytes, URL: {Url}",
            fileInfo.Name, fileInfo.Length, downloadUrl);
    }

    /// <summary>
    /// 确保文件上传服务已初始化
    /// </summary>
    private void EnsureFileUploadServiceInitialized()
    {
        if (_fileUploadService == null && !string.IsNullOrEmpty(ServerUrl))
        {
            _fileUploadService = new FileUploadService(ServerUrl, _currentAccessToken);
            _logger.LogDebug("文件上传服务已初始化: {ServerUrl}", ServerUrl);
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
                // 私聊消息 - 确定对方 ID
                // 自己发的，对方是接收者；别人发的，对方是发送者
                string peerId = message.IsFromSelf
                    ? message.ReceiverId    // 自己发的，对方是接收者
                    : message.SenderId;      // 别人发的，对方是发送者

                if (!_privateMessages.TryGetValue(peerId, out var messages))
                {
                    messages = [];
                    _privateMessages[peerId] = messages;
                }
                messages.Add(message);

                // 更新当前显示
                if (!IsGroupChatMode && SelectedChatUser?.PeerId == peerId)
                {
                    CurrentMessages.Add(message);
                }
            }
        });
    }

    /// <summary>
    /// 从 URL 异步加载图片
    /// </summary>
    private async Task LoadImageFromUrlAsync(ChatMessage message, string url)
    {
        try
        {
            using var httpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var imageBytes = await httpClient.GetByteArrayAsync(url).ConfigureAwait(false);

            Application.Current?.Dispatcher.Invoke(() =>
            {
                using var ms = new System.IO.MemoryStream(imageBytes);
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = ms;
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                message.ImageSource = bitmap;
            });

            _logger.LogDebug("已从 URL 加载图片: {FileName}, 大小: {Size} bytes", message.FileName, imageBytes.Length);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "从 URL 加载图片失败: {Url}", url);
        }
    }

    /// <summary>
    /// 处理接收到的消息
    /// </summary>
    private async void HandleChatMessage(object? data)
    {
        if (data == null) return;

        try
        {
            var json = JsonSerializer.Serialize(data);
            var msgData = JsonSerializer.Deserialize<ChatMessageData>(json, JsonOptions);
            if (msgData == null) return;

            // 忽略自己发送的消息
            if (msgData.SenderId == CurrentUserName) return;

            // 检查私聊消息：如果有 ReceiverId 且不是发给自己的，忽略
            if (!string.IsNullOrEmpty(msgData.ReceiverId) && msgData.ReceiverId != CurrentUserName)
            {
                return;
            }

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
                FileData = msgData.FileData,
                DownloadUrl = msgData.DownloadUrl,
                Timestamp = msgData.Timestamp ?? DateTime.Now,
                IsFromSelf = false
            };

            // 如果是文件消息，记录日志
            if (message.MessageType == ChatMessageType.File)
            {
                if (!string.IsNullOrEmpty(msgData.DownloadUrl))
                {
                    _logger.LogDebug("接收到大文件消息: {FileName}, 大小: {Size}, URL: {Url}",
                        message.FileName, message.FileSize, msgData.DownloadUrl);
                }
                else if (!string.IsNullOrEmpty(msgData.FileData))
                {
                    _logger.LogDebug("接收到小文件消息 (Base64): {FileName}, 大小: {Size}",
                        message.FileName, message.FileSize);
                }
            }

            // 如果是图片消息，优先从 URL 下载，否则使用 Base64
            if (message.MessageType == ChatMessageType.Image)
            {
                // 优先使用 DownloadUrl
                if (!string.IsNullOrEmpty(msgData.DownloadUrl))
                {
                    try
                    {
                        _logger.LogDebug("从 URL 加载图片: {Url}", msgData.DownloadUrl);
                        await LoadImageFromUrlAsync(message, msgData.DownloadUrl);
                    }
                    catch (Exception imgEx)
                    {
                        _logger.LogWarning(imgEx, "从 URL 加载图片失败: {Url}", msgData.DownloadUrl);
                    }
                }
                // 宜后考虑 Base64（向后兼容）
                else if (!string.IsNullOrEmpty(msgData.FileData))
                {
                    try
                    {
                        var imageBytes = Convert.FromBase64String(msgData.FileData);
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            using var ms = new System.IO.MemoryStream(imageBytes);
                            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                            bitmap.BeginInit();
                            bitmap.StreamSource = ms;
                            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            bitmap.Freeze();
                            message.ImageSource = bitmap;
                        });
                        _logger.LogDebug("已加载接收的图片 (Base64): {FileName}, 大小: {Size} bytes", message.FileName, imageBytes.Length);
                    }
                    catch (Exception imgEx)
                    {
                        _logger.LogWarning(imgEx, "加载接收图片失败: {FileName}", message.FileName);
                    }
                }
            }

            AddMessageToCollection(message);

            // 如果不在当前聊天，增加未读数
            if (string.IsNullOrEmpty(message.ReceiverId))
            {
                // 群聊消息 - 如果不在群聊模式或聊天面板不可见，增加群聊未读计数
                if (!IsChatPanelVisible || !IsGroupChatMode)
                {
                    GroupUnreadCount++;
                }
            }
            else
            {
                // 私聊消息 - 始终更新 LastMessage，但只在不在当前聊天时增加未读数
                var user = ChatUsers.FirstOrDefault(u => u.PeerId == message.SenderId);
                if (user != null)
                {
                    // 始终更新最后一条消息
                    user.LastMessage = message;
                    
                    // 如果聊天面板不可见，或在群聊模式，或当前私聊对象不是发送者，增加未读数
                    if (!IsChatPanelVisible || IsGroupChatMode || SelectedChatUser?.PeerId != message.SenderId)
                    {
                        user.UnreadCount++;
                    }
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
        _logger.LogInformation("=== IsJoinedRoom 属性变化: OldValue -> NewValue={NewValue}, 当前线程={ThreadId} ===", 
            value, System.Threading.Thread.CurrentThread.ManagedThreadId);
        
        // 通知相关计算属性
        OnPropertyChanged(nameof(CanToggleMedia));
        
        // 输出堆栈追踪以便调试
        _logger.LogDebug("IsJoinedRoom 变化调用栈: {StackTrace}", 
            new System.Diagnostics.StackTrace().ToString());
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

                UpdateMediaStateOnUiThread(camera: false, mic: null);
                LocalVideoFrame = null;
                StatusMessage = "摄像头已关闭";
                _logger.LogInformation("摄像头已关闭");
            }
            else
            {
                // 启动摄像头（使用选中的设备）
                var deviceId = SelectedCamera?.DeviceId;
                await _webRtcService.StartCameraAsync(deviceId);
                UpdateMediaStateOnUiThread(camera: true, mic: null);
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

                UpdateMediaStateOnUiThread(camera: null, mic: false);
                StatusMessage = "麦克风已关闭";
                _logger.LogInformation("麦克风已关闭");
            }
            else
            {
                // 启动麦克风（使用选中的设备）
                var deviceId = SelectedMicrophone?.DeviceId;
                await _webRtcService.StartMicrophoneAsync(deviceId);
                UpdateMediaStateOnUiThread(camera: null, mic: true);
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
            
            // 使用动态获取的 Token
            if (string.IsNullOrEmpty(_currentAccessToken))
            {
                _logger.LogError("访问令牌为空，无法连接");
                StatusMessage = "访问令牌无效";
                return;
            }
            
            await _signalRService.ConnectAsync(ServerUrl, _currentAccessToken);
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

        // 4. 加入会议 - 使用真实用户名
        var joinRequest = new
        {
            rtpCapabilities = _routerRtpCapabilities,
            sctpCapabilities = (object?)null,
            displayName = CurrentUserName,  // 使用用户输入的真实用户名
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
        _logger.LogInformation("调用JoinRoom: RoomId={RoomId}, IsAdmin={IsAdmin}, CurrentUserName={CurrentUserName}", 
            roomIdToJoin, isAdmin, CurrentUserName);

        var result = await _signalRService.InvokeAsync<JoinRoomResponse>("JoinRoom", joinRoomRequest);
        
        // 详细日志：输出 JoinRoom 响应的完整信息
        _logger.LogInformation("JoinRoom 响应: IsSuccess={IsSuccess}, Code={Code}, Message={Message}, Data={Data}, Peers={Peers}", 
            result.IsSuccess, result.Code, result.Message, 
            result.Data != null ? "not null" : "null",
            result.Data?.Peers?.Length ?? 0);
        
        if (!result.IsSuccess)
        {
            _logger.LogError("JoinRoom failed: {Message}", result.Message);
            
            // 检查是否是"已在房间中"的错误
            if (result.Message?.Contains("already") == true || result.Message?.Contains("已在") == true)
            {
                _logger.LogWarning("检测到已在房间中，同步状态为已加入");
                SyncRoomState(roomIdToJoin, true);
                StatusMessage = $"已在房间 {roomIdToJoin} 中";
                return;
            }
            
            StatusMessage = $"加入房间失败: {result.Message}";
            return;
        }

        // 在 UI 线程上更新 Peer 列表和 ChatUsers 列表
        var peersData = result.Data?.Peers;
        var hostPeerId = result.Data?.HostPeerId;
        var selfPeerId = result.Data?.SelfPeerId;
        var peersCount = peersData?.Length ?? 0;
        _logger.LogInformation("处理房间内 Peers: Count={Count}, HostPeerId={HostPeerId}, SelfPeerId={SelfPeerId}", peersCount, hostPeerId, selfPeerId);
        
        // 保存主持人信息和当前用户 PeerId
        HostPeerId = hostPeerId;
        
        // 直接使用服务端返回的 SelfPeerId，避免通过 DisplayName 比较的不可靠性
        if (!string.IsNullOrEmpty(selfPeerId))
        {
            CurrentPeerId = selfPeerId;
            _logger.LogInformation("使用服务端返回的 SelfPeerId: {SelfPeerId}", selfPeerId);
        }
        
        // 使用 Dispatcher.Invoke 确保在 UI 线程上更新集合
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => UpdatePeersCollection(peersData, roomIdToJoin));
        }
        else
        {
            UpdatePeersCollection(peersData, roomIdToJoin);
        }
        
        _logger.LogInformation("加入房间成功: RoomId={RoomId}, PeerCount={PeerCount}, ChatUserCount={ChatUserCount}, IsHost={IsHost}", 
            roomIdToJoin, Peers.Count, ChatUsers.Count, IsHost);

        // 创建 WebRTC Transport
        await CreateTransportsAsync();

        // 记录 ServeMode 状态
        _logger.LogInformation("当前 ServeMode={ServeMode}，准备启用媒体", ServeMode);
        
        // 如果是 Open 模式，自动开始生产
        if (ServeMode == "Open")
        {
            _logger.LogInformation("开始调用 EnableMediaAsync...");
            await EnableMediaAsync();
            _logger.LogInformation("EnableMediaAsync 完成: IsCameraEnabled={IsCameraEnabled}, IsMicrophoneEnabled={IsMicrophoneEnabled}", 
                IsCameraEnabled, IsMicrophoneEnabled);
        }
        else
        {
            _logger.LogWarning("ServeMode={ServeMode} 不是 Open，跳过 EnableMediaAsync", ServeMode);
        }

        // 通知服务器准备就绪
        if (ServeMode != "Pull")
        {
            await _signalRService.InvokeAsync("Ready");
        }

        StatusMessage = $"已加入房间 {roomIdToJoin}";
    }
    
    /// <summary>
    /// 更新 Peers 集合 - 必须在 UI 线程上调用
    /// </summary>
    private void UpdatePeersCollection(PeerInfo[]? peersData, string roomIdToJoin)
    {
        _logger.LogDebug("UpdatePeersCollection: 清空集合并添加新数据...");
        
        Peers.Clear();
        ChatUsers.Clear();
        
        if (peersData != null)
        {
            _logger.LogInformation("开始添加 Peers 到集合，返回的 Peers 数量: {Count}", peersData.Length);
            
            foreach (var peer in peersData)
            {
                _logger.LogInformation("处理 Peer: PeerId={PeerId}, DisplayName={DisplayName}", 
                    peer.PeerId, peer.DisplayName);
                    
                Peers.Add(peer);
                
                // 同步到聊天用户列表（排除自己）
                // 优先使用 CurrentPeerId 比较，回退到 DisplayName 比较
                bool isSelf = !string.IsNullOrEmpty(CurrentPeerId) 
                    ? peer.PeerId == CurrentPeerId
                    : string.Equals(peer.DisplayName, CurrentUserName, StringComparison.OrdinalIgnoreCase);
                
                if (isSelf && string.IsNullOrEmpty(CurrentPeerId))
                {
                    // 如果 CurrentPeerId 未设置（服务端未返回 SelfPeerId），通过 DisplayName 查找并设置
                    CurrentPeerId = peer.PeerId;
                    _logger.LogInformation("通过 DisplayName 设置 CurrentPeerId: {PeerId}", peer.PeerId);
                }
                
                if (!isSelf && !string.IsNullOrEmpty(peer.PeerId))
                {
                    _logger.LogDebug("添加聊天用户: PeerId={PeerId}, DisplayName={DisplayName}", 
                        peer.PeerId, peer.DisplayName);
                        
                    ChatUsers.Add(new ChatUser
                    {
                        PeerId = peer.PeerId,
                        DisplayName = peer.DisplayName ?? "Unknown",
                        IsOnline = true
                    });
                }
                else
                {
                    _logger.LogDebug("跳过自己: PeerId={PeerId}, DisplayName={DisplayName}, IsSelf={IsSelf}", 
                        peer.PeerId, peer.DisplayName, isSelf);
                }
            }
        }
        
        // 立即同步房间状态到 UI（已在 UI 线程上）
        DoSyncRoomState(roomIdToJoin, true);
        
        _logger.LogInformation("UpdatePeersCollection 完成: PeerCount={PeerCount}, ChatUserCount={ChatUserCount}, IsJoinedRoom={IsJoinedRoom}", 
            Peers.Count, ChatUsers.Count, IsJoinedRoom);
    }
    
    /// <summary>
    /// 同步房间状态到 UI - 确保所有相关属性都被正确通知（强制在 UI 线程上执行）
    /// </summary>
    /// <param name="roomId">房间ID</param>
    /// <param name="isJoined">是否已加入</param>
    private void SyncRoomState(string roomId, bool isJoined)
    {
        _logger.LogInformation("同步房间状态: RoomId={RoomId}, IsJoined={IsJoined}, CurrentIsJoinedRoom={CurrentIsJoinedRoom}", 
            roomId, isJoined, IsJoinedRoom);
        
        // 强制通过 Dispatcher 执行 UI 更新
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            _logger.LogWarning("Dispatcher 为 null，直接更新状态");
            DoSyncRoomState(roomId, isJoined);
            return;
        }
        
        // 使用同步 Invoke，确保状态更新完成后再继续执行
        if (!dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => DoSyncRoomState(roomId, isJoined));
        }
        else
        {
            DoSyncRoomState(roomId, isJoined);
        }
    }
    
    /// <summary>
    /// 实际执行房间状态同步
    /// </summary>
    private void DoSyncRoomState(string roomId, bool isJoined)
    {
        _logger.LogDebug("DoSyncRoomState 执行: RoomId={RoomId}, IsJoined={IsJoined}", roomId, isJoined);
        
        // 确保 RoomId 被更新（即使值相同也触发通知）
        var oldRoomId = RoomId;
        RoomId = roomId;
        if (oldRoomId == roomId)
        {
            OnPropertyChanged(nameof(RoomId));
        }
        
        // 更新加入状态
        IsJoinedRoom = isJoined;
        IsInLobby = !isJoined;
        
        // 手动触发所有相关属性的通知，确保 UI 更新
        OnPropertyChanged(nameof(IsJoinedRoom));
        OnPropertyChanged(nameof(OnlinePeerCount));
        OnPropertyChanged(nameof(CanJoinRoom));
        OnPropertyChanged(nameof(CanToggleMedia));
        OnPropertyChanged(nameof(IsHost));
        
        _logger.LogInformation("房间状态同步完成: IsJoinedRoom={IsJoinedRoom}, OnlinePeerCount={OnlinePeerCount}", 
            IsJoinedRoom, OnlinePeerCount);
    }

    /// <summary>
    /// 离开房间（回到大厅，保持SignalR连接）
    /// </summary>
    private async Task LeaveRoomAsync()
    {
        _logger.LogInformation("开始离开房间...");
        
        try
        {
            // 调用服务器 LeaveRoom，但保持 SignalR 连接
            await _signalRService.InvokeAsync("LeaveRoom");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "调用 LeaveRoom 失败");
        }
        
        // 关闭 WebRTC 媒体，但保持 SignalR 连接
        await _webRtcService.CloseAsync();
        
        // 重置房间相关状态，但保持连接状态
        IsJoinedRoom = false;
        IsInLobby = true;  // 回到大厅状态
        IsCameraEnabled = false;
        IsMicrophoneEnabled = false;
        Peers.Clear();
        RemoteVideos.Clear();
        ChatUsers.Clear();
        HasNoRemoteVideos = true;
        LocalVideoFrame = null;
        
        // 清理主持人和当前用户 ID
        HostPeerId = null;
        CurrentPeerId = null;
        
        // 清理 Transport ID
        _sendTransportId = null;
        _recvTransportId = null;
        _videoProducerId = null;
        _audioProducerId = null;
        
        StatusMessage = "已回到大厅";
        _logger.LogInformation("离开房间完成，回到大厅");
        
        // 通知窗口返回加入房间界面（但保持 SignalR 连接）
        ReturnToJoinRoomRequested?.Invoke();
    }

    /// <summary>
    /// 被踢出房间后的强制离开（只清理本地状态，不调用服务端，完全断开连接）
    /// 用于被踢出场景：服务端已经移除了用户，不需要再调用 LeaveRoom
    /// </summary>
    private async Task ForceLeaveRoomLocalAsync()
    {
        _logger.LogInformation("被踢出房间，开始强制清理本地状态...");
        
        // 完全关闭 WebRTC 服务
        await _webRtcService.CloseAsync();
        
        // 断开 SignalR 连接（被踢用户不应保持连接）
        await _signalRService.DisconnectAsync();
        
        // 重置所有状态
        IsJoinedRoom = false;
        IsInLobby = false;
        IsConnected = false;
        IsCameraEnabled = false;
        IsMicrophoneEnabled = false;
        
        // 清空所有集合
        Peers.Clear();
        RemoteVideos.Clear();
        ChatUsers.Clear();
        HasNoRemoteVideos = true;
        LocalVideoFrame = null;
        
        // 清理主持人和当前用户 ID
        HostPeerId = null;
        CurrentPeerId = null;
        
        // 清理 Transport ID
        _sendTransportId = null;
        _recvTransportId = null;
        _videoProducerId = null;
        _audioProducerId = null;
        _currentAccessToken = string.Empty;
        
        StatusMessage = "已被踢出房间";
        _logger.LogInformation("被踢出房间，本地状态清理完成，已断开连接");
    }

    /// <summary>
    /// 完全退出会议（断开SignalR连接，返回JoinRoomWindow）
    /// </summary>
    [RelayCommand]
    private async Task ExitToJoinWindowAsync()
    {
        _logger.LogInformation("完全退出会议...");
        
        try
        {
            // 如果在房间中，先离开房间
            if (IsJoinedRoom)
            {
                await _signalRService.InvokeAsync("LeaveRoom");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "退出时调用 LeaveRoom 失败");
        }
        
        // 断开 SignalR 连接
        await _signalRService.DisconnectAsync();
        
        // 完全关闭 WebRTC 服务
        await _webRtcService.CloseAsync();
        
        // 重置所有状态
        IsJoinedRoom = false;
        IsInLobby = false;
        IsConnected = false;
        IsCameraEnabled = false;
        IsMicrophoneEnabled = false;
        Peers.Clear();
        RemoteVideos.Clear();
        ChatUsers.Clear();
        HasNoRemoteVideos = true;
        LocalVideoFrame = null;
        
        // 清理主持人和当前用户 ID
        HostPeerId = null;
        CurrentPeerId = null;
        
        // 清理 Transport ID
        _sendTransportId = null;
        _recvTransportId = null;
        _videoProducerId = null;
        _audioProducerId = null;
        _currentAccessToken = string.Empty;
        
        StatusMessage = "已断开连接";
        _logger.LogInformation("完全退出会议，返回加入窗口");
        
        // 通知窗口返回加入房间界面
        ReturnToJoinRoomRequested?.Invoke();
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
            
            // 确保在 UI 线程上更新状态
            UpdateMediaStateOnUiThread(camera: true, mic: null);
            StatusMessage = "摄像头采集中...";

            // 调用 Produce 推送视频
            if (!string.IsNullOrEmpty(_sendTransportId))
            {
                await ProduceVideoAsync();
            }

            await _webRtcService.StartMicrophoneAsync(micDeviceId);
            
            // 确保在 UI 线程上更新状态
            UpdateMediaStateOnUiThread(camera: null, mic: true);

            // 调用 Produce 推送音频
            if (!string.IsNullOrEmpty(_sendTransportId))
            {
                await ProduceAudioAsync();
            }

            _logger.LogInformation("Media enabled: IsCameraEnabled={IsCameraEnabled}, IsMicrophoneEnabled={IsMicrophoneEnabled}", 
                IsCameraEnabled, IsMicrophoneEnabled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable media");
        }
    }
    
    /// <summary>
    /// 在 UI 线程上更新媒体状态
    /// </summary>
    /// <param name="camera">摄像头状态，null 表示不更新</param>
    /// <param name="mic">麦克风状态，null 表示不更新</param>
    private void UpdateMediaStateOnUiThread(bool? camera, bool? mic)
    {
        _logger.LogDebug("UpdateMediaStateOnUiThread: camera={Camera}, mic={Mic}", camera, mic);
        
        void DoUpdate()
        {
            if (camera.HasValue)
            {
                IsCameraEnabled = camera.Value;
                OnPropertyChanged(nameof(IsCameraEnabled));
                OnPropertyChanged(nameof(CanToggleMedia));
                _logger.LogDebug("IsCameraEnabled 已更新为 {Value}", camera.Value);
            }
            if (mic.HasValue)
            {
                IsMicrophoneEnabled = mic.Value;
                OnPropertyChanged(nameof(IsMicrophoneEnabled));
                OnPropertyChanged(nameof(CanToggleMedia));
                _logger.LogDebug("IsMicrophoneEnabled 已更新为 {Value}", mic.Value);
            }
        }
        
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            _logger.LogWarning("UpdateMediaStateOnUiThread: Dispatcher 为 null");
            DoUpdate();
            return;
        }
        
        // 使用 BeginInvoke 异步执行，确保 UI 更新
        dispatcher.BeginInvoke(new Action(DoUpdate), System.Windows.Threading.DispatcherPriority.Send);
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
            _logger.LogDebug("收到远端视频帧: ConsumerId={ConsumerId}, Frame={Width}x{Height}",
                consumerId, frame?.PixelWidth ?? 0, frame?.PixelHeight ?? 0);
            
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
    /// 处理分块消息重组完成事件
    /// 当服务器将大消息分块发送到客户端并重组后触发
    /// </summary>
    private void OnChunkedMessageReceived(string type, string json)
    {
        _logger.LogDebug("分块消息重组完成: Type={Type}", type);
        
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            try
            {
                // 根据类型处理重组后的消息
                switch (type)
                {
                    case "chatMessage":
                        // 解析聊天消息并处理
                        var chatData = JsonSerializer.Deserialize<object>(json, JsonOptions);
                        HandleChatMessage(chatData);
                        break;
                    case "broadcastMessage":
                        // 解析广播消息并处理
                        var broadcastData = JsonSerializer.Deserialize<object>(json, JsonOptions);
                        HandleBroadcastMessage(broadcastData);
                        break;
                    default:
                        _logger.LogDebug("未处理的分块消息类型: {Type}", type);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理分块消息失败: Type={Type}", type);
            }
        });
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
                    case "broadcastMessage":
                        HandleBroadcastMessage(notification.Data);
                        break;
                    case "peerKicked":
                        HandlePeerKicked(notification.Data);
                        break;
                    case "peerMuted":
                        HandlePeerMuted(notification.Data);
                        break;
                    case "roomDismissed":
                        HandleRoomDismissed(notification.Data);
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
            // 检查是否已存在，避免重复添加
            var existingPeer = Peers.FirstOrDefault(p => p.PeerId == notification.Peer.PeerId);
            if (existingPeer == null)
            {
                Peers.Add(notification.Peer);
            }
            
            _logger.LogInformation("Peer joined: PeerId={PeerId}, DisplayName={DisplayName}", 
                notification.Peer.PeerId, notification.Peer.DisplayName);
            StatusMessage = $"用户 {notification.Peer.DisplayName} 加入房间";

            // 同步到聊天用户列表 - 使用 DisplayName 比较来排除自己
            bool isSelf = string.Equals(notification.Peer.DisplayName, CurrentUserName, StringComparison.OrdinalIgnoreCase);
            
            if (!isSelf && !ChatUsers.Any(u => u.PeerId == notification.Peer.PeerId))
            {
                ChatUsers.Add(new ChatUser
                {
                    PeerId = notification.Peer.PeerId ?? "",
                    DisplayName = notification.Peer.DisplayName ?? "Unknown",
                    IsOnline = true
                });
                _logger.LogDebug("添加新聊天用户: {DisplayName}", notification.Peer.DisplayName);
            }
            
            // 手动触发在线人数更新
            OnPropertyChanged(nameof(OnlinePeerCount));
        }
    }

    private void HandlePeerLeaveRoom(object? data)
    {
        if (data == null) return;

        var json = JsonSerializer.Serialize(data);
        var notification = JsonSerializer.Deserialize<PeerLeaveRoomData>(json, JsonOptions);
        if (notification?.PeerId != null)
        {
            _logger.LogInformation("处理用户离开通知: PeerId={PeerId}, ChatUsers数量={Count}", 
                notification.PeerId, ChatUsers.Count);
            
            var peer = Peers.FirstOrDefault(p => p.PeerId == notification.PeerId);
            var displayName = peer?.DisplayName ?? notification.PeerId;
            
            if (peer != null)
            {
                Peers.Remove(peer);
                _logger.LogInformation("移除 Peer: PeerId={PeerId}, DisplayName={DisplayName}", 
                    notification.PeerId, peer.DisplayName);
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
            _logger.LogDebug("当前 ChatUsers 列表: {Users}", 
                string.Join(", ", ChatUsers.Select(u => $"{u.DisplayName}({u.PeerId})")));
            
            var chatUser = ChatUsers.FirstOrDefault(u => u.PeerId == notification.PeerId);
            if (chatUser != null)
            {
                var removed = ChatUsers.Remove(chatUser);
                _logger.LogInformation("移除聊天用户: DisplayName={DisplayName}, PeerId={PeerId}, 移除结果={Result}", 
                    chatUser.DisplayName, chatUser.PeerId, removed);
            }
            else
            {
                _logger.LogWarning("未找到要移除的聊天用户: PeerId={PeerId}", notification.PeerId);
            }
            
            // 更新状态消息
            StatusMessage = $"用户 {displayName} 离开房间";
            
            // 手动触发在线人数更新
            OnPropertyChanged(nameof(OnlinePeerCount));
        }
    }

    /// <summary>
    /// 处理被踢出房间通知
    /// </summary>
    private async void HandlePeerKicked(object? data)
    {
        if (data == null) return;

        try
        {
            var json = JsonSerializer.Serialize(data);
            var notification = JsonSerializer.Deserialize<PeerKickedData>(json, JsonOptions);
            
            _logger.LogInformation("收到踢人通知: PeerId={PeerId}, DisplayName={DisplayName}, CurrentPeerId={CurrentPeerId}", 
                notification?.PeerId, notification?.DisplayName, CurrentPeerId);
            
            // 如果是自己被踢出（注意：这个通知只发给被踢的用户）
            if (notification?.PeerId == CurrentPeerId)
            {
                _logger.LogWarning("你已被主持人踢出房间，开始强制退出流程...");
                
                // 显示提示
                StatusMessage = "你已被主持人踢出房间";
                
                // 使用 WPF UI 风格的消息框
                var messageBox = new Wpf.Ui.Controls.MessageBox
                {
                    Title = "已被踢出房间",
                    Content = "你已被主持人踢出房间，将返回加入房间界面。",
                    CloseButtonText = "确定",
                    CloseButtonAppearance = Wpf.Ui.Controls.ControlAppearance.Primary,
                };
                
                await messageBox.ShowDialogAsync();
                
                // 使用 ForceLeaveRoomLocalAsync 只清理本地状态，不调用服务端
                // 因为服务端已经通过 KickPeerAsync 移除了用户
                _logger.LogInformation("调用 ForceLeaveRoomLocalAsync 强制清理本地状态...");
                await ForceLeaveRoomLocalAsync();
                
                _logger.LogInformation("触发 ReturnToJoinRoomRequested 事件...");
                ReturnToJoinRoomRequested?.Invoke();
            }
            else
            {
                _logger.LogWarning("收到踢人通知但 PeerId 不匹配: notification.PeerId={NotifyPeerId}, CurrentPeerId={CurrentPeerId}", 
                    notification?.PeerId, CurrentPeerId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理被踢出通知失败");
        }
    }

    /// <summary>
    /// 处理被静音通知
    /// </summary>
    private async void HandlePeerMuted(object? data)
    {
        if (data == null) return;

        try
        {
            var json = JsonSerializer.Serialize(data);
            var notification = JsonSerializer.Deserialize<PeerMutedData>(json, JsonOptions);
            if (notification == null) return;
            
            _logger.LogInformation("收到静音通知: PeerId={PeerId}, IsMuted={IsMuted}, CurrentPeerId={CurrentPeerId}", 
                notification.PeerId, notification.IsMuted, CurrentPeerId);
            
            // 如果是自己被静音
            if (notification.PeerId == CurrentPeerId)
            {
                if (notification.IsMuted)
                {
                    _logger.LogWarning("你已被主持人静音");
                    StatusMessage = "你已被主持人静音";
                    
                    // 关闭麦克风
                    if (IsMicrophoneEnabled)
                    {
                        await ToggleMicrophoneAsync();
                    }
                }
                else
                {
                    _logger.LogInformation("主持人已取消你的静音");
                    StatusMessage = "主持人已取消你的静音，你可以开始说话了";
                }
            }
            else
            {
                // 其他用户被静音，更新 ChatUser 状态
                var mutedUser = ChatUsers.FirstOrDefault(u => u.PeerId == notification.PeerId);
                if (mutedUser != null)
                {
                    mutedUser.IsMutedByHost = notification.IsMuted;
                    _logger.LogInformation("用户 {DisplayName} {Action}", 
                        mutedUser.DisplayName, notification.IsMuted ? "被静音" : "取消静音");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理静音通知失败");
        }
    }

    /// <summary>
    /// 处理房间解散通知（主持人离开）
    /// </summary>
    private async void HandleRoomDismissed(object? data)
    {
        if (data == null) return;

        try
        {
            var json = JsonSerializer.Serialize(data);
            var notification = JsonSerializer.Deserialize<RoomDismissedData>(json, JsonOptions);
            
            _logger.LogWarning("收到房间解散通知: RoomId={RoomId}, HostPeerId={HostPeerId}, Reason={Reason}", 
                notification?.RoomId, notification?.HostPeerId, notification?.Reason);
            
            // 显示提示
            StatusMessage = "主持人已离开，房间已解散";
            
            // 使用 WPF UI 风格的消息框
            var messageBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = "会议已结束",
                Content = $"主持人已离开房间，会议已结束。\n{notification?.Reason ?? ""}",
                CloseButtonText = "确定",
                CloseButtonAppearance = Wpf.Ui.Controls.ControlAppearance.Primary,
            };
            
            await messageBox.ShowDialogAsync();
            
            // 使用 ForceLeaveRoomLocalAsync 只清理本地状态，不调用服务端
            // 因为主持人离开后，房间已经被服务端清理
            _logger.LogInformation("调用 ForceLeaveRoomLocalAsync 强制清理本地状态...");
            await ForceLeaveRoomLocalAsync();
            
            _logger.LogInformation("触发 ReturnToJoinRoomRequested 事件...");
            ReturnToJoinRoomRequested?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理房间解散通知失败");
        }
    }

    private async void HandleNewConsumer(object? data)
    {
        if (data == null) return;

        var json = JsonSerializer.Serialize(data);
        var notification = JsonSerializer.Deserialize<NewConsumerData>(json, JsonOptions);
        if (notification != null)
        {
            _logger.LogInformation("New consumer: ConsumerId={ConsumerId}, Kind={Kind}, ProducerPeerId={ProducerPeerId}",
                notification.ConsumerId, notification.Kind, notification.ProducerPeerId);

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
            _logger.LogInformation("检查 Recv Transport DTLS 状态: IsConnected={IsConnected}", _webRtcService.IsRecvTransportDtlsConnected);
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

    /// <summary>
    /// 处理广播消息通知（从服务器 BroadcastMessage 方法发送的消息）
    /// </summary>
    private void HandleBroadcastMessage(object? data)
    {
        if (data == null) return;

        try
        {
            var json = JsonSerializer.Serialize(data);
            _logger.LogDebug("收到广播消息: {Json}", json);
            
            var broadcastData = JsonSerializer.Deserialize<BroadcastMessageData>(json, JsonOptions);
            if (broadcastData == null) return;

            // 忽略自己发送的消息（SenderId 是服务器端的 UserId/PeerId）
            // 注意：检查逻辑需要与实际的 Peer ID 匹配

            // 根据消息类型分发处理
            switch (broadcastData.Type)
            {
                case "chatMessage":
                    HandleBroadcastChatMessage(broadcastData);
                    break;
                case "emojiReaction":
                    HandleBroadcastEmojiReaction(broadcastData);
                    break;
                case "screenShareRequest":
                    HandleBroadcastScreenShareRequest(broadcastData);
                    break;
                case "screenShareResponse":
                    HandleBroadcastScreenShareResponse(broadcastData);
                    break;
                default:
                    _logger.LogDebug("未处理的广播消息类型: {Type}", broadcastData.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理广播消息失败");
        }
    }

    /// <summary>
    /// 处理广播聊天消息
    /// </summary>
    private async void HandleBroadcastChatMessage(BroadcastMessageData broadcastData)
    {
        if (broadcastData.Data == null) return;

        try
        {
            var json = JsonSerializer.Serialize(broadcastData.Data);
            _logger.LogInformation("广播聊天消息原始数据: {Json}", json);
            
            var msgData = JsonSerializer.Deserialize<ChatMessageData>(json, JsonOptions);
            if (msgData == null) return;

            // 检查私聊消息：如果有 ReceiverId 且不是发给自己的，忽略
            if (!string.IsNullOrEmpty(msgData.ReceiverId) && msgData.ReceiverId != CurrentUserName)
            {
                return;
            }

            var message = new ChatMessage
            {
                Id = msgData.Id ?? Guid.NewGuid().ToString(),
                SenderId = broadcastData.SenderId ?? msgData.SenderId ?? "",
                SenderName = broadcastData.SenderName ?? msgData.SenderName ?? "Unknown",
                ReceiverId = msgData.ReceiverId ?? "",
                Content = msgData.Content ?? "",
                MessageType = (ChatMessageType)(msgData.MessageType ?? 0),
                FileName = msgData.FileName,
                FileSize = msgData.FileSize ?? 0,
                DownloadUrl = msgData.DownloadUrl,
                FileData = msgData.FileData,
                Timestamp = msgData.Timestamp ?? DateTime.Now,
                IsFromSelf = false
            };

            // 如果是图片消息，从 URL 下载图片
            if (message.MessageType == ChatMessageType.Image)
            {
                _logger.LogInformation("图片消息解析: FileName={FileName}, DownloadUrl={DownloadUrl}, FileData长度={FileDataLen}",
                    msgData.FileName, msgData.DownloadUrl, msgData.FileData?.Length ?? 0);
                
                if (!string.IsNullOrEmpty(msgData.DownloadUrl))
                {
                    try
                    {
                        _logger.LogDebug("广播消息: 从 URL 加载图片: {Url}", msgData.DownloadUrl);
                        await LoadImageFromUrlAsync(message, msgData.DownloadUrl);
                    }
                    catch (Exception imgEx)
                    {
                        _logger.LogWarning(imgEx, "广播消息: 从 URL 加载图片失败: {Url}", msgData.DownloadUrl);
                    }
                }
                else if (!string.IsNullOrEmpty(msgData.FileData))
                {
                    // Base64 向后兼容
                    try
                    {
                        var imageBytes = Convert.FromBase64String(msgData.FileData);
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            using var ms = new System.IO.MemoryStream(imageBytes);
                            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                            bitmap.BeginInit();
                            bitmap.StreamSource = ms;
                            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            bitmap.Freeze();
                            message.ImageSource = bitmap;
                        });
                    }
                    catch (Exception imgEx)
                    {
                        _logger.LogWarning(imgEx, "广播消息: 加载 Base64 图片失败");
                    }
                }
            }

            // 如果是文件消息，记录日志
            if (message.MessageType == ChatMessageType.File)
            {
                _logger.LogDebug("广播消息: 接收到文件: {FileName}, 大小: {Size}, URL: {Url}",
                    message.FileName, message.FileSize, msgData.DownloadUrl);
            }

            AddMessageToCollection(message);
            _logger.LogInformation("收到广播聊天消息: From={From}, Type={Type}, Content={Content}", 
                message.SenderName, message.MessageType, message.Content);

            // 播放新消息提示音
            _soundService.PlaySound(SoundService.SoundType.Message);

            // 如果不在当前聊天，增加未读数
            if (string.IsNullOrEmpty(message.ReceiverId))
            {
                // 群聊消息 - 如果不在群聊模式或聊天面板不可见，增加群聊未读计数
                if (!IsChatPanelVisible || !IsGroupChatMode)
                {
                    GroupUnreadCount++;
                }
            }
            else
            {
                // 私聊消息 - 始终更新 LastMessage，但只在不在当前聊天时增加未读数
                var user = ChatUsers.FirstOrDefault(u => u.PeerId == message.SenderId);
                if (user != null)
                {
                    // 始终更新最后一条消息
                    user.LastMessage = message;
                    
                    // 如果聊天面板不可见，或在群聊模式，或当前私聊对象不是发送者，增加未读数
                    if (!IsChatPanelVisible || IsGroupChatMode || SelectedChatUser?.PeerId != message.SenderId)
                    {
                        user.UnreadCount++;
                        _logger.LogInformation("私聊未读数增加: User={User}, Count={Count}", user.DisplayName, user.UnreadCount);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理广播聊天消息失败");
        }
    }

    /// <summary>
    /// 处理广播表情反应
    /// </summary>
    private void HandleBroadcastEmojiReaction(BroadcastMessageData broadcastData)
    {
        if (broadcastData.Data == null) return;

        try
        {
            var json = JsonSerializer.Serialize(broadcastData.Data);
            var reactionData = JsonSerializer.Deserialize<EmojiReactionData>(json, JsonOptions);
            if (reactionData == null) return;

            var reaction = new EmojiReaction
            {
                SenderId = broadcastData.SenderId ?? reactionData.SenderId ?? "",
                SenderName = broadcastData.SenderName ?? reactionData.SenderName ?? "Unknown",
                Emoji = reactionData.Emoji ?? "👍"
            };

            ShowEmojiReaction(reaction);
            _logger.LogInformation("收到表情反应: From={From}, Emoji={Emoji}", reaction.SenderName, reaction.Emoji);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理广播表情反应失败");
        }
    }

    /// <summary>
    /// 处理广播屏幕共享请求
    /// </summary>
    private void HandleBroadcastScreenShareRequest(BroadcastMessageData broadcastData)
    {
        if (broadcastData.Data == null) return;

        try
        {
            var json = JsonSerializer.Serialize(broadcastData.Data);
            var requestData = JsonSerializer.Deserialize<ScreenShareRequestData>(json, JsonOptions);
            if (requestData == null) return;

            // 使用 broadcastData 中的发送者信息
            requestData.RequesterId ??= broadcastData.SenderId;
            requestData.RequesterName ??= broadcastData.SenderName;

            // 保存当前请求
            _pendingScreenShareRequest = requestData;
            PendingScreenShareRequesterName = requestData.RequesterName ?? "Unknown";
            HasPendingScreenShareRequest = true;

            StatusMessage = $"{requestData.RequesterName} 请求共享屏幕";
            _logger.LogInformation("收到屏幕共享请求: {RequesterName}", requestData.RequesterName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理广播屏幕共享请求失败");
        }
    }

    /// <summary>
    /// 处理广播屏幕共享响应
    /// </summary>
    private void HandleBroadcastScreenShareResponse(BroadcastMessageData broadcastData)
    {
        if (broadcastData.Data == null) return;

        try
        {
            var json = JsonSerializer.Serialize(broadcastData.Data);
            var responseData = JsonSerializer.Deserialize<ScreenShareResponseData>(json, JsonOptions);
            if (responseData == null) return;

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
            _logger.LogError(ex, "处理广播屏幕共享响应失败");
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
    private bool _isSelected;
    private bool _isHorizontallyMirrored;
    private bool _isVerticallyMirrored;
    private bool _isMuted;
    private double _volume = 100;
    private bool _isMaximized;

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

    /// <summary>
    /// 是否被选中
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>
    /// 是否水平镜像
    /// </summary>
    public bool IsHorizontallyMirrored
    {
        get => _isHorizontallyMirrored;
        set
        {
            if (SetProperty(ref _isHorizontallyMirrored, value))
            {
                OnPropertyChanged(nameof(HorizontalScale));
            }
        }
    }

    /// <summary>
    /// 是否垂直镜像
    /// </summary>
    public bool IsVerticallyMirrored
    {
        get => _isVerticallyMirrored;
        set
        {
            if (SetProperty(ref _isVerticallyMirrored, value))
            {
                OnPropertyChanged(nameof(VerticalScale));
            }
        }
    }

    /// <summary>
    /// 是否静音
    /// </summary>
    public bool IsMuted
    {
        get => _isMuted;
        set => SetProperty(ref _isMuted, value);
    }

    /// <summary>
    /// 音量 (0-100)
    /// </summary>
    public double Volume
    {
        get => _volume;
        set => SetProperty(ref _volume, Math.Clamp(value, 0, 100));
    }

    /// <summary>
    /// 是否最大化
    /// </summary>
    public bool IsMaximized
    {
        get => _isMaximized;
        set => SetProperty(ref _isMaximized, value);
    }

    /// <summary>
    /// 获取水平镜像的 ScaleTransform 值
    /// </summary>
    public double HorizontalScale => _isHorizontallyMirrored ? -1 : 1;

    /// <summary>
    /// 获取垂直镜像的 ScaleTransform 值
    /// </summary>
    public double VerticalScale => _isVerticallyMirrored ? -1 : 1;
}
