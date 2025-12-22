**Dorisoy.Mediasoup** 是一个基于 .NET 8 的完整实时音视频通信解决方案，采用 Mediasoup SFU 架构实现高性能的 WebRTC 媒体服务。项目包含 Mediasoup 核心库（C# 实现）、libuv 异步 I/O 绑定、ASP.NET Core 集成中间件、SignalR 信令服务器，以及 WPF 桌面客户端和 Blazor Web 客户端。支持 Open/Pull/Invite 三种服务模式，提供房间管理、音视频采集传输、多端互通等功能。技术栈涵盖 WebRTC、SignalR、SIPSorcery、OpenCvSharp、NAudio 等，适用于在线会议、远程协作、直播互动等场景。


---

## 解决方案构成

```
Dorisoy.Mediasoup.sln
├── src/                                    # 源代码目录
│   ├── Dorisoy.Mediasoup/                  # Mediasoup SFU 核心库，C# 实现 WebRTC 媒体路由、传输管理
│   ├── Dorisoy.Libuv/                      # libuv 异步 I/O 库的 .NET 绑定，提供底层网络通信能力
│   ├── Dorisoy.Mediasoup.AspNetCore/       # ASP.NET Core 集成中间件，简化 Web 应用集成
│   ├── Dorisoy.Mediasoup.Common/           # 共享模型、DTO、常量和工具类
│   ├── Dorisoy.Mediasoup.Runtimes.Builder/ # 构建 mediasoup-worker 原生二进制 NuGet 包的脚本
│   ├── Dorisoy.Meeting.Server/             # 会议信令服务器，处理房间、成员、媒体状态管理
│   ├── Dorisoy.Meeting.Web/                # ASP.NET Core Web 服务端，提供 REST API 和 SignalR Hub
│   ├── Dorisoy.Meeting.Web.Client/         # Blazor WebAssembly 前端客户端
│   ├── Dorisoy.Meeting.Client/             # WPF 桌面客户端，支持原生音视频采集与传输
│   └── Dorisoy.Utils/                      # 通用工具库（Excel/图像/缓存/拼音等）
│
└── UI/                                     # WPF UI 组件库
    ├── Wpf.Ui/                             # 现代 Fluent 风格 WPF 控件库，提供主题、导航、对话框等
    ├── Wpf.Ui.Abstractions/                # UI 抽象接口，定义导航、页面提供者等契约
    ├── Wpf.Ui.DependencyInjection/         # 依赖注入集成，简化 DI 容器配置
    ├── Wpf.Ui.Gallery/                     # UI 组件展示应用，演示各类控件用法
    ├── Wpf.Ui.SyntaxHighlight/             # 代码语法高亮控件
    ├── Wpf.Ui.ToastNotifications/          # Toast 消息通知组件
    └── Wpf.Ui.Tray/                        # 系统托盘图标支持
```

### 核心项目说明

| 项目 | 类型 | 说明 |
|------|------|------|
| **Dorisoy.Mediasoup** | 类库 | Mediasoup SFU 的 C# 实现，包含 Router、Transport、Producer、Consumer 等核心组件 |
| **Dorisoy.Libuv** | 类库 | libuv 异步事件循环的 .NET 封装，用于 mediasoup-worker 进程通信 |
| **Dorisoy.Meeting.Web** | Web 应用 | 会议服务后端，提供 `/hubs/meetingHub` SignalR 端点和 JWT 认证 |
| **Dorisoy.Meeting.Client** | WPF 应用 | 桌面客户端，集成 SIPSorcery/OpenCvSharp/NAudio 实现音视频 |
| **Wpf.Ui** | UI 库 | 提供 FluentWindow、NavigationView、Snackbar 等现代化控件 |

---


## 1. 项目概述

### 1.1 功能

- **SignalR 通信**：与服务器建立 WebSocket 连接进行信令交互
- **WebRTC 媒体传输**：支持音视频采集、编码、传输和渲染
- **房间管理**：支持加入/离开房间、用户列表管理
- **多种服务模式**：支持 Open、Pull、Invite 三种服务模式
- **媒体控制**：麦克风、摄像头的开启/关闭/暂停/恢复

### 1.2 通信协议

客户端与服务器通过 SignalR Hub 进行通信：

- **Hub 端点**：`/hubs/meetingHub`
- **认证方式**：JWT Bearer Token
- **传输协议**：WebSocket

---

## 2. 技术选型

### 2.1 核心依赖

| 组件 | 用途 | NuGet 包 |
|------|------|----------|
| SignalR Client | 信令通信 | `Microsoft.AspNetCore.SignalR.Client` |
| WebRTC | 媒体传输 | `SIPSorcery` 或 `MixedReality-WebRTC` |
| WPF | UI 框架 | .NET 内置 |
| CommunityToolkit.Mvvm | MVVM 框架 | `CommunityToolkit.Mvvm` |

### 2.2 WebRTC 库选择

由于 WPF 不是浏览器环境，需要使用原生 WebRTC 库：

**方案一：SIPSorcery（推荐）**
- 纯 C# 实现
- 支持 .NET 6+
- 文档完善，社区活跃

**方案二：MixedReality-WebRTC**
- 微软开源
- 基于 Google WebRTC
- 性能更好但配置较复杂

---

## 3. 项目结构

```
src/Dorisoy.Meeting.Client/
├── App.xaml                          # 应用程序入口配置
├── App.xaml.cs                       # 应用程序启动逻辑
├── MainWindow.xaml                   # 主窗口 XAML
├── MainWindow.xaml.cs                # 主窗口代码
├── Dorisoy.Meeting.Client.csproj      # 项目文件
│
├── Models/                           # 数据模型
│   ├── MeetingMessage.cs             # 服务器响应消息模型
│   ├── MeetingNotification.cs        # 服务器通知模型
│   ├── JoinRequest.cs                # 加入会议请求
│   ├── JoinRoomRequest.cs            # 加入房间请求
│   ├── ProduceRequest.cs             # 生产媒体请求
│   ├── CreateWebRtcTransportRequest.cs  # 创建传输请求
│   ├── ConnectWebRtcTransportRequest.cs # 连接传输请求
│   ├── PeerInfo.cs                   # Peer 信息
│   └── Notifications/                # 各类通知模型
│       ├── NewConsumerNotification.cs
│       ├── PeerJoinRoomNotification.cs
│       └── ...
│
├── Services/                         # 服务层
│   ├── ISignalRService.cs            # SignalR 服务接口
│   ├── SignalRService.cs             # SignalR 服务实现
│   ├── IWebRtcService.cs             # WebRTC 服务接口
│   ├── WebRtcService.cs              # WebRTC 服务实现
│   └── IMediaDeviceService.cs        # 媒体设备服务
│
├── ViewModels/                       # 视图模型
│   ├── MainViewModel.cs              # 主视图模型
│   └── PeerViewModel.cs              # Peer 视图模型
│
├── Views/                            # 视图控件
│   ├── VideoPanel.xaml               # 视频面板
│   └── PeerListControl.xaml          # Peer 列表控件
│
├── Converters/                       # 值转换器
│   └── BooleanToVisibilityConverter.cs
│
└── Helpers/                          # 工具类
    ├── Logger.cs                     # 日志工具
    └── JsonHelper.cs                 # JSON 序列化工具
```

---

## 4. 环境准备

### 4.1 创建项目

```bash
# 在 src 目录下创建 WPF 项目
cd src
dotnet new wpf -n Dorisoy.Meeting.Client -f net8.0

# 添加到解决方案
cd ..
dotnet sln Dorisoy.Mediasoup.sln add src/Dorisoy.Meeting.Client/Dorisoy.Meeting.Client.csproj
```

### 4.2 安装 NuGet 包

```bash
cd src/Dorisoy.Meeting.Client

# SignalR 客户端
dotnet add package Microsoft.AspNetCore.SignalR.Client

# WebRTC (选择其一)
dotnet add package SIPSorceryMedia.Windows
# 或
dotnet add package Microsoft.MixedReality.WebRTC

# MVVM 工具包
dotnet add package CommunityToolkit.Mvvm

# JSON 序列化
dotnet add package System.Text.Json

# 日志
dotnet add package Microsoft.Extensions.Logging
dotnet add package Serilog.Extensions.Logging
dotnet add package Serilog.Sinks.File
```

### 4.3 项目文件配置

编辑 `Dorisoy.Meeting.Client.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="8.0.0" />
    <PackageReference Include="SIPSorceryMedia.Windows" Version="8.0.5" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />
    <PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.0" />
    <PackageReference Include="Serilog.Extensions.Logging" Version="8.0.0" />
    <PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
  </ItemGroup>

  <ItemGroup>
    <!-- 引用共享模型库 -->
    <ProjectReference Include="..\Dorisoy.Mediasoup.Common\Dorisoy.Mediasoup.Common.csproj" />
  </ItemGroup>

</Project>
```

---

## 5. 核心实现

### 5.1 数据模型

#### MeetingMessage.cs - 服务器响应消息

```csharp
namespace Dorisoy.Meeting.Client.Models;

/// <summary>
/// 服务器响应消息基类
/// </summary>
public class MeetingMessage
{
    public int Code { get; set; } = 200;
    public string? InternalCode { get; set; }
    public string Message { get; set; } = "Success";
    
    public bool IsSuccess => Code == 200;
}

/// <summary>
/// 带数据的服务器响应消息
/// </summary>
public class MeetingMessage<T> : MeetingMessage
{
    public T? Data { get; set; }
}
```

#### MeetingNotification.cs - 服务器通知

```csharp
namespace Dorisoy.Meeting.Client.Models;

/// <summary>
/// 服务器推送通知
/// </summary>
public class MeetingNotification
{
    public string Type { get; set; } = string.Empty;
    public object? Data { get; set; }
}
```

#### PeerInfo.cs - Peer 信息

```csharp
namespace Dorisoy.Meeting.Client.Models;

/// <summary>
/// Peer 信息
/// </summary>
public class PeerInfo
{
    public string PeerId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string[] Sources { get; set; } = [];
    public Dictionary<string, object> AppData { get; set; } = [];
}
```

### 5.2 SignalR 服务

#### ISignalRService.cs - 接口定义

```csharp
namespace Dorisoy.Meeting.Client.Services;

public interface ISignalRService
{
    bool IsConnected { get; }
    
    event Action<MeetingNotification>? OnNotification;
    event Action? OnConnected;
    event Action<Exception?>? OnDisconnected;
    
    Task ConnectAsync(string serverUrl, string accessToken);
    Task DisconnectAsync();
    
    // Hub 方法调用
    Task<MeetingMessage<T>> InvokeAsync<T>(string methodName, object? arg = null);
    Task<MeetingMessage> InvokeAsync(string methodName, object? arg = null);
}
```

#### SignalRService.cs - 实现

```csharp
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Dorisoy.Meeting.Client.Services;

public class SignalRService : ISignalRService, IAsyncDisposable
{
    private readonly ILogger<SignalRService> _logger;
    private HubConnection? _connection;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public event Action<MeetingNotification>? OnNotification;
    public event Action? OnConnected;
    public event Action<Exception?>? OnDisconnected;

    public SignalRService(ILogger<SignalRService> logger)
    {
        _logger = logger;
    }

    public async Task ConnectAsync(string serverUrl, string accessToken)
    {
        if (_connection != null)
        {
            await DisconnectAsync();
        }

        _connection = new HubConnectionBuilder()
            .WithUrl($"{serverUrl}/hubs/meetingHub", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                options.SkipNegotiation = true;
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
            })
            .WithAutomaticReconnect()
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Debug);
            })
            .Build();

        // 注册通知处理
        _connection.On<MeetingNotification>("Notify", notification =>
        {
            _logger.LogDebug("Received notification: {Type}", notification.Type);
            OnNotification?.Invoke(notification);
        });

        _connection.Closed += async error =>
        {
            _logger.LogWarning(error, "Connection closed");
            OnDisconnected?.Invoke(error);
            await Task.CompletedTask;
        };

        _connection.Reconnected += async connectionId =>
        {
            _logger.LogInformation("Reconnected with connectionId: {ConnectionId}", connectionId);
            OnConnected?.Invoke();
            await Task.CompletedTask;
        };

        await _connection.StartAsync();
        _logger.LogInformation("Connected to server");
        OnConnected?.Invoke();
    }

    public async Task DisconnectAsync()
    {
        if (_connection != null)
        {
            await _connection.StopAsync();
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    public async Task<MeetingMessage<T>> InvokeAsync<T>(string methodName, object? arg = null)
    {
        if (_connection == null || !IsConnected)
        {
            return new MeetingMessage<T> { Code = 500, Message = "Not connected" };
        }

        try
        {
            var result = arg == null
                ? await _connection.InvokeAsync<JsonElement>(methodName)
                : await _connection.InvokeAsync<JsonElement>(methodName, arg);

            return JsonSerializer.Deserialize<MeetingMessage<T>>(result.GetRawText(), JsonOptions)
                   ?? new MeetingMessage<T> { Code = 500, Message = "Deserialization failed" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invoke {MethodName}", methodName);
            return new MeetingMessage<T> { Code = 500, Message = ex.Message };
        }
    }

    public async Task<MeetingMessage> InvokeAsync(string methodName, object? arg = null)
    {
        if (_connection == null || !IsConnected)
        {
            return new MeetingMessage { Code = 500, Message = "Not connected" };
        }

        try
        {
            var result = arg == null
                ? await _connection.InvokeAsync<JsonElement>(methodName)
                : await _connection.InvokeAsync<JsonElement>(methodName, arg);

            return JsonSerializer.Deserialize<MeetingMessage>(result.GetRawText(), JsonOptions)
                   ?? new MeetingMessage { Code = 500, Message = "Deserialization failed" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invoke {MethodName}", methodName);
            return new MeetingMessage { Code = 500, Message = ex.Message };
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}
```

### 5.3 WebRTC 服务（使用 SIPSorcery）

#### WebRtcService.cs

```csharp
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.Windows;
using Microsoft.Extensions.Logging;

namespace Dorisoy.Meeting.Client.Services;

public class WebRtcService : IDisposable
{
    private readonly ILogger<WebRtcService> _logger;
    private RTCPeerConnection? _sendPeerConnection;
    private RTCPeerConnection? _recvPeerConnection;
    private WindowsVideoEndPoint? _videoSource;
    private WindowsAudioEndPoint? _audioSource;

    // Transport 信息缓存
    private string? _sendTransportId;
    private string? _recvTransportId;

    public event Action<RTCTrackEvent>? OnTrackReceived;
    public event Action<byte[], int, int>? OnVideoFrameReceived;

    public WebRtcService(ILogger<WebRtcService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 初始化发送端 PeerConnection
    /// </summary>
    public async Task<RTCPeerConnection> CreateSendPeerConnectionAsync(
        object iceParameters,
        object[] iceCandidates,
        object dtlsParameters)
    {
        var config = new RTCConfiguration
        {
            iceServers = new List<RTCIceServer>()
        };

        _sendPeerConnection = new RTCPeerConnection(config);

        // 初始化音视频源
        _videoSource = new WindowsVideoEndPoint();
        _audioSource = new WindowsAudioEndPoint(new AudioEncoder());

        await _videoSource.StartVideo();
        await _audioSource.StartAudio();

        // 添加媒体轨道
        var videoTrack = new MediaStreamTrack(
            _videoSource.GetVideoSourceFormats(), 
            MediaStreamStatusEnum.SendOnly);
        _sendPeerConnection.addTrack(videoTrack);

        var audioTrack = new MediaStreamTrack(
            _audioSource.GetAudioSourceFormats(), 
            MediaStreamStatusEnum.SendOnly);
        _sendPeerConnection.addTrack(audioTrack);

        // 处理 ICE 候选
        _sendPeerConnection.onicecandidate += candidate =>
        {
            _logger.LogDebug("Send ICE candidate: {Candidate}", candidate?.candidate);
        };

        _sendPeerConnection.onconnectionstatechange += state =>
        {
            _logger.LogInformation("Send connection state: {State}", state);
        };

        return _sendPeerConnection;
    }

    /// <summary>
    /// 初始化接收端 PeerConnection
    /// </summary>
    public RTCPeerConnection CreateRecvPeerConnection(
        object iceParameters,
        object[] iceCandidates,
        object dtlsParameters)
    {
        var config = new RTCConfiguration
        {
            iceServers = new List<RTCIceServer>()
        };

        _recvPeerConnection = new RTCPeerConnection(config);

        // 处理接收到的轨道
        _recvPeerConnection.OnTrack += (evt) =>
        {
            _logger.LogInformation("Received track: {Kind}", evt.Track.Kind);
            OnTrackReceived?.Invoke(evt);
        };

        _recvPeerConnection.onconnectionstatechange += state =>
        {
            _logger.LogInformation("Recv connection state: {State}", state);
        };

        return _recvPeerConnection;
    }

    /// <summary>
    /// 连接发送 Transport
    /// </summary>
    public async Task ConnectSendTransportAsync(object dtlsParameters)
    {
        if (_sendPeerConnection == null) return;
        
        // 在实际实现中，需要将 DTLS 参数应用到连接
        _logger.LogInformation("Connecting send transport with DTLS parameters");
        await Task.CompletedTask;
    }

    /// <summary>
    /// 连接接收 Transport
    /// </summary>
    public async Task ConnectRecvTransportAsync(object dtlsParameters)
    {
        if (_recvPeerConnection == null) return;
        
        _logger.LogInformation("Connecting recv transport with DTLS parameters");
        await Task.CompletedTask;
    }

    /// <summary>
    /// 开始生产视频
    /// </summary>
    public async Task<string?> ProduceVideoAsync()
    {
        if (_sendPeerConnection == null || _videoSource == null) return null;

        await _videoSource.StartVideo();
        _logger.LogInformation("Video production started");
        
        // 返回 Producer ID（在实际实现中从服务器获取）
        return Guid.NewGuid().ToString();
    }

    /// <summary>
    /// 开始生产音频
    /// </summary>
    public async Task<string?> ProduceAudioAsync()
    {
        if (_sendPeerConnection == null || _audioSource == null) return null;

        await _audioSource.StartAudio();
        _logger.LogInformation("Audio production started");
        
        return Guid.NewGuid().ToString();
    }

    /// <summary>
    /// 停止生产视频
    /// </summary>
    public async Task StopVideoAsync()
    {
        if (_videoSource != null)
        {
            await _videoSource.CloseVideo();
            _logger.LogInformation("Video stopped");
        }
    }

    /// <summary>
    /// 停止生产音频
    /// </summary>
    public async Task StopAudioAsync()
    {
        if (_audioSource != null)
        {
            await _audioSource.CloseAudio();
            _logger.LogInformation("Audio stopped");
        }
    }

    /// <summary>
    /// 消费远端媒体
    /// </summary>
    public async Task ConsumeAsync(
        string consumerId,
        string producerId,
        string kind,
        object rtpParameters)
    {
        if (_recvPeerConnection == null) return;

        _logger.LogInformation("Consuming {Kind} from producer {ProducerId}", kind, producerId);
        
        // 添加远端轨道
        var mediaType = kind == "video" ? SDPMediaTypesEnum.video : SDPMediaTypesEnum.audio;
        
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        _videoSource?.Dispose();
        _audioSource?.Dispose();
        _sendPeerConnection?.Dispose();
        _recvPeerConnection?.Dispose();
    }
}
```

### 5.4 主视图模型

#### MainViewModel.cs

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Text.Json;
using Dorisoy.Meeting.Client.Models;
using Dorisoy.Meeting.Client.Services;

namespace Dorisoy.Meeting.Client.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ILogger<MainViewModel> _logger;
    private readonly ISignalRService _signalRService;
    private readonly WebRtcService _webRtcService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isJoinedRoom;
    [ObservableProperty] private string _serverUrl = "http://localhost:9000";
    [ObservableProperty] private int _selectedPeerIndex;
    [ObservableProperty] private int _selectedRoomIndex;
    [ObservableProperty] private string _serveMode = "Open";
    [ObservableProperty] private string _statusMessage = "未连接";

    public ObservableCollection<PeerInfo> Peers { get; } = [];
    public ObservableCollection<string> Rooms { get; } = ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9"];

    // 预设的测试 Token（与 Web 客户端相同）
    private readonly string[] _accessTokens =
    [
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1lIjoiMCIsIm5iZiI6MTcwNTkzMDcxOSwiZXhwIjoxNzMxODUwNzE5LCJpc3MiOiJpc3N1ZXIiLCJhdWQiOiJhdWRpZW5jZSJ9.O0Oo9CIdtDy3RzAw82J9PMUsw3L8XDw18iQh0-M0Znk",
        // ... 其他 token
    ];

    private object? _routerRtpCapabilities;
    private string? _sendTransportId;
    private string? _recvTransportId;

    public MainViewModel(
        ILogger<MainViewModel> logger,
        ISignalRService signalRService,
        WebRtcService webRtcService)
    {
        _logger = logger;
        _signalRService = signalRService;
        _webRtcService = webRtcService;

        _signalRService.OnNotification += HandleNotification;
        _signalRService.OnConnected += () => IsConnected = true;
        _signalRService.OnDisconnected += _ =>
        {
            IsConnected = false;
            IsJoinedRoom = false;
            Peers.Clear();
        };
    }

    /// <summary>
    /// 连接/断开服务器
    /// </summary>
    [RelayCommand]
    private async Task ToggleConnectionAsync()
    {
        if (IsConnected)
        {
            await _signalRService.DisconnectAsync();
            StatusMessage = "已断开连接";
            return;
        }

        try
        {
            var token = _accessTokens[SelectedPeerIndex];
            await _signalRService.ConnectAsync(ServerUrl, token);
            await StartAsync();
            StatusMessage = "已连接";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection failed");
            StatusMessage = $"连接失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 初始化会议
    /// </summary>
    private async Task StartAsync()
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

        // 3. 加入会议
        var joinRequest = new
        {
            rtpCapabilities = _routerRtpCapabilities,
            sctpCapabilities = (object?)null,
            displayName = $"WPF Peer {SelectedPeerIndex}",
            sources = new[] { "audio:mic", "video:cam" },
            appData = new Dictionary<string, object>()
        };

        var joinResult = await _signalRService.InvokeAsync("Join", joinRequest);
        if (!joinResult.IsSuccess)
        {
            _logger.LogError("Join failed: {Message}", joinResult.Message);
            return;
        }

        _logger.LogInformation("Joined meeting successfully");
    }

    /// <summary>
    /// 加入/离开房间
    /// </summary>
    [RelayCommand]
    private async Task ToggleRoomAsync()
    {
        if (IsJoinedRoom)
        {
            await LeaveRoomAsync();
            return;
        }

        await JoinRoomAsync();
    }

    private async Task JoinRoomAsync()
    {
        var isAdmin = SelectedPeerIndex >= 8;
        var joinRoomRequest = new
        {
            roomId = Rooms[SelectedRoomIndex],
            role = isAdmin ? "admin" : "normal"
        };

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
        StatusMessage = $"已加入房间 {Rooms[SelectedRoomIndex]}";

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
    }

    private async Task LeaveRoomAsync()
    {
        var result = await _signalRService.InvokeAsync("LeaveRoom");
        if (result.IsSuccess)
        {
            IsJoinedRoom = false;
            Peers.Clear();
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
            _sendTransportId = sendTransportResult.Data.TransportId;
            _logger.LogInformation("Created send transport: {TransportId}", _sendTransportId);
        }

        // 创建接收 Transport
        var recvTransportResult = await _signalRService.InvokeAsync<CreateTransportResponse>(
            "CreateRecvWebRtcTransport",
            new { forceTcp = false, sctpCapabilities = (object?)null });

        if (recvTransportResult.IsSuccess && recvTransportResult.Data != null)
        {
            _recvTransportId = recvTransportResult.Data.TransportId;
            _logger.LogInformation("Created recv transport: {TransportId}", _recvTransportId);
        }
    }

    /// <summary>
    /// 启用媒体（摄像头和麦克风）
    /// </summary>
    private async Task EnableMediaAsync()
    {
        // TODO: 实现摄像头和麦克风采集，并调用 Produce
        _logger.LogInformation("Enabling media...");
        await Task.CompletedTask;
    }

    /// <summary>
    /// 处理服务器通知
    /// </summary>
    private void HandleNotification(MeetingNotification notification)
    {
        _logger.LogDebug("Handling notification: {Type}", notification.Type);

        App.Current.Dispatcher.Invoke(() =>
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
                default:
                    _logger.LogDebug("Unhandled notification: {Type}", notification.Type);
                    break;
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
            }
        }
    }

    private void HandleNewConsumer(object? data)
    {
        if (data == null) return;
        // TODO: 创建消费者，接收远端媒体
        _logger.LogInformation("New consumer received");
    }

    private void HandleConsumerClosed(object? data)
    {
        if (data == null) return;
        // TODO: 关闭消费者
        _logger.LogInformation("Consumer closed");
    }

    private void HandleProduceSources(object? data)
    {
        if (data == null) return;
        // TODO: 处理生产请求（Pull/Invite 模式）
        _logger.LogInformation("Produce sources requested");
    }
}

// 辅助类型
public class ServeModeResponse
{
    public string ServeMode { get; set; } = "Open";
}

public class JoinRoomResponse
{
    public PeerInfo[] Peers { get; set; } = [];
}

public class CreateTransportResponse
{
    public string TransportId { get; set; } = string.Empty;
    public object? IceParameters { get; set; }
    public object[]? IceCandidates { get; set; }
    public object? DtlsParameters { get; set; }
    public object? SctpParameters { get; set; }
}

public class PeerJoinRoomData
{
    public PeerInfo? Peer { get; set; }
}

public class PeerLeaveRoomData
{
    public string? PeerId { get; set; }
}
```

---

## 6. 完整代码示例

### 6.1 MainWindow.xaml

```xml
<Window x:Class="Dorisoy.Meeting.Client.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        mc:Ignorable="d"
        Title="Dorisoy Meeting Client" Height="600" Width="900">
    
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="300"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>
        
        <!-- 左侧控制面板 -->
        <StackPanel Grid.Column="0" Margin="10">
            <!-- 连接设置 -->
            <GroupBox Header="连接设置" Margin="0,0,0,10">
                <StackPanel Margin="5">
                    <Label Content="服务器地址:"/>
                    <TextBox Text="{Binding ServerUrl}" 
                             IsEnabled="{Binding IsConnected, Converter={StaticResource InverseBoolConverter}}"
                             Margin="0,0,0,5"/>
                    
                    <Label Content="选择 Peer:"/>
                    <ComboBox SelectedIndex="{Binding SelectedPeerIndex}"
                              IsEnabled="{Binding IsConnected, Converter={StaticResource InverseBoolConverter}}"
                              Margin="0,0,0,5">
                        <ComboBoxItem>Peer 0</ComboBoxItem>
                        <ComboBoxItem>Peer 1</ComboBoxItem>
                        <ComboBoxItem>Peer 2</ComboBoxItem>
                        <ComboBoxItem>Peer 3</ComboBoxItem>
                        <ComboBoxItem>Peer 4</ComboBoxItem>
                        <ComboBoxItem>Peer 5</ComboBoxItem>
                        <ComboBoxItem>Peer 6</ComboBoxItem>
                        <ComboBoxItem>Peer 7</ComboBoxItem>
                        <ComboBoxItem>Peer 8 (Admin)</ComboBoxItem>
                        <ComboBoxItem>Peer 9 (Admin)</ComboBoxItem>
                    </ComboBox>
                    
                    <Button Content="{Binding IsConnected, Converter={StaticResource ConnectButtonTextConverter}}"
                            Command="{Binding ToggleConnectionCommand}"
                            Margin="0,5,0,0"/>
                </StackPanel>
            </GroupBox>
            
            <!-- 房间设置 -->
            <GroupBox Header="房间设置" IsEnabled="{Binding IsConnected}" Margin="0,0,0,10">
                <StackPanel Margin="5">
                    <Label Content="选择房间:"/>
                    <ComboBox SelectedIndex="{Binding SelectedRoomIndex}"
                              IsEnabled="{Binding IsJoinedRoom, Converter={StaticResource InverseBoolConverter}}"
                              Margin="0,0,0,5">
                        <ComboBoxItem>Room 0</ComboBoxItem>
                        <ComboBoxItem>Room 1</ComboBoxItem>
                        <ComboBoxItem>Room 2</ComboBoxItem>
                        <ComboBoxItem>Room 3</ComboBoxItem>
                        <ComboBoxItem>Room 4</ComboBoxItem>
                    </ComboBox>
                    
                    <Button Content="{Binding IsJoinedRoom, Converter={StaticResource RoomButtonTextConverter}}"
                            Command="{Binding ToggleRoomCommand}"
                            Margin="0,5,0,0"/>
                </StackPanel>
            </GroupBox>
            
            <!-- Peer 列表 -->
            <GroupBox Header="房间成员" IsEnabled="{Binding IsJoinedRoom}">
                <ListBox ItemsSource="{Binding Peers}" 
                         Height="200"
                         Margin="5">
                    <ListBox.ItemTemplate>
                        <DataTemplate>
                            <StackPanel Orientation="Horizontal">
                                <TextBlock Text="{Binding DisplayName}" Margin="0,0,10,0"/>
                                <TextBlock Text="{Binding PeerId}" Foreground="Gray"/>
                            </StackPanel>
                        </DataTemplate>
                    </ListBox.ItemTemplate>
                </ListBox>
            </GroupBox>
            
            <!-- 状态 -->
            <TextBlock Text="{Binding StatusMessage}" 
                       Margin="0,10,0,0" 
                       FontStyle="Italic"/>
            <TextBlock Text="{Binding ServeMode, StringFormat='服务模式: {0}'}" 
                       Margin="0,5,0,0"/>
        </StackPanel>
        
        <!-- 右侧视频区域 -->
        <Grid Grid.Column="1" Background="#1E1E1E">
            <Grid.RowDefinitions>
                <RowDefinition Height="*"/>
                <RowDefinition Height="150"/>
            </Grid.RowDefinitions>
            
            <!-- 远端视频 -->
            <Border Grid.Row="0" Background="#2D2D2D" Margin="10">
                <TextBlock Text="远端视频" 
                           Foreground="White" 
                           HorizontalAlignment="Center" 
                           VerticalAlignment="Center"/>
            </Border>
            
            <!-- 本地视频预览 -->
            <Border Grid.Row="1" Background="#2D2D2D" Margin="10" Width="200" HorizontalAlignment="Right">
                <TextBlock Text="本地视频" 
                           Foreground="White" 
                           HorizontalAlignment="Center" 
                           VerticalAlignment="Center"/>
            </Border>
        </Grid>
    </Grid>
</Window>
```

### 6.2 App.xaml.cs - 依赖注入配置

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Windows;
using Dorisoy.Meeting.Client.Services;
using Dorisoy.Meeting.Client.ViewModels;

namespace Dorisoy.Meeting.Client;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 配置 Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File("logs/meeting-client-.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        // 配置依赖注入
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // 显示主窗口
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // 日志
        services.AddLogging(builder =>
        {
            builder.AddSerilog();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // 服务
        services.AddSingleton<ISignalRService, SignalRService>();
        services.AddSingleton<WebRtcService>();

        // ViewModels
        services.AddTransient<MainViewModel>();

        // Views
        services.AddTransient<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.CloseAndFlush();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
```

### 6.3 MainWindow.xaml.cs

```csharp
using System.Windows;
using Dorisoy.Meeting.Client.ViewModels;

namespace Dorisoy.Meeting.Client;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
```

---

## 7. 构建与运行

### 7.1 构建项目

```bash
cd src/Dorisoy.Meeting.Client
dotnet build
```

### 7.2 运行

```bash
# 首先确保服务器在运行
cd ../Dorisoy.Meeting.Web
dotnet run

# 然后运行客户端
cd ../Dorisoy.Meeting.Client
dotnet run
```

### 7.3 发布

```bash
# 发布为单文件可执行程序
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

---

## 8. 常见问题

### Q1: WebRTC 连接失败？

检查以下几点：
1. 确保服务器的 ICE 配置正确
2. 检查防火墙设置
3. 查看日志中的 ICE 候选信息

### Q2: 视频无法显示？

1. 检查摄像头权限
2. 确认摄像头设备可用
3. 检查视频编解码器支持

### Q3: 音频有回声？

1. 启用回声消除（AEC）
2. 使用耳机测试
3. 调整音频采集参数

### Q4: 如何支持更多编解码器？

SIPSorcery 默认支持 VP8、H264 等编解码器。如需其他编解码器，可以：
1. 安装对应的编解码器包
2. 配置 `RTCPeerConnection` 的编解码器优先级

---

## 附录 A: Hub 方法参考

| 方法名 | 参数 | 返回值 | 说明 |
|--------|------|--------|------|
| `GetServeMode` | 无 | `{ serveMode: string }` | 获取服务模式 |
| `GetRouterRtpCapabilities` | 无 | RtpCapabilities | 获取 RTP 能力 |
| `Join` | JoinRequest | MeetingMessage | 加入会议 |
| `JoinRoom` | JoinRoomRequest | `{ peers: Peer[] }` | 加入房间 |
| `LeaveRoom` | 无 | MeetingMessage | 离开房间 |
| `CreateSendWebRtcTransport` | CreateWebRtcTransportRequest | CreateWebRtcTransportResult | 创建发送 Transport |
| `CreateRecvWebRtcTransport` | CreateWebRtcTransportRequest | CreateWebRtcTransportResult | 创建接收 Transport |
| `ConnectWebRtcTransport` | ConnectWebRtcTransportRequest | MeetingMessage | 连接 Transport |
| `Produce` | ProduceRequest | `{ id: string, source: string }` | 生产媒体 |
| `CloseProducer` | producerId | MeetingMessage | 关闭生产者 |
| `ResumeConsumer` | consumerId | MeetingMessage | 恢复消费者 |
| `Ready` | 无 | MeetingMessage | 准备就绪 |
| `Pull` | PullRequest | MeetingMessage | Pull 模式拉取 |
| `Invite` | InviteRequest | MeetingMessage | Invite 模式邀请 |

## 附录 B: 通知类型参考

| 通知类型 | 数据结构 | 说明 |
|----------|----------|------|
| `peerJoinRoom` | `{ peer: Peer }` | 用户加入房间 |
| `peerLeaveRoom` | `{ peerId: string }` | 用户离开房间 |
| `newConsumer` | NewConsumerNotification | 新消费者 |
| `consumerClosed` | `{ consumerId: string }` | 消费者关闭 |
| `consumerPaused` | ConsumerNotification | 消费者暂停 |
| `consumerResumed` | ConsumerNotification | 消费者恢复 |
| `producerScore` | ProducerScoreNotification | 生产者分值 |
| `producerClosed` | `{ producerId: string }` | 生产者关闭 |
| `produceSources` | `{ sources: string[] }` | 请求生产源 |
| `closeSources` | `{ sources: string[] }` | 关闭源 |
