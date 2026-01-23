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
    
    // 视频/音频编码器
    private Vp8Encoder? _videoEncoder;
    private AudioFramePacketizer? _audioPacketizer;
<<<<<<< HEAD
=======
    
    // 当前视频编解码器类型
    private VideoCodecType _currentVideoCodec = VideoCodecType.VP9;
>>>>>>> pro

    // 视频采集
    private VideoCapture? _videoCapture;
    private Thread? _videoCaptureThread;
    private volatile bool _isVideoCaptureRunning;
    private readonly object _videoCaptureLock = new();
    
    // 屏幕共享
    private ScreenCapture? _screenCapture;
    private volatile bool _isScreenSharing;
    
    // 录制相关
    private volatile bool _isRecording;
    private string? _recordingOutputPath;

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
    public bool IsProducingVideo => _isVideoCaptureRunning || _isScreenSharing;

    /// <summary>
    /// 是否正在生产音频
    /// </summary>
    public bool IsProducingAudio => _isAudioCaptureRunning;
    
    /// <summary>
    /// 是否正在屏幕共享
    /// </summary>
    public bool IsScreenSharing => _isScreenSharing;
    
    /// <summary>
    /// 是否正在录制
    /// </summary>
    public bool IsRecording => _isRecording;
    
    /// <summary>
    /// 屏幕共享帧更新事件
    /// </summary>
    public event Action<WriteableBitmap>? OnScreenShareFrame;

    /// <summary>
    /// 屏幕共享设置
    /// </summary>
    public ScreenShareSettings? ScreenShareSettings { get; set; }
    
    /// <summary>
    /// 屏幕共享是否显示鼠标指针
    /// </summary>
    public bool ScreenShareShowCursor { get; set; } = true;

    /// <summary>
    /// 当前视频质量配置
    /// </summary>
    public VideoQualitySettings? VideoQuality { get; set; }

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
    /// 开始摄像头采集
    /// </summary>
    public Task StartCameraAsync(string? deviceId = null)
    {
        if (_isVideoCaptureRunning)
        {
            _logger.LogWarning("Camera is already running");
            return Task.CompletedTask;
        }

        try
        {
            int cameraIndex = 0;
            if (!string.IsNullOrEmpty(deviceId) && int.TryParse(deviceId, out int index))
            {
                cameraIndex = index;
            }

            _logger.LogInformation("Starting camera with index {CameraIndex}", cameraIndex);

            lock (_videoCaptureLock)
            {
                _videoCapture = new VideoCapture(cameraIndex, VideoCaptureAPIs.DSHOW);

                if (!_videoCapture.IsOpened())
                {
<<<<<<< HEAD
                    throw new Exception($"Failed to open camera {cameraIndex}");
=======
                    // 先确保释放已有资源
                    if (_videoCapture != null)
                    {
                        try
                        {
                            _videoCapture.Release();
                            _videoCapture.Dispose();
                        }
                        catch { }
                        _videoCapture = null;
                    }

                    // 尝试打开摄像头，最多重试 3 次
                    Exception? lastException = null;
                    for (int retry = 0; retry < 3; retry++)
                    {
                        try
                        {
                            if (retry > 0)
                            {
                                _logger.LogInformation("Retrying camera open, attempt {Attempt}", retry + 1);
                                Thread.Sleep(500); // 等待摄像头资源释放
                            }

                            _videoCapture = new VideoCapture(cameraIndex, VideoCaptureAPIs.DSHOW);

                            if (_videoCapture.IsOpened())
                            {
                                // 设置视频参数
                                _videoCapture.Set(VideoCaptureProperties.FrameWidth, 640);
                                _videoCapture.Set(VideoCaptureProperties.FrameHeight, 480);
                                _videoCapture.Set(VideoCaptureProperties.Fps, 30);

                                _isVideoCaptureRunning = true;
                                _logger.LogInformation("Camera {Index} opened successfully", cameraIndex);
                                return; // 成功，退出循环
                            }
                            else
                            {
                                _videoCapture.Release();
                                _videoCapture.Dispose();
                                _videoCapture = null;
                                lastException = new Exception($"Camera {cameraIndex} exists but failed to open");
                            }
                        }
                        catch (Exception ex)
                        {
                            lastException = ex;
                            _logger.LogWarning(ex, "Camera open attempt {Attempt} failed", retry + 1);
                            if (_videoCapture != null)
                            {
                                try { _videoCapture.Release(); _videoCapture.Dispose(); } catch { }
                                _videoCapture = null;
                            }
                        }
                    }

                    // 所有重试都失败
                    throw new Exception(
                        $"无法打开摄像头 {cameraIndex}。\n" +
                        $"可能原因：\n" +
                        $"1. 摄像头被其他应用程序占用\n" +
                        $"2. 摄像头未正确连接\n" +
                        $"3. 需要授予摄像头访问权限\n" +
                        $"请关闭其他使用摄像头的应用后重试。",
                        lastException);
>>>>>>> pro
                }

                // 设置视频参数
                _videoCapture.Set(VideoCaptureProperties.FrameWidth, 640);
                _videoCapture.Set(VideoCaptureProperties.FrameHeight, 480);
                _videoCapture.Set(VideoCaptureProperties.Fps, 30);

                _isVideoCaptureRunning = true;
            }

            // 启动视频采集线程
            _videoCaptureThread = new Thread(VideoCaptureLoop)
            {
                IsBackground = true,
                Name = "VideoCaptureThread"
            };
            _videoCaptureThread.Start();

            _logger.LogInformation("Camera started successfully");
            OnConnectionStateChanged?.Invoke("video_started");

            return Task.CompletedTask;
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
    /// 开始麦克风采集
    /// </summary>
    public Task StartMicrophoneAsync(string? deviceId = null)
    {
        if (_isAudioCaptureRunning)
        {
            _logger.LogWarning("Microphone is already running");
            return Task.CompletedTask;
        }

        try
        {
            int micIndex = 0;
            if (!string.IsNullOrEmpty(deviceId) && int.TryParse(deviceId, out int index))
            {
                micIndex = index;
            }

            _logger.LogInformation("Starting microphone with index {MicIndex}", micIndex);

            _waveIn = new WaveInEvent
            {
                DeviceNumber = micIndex,
                WaveFormat = new WaveFormat(48000, 16, 1), // 48kHz, 16-bit, mono
                BufferMilliseconds = 20
            };

            _waveIn.DataAvailable += OnAudioDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;

            _waveIn.StartRecording();
            _isAudioCaptureRunning = true;

            _logger.LogInformation("Microphone started successfully");
            OnConnectionStateChanged?.Invoke("audio_started");

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start microphone");
            throw;
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

<<<<<<< HEAD
=======
    /// <summary>
    /// 开始屏幕共享
    /// </summary>
    public async Task StartScreenShareAsync()
    {
        _logger.LogInformation("开始屏幕共享");

        if (_isScreenSharing)
        {
            _logger.LogWarning("屏幕共享已在运行");
            return;
        }

        try
        {
            // 如果摄像头正在运行，先停止
            if (_isVideoCaptureRunning)
            {
                _logger.LogInformation("停止摄像头以开始屏幕共享...");
                await StopCameraAsync();
            }

            // 创建屏幕捕获实例
            _screenCapture = new ScreenCapture(_loggerFactory.CreateLogger<ScreenCapture>());
            
            // 应用屏幕共享设置
            var settings = ScreenShareSettings ?? Models.ScreenShareSettings.GetPreset(ScreenShareQualityPreset.Standard);
            
            // 设置分辨率 - 如果是 0 则使用原始屏幕分辨率
            int targetWidth = settings.Width > 0 ? settings.Width : (int)SystemParameters.PrimaryScreenWidth;
            int targetHeight = settings.Height > 0 ? settings.Height : (int)SystemParameters.PrimaryScreenHeight;
            _screenCapture.SetTargetResolution(targetWidth, targetHeight);
            
            // 设置帧率
            _screenCapture.SetTargetFps(settings.FrameRate > 0 ? settings.FrameRate : 15);
            
            // 设置是否显示鼠标指针
            _screenCapture.SetDrawCursor(ScreenShareShowCursor);
            
            _logger.LogInformation("屏幕共享设置: {Width}x{Height} @ {Fps}fps, 显示鼠标={ShowCursor}",
                targetWidth, targetHeight, settings.FrameRate, ScreenShareShowCursor);
            
            // 订阅捕获帧事件 - 编码并发送
            _screenCapture.OnFrameCaptured += OnScreenFrameCaptured;
            
            // 订阅 UI 预览事件
            _screenCapture.OnBitmapCaptured += OnScreenBitmapCaptured;
            
            // 开始捕获
            _screenCapture.Start();
            _isScreenSharing = true;

            _logger.LogInformation("屏幕共享已启动");
            OnConnectionStateChanged?.Invoke("screen_share_started");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "开始屏幕共享失败");
            _isScreenSharing = false;
            _screenCapture?.Dispose();
            _screenCapture = null;
            throw;
        }
    }
    
    /// <summary>
    /// 屏幕捕获帧回调 - 编码并发送
    /// </summary>
    private void OnScreenFrameCaptured(byte[] imageData, int width, int height)
    {
        try
        {
            // 触发视频数据事件
            OnVideoData?.Invoke(imageData, width, height);
            
            // 编码并发送视频帧
            EncodeAndSendVideoFrame(imageData, width, height);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "屏幕捕获帧处理失败");
        }
    }
    
    /// <summary>
    /// 屏幕捕获位图回调 - 本地预览
    /// </summary>
    private void OnScreenBitmapCaptured(WriteableBitmap bitmap)
    {
        try
        {
            // 触发屏幕共享预览事件
            OnScreenShareFrame?.Invoke(bitmap);
            
            // 同时触发本地视频帧事件（让共享内容显示在本地视频区域）
            OnLocalVideoFrame?.Invoke(bitmap);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "屏幕位图处理失败");
        }
    }

    /// <summary>
    /// 停止屏幕共享
    /// </summary>
    public Task StopScreenShareAsync()
    {
        _logger.LogInformation("停止屏幕共享");

        if (!_isScreenSharing)
        {
            return Task.CompletedTask;
        }

        try
        {
            if (_screenCapture != null)
            {
                // 取消订阅事件
                _screenCapture.OnFrameCaptured -= OnScreenFrameCaptured;
                _screenCapture.OnBitmapCaptured -= OnScreenBitmapCaptured;
                
                // 停止并释放
                _screenCapture.Stop();
                _screenCapture.Dispose();
                _screenCapture = null;
            }
            
            _isScreenSharing = false;
            
            _logger.LogInformation("屏幕共享已停止");
            OnConnectionStateChanged?.Invoke("screen_share_stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止屏幕共享失败");
        }

        return Task.CompletedTask;
    }

>>>>>>> pro
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
    /// 移除远端消费者
    /// </summary>
    public Task RemoveConsumerAsync(string consumerId)
    {
        _logger.LogInformation("Removing consumer: {ConsumerId}", consumerId);

        _consumers.TryRemove(consumerId, out _);

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
            _logger.LogInformation("Keyframe requested by server, forcing VP8 encoder to generate keyframe");
            _videoEncoder?.ForceKeyFrame();
        };

        _logger.LogInformation("Send transport created: {TransportId}", transportId);
    }

    /// <summary>
    /// 创建接收 Transport
    /// </summary>
    public void CreateRecvTransport(string transportId, object iceParameters, object iceCandidates, object dtlsParameters)
    {
        _logger.LogInformation("Creating recv transport: {TransportId}", transportId);

<<<<<<< HEAD
=======
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
        
        // 重置 DTLS 连接状态 - 这是关键！新的 transport 需要重新建立 DTLS
        _isRecvTransportDtlsConnected = false;
        
        // 清理旧的 RTP 解码器
        if (_rtpDecoder != null)
        {
            _rtpDecoder.OnDecodedVideoFrame -= HandleRemoteVideoFrame;
            _rtpDecoder.OnDecodedAudioSamples -= HandleDecodedAudioSamples;
            _rtpDecoder.Dispose();
            _rtpDecoder = null;
        }

>>>>>>> pro
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
    /// 初始化视频编码器
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
            
            _videoEncoder = new Vp8Encoder(_loggerFactory.CreateLogger<Vp8Encoder>(), targetWidth, targetHeight)
            {
                Bitrate = quality.Bitrate,
                FrameRate = quality.FrameRate,
                CpuUsed = quality.CpuUsed,
                KeyFrameInterval = quality.KeyFrameInterval
            };
            
            _videoEncoder.OnFrameEncoded += (data, isKeyFrame) =>
            {
                OnEncodedVideoFrame?.Invoke(data, isKeyFrame);
                // 通过 SendTransport 发送 RTP 包
                _sendTransport?.SendVideoRtpPacketAsync(data, isKeyFrame);
            };
            
            if (!_videoEncoder.Initialize())
            {
                _logger.LogWarning("Failed to initialize VP8 encoder");
                _videoEncoder = null;
                return false;
            }
            
            _logger.LogInformation("VP8 encoder initialized: {Width}x{Height} @ {Fps}fps, {Bitrate}bps (Quality: {Quality})",
                targetWidth, targetHeight, quality.FrameRate, quality.Bitrate, quality.DisplayName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating VP8 encoder");
            return false;
        }
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
        await StopScreenShareAsync();

        // 停止音频播放
        StopAudioPlayback();

        // 释放编码器
        DisposeEncoders();

        // 关闭 Transport
        _sendTransport?.Close();
        _sendTransport = null;

        _recvTransport?.Close();
        _recvTransport = null;
        
        // 重置 DTLS 连接状态
        _isRecvTransportDtlsConnected = false;
        
        // 释放解码器
        _rtpDecoder?.Dispose();
        _rtpDecoder = null;

        _device = null;
        _consumers.Clear();
        _remoteVideoBitmaps.Clear();

        _logger.LogInformation("WebRTC service closed");
    }
    
    /// <summary>
    /// 开始录制 - 录制本地视频和音频
    /// </summary>
    /// <param name="outputPath">输出文件路径</param>
    public Task StartRecordingAsync(string outputPath)
    {
        if (_isRecording)
        {
            _logger.LogWarning("已在录制中");
            return Task.CompletedTask;
        }
        
        try
        {
            _recordingOutputPath = outputPath;
            _isRecording = true;
            
            // TODO: 实现实际录制逻辑
            // 可以使用 FFmpeg 或 OpenCV 将视频帧写入文件
            // 音频可以使用 NAudio 录制
            
            _logger.LogInformation("开始录制: {Path}", outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "开始录制失败");
            _isRecording = false;
            throw;
        }
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// 停止录制
    /// </summary>
    public Task StopRecordingAsync()
    {
        if (!_isRecording)
        {
            _logger.LogWarning("未在录制中");
            return Task.CompletedTask;
        }
        
        try
        {
            _isRecording = false;
            
            // TODO: 停止录制并保存文件
            
            _logger.LogInformation("停止录制: {Path}", _recordingOutputPath);
            _recordingOutputPath = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止录制失败");
            throw;
        }
        
        return Task.CompletedTask;
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

            // 停止屏幕共享
            _isScreenSharing = false;
            if (_screenCapture != null)
            {
                try
                {
                    _screenCapture.OnFrameCaptured -= OnScreenFrameCaptured;
                    _screenCapture.OnBitmapCaptured -= OnScreenBitmapCaptured;
                    _screenCapture.Stop();
                    _screenCapture.Dispose();
                }
                catch { }
                _screenCapture = null;
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
            
            // 重置 DTLS 连接状态
            _isRecvTransportDtlsConnected = false;
            
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
