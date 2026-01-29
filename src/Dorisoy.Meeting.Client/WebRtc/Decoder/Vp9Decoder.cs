using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;

namespace Dorisoy.Meeting.Client.WebRtc.Decoder;

/// <summary>
/// VP9 视频解码器 - 使用 FFmpeg 解码 VP9 帧
/// 将从 mediasoup 接收的 VP9 编码数据解码为 BGR24 图像用于显示
/// </summary>
public unsafe class Vp9Decoder : IVideoDecoder
{
    private readonly ILogger _logger;
    private readonly object _lock = new();  // 线程安全锁
    private AVCodecContext* _codecContext;
    private AVFrame* _frame;
    private AVFrame* _rgbFrame;
    private SwsContext* _swsContext;
    private byte* _rgbBuffer;
    private int _rgbBufferSize;
    private bool _initialized;
    private volatile bool _disposed;
    
    private int _lastWidth;
    private int _lastHeight;
    
    // 解码统计 - 用于检测需要请求关键帧的情况
    private int _consecutiveDecodeFailures;
    private const int MAX_CONSECUTIVE_FAILURES = 10;  // 增加到 10 次，减少关键帧请求频率
    
    // 关键帧请求节流 - 避免短时间内重复请求
    private DateTime _lastKeyFrameRequestTime = DateTime.MinValue;
    private static readonly TimeSpan KeyFrameRequestCooldown = TimeSpan.FromSeconds(2);

    /// <summary>
    /// 解码后的视频帧事件 (BGR24 数据, 宽度, 高度)
    /// </summary>
    public event Action<byte[], int, int>? OnFrameDecoded;
    
    /// <summary>
    /// 请求关键帧事件 - 当解码失败多次时触发
    /// </summary>
    public event Action? OnKeyFrameRequested;

    public Vp9Decoder(ILogger logger)
    {
        _logger = logger;
        Initialize();
    }

    /// <summary>
    /// 初始化 FFmpeg 解码器
    /// </summary>
    private void Initialize()
    {
        try
        {
            // 确保 FFmpeg 已初始化
            if (!FFmpegConfig.IsInitialized)
            {
                _logger.LogWarning("FFmpeg not initialized, attempting initialization...");
                if (!FFmpegConfig.Initialize(_logger))
                {
                    _logger.LogError("Failed to initialize FFmpeg");
                    return;
                }
            }
            
            // 查找 VP9 解码器
            var codec = ffmpeg.avcodec_find_decoder(AVCodecID.AV_CODEC_ID_VP9);
            if (codec == null)
            {
                _logger.LogError("VP9 decoder not found. Make sure FFmpeg is built with libvpx support.");
                return;
            }

            // 分配编解码器上下文
            _codecContext = ffmpeg.avcodec_alloc_context3(codec);
            if (_codecContext == null)
            {
                _logger.LogError("Failed to allocate codec context");
                return;
            }

            // 打开编解码器 - 启用多线程解码
            _codecContext->thread_count = Math.Min(4, Environment.ProcessorCount);  // 限制线程数
            _codecContext->thread_type = ffmpeg.FF_THREAD_SLICE;  // 仅使用切片多线程，减少延迟
            
            // 启用低延迟解码模式
            _codecContext->flags |= ffmpeg.AV_CODEC_FLAG_LOW_DELAY;
            _codecContext->flags2 |= ffmpeg.AV_CODEC_FLAG2_FAST;  // 启用快速解码模式
            
            var result = ffmpeg.avcodec_open2(_codecContext, codec, null);
            if (result < 0)
            {
                _logger.LogError("Failed to open codec: {Error}", GetErrorMessage(result));
                return;
            }

            // 分配帧
            _frame = ffmpeg.av_frame_alloc();
            _rgbFrame = ffmpeg.av_frame_alloc();

            if (_frame == null || _rgbFrame == null)
            {
                _logger.LogError("Failed to allocate frames");
                return;
            }

            _initialized = true;
            _logger.LogInformation("VP9 decoder initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize VP9 decoder");
        }
    }

    /// <summary>
    /// 解码 VP9 帧
    /// </summary>
    /// <param name="frameData">VP9 编码数据</param>
    /// <returns>是否解码成功</returns>
    public bool Decode(byte[] frameData)
    {
        if (!_initialized || _disposed)
        {
            return false;
        }

        lock (_lock)
        {
            if (!_initialized || _disposed)
            {
                return false;
            }

            try
            {
                fixed (byte* pData = frameData)
                {
                    var packet = ffmpeg.av_packet_alloc();
                    try
                    {
                        packet->data = pData;
                        packet->size = frameData.Length;

                        var sendResult = ffmpeg.avcodec_send_packet(_codecContext, packet);
                        if (sendResult < 0)
                        {
                            _logger.LogTrace("Failed to send packet: {Error}", GetErrorMessage(sendResult));
                            return false;
                        }

                        var receiveResult = ffmpeg.avcodec_receive_frame(_codecContext, _frame);
                        if (receiveResult < 0)
                        {
                            if (receiveResult != ffmpeg.AVERROR(ffmpeg.EAGAIN) && 
                                receiveResult != ffmpeg.AVERROR_EOF)
                            {
                                _logger.LogTrace("Failed to receive frame: {Error}", GetErrorMessage(receiveResult));
                            }
                            
                            // 增加连续失败计数
                            _consecutiveDecodeFailures++;
                            if (_consecutiveDecodeFailures >= MAX_CONSECUTIVE_FAILURES)
                            {
                                // 检查节流时间
                                var now = DateTime.UtcNow;
                                if (now - _lastKeyFrameRequestTime > KeyFrameRequestCooldown)
                                {
                                    _logger.LogWarning("VP9 解码连续失败 {Count} 次，请求关键帧", _consecutiveDecodeFailures);
                                    OnKeyFrameRequested?.Invoke();
                                    _lastKeyFrameRequestTime = now;
                                }
                                _consecutiveDecodeFailures = 0;  // 重置计数
                            }
                            return false;
                        }
                        
                        // 解码成功，重置失败计数
                        _consecutiveDecodeFailures = 0;

                        ConvertToBgr24();
                        return true;
                    }
                    finally
                    {
                        ffmpeg.av_packet_free(&packet);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decoding VP9 frame");
                return false;
            }
        }
    }

    /// <summary>
    /// 将解码后的帧转换为 BGR24 格式
    /// </summary>
    private void ConvertToBgr24()
    {
        if (_disposed || _frame == null || _rgbFrame == null)
        {
            return;
        }

        var width = _frame->width;
        var height = _frame->height;

        if (width <= 0 || height <= 0)
        {
            return;
        }

        // 检查是否需要重新初始化转换上下文
        if (_swsContext == null || _lastWidth != width || _lastHeight != height)
        {
            if (_swsContext != null)
            {
                ffmpeg.sws_freeContext(_swsContext);
            }
            if (_rgbBuffer != null)
            {
                ffmpeg.av_free(_rgbBuffer);
            }

            _swsContext = ffmpeg.sws_getContext(
                width, height, (AVPixelFormat)_frame->format,
                width, height, AVPixelFormat.AV_PIX_FMT_BGR24,
                ffmpeg.SWS_FAST_BILINEAR,  // 使用快速双线性插值，减少 CPU 开销
                null, null, null);

            if (_swsContext == null)
            {
                _logger.LogError("Failed to create sws context");
                return;
            }

            _rgbBufferSize = ffmpeg.av_image_get_buffer_size(
                AVPixelFormat.AV_PIX_FMT_BGR24, width, height, 1);
            
            _rgbBuffer = (byte*)ffmpeg.av_malloc((ulong)_rgbBufferSize);
            
            var dstData = new byte_ptrArray4();
            var dstLinesize = new int_array4();
            ffmpeg.av_image_fill_arrays(
                ref dstData, ref dstLinesize,
                _rgbBuffer, AVPixelFormat.AV_PIX_FMT_BGR24,
                width, height, 1);
            
            _rgbFrame->data[0] = dstData[0];
            _rgbFrame->linesize[0] = dstLinesize[0];
            _rgbFrame->width = width;
            _rgbFrame->height = height;

            _lastWidth = width;
            _lastHeight = height;
        }

        if (_disposed || _swsContext == null || _rgbFrame == null || _rgbFrame->data[0] == null)
        {
            return;
        }

        ffmpeg.sws_scale(
            _swsContext,
            _frame->data, _frame->linesize,
            0, height,
            _rgbFrame->data, _rgbFrame->linesize);

        if (_disposed || _rgbFrame == null || _rgbFrame->data[0] == null)
        {
            return;
        }

        var stride = _rgbFrame->linesize[0];
        var dataSize = stride * height;
        
        if (dataSize <= 0 || dataSize > 100 * 1024 * 1024)
        {
            return;
        }

        var bgrData = new byte[dataSize];
        Marshal.Copy((IntPtr)_rgbFrame->data[0], bgrData, 0, dataSize);

        OnFrameDecoded?.Invoke(bgrData, width, height);
    }

    private static string GetErrorMessage(int error)
    {
        var bufferSize = 1024;
        var buffer = stackalloc byte[bufferSize];
        ffmpeg.av_strerror(error, buffer, (ulong)bufferSize);
        return Marshal.PtrToStringAnsi((IntPtr)buffer) ?? $"Error {error}";
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            try
            {
                if (_swsContext != null)
                {
                    ffmpeg.sws_freeContext(_swsContext);
                    _swsContext = null;
                }

                if (_rgbBuffer != null)
                {
                    ffmpeg.av_free(_rgbBuffer);
                    _rgbBuffer = null;
                }

                if (_frame != null)
                {
                    var frame = _frame;
                    ffmpeg.av_frame_free(&frame);
                    _frame = null;
                }

                if (_rgbFrame != null)
                {
                    var frame = _rgbFrame;
                    ffmpeg.av_frame_free(&frame);
                    _rgbFrame = null;
                }

                if (_codecContext != null)
                {
                    var ctx = _codecContext;
                    ffmpeg.avcodec_free_context(&ctx);
                    _codecContext = null;
                }

                _logger.LogInformation("VP9 decoder disposed");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during VP9 decoder disposal");
            }
        }

        GC.SuppressFinalize(this);
    }
}
