# Dorisoy.Mediasoup

<<<<<<< HEAD
**Dorisoy.Mediasoup** 是一个基于 .NET 8 的完整实时音视频通信解决方案，采用 Mediasoup SFU 架构实现高性能的 WebRTC 媒体服务。项目包含 Mediasoup 核心库（C# 实现）、libuv 异步 I/O 绑定、ASP.NET Core 集成中间件、SignalR 信令服务器，以及 WPF 桌面客户端和 Vue.js Web 客户端。支持 Open/Pull/Invite 三种服务模式，提供房间管理、音视频采集传输、多端互通等功能。技术栈涵盖 WebRTC、SignalR、SIPSorcery、OpenCvSharp、NAudio 等，适用于在线会议、远程协作、直播互动等场景。
=======
<div align="center">

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/WPF-Windows-0078D6)](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
[![mediasoup](https://img.shields.io/badge/mediasoup-3.15.7-orange)](https://github.com/versatica/mediasoup)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)

**基于 SFU 架构的开源视频会议系统**

</div>

---

Dorisoy.Mediasoup 是一个基于 .NET 8 和 Mediasoup SFU（选择性转发单元）架构的**企业级开源视频会议系统**。项目采用纯 C# 实现 Mediasoup 核心库，提供 WPF 桌面客户端和 Vue.js Web 客户端，支持**低延迟、高并发**的实时音视频通信。

系统集成了丰富的协作功能：**协同白板**、**实时投票**、**同步转译**、**屏幕截图标注**、**即时聊天**等，是构建在线会议、远程协作、在线教育、直播互动等场景的理想选择。

---

## 核心优势

| 特性 | 描述 |
|------|------|
| **SFU 架构** | 采用 Mediasoup 选择性转发，服务器仅转发媒体流，客户端承担编解码，大幅降低服务器负载 |
| **多编解码器** | 支持 VP8、VP9（默认）、H.264，VP9 可节省 30% 带宽，H.264 支持硬件加速 |
| **多端互通** | WPF 桌面端 + Vue.js Web 端无缝互通，统一 SignalR 信令协议 |
| **灵活服务模式** | Open（开放）、Pull（按需）、Invite（邀请）三种模式适配不同会议场景 |
| **丰富协作工具** | 内置白板、投票、翻译、截图、聊天等协作功能，开箱即用 |
| **纯 C# 实现** | Mediasoup 核心库纯 C# 实现，与 .NET 生态无缝集成 |
>>>>>>> pro

---

## 屏幕截图

<img src="https://github.com/dorisoy/Dorisoy.Mediasoup/blob/main/screen/1.png"/>

<img src="https://github.com/dorisoy/Dorisoy.Mediasoup/blob/main/screen/2.png"/>

<img src="https://github.com/dorisoy/Dorisoy.Mediasoup/blob/main/screen/3.png"/>

<img src="https://github.com/dorisoy/Dorisoy.Mediasoup/blob/main/screen/4.png"/>
---

## 功能特性

### 音视频通信

| 功能 | 描述 |
|------|------|
| **实时音视频** | 低延迟、高质量的音视频采集、编码、传输和渲染 |
| **多编解码器** | VP8、VP9（默认）、H.264 视频编解码，Opus 音频编解码 |
| **屏幕共享** | 支持全屏、窗口、区域共享，可共享系统音频 |
| **活跃发言者** | 基于音频级别的发言者识别和高亮显示 |
| **摄像头切换** | 支持多摄像头切换和分辨率调整 |

### 协作工具

| 功能 | 描述 |
|------|------|
| **协同白板** | 支持画笔、矩形、椭圆、文字、橡皮擦、选择等工具，实时同步绘制 |
| **实时投票** | 主持人创建投票，参与者实时投票，结果即时统计展示 |
| **同步转译** | 语音识别 + 多语言翻译，实时生成字幕，支持保存转录内容 |
| **屏幕截图** | 选区截图 + 内置标注工具（画笔、矩形、椭圆、箭头、文字），自动复制到剪贴板 |
| **协同编辑器** | 多人实时协同文档编辑 |

### 即时通讯

| 功能 | 描述 |
|------|------|
| **群聊/私聊** | 支持房间群聊和一对一私聊 |
| **多媒体消息** | 支持文本、图片、文件、表情消息 |
| **表情反应** | 会中表情反馈，带音效的表情动画 |
| **未读提醒** | 智能未读消息计数和提醒 |

### 会议管理

| 功能 | 描述 |
|------|------|
| **房间管理** | 创建/加入/离开房间，完整生命周期管理 |
| **成员管理** | 查看在线成员，踢出用户，禁言控制 |
| **主持人权限** | 邀请发言、关闭媒体流、发起投票等主持人专属功能 |
| **JWT 认证** | 安全的用户身份验证和授权机制 |

---

## 解决方案架构

```
Dorisoy.Mediasoup.sln
│
├── src/                                         # 源代码目录
│   │
│   ├── Dorisoy.Mediasoup/                       # [核心库] Mediasoup SFU C# 实现
│   │   ├── Router/                              #   路由器 - 管理 Transport、Producer、Consumer
│   │   ├── Transport/                           #   传输层抽象基类
│   │   ├── WebRtcTransport/                     #   WebRTC 传输实现
│   │   ├── PlainTransport/                      #   Plain RTP 传输（用于 FFmpeg/GStreamer）
│   │   ├── PipeTransport/                       #   管道传输（用于路由器间通信）
│   │   ├── DirectTransport/                     #   直接传输
│   │   ├── Producer/                            #   媒体生产者
│   │   ├── Consumer/                            #   媒体消费者
│   │   ├── DataProducer/                        #   数据通道生产者
│   │   ├── DataConsumer/                        #   数据通道消费者
│   │   ├── Worker/                              #   Worker 进程管理
│   │   ├── Channel/                             #   与原生 worker 的通信通道
│   │   ├── WebRtcServer/                        #   WebRTC 服务器（端口复用）
│   │   ├── RtpObserver/                         #   RTP 观察者基类
│   │   ├── AudioLevelObserver/                  #   音频级别观察者
│   │   ├── ActiveSpeakerObserver/               #   活跃发言者观察者
│   │   ├── Settings/                            #   配置设置类
│   │   ├── ORTC/                                #   ORTC 能力协商
│   │   ├── MediasoupServer.cs                   #   服务器入口类
│   │   └── MediasoupOptions.cs                  #   配置选项
│   │
│   ├── Dorisoy.Libuv/                           # [底层库] libuv 异步 I/O 的 .NET 绑定
│   │   └── src/                                 #   TCP/UDP/Pipe 异步通信实现
│   │
│   ├── Dorisoy.Mediasoup.Common/                # [共享库] 公共模型和工具
│   │   ├── ClientRequest/                       #   客户端请求模型（21个）
│   │   ├── RtpParameters/                       #   RTP 参数定义
│   │   ├── SctpParameters/                      #   SCTP 参数定义
│   │   ├── FBS/                                 #   FlatBuffers 序列化定义（28个子目录）
│   │   ├── EventEmitter/                        #   事件发射器实现
│   │   ├── H264ProfileLevelId/                  #   H.264 配置解析
│   │   ├── ScalabilityMode/                     #   可伸缩模式解析
│   │   └── Constants/                           #   常量定义
│   │
│   ├── Dorisoy.Mediasoup.AspNetCore/            # [集成库] ASP.NET Core 集成中间件
│   │   └── Microsoft/Extensions/                #   DI 扩展方法
│   │
│   ├── Dorisoy.Meeting.Server/                  # [服务端] 会议信令服务器
│   │   ├── SignalR/                             #   SignalR Hub 和服务
│   │   │   ├── MeetingHub.cs                    #     会议 Hub（1400+ 行核心逻辑）
│   │   │   ├── Models/                          #     消息通知模型
│   │   │   └── Services/                        #     Hub 相关服务
│   │   ├── Models/                              #   业务模型（11个）
│   │   ├── Authorization/                       #   JWT 认证和授权
│   │   ├── Settings/                            #   服务器配置
│   │   ├── Exceptions/                          #   自定义异常（6个）
│   │   ├── Room.cs                              #   房间管理（音频观察、成员管理）
│   │   ├── Peer.cs                              #   Peer 管理（Transport/Producer/Consumer）
│   │   ├── Scheduler.cs                         #   调度器（房间创建、成员加入离开）
│   │   └── ServeMode.cs                         #   三种服务模式枚举
│   │
│   ├── Dorisoy.Meeting.Web/                     # [Web后端] ASP.NET Core 应用
│   │   ├── Controllers/                         #   REST API 控制器
│   │   ├── Startup.cs                           #   应用配置（认证、CORS、Mediasoup）
│   │   ├── Program.cs                           #   应用入口
│   │   ├── mediasoupsettings.json               #   Mediasoup 详细配置
│   │   ├── appsettings.json                     #   应用配置
│   │   ├── dtls-cert.pem / dtls-key.pem         #   DTLS 证书
│   │   └── wwwroot/                             #   静态文件
│   │
│   ├── Dorisoy.Meeting.Client/                  # [桌面端] WPF 客户端
│   │   ├── WebRtc/                              #   WebRTC 实现
│   │   │   ├── MediasoupDevice.cs               #     Mediasoup 设备封装
│   │   │   ├── MediasoupTransport.cs            #     传输层实现（55KB 核心）
│   │   │   ├── MediasoupSdpBuilder.cs           #     SDP 构建器
│   │   │   ├── AudioEncoders.cs                 #     音频编码器（Opus/PCMA）
│   │   │   ├── VideoEncoders.cs                 #     视频编码器（VP8/H264）
│   │   │   ├── VideoDecoders.cs                 #     视频解码器
│   │   │   ├── RtpMediaDecoder.cs               #     RTP 媒体解码
│   │   │   └── FFmpegConfig.cs                  #     FFmpeg 配置
│   │   ├── Services/                            #   服务层
│   │   │   ├── SignalRService.cs                #     SignalR 通信服务
│   │   │   └── WebRtcService.cs                 #     WebRTC 服务
│   │   ├── ViewModels/                          #   MVVM 视图模型
│   │   ├── Models/                              #   数据模型
│   │   ├── Views/                               #   视图控件
│   │   ├── Converters/                          #   值转换器
│   │   ├── Helpers/                             #   工具类
│   │   ├── MainWindow.xaml                      #   主窗口（31KB 完整 UI）
│   │   └── App.xaml.cs                          #   应用入口和 DI 配置
│   │
│   ├── Dorisoy.Meeting.Web.Client/              # [Web前端] Vue.js 客户端
│   │   ├── src/
│   │   │   ├── App.vue                          #   主应用组件（40KB 完整实现）
│   │   │   ├── lib/                             #   依赖库
│   │   │   └── assets/                          #   静态资源
│   │   ├── package.json                         #   NPM 依赖配置
│   │   └── vite.config.js                       #   Vite 构建配置
│   │
│   ├── Dorisoy.Utils/                           # [工具库] 通用工具
│   │   ├── Extensions/                          #   扩展方法（21个）
│   │   ├── Cryptography/                        #   加密工具
│   │   ├── Json/                                #   JSON 序列化工具
│   │   └── Models/                              #   通用模型
│   │
│   └── Dorisoy.Mediasoup.Runtimes.Builder/      # [构建工具] 原生运行时构建脚本
│       ├── build_nuget.ps1                      #   Windows 构建脚本
│       └── build_nuget.sh                       #   Linux/macOS 构建脚本
│
└── tools/TokenGenerator/                        # Token 生成工具
    └── Program.cs                               #   JWT Token 生成器
```

---

## 技术栈

### 后端技术

| 技术 | 版本 | 用途 |
|------|------|------|
| .NET | 8.0 | 运行时框架 |
| ASP.NET Core | 8.0 | Web 框架 |
| SignalR | 8.0 | 实时双向通信、信令服务 |
| Mediasoup | 3.15.7 | SFU 媒体服务器 |
| FlatBuffers | - | 高效序列化 |
| Serilog | - | 结构化日志 |

### 客户端技术

| 技术 | 用途 |
|------|------|
| WPF + Wpf.Ui | 桌面客户端 UI 框架（Fluent Design） |
| SIPSorcery | .NET WebRTC 实现 |
| NAudio | 音频采集和处理 |
| OpenCvSharp | 视频采集和处理 |
| FFmpeg | 视频编解码（VP8/VP9/H264） |
| CommunityToolkit.Mvvm | MVVM 框架 |
| Vue.js 3 | Web 前端框架 |
| mediasoup-client | 浏览器 WebRTC 客户端 |

### 支持的编解码器

| 类型 | 编解码器 | 说明 |
|------|---------|------|
| 视频 | VP9（默认） | 高压缩率，节省 30% 带宽 |
| 视频 | VP8 | 兼容性好 |
| 视频 | H.264 | 硬件加速支持 |
| 音频 | Opus (48kHz) | 高质量立体声 |

---

## 三种服务模式

系统支持三种不同的会议服务模式，适配不同场景需求：

### 1. Open 模式（开放模式）

```
适用场景：自由会议、开放讨论、小型团队协作
```

| 特点 | 说明 |
|------|------|
| 自动推流 | 用户进入后立即开始推流 |
| 自动消费 | 自动消费房间内其他用户的媒体流 |
| 持续推流 | 媒体流即使无人消费也不会自动停止 |

### 2. Pull 模式（拉取模式）

```
适用场景：大型会议、按需观看、节省带宽
```

| 特点 | 说明 |
|------|------|
| 按需拉取 | 用户主动选择要拉取的媒体流 |
| 按需推流 | 触发其他用户推流 |
| 自动停流 | 无人消费时自动停止推流，节省带宽 |

### 3. Invite 模式（邀请模式）

```
适用场景：正式会议、课堂教学、大型直播
```

| 特点 | 说明 |
|------|------|
| 主持人邀请 | 仅主持人可邀请用户推流 |
| 申请审批 | 用户可申请推流，需主持人同意 |
| 集中管控 | 主持人可关闭任意用户的媒体流 |

---

## 系统架构

```
┌─────────────────────────────────────────────────────────────────────┐
│                         Client Layer                                 │
├─────────────────────────────────┬───────────────────────────────────┤
│      WPF Desktop Client         │        Vue.js Web Client          │
│  ┌───────────────────────────┐  │  ┌─────────────────────────────┐  │
│  │  SIPSorcery WebRTC        │  │  │  mediasoup-client           │  │
│  │  NAudio + OpenCvSharp     │  │  │  WebRTC Native API          │  │
│  │  FFmpeg Encoders          │  │  │  Browser Media API          │  │
│  └───────────────────────────┘  │  └─────────────────────────────┘  │
└────────────────┬────────────────┴────────────────┬──────────────────┘
                 │         SignalR WebSocket       │
                 └────────────────┬────────────────┘
                                  │
┌─────────────────────────────────▼───────────────────────────────────┐
│                       Signaling Layer                                │
│  ┌────────────────────────────────────────────────────────────────┐ │
│  │                    MeetingHub (SignalR)                         │ │
│  │  • GetRouterRtpCapabilities  • CreateWebRtcTransport           │ │
│  │  • Join / JoinRoom           • ConnectWebRtcTransport          │ │
│  │  • LeaveRoom                 • Produce / CloseProducer         │ │
│  │  • Consume / ResumeConsumer  • Pull / Invite                   │ │
│  └────────────────────────────────────────────────────────────────┘ │
│  ┌─────────────────┐  ┌─────────────────┐  ┌────────────────────┐   │
│  │    Scheduler    │  │      Room       │  │       Peer         │   │
│  │  房间生命周期   │  │  成员管理       │  │  Transport/Producer│   │
│  │  成员调度      │  │  音频观察       │  │  Consumer 管理     │   │
│  └─────────────────┘  └─────────────────┘  └────────────────────┘   │
└─────────────────────────────────┬───────────────────────────────────┘
                                  │
┌─────────────────────────────────▼───────────────────────────────────┐
│                        SFU Media Layer                               │
│  ┌────────────────────────────────────────────────────────────────┐ │
│  │                    MediasoupServer                              │ │
│  │  ┌──────────────────────────────────────────────────────────┐  │ │
│  │  │                      Worker(s)                            │  │ │
│  │  │  ┌─────────────────────────────────────────────────────┐ │  │ │
│  │  │  │                     Router                           │ │  │ │
│  │  │  │  ┌─────────────┐ ┌─────────────┐ ┌───────────────┐  │ │  │ │
│  │  │  │  │  Transport  │ │  Producer   │ │   Consumer    │  │ │  │ │
│  │  │  │  │  (WebRTC/   │ │  (Audio/    │ │   (Audio/     │  │ │  │ │
│  │  │  │  │   Plain)    │ │   Video)    │ │    Video)     │  │ │  │ │
│  │  │  │  └─────────────┘ └─────────────┘ └───────────────┘  │ │  │ │
│  │  │  │  ┌─────────────────────────────────────────────────┐│ │  │ │
│  │  │  │  │  RtpObserver (AudioLevel/ActiveSpeaker)         ││ │  │ │
│  │  │  │  └─────────────────────────────────────────────────┘│ │  │ │
│  │  │  └─────────────────────────────────────────────────────┘ │  │ │
│  │  └──────────────────────────────────────────────────────────┘  │ │
│  └────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 使用场景

| 场景 | 推荐配置 | 适用功能 |
|------|----------|----------|
| **企业内部会议** | Open 模式 + VP9 | 音视频、屏幕共享、白板、聊天 |
| **在线培训/教育** | Invite 模式 + VP9 | 主持人管控、白板、投票、转译 |
| **远程协作** | Open 模式 + H264 | 屏幕共享、截图标注、协同编辑 |
| **大型网络研讨** | Pull 模式 + VP9 | 按需拉流、节省带宽 |
| **直播互动** | Invite 模式 | 主播控制、表情互动、实时聊天 |

---

## 快速开始

### 环境要求

- .NET 8.0 SDK
- Node.js 18+ (Web 客户端)
- FFmpeg DLL（客户端编解码，构建时自动复制）
- mediasoup-worker 原生二进制文件

### 1. 克隆项目

```bash
git clone https://github.com/dorisoy/Dorisoy.Mediasoup.git
cd Dorisoy.Mediasoup
```

### 2. 配置服务端

编辑 `src/Dorisoy.Meeting.Web/mediasoupsettings.json`：

```json
{
    "MediasoupStartupSettings": {
        "MediasoupVersion": "3.15.7",
        "NumberOfWorkers": 1,
        "UseWebRtcServer": true
    },
    "MediasoupSettings": {
        "WebRtcServerSettings": {
            "ListenInfos": [
                {
                    "Protocol": "udp",
                    "Ip": "0.0.0.0",
                    "AnnouncedAddress": "你的公网或局域网IP",
                    "Port": 44444
                }
            ]
        }
    },
    "MeetingServerSettings": {
        "ServeMode": "Open"  // Open, Pull 或 Invite
    }
}
```

### 3. 启动服务端

```bash
cd src/Dorisoy.Meeting.Web
dotnet run
```

服务端启动后：
- HTTP API: `http://localhost:9000`
- SignalR Hub: `ws://localhost:9000/hubs/meetingHub`
- Swagger 文档: `http://localhost:9000/swagger`

### 4. 启动 Web 客户端

```bash
cd src/Dorisoy.Meeting.Web.Client
npm install
npm run dev
```

### 5. 启动 WPF 客户端

```bash
cd src/Dorisoy.Meeting.Client
dotnet run
```

---

## SignalR API 参考

### Hub 方法

| 方法 | 参数 | 返回值 | 说明 |
|------|------|--------|------|
| `GetServeMode` | - | `{ serveMode }` | 获取服务模式 |
| `GetRouterRtpCapabilities` | - | `RtpCapabilities` | 获取路由器 RTP 能力 |
| `Join` | `JoinRequest` | `MeetingMessage` | 加入会议 |
| `JoinRoom` | `JoinRoomRequest` | `{ peers[] }` | 加入房间 |
| `LeaveRoom` | - | `MeetingMessage` | 离开房间 |
| `CreateSendWebRtcTransport` | `CreateWebRtcTransportRequest` | `TransportInfo` | 创建发送 Transport |
| `CreateRecvWebRtcTransport` | `CreateWebRtcTransportRequest` | `TransportInfo` | 创建接收 Transport |
| `ConnectWebRtcTransport` | `ConnectRequest` | `MeetingMessage` | 连接 Transport |
| `Produce` | `ProduceRequest` | `{ id, source }` | 生产媒体 |
| `Consume` | `ConsumeRequest` | `ConsumerInfo` | 消费媒体 |
| `ResumeConsumer` | `consumerId` | `MeetingMessage` | 恢复消费者 |
| `CloseProducer` | `producerId` | `MeetingMessage` | 关闭生产者 |
| `Ready` | - | `MeetingMessage` | 通知准备就绪 |
| `Pull` | `PullRequest` | `MeetingMessage` | 拉取媒体流 (Pull 模式) |
| `Invite` | `InviteRequest` | `MeetingMessage` | 邀请推流 (Invite 模式) |

### 服务端通知

| 通知类型 | 数据 | 说明 |
|----------|------|------|
| `peerJoinRoom` | `{ peer }` | 用户加入房间 |
| `peerLeaveRoom` | `{ peerId }` | 用户离开房间 |
| `newConsumer` | `ConsumerInfo` | 新消费者可用 |
| `consumerClosed` | `{ consumerId }` | 消费者已关闭 |
| `consumerPaused` | `{ consumerId }` | 消费者已暂停 |
| `consumerResumed` | `{ consumerId }` | 消费者已恢复 |
| `producerClosed` | `{ producerId }` | 生产者已关闭 |
| `produceSources` | `{ sources[] }` | 请求生产媒体源 |
| `closeSources` | `{ sources[] }` | 请求关闭媒体源 |
| `activeSpeaker` | `{ peerId, volume }` | 活跃发言者变化 |

---

## 项目依赖

### 服务端 NuGet 包

```xml
<PackageReference Include="Microsoft.AspNetCore.SignalR" Version="1.1.0" />
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.0" />
<PackageReference Include="Microsoft.VisualStudio.Threading" Version="17.10.48" />
<PackageReference Include="FlatSharp.Runtime" Version="7.5.1" />
<PackageReference Include="Serilog.AspNetCore" Version="8.0.0" />
<PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />
```

### 客户端 NuGet 包

```xml
<PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="8.0.0" />
<PackageReference Include="SIPSorceryMedia.Windows" Version="8.0.5" />
<PackageReference Include="SIPSorceryMedia.FFmpeg" Version="1.2.0" />
<PackageReference Include="NAudio" Version="2.2.1" />
<PackageReference Include="OpenCvSharp4.Windows" Version="4.9.0" />
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />
```

---

## 部署指南

### Docker 部署

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 9000
EXPOSE 44444/udp
EXPOSE 44444/tcp

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish "src/Dorisoy.Meeting.Web/Dorisoy.Meeting.Web.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Dorisoy.Meeting.Web.dll"]
```

### 生产环境配置

1. **配置 HTTPS**
```json
{
    "Kestrel": {
        "Endpoints": {
            "Https": {
                "Url": "https://0.0.0.0:443",
                "Certificate": {
                    "Path": "/path/to/cert.pfx",
                    "Password": "your-password"
                }
            }
        }
    }
}
```

2. **配置公网 IP**
```json
{
    "MediasoupSettings": {
        "WebRtcServerSettings": {
            "ListenInfos": [{
                "Ip": "0.0.0.0",
                "AnnouncedAddress": "公网IP或域名"
            }]
        }
    }
}
```

3. **开放端口**
   - TCP 9000 (HTTP/WebSocket)
   - UDP/TCP 44444 (WebRTC)
   - UDP 40000-49999 (媒体传输)

---

## 常见问题

### Q: WebRTC 连接失败？

1. 检查 `AnnouncedAddress` 配置是否正确
2. 确保 UDP/TCP 端口已开放
3. 检查防火墙和 NAT 配置
4. 查看服务端日志中的 ICE 候选信息

### Q: 音视频无法显示？

1. 确认浏览器已授权摄像头/麦克风权限
2. 检查编解码器兼容性
3. 查看 Console 和 Network 错误信息

### Q: 如何调整视频质量？

修改 `mediasoupsettings.json` 中的比特率设置：
```json
{
    "WebRtcTransportSettings": {
        "InitialAvailableOutgoingBitrate": 1000000,
        "MaximumIncomingBitrate": 1500000
    }
}
```

### Q: 如何支持更多用户？

1. 增加 `NumberOfWorkers` 数量
2. 使用负载均衡分发到多个服务器
3. 考虑使用 `PipeTransport` 跨路由器通信

<<<<<<< HEAD
---

## 许可证

本项目采用 MIT 许可证。详见 [LICENSE](LICENSE) 文件。

---

## 贡献

欢迎提交 Issue 和 Pull Request！
=======

### Q: 编解码器如何选择？

| 场景 | 推荐编解码器 | 原因 |
|------|--------------|------|
| 大型会议 (30+ 人) | VP9 | 节省 30% 带宽，最大化参会人数 |
| 小型会议 (5-10 人) | H264 | 硬件加速，低 CPU 占用 |
| 兼容性优先 | VP8 | 广泛支持 |

---

## 开源贡献

欢迎参与项目贡献！
>>>>>>> pro

1. Fork 本仓库
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 开启 Pull Request

---

## 许可证

本项目采用 MIT 许可证。详见 [LICENSE](LICENSE) 文件。

---

## 联系方式

- GitHub: [@dorisoy](https://github.com/dorisoy)
- 项目地址: [Dorisoy.Mediasoup](https://github.com/dorisoy/Dorisoy.Mediasoup)
