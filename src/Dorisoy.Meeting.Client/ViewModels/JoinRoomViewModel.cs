using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
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
    private readonly ISignalRService _signalRService;
    private readonly HttpClient _httpClient;
    private System.Threading.CancellationTokenSource? _previewCts;
    
    // 记忆文件路径
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Dorisoy.Meeting", "settings.json");

    // 当前获取的访问令牌
    private string? _currentAccessToken;

    #region 可观察属性

    /// <summary>
    /// 房间号码第1位
    /// </summary>
    [ObservableProperty]
    private string _roomDigit1 = string.Empty;

    /// <summary>
    /// 房间号码第2位
    /// </summary>
    [ObservableProperty]
    private string _roomDigit2 = string.Empty;

    /// <summary>
    /// 房间号码第3位
    /// </summary>
    [ObservableProperty]
    private string _roomDigit3 = string.Empty;

    /// <summary>
    /// 房间号码第4位
    /// </summary>
    [ObservableProperty]
    private string _roomDigit4 = string.Empty;

    /// <summary>
    /// 房间号码第5位
    /// </summary>
    [ObservableProperty]
    private string _roomDigit5 = string.Empty;

    /// <summary>
    /// 完整房间号码 (5位数字)
    /// </summary>
    public string RoomId => $"{RoomDigit1}{RoomDigit2}{RoomDigit3}{RoomDigit4}{RoomDigit5}";

    /// <summary>
    /// 最近使用的房间号列表
    /// </summary>
    public ObservableCollection<string> RecentRooms { get; } = [];

    /// <summary>
    /// 是否有最近的房间号
    /// </summary>
    public bool HasRecentRooms => RecentRooms.Count > 0;

    /// <summary>
    /// 用户名
    /// </summary>
    [ObservableProperty]
    private string _userName = string.Empty;

    /// <summary>
    /// 服务器地址
    /// </summary>
    [ObservableProperty]
    private string _serverUrl = "http://192.168.30.8:9000";

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
    public bool CanJoin => RoomId.Length == 5 && 
                           RoomId.All(char.IsDigit) &&
                           !string.IsNullOrWhiteSpace(UserName);

    /// <summary>
    /// 对话结果 - 是否确认加入
    /// </summary>
    [ObservableProperty]
    private bool _dialogResult;

    /// <summary>
    /// 是否正在检测连接
    /// </summary>
    [ObservableProperty]
    private bool _isCheckingConnection;

    /// <summary>
    /// 错误信息
    /// </summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// 是否有错误
    /// </summary>
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

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
        IWebRtcService webRtcService,
        ISignalRService signalRService)
    {
        _logger = logger;
        _webRtcService = webRtcService;
        _signalRService = signalRService;
        _httpClient = new HttpClient();

        // 加载记忆的设置
        LoadSettings();

        // 如果没有记忆的房间号，生成随机5位数字房间号
        if (string.IsNullOrEmpty(RoomId) || RoomId.Length != 5)
        {
            GenerateRandomRoomId();
        }

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
        var roomId = random.Next(10000, 99999).ToString();
        SetRoomId(roomId);
    }

    /// <summary>
    /// 选择最近的房间号
    /// </summary>
    [RelayCommand]
    private void SelectRecentRoom(string roomId)
    {
        if (!string.IsNullOrEmpty(roomId) && roomId.Length == 5)
        {
            SetRoomId(roomId);
        }
    }

    /// <summary>
    /// 设置房间号
    /// </summary>
    private void SetRoomId(string roomId)
    {
        if (roomId.Length >= 5)
        {
            RoomDigit1 = roomId[0].ToString();
            RoomDigit2 = roomId[1].ToString();
            RoomDigit3 = roomId[2].ToString();
            RoomDigit4 = roomId[3].ToString();
            RoomDigit5 = roomId[4].ToString();
        }
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
        if (!CanJoin || IsCheckingConnection) return;

        // 清除之前的错误信息
        ErrorMessage = null;
        OnPropertyChanged(nameof(HasError));
        
        IsCheckingConnection = true;
        try
        {
            _logger.LogInformation("正在连接服务器: {ServerUrl}", ServerUrl);
            
            // 1. 从服务器获取动态 Token
            var token = await GetAccessTokenAsync(UserName);
            if (string.IsNullOrEmpty(token))
            {
                ErrorMessage = "获取访问令牌失败，请检查服务器地址";
                OnPropertyChanged(nameof(HasError));
                return;
            }
            _currentAccessToken = token;
            _logger.LogInformation("成功获取访问令牌");
            
            // 2. 使用 Token 连接 SignalR 验证服务器可达性
            await _signalRService.ConnectAsync(ServerUrl, token);
            
            if (!_signalRService.IsConnected)
            {
                ErrorMessage = "无法连接到服务器，请检查网络或服务器地址";
                OnPropertyChanged(nameof(HasError));
                _logger.LogWarning("服务器连接失败: {ServerUrl}", ServerUrl);
                return;
            }
            
            _logger.LogInformation("服务器连接成功");
            
            // 断开连接（后续在 MainViewModel 中重新连接）
            await _signalRService.DisconnectAsync();
            
            // 停止预览
            await StopPreviewAsync();
            
            // 保存记忆
            SaveSettings();

            DialogResult = true;
            RequestClose?.Invoke(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "服务器连接失败");
            ErrorMessage = $"连接服务器失败: {ex.Message}";
            OnPropertyChanged(nameof(HasError));
        }
        finally
        {
            IsCheckingConnection = false;
        }
    }

    /// <summary>
    /// 从服务器获取访问令牌
    /// </summary>
    private async Task<string?> GetAccessTokenAsync(string userName)
    {
        try
        {
            // 调用服务器 API 获取 Token
            var url = $"{ServerUrl}/api/Token/createToken?userIdOrUsername={Uri.EscapeDataString(userName)}";
            var response = await _httpClient.PostAsync(url, null);
            
            if (response.IsSuccessStatusCode)
            {
                var token = await response.Content.ReadAsStringAsync();
                return token;
            }
            
            _logger.LogWarning("获取 Token 失败: {StatusCode}", response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 Token 异常");
            return null;
        }
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

    partial void OnRoomDigit1Changed(string value) => OnPropertyChanged(nameof(CanJoin));
    partial void OnRoomDigit2Changed(string value) => OnPropertyChanged(nameof(CanJoin));
    partial void OnRoomDigit3Changed(string value) => OnPropertyChanged(nameof(CanJoin));
    partial void OnRoomDigit4Changed(string value) => OnPropertyChanged(nameof(CanJoin));
    partial void OnRoomDigit5Changed(string value) => OnPropertyChanged(nameof(CanJoin));

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
            AccessToken = _currentAccessToken ?? string.Empty,
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
        _httpClient.Dispose();
    }

    /// <summary>
    /// 加载设置
    /// </summary>
    private void LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<MeetingSettings>(json);
                if (settings != null)
                {
                    // 加载用户名
                    if (!string.IsNullOrEmpty(settings.LastUserName))
                    {
                        UserName = settings.LastUserName;
                    }
                    
                    // 加载最近的房间号
                    if (settings.RecentRoomIds != null)
                    {
                        foreach (var roomId in settings.RecentRoomIds.Take(3))
                        {
                            RecentRooms.Add(roomId);
                        }
                        OnPropertyChanged(nameof(HasRecentRooms));
                        
                        // 如果有记忆的房间号，默认填充第一个
                        if (RecentRooms.Count > 0)
                        {
                            SetRoomId(RecentRooms[0]);
                        }
                    }
                    
                    _logger.LogDebug("加载设置成功: UserName={UserName}, RecentRooms={Count}", 
                        UserName, RecentRooms.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "加载设置失败");
        }
    }

    /// <summary>
    /// 保存设置
    /// </summary>
    private void SaveSettings()
    {
        try
        {
            // 确保目录存在
            var dir = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // 更新最近房间号列表
            var recentRoomIds = new List<string>();
            if (!string.IsNullOrEmpty(RoomId) && RoomId.Length == 5)
            {
                recentRoomIds.Add(RoomId);
            }
            foreach (var roomId in RecentRooms.Where(r => r != RoomId).Take(2))
            {
                recentRoomIds.Add(roomId);
            }

            var settings = new MeetingSettings
            {
                LastUserName = UserName,
                RecentRoomIds = recentRoomIds
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
            
            _logger.LogDebug("保存设置成功: RoomId={RoomId}, UserName={UserName}", RoomId, UserName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "保存设置失败");
        }
    }

    #endregion
}

/// <summary>
/// 会议设置模型
/// </summary>
internal class MeetingSettings
{
    public string? LastUserName { get; set; }
    public List<string>? RecentRoomIds { get; set; }
}
