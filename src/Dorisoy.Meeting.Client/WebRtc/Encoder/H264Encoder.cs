using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;

namespace Dorisoy.Meeting.Client.WebRtc.Encoder;

/// <summary>
/// H264 视频编码器 - 使用 FFmpeg 编码 H264 帧
/// 用于将本地采集的视频帧编码后发送到 mediasoup 服务器
/// 支持多种 Profile (Baseline/Main/High)
/// </summary>
public unsafe class H264Encoder : IVideoEncoder
{
    private readonly ILogger _logger;
    private readonly object _encodeLock = new();
    private AVCodecContext* _codecContext;
    private AVFrame* _frame;
    private SwsContext* _swsContext;
    private byte* _yuvBuffer;
    private bool _initialized;
    private bool _disposed;
    
    private int _width;
    private int _height;
    private int _frameIndex;
    
    private volatile bool _forceKeyFrameRequested;
    private bool _isFirstFrame = true;

    /// <summary>
    /// 编码后的帧事件 (H264 NAL 数据, 是否关键帧)
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

    /// <summary>
    /// H264 Profile (baseline, main, high)
    /// </summary>
    public string Profile { get; set; } = "baseline";

    /// <summary>
    /// 编码预设 (ultrafast, superfast, veryfast, faster, fast, medium, slow, slower, veryslow)
    /// </summary>
    public string Preset { get; set; } = "ultrafast";

    /// <summary>
    /// 关键帧间隔（秒）
    /// </summary>
    public int KeyFrameInterval { get; set; } = 2;

    /// <summary>
    /// 编码调优 (zerolatency, film, animation, grain, stillimage, fastdecode)
    /// </summary>
    public string Tune { get; set; } = "zerolatency";

    public H264Encoder(ILogger logger, int width = 640, int height = 480)
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
            if (!FFmpegConfig.IsInitialized)
            {
                _logger.LogWarning("FFmpeg not initialized, attempting initialization...");
                if (!FFmpegConfig.Initialize(_logger))
                {
                    _logger.LogError("Failed to initialize FFmpeg");
                    return false;
                }
            }
            
            // 查找 H264 编码器 (优先使用 libx264)
            var codec = ffmpeg.avcodec_find_encoder_by_name("libx264");
            if (codec == null)
            {
                // 回退到默认 H264 编码器
                codec = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_H264);
            }
            
            if (codec == null)
            {
                _logger.LogError("H264 encoder not found. Make sure FFmpeg is built with libx264 support.");
                return false;
            }

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
            _codecContext->gop_size = FrameRate * KeyFrameInterval;
            _codecContext->max_b_frames = 0; // 实时流不使用 B 帧
            _codecContext->thread_count = Environment.ProcessorCount;
            
            _isFirstFrame = true;

            // H264 特定选项
            ffmpeg.av_opt_set(_codecContext->priv_data, "preset", Preset, 0);
            ffmpeg.av_opt_set(_codecContext->priv_data, "tune", Tune, 0);
            ffmpeg.av_opt_set(_codecContext->priv_data, "profile", Profile, 0);
            
            // WebRTC 兼容性设置
            ffmpeg.av_opt_set(_codecContext->priv_data, "level", "3.1", 0);
            ffmpeg.av_opt_set(_codecContext->priv_data, "crf", "23", 0); // 恒定质量因子
            
            // 注意：不要使用 AV_CODEC_FLAG_GLOBAL_HEADER
            // 对于 RTP 流，我们需要 SPS/PPS 包含在每个关键帧中
            // 而不是存储在 extradata 中
            // 编码器会输出 Annex B 格式 (NAL 起始码 00 00 00 01)

            var result = ffmpeg.avcodec_open2(_codecContext, codec, null);
            if (result < 0)
            {
                _logger.LogError("Failed to open codec: {Error}", GetErrorMessage(result));
                return false;
            }

            _frame = ffmpeg.av_frame_alloc();
            if (_frame == null)
            {
                _logger.LogError("Failed to allocate frame");
                return false;
            }

            _frame->format = (int)AVPixelFormat.AV_PIX_FMT_YUV420P;
            _frame->width = _width;
            _frame->height = _height;

            var yuvBufferSize = ffmpeg.av_image_get_buffer_size(
                AVPixelFormat.AV_PIX_FMT_YUV420P, _width, _height, 1);
            _yuvBuffer = (byte*)ffmpeg.av_malloc((ulong)yuvBufferSize);
            
            var dstData = new byte_ptrArray4();
            var dstLinesize = new int_array4();
            ffmpeg.av_image_fill_arrays(ref dstData, ref dstLinesize,
                _yuvBuffer, AVPixelFormat.AV_PIX_FMT_YUV420P, _width, _height, 1);
            
            _frame->data[0] = dstData[0];
            _frame->data[1] = dstData[1];
            _frame->data[2] = dstData[2];
            _frame->data[3] = dstData[3];
            _frame->linesize[0] = dstLinesize[0];
            _frame->linesize[1] = dstLinesize[1];
            _frame->linesize[2] = dstLinesize[2];
            _frame->linesize[3] = dstLinesize[3];

            _initialized = true;
            _logger.LogInformation("H264 encoder initialized: {Width}x{Height} @ {Fps}fps, {Bitrate}bps, profile={Profile}",
                _width, _height, FrameRate, Bitrate, Profile);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize H264 encoder");
            return false;
        }
    }

    /// <summary>
    /// 编码 BGR24 帧
    /// </summary>
    public bool Encode(byte[] bgrData, int width, int height)
    {
        if (_disposed)
            return false;
        
        lock (_encodeLock)
        {
            if (!_initialized || _disposed)
                return false;
            
            if (bgrData == null || bgrData.Length == 0)
            {
                _logger.LogWarning("Empty BGR data received");
                return false;
            }
            
            int expectedSize = width * height * 3;
            if (bgrData.Length < expectedSize)
            {
                _logger.LogWarning("BGR data size mismatch: expected {Expected}, got {Actual}", expectedSize, bgrData.Length);
                return false;
            }

            try
            {
                if (width != _width || height != _height)
                {
                    _logger.LogInformation("Resolution changed from {OldW}x{OldH} to {NewW}x{NewH}, reinitializing encoder",
                        _width, _height, width, height);
                    
                    CleanupResources();
                    _width = width;
                    _height = height;
                    _initialized = false;
                    
                    if (!Initialize())
                    {
                        _logger.LogError("Failed to reinitialize encoder for new resolution");
                        return false;
                    }
                }
                
                if (_codecContext == null || _frame == null || _yuvBuffer == null)
                {
                    _logger.LogError("Encoder resources not properly initialized");
                    return false;
                }

                if (!ConvertBgr24ToYuv420P(bgrData, width, height))
                    return false;

                _frame->pts = _frameIndex++;
                
                bool wasKeyFrameRequested = _forceKeyFrameRequested || _isFirstFrame;
                if (wasKeyFrameRequested)
                {
                    _forceKeyFrameRequested = false;
                    _isFirstFrame = false;
                    
                    _frame->pict_type = AVPictureType.AV_PICTURE_TYPE_I;
                    #pragma warning disable CS0618
                    _frame->key_frame = 1;
                    #pragma warning restore CS0618
                    
                    _logger.LogInformation("强制生成关键帧: frame={FrameIndex}", _frameIndex - 1);
                }
                else
                {
                    _frame->pict_type = AVPictureType.AV_PICTURE_TYPE_NONE;
                    #pragma warning disable CS0618
                    _frame->key_frame = 0;
                    #pragma warning restore CS0618
                }

                var sendResult = ffmpeg.avcodec_send_frame(_codecContext, _frame);
                if (sendResult < 0)
                {
                    _logger.LogTrace("Failed to send frame: {Error}", GetErrorMessage(sendResult));
                    return false;
                }

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

                    var encodedData = new byte[packet->size];
                    Marshal.Copy((IntPtr)packet->data, encodedData, 0, packet->size);

                    var isKeyFrame = (packet->flags & ffmpeg.AV_PKT_FLAG_KEY) != 0;
                    
                    OnFrameEncoded?.Invoke(encodedData, isKeyFrame);
                    return true;
                }
                finally
                {
                    ffmpeg.av_packet_free(&packet);
                }
            }
            catch (AccessViolationException ex)
            {
                _logger.LogError(ex, "Memory access violation in H264 encoder - reinitializing");
                try
                {
                    CleanupResources();
                    _initialized = false;
                    Initialize();
                }
                catch { }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error encoding H264 frame");
                return false;
            }
        }
    }

    /// <summary>
    /// 获取 SPS/PPS 参数集 (用于 RTP 打包)
    /// </summary>
    public byte[]? GetExtraData()
    {
        if (_codecContext == null || _codecContext->extradata == null || _codecContext->extradata_size <= 0)
            return null;
            
        var extraData = new byte[_codecContext->extradata_size];
        Marshal.Copy((IntPtr)_codecContext->extradata, extraData, 0, _codecContext->extradata_size);
        return extraData;
    }

    private void CleanupResources()
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
    }

    private bool ConvertBgr24ToYuv420P(byte[] bgrData, int width, int height)
    {
        try
        {
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
                srcLinesize[0] = width * 3;

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
            lock (_encodeLock)
            {
                _disposed = true;
                CleanupResources();
            }
            _logger.LogInformation("H264 encoder disposed");
        }
        GC.SuppressFinalize(this);
    }
}
