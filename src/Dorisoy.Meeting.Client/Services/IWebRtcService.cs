using System.Windows.Media.Imaging;
using Dorisoy.Meeting.Client.WebRtc;

namespace Dorisoy.Meeting.Client.Services;

/// <summary>
/// WebRTC 服务接口 - 处理媒体采集、传输和渲染
/// </summary>
public interface IWebRtcService : IDisposable
{
    /// <summary>
    /// 本地视频帧更新事件
    /// </summary>
    event Action<WriteableBitmap>? OnLocalVideoFrame;

    /// <summary>
    /// 远端视频帧更新事件
    /// </summary>
    event Action<string, WriteableBitmap>? OnRemoteVideoFrame;

    /// <summary>
    /// 连接状态变更事件
    /// </summary>
    event Action<string>? OnConnectionStateChanged;

    /// <summary>
    /// Recv Transport DTLS 连接完成事件
    /// </summary>
    event Action? OnRecvTransportDtlsConnected;

    /// <summary>
    /// 是否正在生产视频
    /// </summary>
    bool IsProducingVideo { get; }

    /// <summary>
    /// 是否正在生产音频
    /// </summary>
    bool IsProducingAudio { get; }

    /// <summary>
    /// Mediasoup 设备
    /// </summary>
    MediasoupDevice? Device { get; }

    /// <summary>
    /// 发送 Transport
    /// </summary>
    MediasoupTransport? SendTransport { get; }

    /// <summary>
    /// 接收 Transport
    /// </summary>
    MediasoupTransport? RecvTransport { get; }

    /// <summary>
    /// 初始化服务
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// 获取可用摄像头列表
    /// </summary>
    Task<IEnumerable<MediaDeviceInfo>> GetCamerasAsync();

    /// <summary>
    /// 获取可用麦克风列表
    /// </summary>
    Task<IEnumerable<MediaDeviceInfo>> GetMicrophonesAsync();

    /// <summary>
    /// 开始摄像头采集
    /// </summary>
    /// <param name="deviceId">设备ID，null表示默认设备</param>
    Task StartCameraAsync(string? deviceId = null);

    /// <summary>
    /// 停止摄像头采集
    /// </summary>
    Task StopCameraAsync();

    /// <summary>
    /// 开始麦克风采集
    /// </summary>
    /// <param name="deviceId">设备ID，null表示默认设备</param>
    Task StartMicrophoneAsync(string? deviceId = null);

    /// <summary>
    /// 停止麦克风采集
    /// </summary>
    Task StopMicrophoneAsync();

    /// <summary>
    /// 加载设备能力
    /// </summary>
    void LoadDevice(object routerRtpCapabilities);

    /// <summary>
    /// 创建发送 Transport
    /// </summary>
    void CreateSendTransport(string transportId, object iceParameters, object iceCandidates, object dtlsParameters);

    /// <summary>
    /// 创建接收 Transport
    /// </summary>
    void CreateRecvTransport(string transportId, object iceParameters, object iceCandidates, object dtlsParameters);

    /// <summary>
    /// 连接发送 Transport - DTLS 握手
    /// </summary>
    /// <param name="connectCallback">回调函数，用于调用服务器 ConnectWebRtcTransport API</param>
    Task ConnectSendTransportAsync(Func<string, object, Task> connectCallback);

    /// <summary>
    /// 连接接收 Transport - DTLS 握手
    /// </summary>
    /// <param name="connectCallback">回调函数，用于调用服务器 ConnectWebRtcTransport API</param>
    Task ConnectRecvTransportAsync(Func<string, object, Task> connectCallback);

    /// <summary>
    /// 设置 recv transport 的 SDP 协商完成回调
    /// 当 SDP 协商完成后，自动调用 ConnectWebRtcTransport
    /// </summary>
    /// <param name="connectCallback">回调函数，用于调用服务器 ConnectWebRtcTransport API</param>
    void SetupRecvTransportNegotiationCallback(Func<string, object, Task> connectCallback);

    /// <summary>
    /// Recv Transport 是否已完成 DTLS 连接
    /// </summary>
    bool IsRecvTransportDtlsConnected { get; }

    /// <summary>
    /// 添加远端消费者
    /// </summary>
    /// <param name="consumerId">消费者ID</param>
    /// <param name="kind">媒体类型</param>
    /// <param name="rtpParameters">RTP参数</param>
    Task AddConsumerAsync(string consumerId, string kind, object? rtpParameters);

    /// <summary>
    /// 移除远端消费者
    /// </summary>
    /// <param name="consumerId">消费者ID</param>
    Task RemoveConsumerAsync(string consumerId);

    /// <summary>
    /// 关闭所有连接
    /// </summary>
    Task CloseAsync();
}

/// <summary>
/// 媒体设备信息
/// </summary>
public class MediaDeviceInfo
{
    /// <summary>
    /// 设备ID
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// 设备名称
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// 设备类型
    /// </summary>
    public string Kind { get; set; } = string.Empty;
}
