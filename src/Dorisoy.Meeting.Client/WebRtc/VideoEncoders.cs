using System.IO;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;

namespace Dorisoy.Meeting.Client.WebRtc;

/// <summary>
/// VP8 视频编码器 - 使用 FFmpeg 编码 VP8 帧
/// 用于将本地采集的视频帧编码后发送到 mediasoup 服务器
/// </summary>
public unsafe class Vp8Encoder : IDisposable
{
    private readonly ILogger _logger;
    private AVCodecContext* _codecContext;
    private AVFrame* _frame;
    private SwsContext* _swsContext;
    private byte* _yuvBuffer;
    private bool _initialized;
    private bool _disposed;
    
    private int _width;
    private int _height;
    private int _frameIndex;
    
    // 关键帧请求标志 - 用于响应 PLI/FIR
    private volatile bool _forceKeyFrameRequested;
    
    // 第一帧标志 - 确保第一帧必须是关键帧
    private bool _isFirstFrame = true;

    /// <summary>
    /// 编码后的帧事件 (VP8 数据)
    /// </summary>
    public event Action<byte[], bool>? OnFrameEncoded;

    /// <summary>
    /// 目标帧率
    /// </summary>
    public int FrameRate { get; set; } = 30;

    /// <summary>
    /// 目标比特率 (bps)
    /// </summary>
    public int Bitrate { get; set; } = 1500000;

    public Vp8Encoder(ILogger logger, int width = 640, int height = 480)
    {
        _logger = logger;
        _width = width;
        _height = height;
    }

    /// <summary>
    /// 初始化编码器
    /// </summary>
    public bool Initialize()
    {
        if (_initialized)
            return true;

        try
        {
            // 确保 FFmpeg 已初始化（应在 App.xaml.cs 中调用 FFmpegConfig.Initialize()）
            if (!FFmpegConfig.IsInitialized)
            {
                _logger.LogWarning("FFmpeg not initialized, attempting initialization...");
                if (!FFmpegConfig.Initialize(_logger))
                {
                    _logger.LogError("Failed to initialize FFmpeg");
                    return false;
                }
            }
            
            // 查找 VP8 编码器
            var codec = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_VP8);
            if (codec == null)
            {
                _logger.LogError("VP8 encoder not found. Make sure FFmpeg is built with libvpx support.");
                return false;
            }

            // 分配编解码器上下文
            _codecContext = ffmpeg.avcodec_alloc_context3(codec);
            if (_codecContext == null)
            {
                _logger.LogError("Failed to allocate codec context");
                return false;
            }

            // 设置编码参数
            _codecContext->width = _width;
            _codecContext->height = _height;
            _codecContext->time_base = new AVRational { num = 1, den = FrameRate };
            _codecContext->framerate = new AVRational { num = FrameRate, den = 1 };
            _codecContext->pix_fmt = AVPixelFormat.AV_PIX_FMT_YUV420P;
            _codecContext->bit_rate = Bitrate;
            _codecContext->gop_size = FrameRate; // 关键帧间隔 1 秒（缩短以便更快给新加入的消费者发送关键帧）
            _codecContext->max_b_frames = 0; // VP8 不使用 B 帧
            _codecContext->thread_count = Environment.ProcessorCount;
            
            // 强制第一帧为关键帧
            _isFirstFrame = true;

            // 设置实时编码选项
            ffmpeg.av_opt_set(_codecContext->priv_data, "deadline", "realtime", 0);
            ffmpeg.av_opt_set(_codecContext->priv_data, "cpu-used", "4", 0);

            // 打开编解码器
            var result = ffmpeg.avcodec_open2(_codecContext, codec, null);
            if (result < 0)
            {
                _logger.LogError("Failed to open codec: {Error}", GetErrorMessage(result));
                return false;
            }

            // 分配帧
            _frame = ffmpeg.av_frame_alloc();
            if (_frame == null)
            {
                _logger.LogError("Failed to allocate frame");
                return false;
            }

            _frame->format = (int)AVPixelFormat.AV_PIX_FMT_YUV420P;
            _frame->width = _width;
            _frame->height = _height;

            // 分配 YUV 缓冲区
            var yuvBufferSize = ffmpeg.av_image_get_buffer_size(
                AVPixelFormat.AV_PIX_FMT_YUV420P, _width, _height, 1);
            _yuvBuffer = (byte*)ffmpeg.av_malloc((ulong)yuvBufferSize);
            
            var dstData = new byte_ptrArray4();
            var dstLinesize = new int_array4();
            ffmpeg.av_image_fill_arrays(ref dstData, ref dstLinesize,
                _yuvBuffer, AVPixelFormat.AV_PIX_FMT_YUV420P, _width, _height, 1);
            
            // 复制数据指针
            _frame->data[0] = dstData[0];
            _frame->data[1] = dstData[1];
            _frame->data[2] = dstData[2];
            _frame->data[3] = dstData[3];
            _frame->linesize[0] = dstLinesize[0];
            _frame->linesize[1] = dstLinesize[1];
            _frame->linesize[2] = dstLinesize[2];
            _frame->linesize[3] = dstLinesize[3];

            _initialized = true;
            _logger.LogInformation("VP8 encoder initialized: {Width}x{Height} @ {Fps}fps, {Bitrate}bps",
                _width, _height, FrameRate, Bitrate);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize VP8 encoder");
            return false;
        }
    }

    /// <summary>
    /// 编码 BGR24 帧
    /// </summary>
    /// <param name="bgrData">BGR24 格式的图像数据</param>
    /// <param name="width">图像宽度</param>
    /// <param name="height">图像高度</param>
    /// <returns>是否编码成功</returns>
    public bool Encode(byte[] bgrData, int width, int height)
    {
        if (!_initialized || _disposed)
            return false;

        try
        {
            // 检查分辨率是否变化
            if (width != _width || height != _height)
            {
                Reconfigure(width, height);
            }

            // 转换 BGR24 到 YUV420P
            if (!ConvertBgr24ToYuv420P(bgrData, width, height))
                return false;

            // 设置帧时间戳
            _frame->pts = _frameIndex++;
            
            // 检查是否需要强制生成关键帧 (响应 PLI/FIR 请求或第一帧)
            bool wasKeyFrameRequested = _forceKeyFrameRequested || _isFirstFrame;
            if (wasKeyFrameRequested)
            {
                _forceKeyFrameRequested = false; // 立即重置标志
                _isFirstFrame = false; // 重置第一帧标志
                
                // 设置帧类型为 I 帧
                _frame->pict_type = AVPictureType.AV_PICTURE_TYPE_I;
                #pragma warning disable CS0618 // 使用 FFmpeg 7.0 新 API
                _frame->key_frame = 1;
                #pragma warning restore CS0618
                
                _logger.LogInformation("强制生成关键帧: frame={FrameIndex}", _frameIndex - 1);
            }
            else
            {
                // 让编码器自动决定帧类型
                _frame->pict_type = AVPictureType.AV_PICTURE_TYPE_NONE;
                #pragma warning disable CS0618
                _frame->key_frame = 0;
                #pragma warning restore CS0618
            }

            // 发送帧到编码器
            var sendResult = ffmpeg.avcodec_send_frame(_codecContext, _frame);
            if (sendResult < 0)
            {
                _logger.LogTrace("Failed to send frame: {Error}", GetErrorMessage(sendResult));
                return false;
            }

            // 接收编码后的数据包
            var packet = ffmpeg.av_packet_alloc();
            try
            {
                var receiveResult = ffmpeg.avcodec_receive_packet(_codecContext, packet);
                if (receiveResult < 0)
                {
                    if (receiveResult != ffmpeg.AVERROR(ffmpeg.EAGAIN) &&
                        receiveResult != ffmpeg.AVERROR_EOF)
                    {
                        _logger.LogTrace("Failed to receive packet: {Error}", GetErrorMessage(receiveResult));
                    }
                    return false;
                }

                // 复制编码数据
                var encodedData = new byte[packet->size];
                Marshal.Copy((IntPtr)packet->data, encodedData, 0, packet->size);

                // 检查是否是关键帧
                var isKeyFrame = (packet->flags & ffmpeg.AV_PKT_FLAG_KEY) != 0;
                
                // 日志记录关键帧生成结果
                if (wasKeyFrameRequested)
                {
                    // 检查 VP8 帧数据的第一个字节，验证是否真的是关键帧
                    // VP8 帧第一个字节: bit 0 = P (0=keyframe, 1=interframe)
                    bool isVp8Keyframe = encodedData.Length > 0 && (encodedData[0] & 0x01) == 0;
                    
                    if (isKeyFrame && isVp8Keyframe)
                    {
                        _logger.LogInformation("关键帧生成成功: frame={FrameIndex}, size={Size}bytes, VP8_P=0", _frameIndex - 1, packet->size);
                    }
                    else if (isKeyFrame && !isVp8Keyframe)
                    {
                        _logger.LogWarning("编码器标记为关键帧但VP8数据不是: frame={FrameIndex}, firstByte=0x{FirstByte:X2}", _frameIndex - 1, encodedData.Length > 0 ? encodedData[0] : 0);
                    }
                    else
                    {
                        _logger.LogWarning("关键帧请求失败 - 编码器返回非关键帧: frame={FrameIndex}, isKeyFrame={IsKey}, firstByte=0x{FirstByte:X2}", 
                            _frameIndex - 1, isKeyFrame, encodedData.Length > 0 ? encodedData[0] : 0);
                    }
                }

                // 触发编码完成事件
                OnFrameEncoded?.Invoke(encodedData, isKeyFrame);

                return true;
            }
            finally
            {
                ffmpeg.av_packet_free(&packet);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error encoding VP8 frame");
            return false;
        }
    }

    /// <summary>
    /// 重新配置编码器分辨率
    /// </summary>
    private void Reconfigure(int width, int height)
    {
        _width = width;
        _height = height;

        // 释放旧的缩放上下文
        if (_swsContext != null)
        {
            ffmpeg.sws_freeContext(_swsContext);
            _swsContext = null;
        }

        // 重新分配 YUV 缓冲区
        if (_yuvBuffer != null)
        {
            ffmpeg.av_free(_yuvBuffer);
        }

        var yuvBufferSize = ffmpeg.av_image_get_buffer_size(
            AVPixelFormat.AV_PIX_FMT_YUV420P, _width, _height, 1);
        _yuvBuffer = (byte*)ffmpeg.av_malloc((ulong)yuvBufferSize);

        // 更新帧参数
        _frame->width = _width;
        _frame->height = _height;

        var dstData = new byte_ptrArray4();
        var dstLinesize = new int_array4();
        ffmpeg.av_image_fill_arrays(ref dstData, ref dstLinesize,
            _yuvBuffer, AVPixelFormat.AV_PIX_FMT_YUV420P, _width, _height, 1);
        
        // 复制数据指针
        _frame->data[0] = dstData[0];
        _frame->data[1] = dstData[1];
        _frame->data[2] = dstData[2];
        _frame->data[3] = dstData[3];
        _frame->linesize[0] = dstLinesize[0];
        _frame->linesize[1] = dstLinesize[1];
        _frame->linesize[2] = dstLinesize[2];
        _frame->linesize[3] = dstLinesize[3];

        _logger.LogInformation("VP8 encoder reconfigured: {Width}x{Height}", _width, _height);
    }

    /// <summary>
    /// 将 BGR24 转换为 YUV420P
    /// </summary>
    private bool ConvertBgr24ToYuv420P(byte[] bgrData, int width, int height)
    {
        try
        {
            // 创建或更新缩放上下文
            if (_swsContext == null)
            {
                _swsContext = ffmpeg.sws_getContext(
                    width, height, AVPixelFormat.AV_PIX_FMT_BGR24,
                    _width, _height, AVPixelFormat.AV_PIX_FMT_YUV420P,
                    ffmpeg.SWS_FAST_BILINEAR, null, null, null);

                if (_swsContext == null)
                {
                    _logger.LogError("Failed to create sws context for encoding");
                    return false;
                }
            }

            fixed (byte* pBgr = bgrData)
            {
                var srcData = new byte_ptrArray4();
                var srcLinesize = new int_array4();
                srcData[0] = pBgr;
                srcLinesize[0] = width * 3; // BGR24 stride

                ffmpeg.sws_scale(
                    _swsContext,
                    srcData, srcLinesize,
                    0, height,
                    _frame->data, _frame->linesize);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert BGR24 to YUV420P");
            return false;
        }
    }

    /// <summary>
    /// 强制下一帧为关键帧 (用于响应 PLI/FIR 请求)
    /// </summary>
    public void ForceKeyFrame()
    {
        _forceKeyFrameRequested = true;
        _logger.LogDebug("Keyframe requested, will be generated on next encode");
    }

    /// <summary>
    /// 获取错误消息
    /// </summary>
    private static string GetErrorMessage(int error)
    {
        var bufferSize = 1024;
        var buffer = stackalloc byte[bufferSize];
        ffmpeg.av_strerror(error, buffer, (ulong)bufferSize);
        return Marshal.PtrToStringAnsi((IntPtr)buffer) ?? $"Error {error}";
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_swsContext != null)
            {
                ffmpeg.sws_freeContext(_swsContext);
                _swsContext = null;
            }

            if (_yuvBuffer != null)
            {
                ffmpeg.av_free(_yuvBuffer);
                _yuvBuffer = null;
            }

            if (_frame != null)
            {
                var frame = _frame;
                ffmpeg.av_frame_free(&frame);
                _frame = null;
            }

            if (_codecContext != null)
            {
                var ctx = _codecContext;
                ffmpeg.avcodec_free_context(&ctx);
                _codecContext = null;
            }

            _disposed = true;
            _logger.LogInformation("VP8 encoder disposed");
        }
        GC.SuppressFinalize(this);
    }
}
