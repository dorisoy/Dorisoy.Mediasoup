using Microsoft.Extensions.Logging;
using NAudio.Wave;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using Dorisoy.Meeting.Client.Models;
using Dorisoy.Meeting.Client.WebRtc;
using Dorisoy.Meeting.Client.WebRtc.Encoder;
using Dorisoy.Meeting.Client.WebRtc.Decoder;

namespace Dorisoy.Meeting.Client.Services;

/// <summary>
/// WebRTC 服务实现 - 使用 SIPSorcery + OpenCvSharp4 + NAudio
/// </summary>
public class WebRtcService : IWebRtcService
{
    private readonly ILogger<WebRtcService> _logger;
    private readonly ILoggerFactory _loggerFactory;

    // Mediasoup 设备和 Transport
    private MediasoupDevice? _device;
    private MediasoupTransport? _sendTransport;
    private MediasoupTransport? _recvTransport;
    
    // RTP 媒体解码器
    private RtpMediaDecoder? _rtpDecoder;
    
    // 视频编码器 - 支持 VP8/VP9/H264
    private IVideoEncoder? _videoEncoder;
    private AudioFramePacketizer? _audioPacketizer;
    
    // 当前视频编解码器类型
    private VideoCodecType _currentVideoCodec = VideoCodecType.VP8;

    // 视频采集
    private VideoCapture? _videoCapture;
    private Thread? _videoCaptureThread;
    private volatile bool _isVideoCaptureRunning;
    private readonly object _videoCaptureLock = new();

    // 音频采集
    private WaveInEvent? _waveIn;
    private volatile bool _isAudioCaptureRunning;

    // 远端音频播放
    private WaveOutEvent? _waveOut;
    private BufferedWaveProvider? _audioBuffer;
    private readonly object _audioPlaybackLock = new();

    // 远端消费者
    private readonly ConcurrentDictionary<string, ConsumerInfo> _consumers = new();

    // 视频帧缓存
    private WriteableBitmap? _localVideoBitmap;
    private readonly ConcurrentDictionary<string, WriteableBitmap> _remoteVideoBitmaps = new();

    /// <summary>
    /// 本地视频帧更新事件
    /// </summary>
    public event Action<WriteableBitmap>? OnLocalVideoFrame;

    /// <summary>
    /// 远端视频帧更新事件
    /// </summary>
    public event Action<string, WriteableBitmap>? OnRemoteVideoFrame;

    /// <summary>
    /// 连接状态变更事件
    /// </summary>
    public event Action<string>? OnConnectionStateChanged;

    /// <summary>
    /// Recv Transport DTLS 连接完成事件
    /// </summary>
    public event Action? OnRecvTransportDtlsConnected;

    /// <summary>
    /// Recv Transport 是否已完成 DTLS 连接
    /// </summary>
    private volatile bool _isRecvTransportDtlsConnected;
    public bool IsRecvTransportDtlsConnected => _isRecvTransportDtlsConnected;

    /// <summary>
    /// 音频数据事件（用于推送到服务器）
    /// </summary>
    public event Action<byte[]>? OnAudioData;

    /// <summary>
    /// 视频数据事件（用于推送到服务器）
    /// </summary>
    public event Action<byte[], int, int>? OnVideoData;

    /// <summary>
    /// 编码后的视频帧事件 (VP8 数据)
    /// </summary>
    public event Action<byte[], bool>? OnEncodedVideoFrame;

    /// <summary>
    /// 编码后的音频帧事件 (Opus 数据)
    /// </summary>
    public event Action<byte[]>? OnEncodedAudioFrame;

    /// <summary>
    /// 是否正在生产视频
    /// </summary>
    public bool IsProducingVideo => _isVideoCaptureRunning;

    /// <summary>
    /// 是否正在生产音频
    /// </summary>
    public bool IsProducingAudio => _isAudioCaptureRunning;

    /// <summary>
    /// 当前视频质量配置
    /// </summary>
    public VideoQualitySettings? VideoQuality { get; set; }
    
    /// <summary>
    /// 当前视频编解码器类型
    /// </summary>
    public VideoCodecType CurrentVideoCodec
    {
        get => _currentVideoCodec;
        set
        {
            if (_currentVideoCodec != value)
            {
                _logger.LogInformation("视频编解码器切换: {OldCodec} -> {NewCodec}", _currentVideoCodec, value);
                _currentVideoCodec = value;
                
                // 如果正在采集视频，需要重新初始化编码器
                if (_isVideoCaptureRunning && _videoEncoder != null)
                {
                    // 释放旧编码器
                    _videoEncoder.Dispose();
                    _videoEncoder = null;
                }
                
                // 通知 RTP 解码器切换解码器类型
                _rtpDecoder?.SetVideoCodecType(value);
                
                // 同步更新 SendTransport 的编解码器类型（用于 RTP 打包）
                _sendTransport?.SetVideoCodecType(value);
            }
        }
    }

    /// <summary>
    /// Mediasoup 设备
    /// </summary>
    public MediasoupDevice? Device => _device;

    /// <summary>
    /// 发送 Transport
    /// </summary>
    public MediasoupTransport? SendTransport => _sendTransport;

    /// <summary>
    /// 接收 Transport
    /// </summary>
    public MediasoupTransport? RecvTransport => _recvTransport;

    public WebRtcService(ILogger<WebRtcService> logger, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// 初始化服务
    /// </summary>
    public Task InitializeAsync()
    {
        _logger.LogInformation("WebRTC service initialized with OpenCvSharp4 and NAudio");
        return Task.CompletedTask;
    }

    #region 设备枚举

    /// <summary>
    /// 获取可用摄像头列表 - 使用 DirectShow 获取真实设备名称
    /// </summary>
    public Task<IEnumerable<MediaDeviceInfo>> GetCamerasAsync()
    {
        var cameras = new List<MediaDeviceInfo>();

        try
        {
            // 使用 DirectShow 枚举视频输入设备，获取真实设备名称
            var videoDevices = Helpers.DirectShowDeviceEnumerator.GetVideoInputDevices();
            
            foreach (var device in videoDevices)
            {
                cameras.Add(new MediaDeviceInfo
                {
                    DeviceId = device.Index.ToString(),
                    Label = device.Name,
                    Kind = "videoinput"
                });
            }

            _logger.LogInformation("Found {Count} cameras: {Names}", 
                cameras.Count, 
                string.Join(", ", cameras.Select(c => c.Label)));

            if (cameras.Count == 0)
            {
                // 如果 DirectShow 枚举失败，回退到 OpenCV 方式
                cameras = FallbackEnumerateCameras();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate cameras with DirectShow, falling back to OpenCV");
            cameras = FallbackEnumerateCameras();
        }

        return Task.FromResult<IEnumerable<MediaDeviceInfo>>(cameras);
    }

    /// <summary>
    /// 回退方式：使用 OpenCV 枚举摄像头
    /// </summary>
    private List<MediaDeviceInfo> FallbackEnumerateCameras()
    {
        var cameras = new List<MediaDeviceInfo>();

        try
        {
            for (int i = 0; i < 10; i++)
            {
                using var testCapture = new VideoCapture(i, VideoCaptureAPIs.DSHOW);
                if (testCapture.IsOpened())
                {
                    cameras.Add(new MediaDeviceInfo
                    {
                        DeviceId = i.ToString(),
                        Label = $"摄像头 {i}",
                        Kind = "videoinput"
                    });
                    testCapture.Release();
                }
                else
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenCV fallback enumeration failed");
        }

        if (cameras.Count == 0)
        {
            cameras.Add(new MediaDeviceInfo
            {
                DeviceId = "0",
                Label = "默认摄像头",
                Kind = "videoinput"
            });
        }

        return cameras;
    }

    /// <summary>
    /// 获取可用麦克风列表
    /// </summary>
    public Task<IEnumerable<MediaDeviceInfo>> GetMicrophonesAsync()
    {
        var microphones = new List<MediaDeviceInfo>();

        try
        {
            int deviceCount = WaveIn.DeviceCount;
            for (int i = 0; i < deviceCount; i++)
            {
                var caps = WaveIn.GetCapabilities(i);
                microphones.Add(new MediaDeviceInfo
                {
                    DeviceId = i.ToString(),
                    Label = caps.ProductName,
                    Kind = "audioinput"
                });
            }

            if (microphones.Count == 0)
            {
                microphones.Add(new MediaDeviceInfo
                {
                    DeviceId = "0",
                    Label = "Default Microphone",
                    Kind = "audioinput"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate microphones");
            microphones.Add(new MediaDeviceInfo
            {
                DeviceId = "0",
                Label = "Default Microphone",
                Kind = "audioinput"
            });
        }

        return Task.FromResult<IEnumerable<MediaDeviceInfo>>(microphones);
    }

    #endregion

    #region 视频采集

    /// <summary>
    /// 开始摄像头采集 - 异步执行，不阻塞 UI 线程
    /// </summary>
    public async Task StartCameraAsync(string? deviceId = null)
    {
        if (_isVideoCaptureRunning)
        {
            _logger.LogWarning("Camera is already running");
            return;
        }

        try
        {
            int cameraIndex = 0;
            if (!string.IsNullOrEmpty(deviceId) && int.TryParse(deviceId, out int index))
            {
                cameraIndex = index;
            }

            _logger.LogInformation("Starting camera with index {CameraIndex}", cameraIndex);

            // 在后台线程初始化摄像头，避免阻塞 UI
            await Task.Run(() =>
            {
                lock (_videoCaptureLock)
                {
                    _videoCapture = new VideoCapture(cameraIndex, VideoCaptureAPIs.DSHOW);

                    if (!_videoCapture.IsOpened())
                    {
                        throw new Exception($"Failed to open camera {cameraIndex}");
                    }

                    // 设置视频参数
                    _videoCapture.Set(VideoCaptureProperties.FrameWidth, 640);
                    _videoCapture.Set(VideoCaptureProperties.FrameHeight, 480);
                    _videoCapture.Set(VideoCaptureProperties.Fps, 30);

                    _isVideoCaptureRunning = true;
                }
            }).ConfigureAwait(false);

            // 启动视频采集线程
            _videoCaptureThread = new Thread(VideoCaptureLoop)
            {
                IsBackground = true,
                Name = "VideoCaptureThread"
            };
            _videoCaptureThread.Start();

            _logger.LogInformation("Camera started successfully");
            OnConnectionStateChanged?.Invoke("video_started");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start camera");
            throw;
        }
    }

    /// <summary>
    /// 视频采集循环
    /// </summary>
    private void VideoCaptureLoop()
    {
        using var frame = new Mat();
        var frameInterval = TimeSpan.FromMilliseconds(33); // ~30fps

        while (_isVideoCaptureRunning)
        {
            try
            {
                bool frameRead = false;

                lock (_videoCaptureLock)
                {
                    if (_videoCapture == null || !_videoCapture.IsOpened())
                    {
                        break;
                    }

                    frameRead = _videoCapture.Read(frame);
                }

                if (!frameRead || frame.Empty())
                {
                    Thread.Sleep(10);
                    continue;
                }

                // 在采集线程中创建副本用于 UI 线程
                byte[]? imageData = null;
                int width = 0;
                int height = 0;
                int channels = 0;

                try
                {
                    width = frame.Width;
                    height = frame.Height;
                    channels = frame.Channels();

                    // 复制图像数据
                    var dataSize = width * height * channels;
                    imageData = new byte[dataSize];

                    if (channels == 1)
                    {
                        // 灰度图转 BGR
                        using var bgrFrame = new Mat();
                        Cv2.CvtColor(frame, bgrFrame, ColorConversionCodes.GRAY2BGR);
                        channels = 3;
                        imageData = new byte[width * height * channels];
                        System.Runtime.InteropServices.Marshal.Copy(bgrFrame.Data, imageData, 0, imageData.Length);
                    }
                    else
                    {
                        System.Runtime.InteropServices.Marshal.Copy(frame.Data, imageData, 0, imageData.Length);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error copying frame data");
                    Thread.Sleep(10);
                    continue;
                }

                // 触发视频数据事件
                OnVideoData?.Invoke(imageData, width, height);

                // VP8 编码并发送
                EncodeAndSendVideoFrame(imageData, width, height);

                // 在 UI 线程更新显示，使用复制的数据
                var capturedData = imageData;
                var capturedWidth = width;
                var capturedHeight = height;
                var capturedChannels = channels;

                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        if (capturedData == null || capturedWidth <= 0 || capturedHeight <= 0)
                        {
                            return;
                        }

                        // 创建 WriteableBitmap
                        var bitmap = new WriteableBitmap(
                            capturedWidth,
                            capturedHeight,
                            96, 96,
                            System.Windows.Media.PixelFormats.Bgr24,
                            null);

                        bitmap.Lock();
                        try
                        {
                            var backBuffer = bitmap.BackBuffer;
                            var stride = bitmap.BackBufferStride;
                            var sourceStride = capturedWidth * capturedChannels;

                            for (int y = 0; y < capturedHeight; y++)
                            {
                                System.Runtime.InteropServices.Marshal.Copy(
                                    capturedData,
                                    y * sourceStride,
                                    backBuffer + y * stride,
                                    Math.Min(sourceStride, stride));
                            }

                            bitmap.AddDirtyRect(new System.Windows.Int32Rect(0, 0, capturedWidth, capturedHeight));
                        }
                        finally
                        {
                            bitmap.Unlock();
                        }

                        _localVideoBitmap = bitmap;
                        OnLocalVideoFrame?.Invoke(bitmap);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error updating video frame");
                    }
                });

                // 控制帧率
                Thread.Sleep(frameInterval);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in video capture loop");
                Thread.Sleep(100);
            }
        }

        _logger.LogInformation("Video capture loop ended");
    }

    /// <summary>
    /// 停止摄像头采集
    /// </summary>
    public Task StopCameraAsync()
    {
        if (!_isVideoCaptureRunning)
        {
            return Task.CompletedTask;
        }

        _logger.LogInformation("Stopping camera...");

        _isVideoCaptureRunning = false;

        // 等待采集线程结束
        _videoCaptureThread?.Join(1000);

        lock (_videoCaptureLock)
        {
            _videoCapture?.Release();
            _videoCapture?.Dispose();
            _videoCapture = null;
        }

        _localVideoBitmap = null;

        _logger.LogInformation("Camera stopped");
        OnConnectionStateChanged?.Invoke("video_stopped");

        return Task.CompletedTask;
    }

    #endregion

    #region 音频采集

    /// <summary>
    /// 开始麦克风采集 - 异步执行，不阻塞 UI 线程
    /// </summary>
    public async Task StartMicrophoneAsync(string? deviceId = null)
    {
        if (_isAudioCaptureRunning)
        {
            _logger.LogWarning("Microphone is already running");
            return;
        }

        try
        {
            int micIndex = 0;
            if (!string.IsNullOrEmpty(deviceId) && int.TryParse(deviceId, out int index))
            {
                micIndex = index;
            }

            // 检查设备数量
            var deviceCount = WaveInEvent.DeviceCount;
            _logger.LogInformation("检测到 {DeviceCount} 个音频输入设备", deviceCount);

            if (deviceCount == 0)
            {
                _logger.LogWarning("没有可用的麦克风设备");
                return;
            }

            // 确保设备索引有效
            if (micIndex >= deviceCount)
            {
                _logger.LogWarning("设备索引 {MicIndex} 无效，使用默认设备 0", micIndex);
                micIndex = 0;
            }

            // 记录设备信息
            var deviceInfo = WaveInEvent.GetCapabilities(micIndex);
            _logger.LogInformation("启动麦克风: Index={MicIndex}, Name={DeviceName}, Channels={Channels}", 
                micIndex, deviceInfo.ProductName, deviceInfo.Channels);

            // 在后台线程初始化麦克风，避免阻塞 UI
            await Task.Run(() =>
            {
                // 确定通道数 - 使用设备支持的通道数，最多 1 个（单声道）
                int channels = Math.Min(deviceInfo.Channels, 1);
                if (channels == 0) channels = 1;

                _waveIn = new WaveInEvent
                {
                    DeviceNumber = micIndex,
                    WaveFormat = new WaveFormat(48000, 16, channels),
                    BufferMilliseconds = 50 // 增加缓冲区大小以提高稳定性
                };

                _waveIn.DataAvailable += OnAudioDataAvailable;
                _waveIn.RecordingStopped += OnRecordingStopped;

                _waveIn.StartRecording();
                _isAudioCaptureRunning = true;
            }).ConfigureAwait(false);

            _logger.LogInformation("Microphone started successfully");
            OnConnectionStateChanged?.Invoke("audio_started");
        }
        catch (NAudio.MmException mmEx)
        {
            _logger.LogWarning(mmEx, "麦克风打开失败 (MME 错误): {Result} - 可能设备被占用或不可用", mmEx.Result);
            // 不抛出异常，允许继续没有音频
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动麦克风失败");
            // 不抛出异常，允许继续没有音频
        }
    }

    /// <summary>
    /// 音频数据到达事件
    /// </summary>
    private void OnAudioDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded > 0)
        {
            var audioData = new byte[e.BytesRecorded];
            Array.Copy(e.Buffer, audioData, e.BytesRecorded);
            OnAudioData?.Invoke(audioData);
            
            // Opus 编码并发送
            EncodeAndSendAudioFrame(audioData);
        }
    }

    /// <summary>
    /// 录音停止事件
    /// </summary>
    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            _logger.LogError(e.Exception, "Recording stopped with error");
        }
    }

    /// <summary>
    /// 停止麦克风采集
    /// </summary>
    public Task StopMicrophoneAsync()
    {
        if (!_isAudioCaptureRunning)
        {
            return Task.CompletedTask;
        }

        _logger.LogInformation("Stopping microphone...");

        _isAudioCaptureRunning = false;

        if (_waveIn != null)
        {
            _waveIn.DataAvailable -= OnAudioDataAvailable;
            _waveIn.RecordingStopped -= OnRecordingStopped;
            _waveIn.StopRecording();
            _waveIn.Dispose();
            _waveIn = null;
        }

        _logger.LogInformation("Microphone stopped");
        OnConnectionStateChanged?.Invoke("audio_stopped");

        return Task.CompletedTask;
    }

    /// <summary>
    /// 开始屏幕共享
    /// </summary>
    public async Task StartScreenShareAsync()
    {
        _logger.LogInformation("开始屏幕共享");

        // 屏幕共享使用现有的视频捕获逻辑，只需要切换到屏幕捕获源
        // 这里使用 Windows Graphics Capture API 捕获屏幕
        try
        {
            // 如果摄像头正在运行，先停止
            if (_isVideoCaptureRunning)
            {
                await StopCameraAsync();
            }

            // 开始屏幕捕获（模拟实现 - 实际需要集成屏幕捕获API）
            // 目前使用摄像头作为占位符
            _logger.LogWarning("屏幕共享功能需要集成 Windows Graphics Capture API");

            OnConnectionStateChanged?.Invoke("screen_share_started");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "开始屏幕共享失败");
            throw;
        }
    }

    /// <summary>
    /// 停止屏幕共享
    /// </summary>
    public Task StopScreenShareAsync()
    {
        _logger.LogInformation("停止屏幕共享");

        OnConnectionStateChanged?.Invoke("screen_share_stopped");

        return Task.CompletedTask;
    }

    #endregion

    #region 消费者管理

    /// <summary>
    /// 添加远端消费者
    /// </summary>
    public async Task AddConsumerAsync(string consumerId, string kind, object? rtpParameters)
    {
        _logger.LogInformation("Adding consumer: {ConsumerId}, Kind: {Kind}", consumerId, kind);

        _consumers.TryAdd(consumerId, new ConsumerInfo
        {
            ConsumerId = consumerId,
            Kind = kind,
            RtpParameters = rtpParameters
        });

        // 使用 MediasoupTransport 处理 Consumer
        if (_recvTransport != null && rtpParameters != null)
        {
            try
            {
                await _recvTransport.ConsumeAsync(consumerId, kind, rtpParameters);
                _logger.LogInformation("Consumer {ConsumerId} added to recv transport", consumerId);
                
                // 对于视频 Consumer，根据远端 MimeType 设置解码器类型
                if (kind == "video" && _rtpDecoder != null)
                {
                    var remoteCodecType = ExtractCodecTypeFromRtpParameters(rtpParameters);
                    _rtpDecoder.SetConsumerVideoCodecType(consumerId, remoteCodecType);
                    _logger.LogInformation("Consumer {ConsumerId} 远端编解码器: {Codec}", consumerId, remoteCodecType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to consume {ConsumerId} on recv transport", consumerId);
            }
        }
        else
        {
            _logger.LogWarning("No recv transport available for consumer {ConsumerId}", consumerId);
        }
    }
    
    /// <summary>
    /// 从 RTP 参数中提取编解码器类型
    /// </summary>
    private VideoCodecType ExtractCodecTypeFromRtpParameters(object rtpParameters)
    {
        try
        {
            var json = JsonSerializer.Serialize(rtpParameters);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("codecs", out var codecsElement) && 
                codecsElement.GetArrayLength() > 0)
            {
                var firstCodec = codecsElement[0];
                if (firstCodec.TryGetProperty("mimeType", out var mimeTypeElement))
                {
                    var mimeType = mimeTypeElement.GetString();
                    return RtpMediaDecoder.GetCodecTypeFromMimeType(mimeType);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析 RTP 参数获取编解码器类型失败");
        }
        
        return VideoCodecType.VP8;
    }

    /// <summary>
    /// 移除远端消费者
    /// </summary>
    public Task RemoveConsumerAsync(string consumerId)
    {
        _logger.LogInformation("Removing consumer: {ConsumerId}", consumerId);

        _consumers.TryRemove(consumerId, out _);
        
        // 清理 RTP 解码器中该 Consumer 的资源
        _rtpDecoder?.RemoveConsumer(consumerId);
        
        // 清理 Transport 中该 Consumer 的资源
        _recvTransport?.RemoveConsumer(consumerId);

        return Task.CompletedTask;
    }

    #endregion

    #region Mediasoup 集成

    /// <summary>
    /// 加载设备能力
    /// </summary>
    public void LoadDevice(object routerRtpCapabilities)
    {
        _logger.LogInformation("Loading mediasoup device...");

        _device = new MediasoupDevice(_loggerFactory.CreateLogger<MediasoupDevice>());
        _device.Load(routerRtpCapabilities);

        _logger.LogInformation("Device loaded, can produce video: {CanVideo}, audio: {CanAudio}",
            _device.CanProduce("video"),
            _device.CanProduce("audio"));
    }

    /// <summary>
    /// 创建发送 Transport
    /// </summary>
    public void CreateSendTransport(string transportId, object iceParameters, object iceCandidates, object dtlsParameters)
    {
        _logger.LogInformation("Creating send transport: {TransportId}", transportId);

        // 先清理旧的 Transport（如果存在）
        if (_sendTransport != null)
        {
            _logger.LogWarning("清理旧的 send transport: {OldTransportId}", _sendTransport.TransportId);
            try
            {
                _sendTransport.Close();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "清理旧 send transport 失败");
            }
            _sendTransport = null;
        }

        var iceParams = ParseIceParameters(iceParameters);
        var iceCands = ParseIceCandidates(iceCandidates);
        var dtlsParams = ParseDtlsParameters(dtlsParameters);

        _sendTransport = new MediasoupTransport(
            _loggerFactory.CreateLogger<MediasoupTransport>(),
            transportId,
            TransportDirection.Send);

        _sendTransport.Initialize(iceParams, iceCands, dtlsParams);
        _sendTransport.OnConnectionStateChanged += (state) =>
        {
            _logger.LogDebug("Send transport state: {State}", state);
            OnConnectionStateChanged?.Invoke($"send_transport_{state}");
        };

        // 订阅关键帧请求事件 - 当服务器发送 PLI/FIR 请求时，强制编码器生成关键帧
        _sendTransport.OnKeyFrameRequested += () =>
        {
            _logger.LogInformation("Keyframe requested by server, forcing encoder to generate keyframe");
            _videoEncoder?.ForceKeyFrame();
        };

        // 设置初始编解码器类型
        _sendTransport.SetVideoCodecType(_currentVideoCodec);
        
        // 设置视频比特率
        var quality = VideoQuality ?? VideoQualitySettings.GetPreset(VideoQualityPreset.High);
        _sendTransport.VideoBitrate = quality.Bitrate;

        _logger.LogInformation("Send transport created: {TransportId}, codec={Codec}", transportId, _currentVideoCodec);
    }

    /// <summary>
    /// 创建接收 Transport
    /// </summary>
    public void CreateRecvTransport(string transportId, object iceParameters, object iceCandidates, object dtlsParameters)
    {
        _logger.LogInformation("Creating recv transport: {TransportId}", transportId);

        // 先清理旧的 Transport（如果存在）
        if (_recvTransport != null)
        {
            _logger.LogWarning("清理旧的 recv transport: {OldTransportId}", _recvTransport.TransportId);
            try
            {
                _recvTransport.Close();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "清理旧 recv transport 失败");
            }
            _recvTransport = null;
        }
        
        // 清理旧的 RTP 解码器
        if (_rtpDecoder != null)
        {
            _rtpDecoder.OnDecodedVideoFrame -= HandleRemoteVideoFrame;
            _rtpDecoder.OnDecodedAudioSamples -= HandleDecodedAudioSamples;
            _rtpDecoder.Dispose();
            _rtpDecoder = null;
        }

        var iceParams = ParseIceParameters(iceParameters);
        var iceCands = ParseIceCandidates(iceCandidates);
        var dtlsParams = ParseDtlsParameters(dtlsParameters);

        _recvTransport = new MediasoupTransport(
            _loggerFactory.CreateLogger<MediasoupTransport>(),
            transportId,
            TransportDirection.Recv);

        _recvTransport.Initialize(iceParams, iceCands, dtlsParams);
        _recvTransport.OnConnectionStateChanged += (state) =>
        {
            _logger.LogDebug("Recv transport state: {State}", state);
            OnConnectionStateChanged?.Invoke($"recv_transport_{state}");

            // 当 DTLS 连接完成时，触发事件
            if (state == "connected")
            {
                _isRecvTransportDtlsConnected = true;
                _logger.LogInformation("Recv transport DTLS connected, triggering OnRecvTransportDtlsConnected event");
                OnRecvTransportDtlsConnected?.Invoke();
            }
        };

        // 初始化 RTP 解码器
        _rtpDecoder = new RtpMediaDecoder(_loggerFactory);
        
        // 订阅解码后的视频帧事件
        _rtpDecoder.OnDecodedVideoFrame += HandleRemoteVideoFrame;
        
        // 订阅解码后的音频采样事件
        _rtpDecoder.OnDecodedAudioSamples += HandleDecodedAudioSamples;

        // 订阅 RTP 包事件，转发到解码器
        _recvTransport.OnVideoRtpPacketReceived += (consumerId, rtpPacket) =>
        {
            _rtpDecoder?.ProcessVideoRtpPacket(consumerId, rtpPacket);
        };
        
        _recvTransport.OnAudioRtpPacketReceived += (consumerId, rtpPacket) =>
        {
            _rtpDecoder?.ProcessAudioRtpPacket(consumerId, rtpPacket);
        };

        // 订阅远端视频帧事件 (备用)
        _recvTransport.OnRemoteVideoFrame += HandleRemoteVideoFrame;

        // 订阅远端音频事件 (备用)
        _recvTransport.OnRemoteAudioData += HandleRemoteAudioData;

        // 初始化音频播放器
        InitializeAudioPlayback();

        _logger.LogInformation("Recv transport created with RTP decoder: {TransportId}", transportId);
    }

    /// <summary>
    /// 设置 recv transport 的 SDP 协商完成回调
    /// 当 SDP 协商完成后，调用 ConnectWebRtcTransport
    /// </summary>
    public void SetupRecvTransportNegotiationCallback(Func<string, object, Task> connectCallback)
    {
        if (_recvTransport == null)
        {
            _logger.LogWarning("Recv transport not created, cannot setup negotiation callback");
            return;
        }

        // 使用本地变量防止重复调用 ConnectWebRtcTransport
        bool hasConnected = false;

        _recvTransport.OnNegotiationCompleted += async () =>
        {
            // 只在第一次 SDP 协商完成时调用 ConnectWebRtcTransport
            if (hasConnected)
            {
                _logger.LogDebug("Recv transport already connected, skipping ConnectWebRtcTransport");
                return;
            }

            try
            {
                hasConnected = true;
                var dtlsParameters = _recvTransport.GetLocalDtlsParameters();
                _logger.LogInformation("SDP negotiation completed, now connecting recv transport with DTLS parameters");

                // 调用回调函数通知服务器
                await connectCallback(_recvTransport.TransportId, dtlsParameters);

                // 标记为已连接
                _recvTransport.SetConnected();
                _logger.LogInformation("Recv transport connected after SDP negotiation");
            }
            catch (Exception ex)
            {
                hasConnected = false; // 失败时重置，允许重试
                _logger.LogError(ex, "Failed to connect recv transport after negotiation");
            }
        };

        _logger.LogDebug("Recv transport negotiation callback setup complete");
    }

    /// <summary>
    /// 连接发送 Transport - DTLS 握手
    /// </summary>
    public async Task ConnectSendTransportAsync(Func<string, object, Task> connectCallback)
    {
        if (_sendTransport == null)
        {
            _logger.LogWarning("Send transport not created");
            return;
        }

        try
        {
            // 首先启动 Send Transport 的 SDP 协商和 DTLS
            await _sendTransport.StartSendTransportAsync();

            var dtlsParameters = _sendTransport.GetLocalDtlsParameters();
            _logger.LogInformation("Connecting send transport with DTLS parameters");

            // 调用回调函数通知服务器
            await connectCallback(_sendTransport.TransportId, dtlsParameters);

            // 标记为已连接
            _sendTransport.SetConnected();
            _logger.LogInformation("Send transport connected");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect send transport");
            throw;
        }
    }

    /// <summary>
    /// 连接接收 Transport - DTLS 握手
    /// </summary>
    public async Task ConnectRecvTransportAsync(Func<string, object, Task> connectCallback)
    {
        if (_recvTransport == null)
        {
            _logger.LogWarning("Recv transport not created");
            return;
        }

        try
        {
            var dtlsParameters = _recvTransport.GetLocalDtlsParameters();
            _logger.LogInformation("Connecting recv transport with DTLS parameters");

            // 调用回调函数通知服务器
            await connectCallback(_recvTransport.TransportId, dtlsParameters);

            // 标记为已连接
            _recvTransport.SetConnected();
            _logger.LogInformation("Recv transport connected");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect recv transport");
            throw;
        }
    }

    #endregion

    #region 远端媒体处理

    /// <summary>
    /// 初始化音频播放
    /// </summary>
    private void InitializeAudioPlayback()
    {
        lock (_audioPlaybackLock)
        {
            if (_waveOut != null) return;

            try
            {
                // 创建音频缓冲区 - 48kHz, 16-bit, 立体声
                _audioBuffer = new BufferedWaveProvider(new WaveFormat(48000, 16, 2))
                {
                    BufferDuration = TimeSpan.FromSeconds(1),
                    DiscardOnBufferOverflow = true
                };

                _waveOut = new WaveOutEvent
                {
                    DesiredLatency = 100
                };
                _waveOut.Init(_audioBuffer);
                _waveOut.Play();

                _logger.LogInformation("Audio playback initialized");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize audio playback");
            }
        }
    }

    /// <summary>
    /// 处理远端视频帧
    /// </summary>
    private void HandleRemoteVideoFrame(string consumerId, byte[] frameData, int width, int height)
    {
        try
        {
            // 在 UI 线程更新
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    if (frameData == null || width <= 0 || height <= 0)
                        return;

                    // 创建或获取 WriteableBitmap
                    if (!_remoteVideoBitmaps.TryGetValue(consumerId, out var bitmap) ||
                        bitmap.PixelWidth != width || bitmap.PixelHeight != height)
                    {
                        bitmap = new WriteableBitmap(
                            width, height, 96, 96,
                            System.Windows.Media.PixelFormats.Bgr24, null);
                        _remoteVideoBitmaps[consumerId] = bitmap;
                    }

                    // 更新位图数据
                    bitmap.Lock();
                    try
                    {
                        var backBuffer = bitmap.BackBuffer;
                        var stride = bitmap.BackBufferStride;
                        var sourceStride = width * 3; // BGR24

                        for (int y = 0; y < height; y++)
                        {
                            System.Runtime.InteropServices.Marshal.Copy(
                                frameData, y * sourceStride,
                                backBuffer + y * stride,
                                Math.Min(sourceStride, stride));
                        }

                        bitmap.AddDirtyRect(new System.Windows.Int32Rect(0, 0, width, height));
                    }
                    finally
                    {
                        bitmap.Unlock();
                    }

                    OnRemoteVideoFrame?.Invoke(consumerId, bitmap);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating remote video frame");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling remote video frame");
        }
    }

    /// <summary>
    /// 处理远端音频数据
    /// </summary>
    private void HandleRemoteAudioData(string consumerId, byte[] audioData)
    {
        try
        {
            lock (_audioPlaybackLock)
            {
                if (_audioBuffer != null && audioData != null && audioData.Length > 0)
                {
                    _audioBuffer.AddSamples(audioData, 0, audioData.Length);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling remote audio data");
        }
    }

    /// <summary>
    /// 处理解码后的音频采样 (short[] PCM)
    /// </summary>
    private void HandleDecodedAudioSamples(string consumerId, short[] samples)
    {
        try
        {
            lock (_audioPlaybackLock)
            {
                if (_audioBuffer != null && samples != null && samples.Length > 0)
                {
                    // 将 short[] 转换为 byte[]
                    var bytes = new byte[samples.Length * 2];
                    Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
                    _audioBuffer.AddSamples(bytes, 0, bytes.Length);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling decoded audio samples");
        }
    }

    /// <summary>
    /// 停止音频播放
    /// </summary>
    private void StopAudioPlayback()
    {
        lock (_audioPlaybackLock)
        {
            if (_waveOut != null)
            {
                try
                {
                    _waveOut.Stop();
                    _waveOut.Dispose();
                }
                catch { }
                _waveOut = null;
            }
            _audioBuffer = null;
        }
    }

    #endregion

    #region 编码和发送

    /// <summary>
    /// 初始化视频编码器 - 根据当前编解码器类型创建对应编码器
    /// </summary>
    private bool EnsureVideoEncoderInitialized(int width, int height)
    {
        if (_videoEncoder != null)
            return true;

        try
        {
            // 应用视频质量配置
            var quality = VideoQuality ?? VideoQualitySettings.GetPreset(VideoQualityPreset.High);
            
            // 使用配置的分辨率，如果配置中没有则使用传入的值
            var targetWidth = quality.Width > 0 ? quality.Width : width;
            var targetHeight = quality.Height > 0 ? quality.Height : height;
            
            // 根据当前编解码器类型创建对应的编码器
            _videoEncoder = CreateVideoEncoder(_currentVideoCodec, targetWidth, targetHeight);
            
            if (_videoEncoder == null)
            {
                _logger.LogWarning("创建视频编码器失败: {Codec}", _currentVideoCodec);
                return false;
            }
            
            // 设置编码参数
            _videoEncoder.Bitrate = quality.Bitrate;
            _videoEncoder.FrameRate = quality.FrameRate;
            _videoEncoder.KeyFrameInterval = quality.KeyFrameInterval;
            
            _videoEncoder.OnFrameEncoded += (data, isKeyFrame) =>
            {
                OnEncodedVideoFrame?.Invoke(data, isKeyFrame);
                // 通过 SendTransport 发送 RTP 包
                _sendTransport?.SendVideoRtpPacketAsync(data, isKeyFrame);
            };
            
            if (!_videoEncoder.Initialize())
            {
                _logger.LogWarning("初始化视频编码器失败: {Codec}", _currentVideoCodec);
                _videoEncoder = null;
                return false;
            }
            
            _logger.LogInformation("{Codec} 编码器已初始化: {Width}x{Height} @ {Fps}fps, {Bitrate}bps (Quality: {Quality})",
                _currentVideoCodec, targetWidth, targetHeight, quality.FrameRate, quality.Bitrate, quality.DisplayName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建视频编码器异常: {Codec}", _currentVideoCodec);
            return false;
        }
    }
    
    /// <summary>
    /// 根据编解码器类型创建对应的编码器
    /// </summary>
    private IVideoEncoder? CreateVideoEncoder(VideoCodecType codecType, int width, int height)
    {
        return codecType switch
        {
            VideoCodecType.VP8 => new Vp8Encoder(_loggerFactory.CreateLogger<Vp8Encoder>(), width, height),
            VideoCodecType.VP9 => new Vp9Encoder(_loggerFactory.CreateLogger<Vp9Encoder>(), width, height),
            VideoCodecType.H264 => new H264Encoder(_loggerFactory.CreateLogger<H264Encoder>(), width, height),
            _ => new Vp8Encoder(_loggerFactory.CreateLogger<Vp8Encoder>(), width, height)
        };
    }

    /// <summary>
    /// 初始化音频编码器
    /// </summary>
    private bool EnsureAudioEncoderInitialized()
    {
        if (_audioPacketizer != null)
            return true;

        try
        {
            _audioPacketizer = new AudioFramePacketizer(_loggerFactory.CreateLogger<AudioFramePacketizer>(), 48000, 1);
            _audioPacketizer.OnFrameReady += (data) =>
            {
                OnEncodedAudioFrame?.Invoke(data);
                // 通过 SendTransport 发送 RTP 包
                _sendTransport?.SendAudioRtpPacketAsync(data);
            };
            
            if (!_audioPacketizer.Initialize())
            {
                _logger.LogWarning("Failed to initialize Opus encoder");
                _audioPacketizer = null;
                return false;
            }
            
            _logger.LogInformation("Opus encoder initialized");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Opus encoder");
            return false;
        }
    }

    /// <summary>
    /// 编码并发送视频帧
    /// </summary>
    private void EncodeAndSendVideoFrame(byte[] bgrData, int width, int height)
    {
        try
        {
            // 确保编码器已初始化
            if (!EnsureVideoEncoderInitialized(width, height))
                return;

            // 编码帧
            _videoEncoder?.Encode(bgrData, width, height);
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Error encoding video frame");
        }
    }

    /// <summary>
    /// 编码并发送音频帧
    /// </summary>
    private void EncodeAndSendAudioFrame(byte[] pcmData)
    {
        try
        {
            // 确保编码器已初始化
            if (!EnsureAudioEncoderInitialized())
                return;

            // 添加音频数据到打包器
            _audioPacketizer?.AddBytes(pcmData);
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Error encoding audio frame");
        }
    }

    /// <summary>
    /// 释放编码器资源
    /// </summary>
    private void DisposeEncoders()
    {
        _videoEncoder?.Dispose();
        _videoEncoder = null;
        
        _audioPacketizer?.Dispose();
        _audioPacketizer = null;
    }

    private static IceParameters ParseIceParameters(object iceParameters)
    {
        var json = JsonSerializer.Serialize(iceParameters);
        return JsonSerializer.Deserialize<IceParameters>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new IceParameters();
    }

    private static List<IceCandidate> ParseIceCandidates(object iceCandidates)
    {
        var json = JsonSerializer.Serialize(iceCandidates);
        return JsonSerializer.Deserialize<List<IceCandidate>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<IceCandidate>();
    }

    private static DtlsParameters ParseDtlsParameters(object dtlsParameters)
    {
        var json = JsonSerializer.Serialize(dtlsParameters);
        return JsonSerializer.Deserialize<DtlsParameters>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new DtlsParameters();
    }

    #endregion

    #region 生命周期

    /// <summary>
    /// 关闭所有连接
    /// </summary>
    public async Task CloseAsync()
    {
        _logger.LogInformation("Closing WebRTC service...");

        await StopCameraAsync();
        await StopMicrophoneAsync();

        // 停止音频播放
        StopAudioPlayback();

        // 释放编码器
        DisposeEncoders();

        // 关闭 Transport
        _sendTransport?.Close();
        _sendTransport = null;

        _recvTransport?.Close();
        _recvTransport = null;
        
        // 释放解码器
        _rtpDecoder?.Dispose();
        _rtpDecoder = null;

        _device = null;
        _consumers.Clear();
        _remoteVideoBitmaps.Clear();

        _logger.LogInformation("WebRTC service closed");
    }

    /// <summary>
    /// 释放资源 - 使用同步方式避免死锁
    /// </summary>
    public void Dispose()
    {
        try
        {
            // 停止视频采集
            _isVideoCaptureRunning = false;
            _videoCaptureThread?.Join(500); // 限时等待

            lock (_videoCaptureLock)
            {
                _videoCapture?.Release();
                _videoCapture?.Dispose();
                _videoCapture = null;
            }

            // 停止音频采集
            _isAudioCaptureRunning = false;
            if (_waveIn != null)
            {
                _waveIn.DataAvailable -= OnAudioDataAvailable;
                _waveIn.RecordingStopped -= OnRecordingStopped;
                try
                {
                    _waveIn.StopRecording();
                }
                catch { }
                _waveIn.Dispose();
                _waveIn = null;
            }

            // 停止音频播放
            StopAudioPlayback();

            // 释放编码器
            DisposeEncoders();

            // 关闭 Transport
            _sendTransport?.Dispose();
            _sendTransport = null;

            _recvTransport?.Dispose();
            _recvTransport = null;
            
            // 释放解码器
            _rtpDecoder?.Dispose();
            _rtpDecoder = null;

            _device = null;
            _consumers.Clear();
            _remoteVideoBitmaps.Clear();
            _localVideoBitmap = null;

            _logger.LogInformation("WebRTC service disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing WebRTC service");
        }

        GC.SuppressFinalize(this);
    }

    #endregion
}

/// <summary>
/// 消费者信息
/// </summary>
internal class ConsumerInfo
{
    public string ConsumerId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public object? RtpParameters { get; set; }
}
