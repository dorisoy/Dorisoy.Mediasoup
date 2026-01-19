using Microsoft.Extensions.Logging;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using Concentus.Structs;
using Concentus.Enums;
using System.Collections.Concurrent;
using Dorisoy.Meeting.Client.WebRtc.Decoder;
using Dorisoy.Meeting.Client.Models;

namespace Dorisoy.Meeting.Client.WebRtc;

/// <summary>
/// RTP 媒体解码器 - 处理视频/音频 RTP 包的解包和解码
/// 支持 VP8/VP9/H264 视频解码和 Opus 音频解码
/// </summary>
public class RtpMediaDecoder : IDisposable
{
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private bool _disposed;

    // 视频解包器 - 根据当前编解码器类型使用对应的解包器
    private readonly ConcurrentDictionary<string, Vp8Depacketizer> _vp8Depacketizers = new();
    private readonly ConcurrentDictionary<string, Vp9Depacketizer> _vp9Depacketizers = new();
    private readonly ConcurrentDictionary<string, H264Depacketizer> _h264Depacketizers = new();
    
    // 视频解码器 - 支持多种编解码器类型
    private readonly ConcurrentDictionary<string, IVideoDecoder> _videoDecoders = new();
    
    // 当前视频编解码器类型（用于发送端）
    private VideoCodecType _currentVideoCodec = VideoCodecType.VP8;
    
    // 每个 Consumer 独立的编解码器类型（用于接收端）
    private readonly ConcurrentDictionary<string, VideoCodecType> _consumerCodecTypes = new();

    // Opus 解码器 (Concentus - 纯 C# 实现)
    private readonly ConcurrentDictionary<string, OpusDecoder> _audioDecoders = new();
    private const int OPUS_SAMPLE_RATE = 48000;
    private const int OPUS_CHANNELS = 2;
    private const int OPUS_FRAME_SIZE = 960; // 20ms at 48kHz

    /// <summary>
    /// 解码后的视频帧事件 (BGR24 格式)
    /// </summary>
    public event Action<string, byte[], int, int>? OnDecodedVideoFrame;

    /// <summary>
    /// 解码后的音频采样事件 (PCM)
    /// </summary>
    public event Action<string, short[]>? OnDecodedAudioSamples;
    
    /// <summary>
    /// VP8 帧数据事件 (解包后但未解码)
    /// </summary>
    public event Action<string, byte[], bool>? OnVp8FrameReceived;

    public RtpMediaDecoder(ILogger logger)
    {
        _logger = logger;
        _loggerFactory = null!;
    }
    
    public RtpMediaDecoder(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<RtpMediaDecoder>();
    }
    
    /// <summary>
    /// 设置视频编解码器类型
    /// </summary>
    /// <param name="codecType">编解码器类型</param>
    public void SetVideoCodecType(VideoCodecType codecType)
    {
        if (_currentVideoCodec != codecType)
        {
            _logger.LogInformation("解码器切换: {OldCodec} -> {NewCodec}", _currentVideoCodec, codecType);
            _currentVideoCodec = codecType;
            
            // 清理旧的解码器，下次解码时会创建新的
            foreach (var decoder in _videoDecoders.Values)
            {
                decoder.Dispose();
            }
            _videoDecoders.Clear();
            
            // 清理解包器
            _vp8Depacketizers.Clear();
            _vp9Depacketizers.Clear();
            _h264Depacketizers.Clear();
        }
    }
    
    /// <summary>
    /// 获取当前视频编解码器类型
    /// </summary>
    public VideoCodecType CurrentVideoCodec => _currentVideoCodec;
    
    /// <summary>
    /// 为指定 Consumer 设置编解码器类型（用于接收端根据远端 MimeType 设置）
    /// </summary>
    /// <param name="consumerId">Consumer ID</param>
    /// <param name="codecType">编解码器类型</param>
    public void SetConsumerVideoCodecType(string consumerId, VideoCodecType codecType)
    {
        _consumerCodecTypes[consumerId] = codecType;
        _logger.LogInformation("为 Consumer {ConsumerId} 设置解码器类型: {Codec}", consumerId, codecType);
    }
    
    /// <summary>
    /// 根据 MimeType 获取编解码器类型
    /// </summary>
    public static VideoCodecType GetCodecTypeFromMimeType(string? mimeType)
    {
        if (string.IsNullOrEmpty(mimeType))
            return VideoCodecType.VP8;
            
        return mimeType.ToUpperInvariant() switch
        {
            var m when m.Contains("VP9") => VideoCodecType.VP9,
            var m when m.Contains("H264") => VideoCodecType.H264,
            var m when m.Contains("VP8") => VideoCodecType.VP8,
            _ => VideoCodecType.VP8
        };
    }
    
    /// <summary>
    /// 获取指定 Consumer 的编解码器类型
    /// </summary>
    private VideoCodecType GetConsumerCodecType(string consumerId)
    {
        // 优先使用 Consumer 独立的编解码器类型，否则使用全局默认值
        if (_consumerCodecTypes.TryGetValue(consumerId, out var codecType))
        {
            return codecType;
        }
        return _currentVideoCodec;
    }
    
    /// <summary>
    /// 移除指定 Consumer 的所有解码器和解包器资源
    /// 当用户离开房间或 Consumer 关闭时调用
    /// </summary>
    /// <param name="consumerId">Consumer ID</param>
    public void RemoveConsumer(string consumerId)
    {
        _logger.LogInformation("移除 Consumer 解码器资源: {ConsumerId}", consumerId);
        
        // 移除视频解码器
        if (_videoDecoders.TryRemove(consumerId, out var videoDecoder))
        {
            try
            {
                videoDecoder.Dispose();
                _logger.LogDebug("已释放视频解码器: {ConsumerId}", consumerId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "释放视频解码器失败: {ConsumerId}", consumerId);
            }
        }
        
        // 移除解包器
        _vp8Depacketizers.TryRemove(consumerId, out _);
        _vp9Depacketizers.TryRemove(consumerId, out _);
        _h264Depacketizers.TryRemove(consumerId, out _);
        
        // 移除编解码器类型记录
        _consumerCodecTypes.TryRemove(consumerId, out _);
        
        // 移除音频解码器
        if (_audioDecoders.TryRemove(consumerId, out var audioDecoder))
        {
            try
            {
                // OpusDecoder 没有 Dispose 方法，但我们仍然从字典中移除
                _logger.LogDebug("已移除音频解码器: {ConsumerId}", consumerId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "移除音频解码器失败: {ConsumerId}", consumerId);
            }
        }
        
        _logger.LogDebug("Consumer 资源清理完成: {ConsumerId}", consumerId);
    }

    /// <summary>
    /// 处理视频 RTP 包 - 根据每个 Consumer 的编解码器类型选择对应的解包器
    /// </summary>
    public void ProcessVideoRtpPacket(string consumerId, RTPPacket rtpPacket)
    {
        try
        {
            // 获取该 Consumer 的编解码器类型
            var codecType = GetConsumerCodecType(consumerId);
            
            _logger.LogDebug("Processing video RTP: ConsumerId={ConsumerId}, Seq={Seq}, Marker={Marker}, Codec={Codec}",
                consumerId, rtpPacket.Header.SequenceNumber, rtpPacket.Header.MarkerBit, codecType);
            
            var payload = rtpPacket.Payload;
            var marker = rtpPacket.Header.MarkerBit == 1;
            
            byte[]? frameData = null;
            bool isKeyFrame = false;
            
            switch (codecType)
            {
                case VideoCodecType.VP8:
                    (frameData, isKeyFrame) = ProcessVp8Packet(consumerId, payload, marker);
                    break;
                case VideoCodecType.VP9:
                    (frameData, isKeyFrame) = ProcessVp9Packet(consumerId, payload, marker);
                    break;
                case VideoCodecType.H264:
                    (frameData, isKeyFrame) = ProcessH264Packet(consumerId, payload, marker);
                    break;
            }
            
            if (frameData != null && frameData.Length > 0)
            {
                DecodeVideoFrame(consumerId, frameData, isKeyFrame, codecType);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing video RTP packet for consumer {ConsumerId}", consumerId);
        }
    }
    
    /// <summary>
    /// 处理 VP8 RTP 包
    /// </summary>
    private (byte[]? frameData, bool isKeyFrame) ProcessVp8Packet(string consumerId, byte[] payload, bool marker)
    {
        if (!_vp8Depacketizers.TryGetValue(consumerId, out var depacketizer))
        {
            depacketizer = new Vp8Depacketizer();
            _vp8Depacketizers[consumerId] = depacketizer;
            _logger.LogDebug("Created VP8 depacketizer for consumer {ConsumerId}", consumerId);
        }
        
        depacketizer.AddRtpPacket(payload, marker);
        
        if (marker && depacketizer.IsFrameComplete)
        {
            var frameData = depacketizer.GetFrame();
            var isKeyFrame = depacketizer.IsKeyFrame;
            depacketizer.Reset();
            return (frameData, isKeyFrame);
        }
        
        return (null, false);
    }
    
    /// <summary>
    /// 处理 VP9 RTP 包
    /// </summary>
    private (byte[]? frameData, bool isKeyFrame) ProcessVp9Packet(string consumerId, byte[] payload, bool marker)
    {
        if (!_vp9Depacketizers.TryGetValue(consumerId, out var depacketizer))
        {
            depacketizer = new Vp9Depacketizer();
            _vp9Depacketizers[consumerId] = depacketizer;
            _logger.LogDebug("Created VP9 depacketizer for consumer {ConsumerId}", consumerId);
        }
        
        depacketizer.AddRtpPacket(payload, marker);
        
        if (marker && depacketizer.IsFrameComplete)
        {
            var frameData = depacketizer.GetFrame();
            var isKeyFrame = depacketizer.IsKeyFrame;
            depacketizer.Reset();
            return (frameData, isKeyFrame);
        }
        
        return (null, false);
    }
    
    /// <summary>
    /// 处理 H264 RTP 包
    /// </summary>
    private (byte[]? frameData, bool isKeyFrame) ProcessH264Packet(string consumerId, byte[] payload, bool marker)
    {
        if (!_h264Depacketizers.TryGetValue(consumerId, out var depacketizer))
        {
            depacketizer = new H264Depacketizer();
            _h264Depacketizers[consumerId] = depacketizer;
            _logger.LogDebug("Created H264 depacketizer for consumer {ConsumerId}", consumerId);
        }
        
        depacketizer.AddRtpPacket(payload, marker);
        
        if (marker && depacketizer.IsFrameComplete)
        {
            var frameData = depacketizer.GetFrame();
            var isKeyFrame = depacketizer.IsKeyFrame;
            depacketizer.Reset();
            return (frameData, isKeyFrame);
        }
        
        return (null, false);
    }

    /// <summary>
    /// 解码视频帧 - 根据指定的编解码器类型使用对应解码器
    /// </summary>
    private void DecodeVideoFrame(string consumerId, byte[] frameData, bool isKeyFrame, VideoCodecType codecType)
    {
        try
        {
            // 发送帧数据事件 (解包后但未解码)
            OnVp8FrameReceived?.Invoke(consumerId, frameData, isKeyFrame);
            
            // 获取或创建解码器
            if (!_videoDecoders.TryGetValue(consumerId, out var decoder))
            {
                decoder = CreateVideoDecoder(codecType);
                if (decoder == null)
                {
                    _logger.LogWarning("创建解码器失败: {Codec}", codecType);
                    return;
                }
                
                decoder.OnFrameDecoded += (bgrData, width, height) =>
                {
                    OnDecodedVideoFrame?.Invoke(consumerId, bgrData, width, height);
                };
                _videoDecoders[consumerId] = decoder;
                _logger.LogInformation("创建 {Codec} 解码器: Consumer={ConsumerId}", codecType, consumerId);
            }
            
            // 解码帧
            var success = decoder.Decode(frameData);
            if (success)
            {
                _logger.LogTrace("{Codec} 帧解码成功: Consumer={ConsumerId}", codecType, consumerId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "视频解码失败: Consumer={ConsumerId}, Codec={Codec}", consumerId, codecType);
        }
    }
    
    /// <summary>
    /// 根据编解码器类型创建对应的解码器
    /// </summary>
    private IVideoDecoder? CreateVideoDecoder(VideoCodecType codecType)
    {
        var logger = _loggerFactory?.CreateLogger(codecType.ToString() + "Decoder") ?? _logger;
        
        return codecType switch
        {
            VideoCodecType.VP8 => new Vp8Decoder(logger),
            VideoCodecType.VP9 => new Vp9Decoder(logger),
            VideoCodecType.H264 => new H264Decoder(logger),
            _ => new Vp8Decoder(logger)
        };
    }

    /// <summary>
    /// 处理音频 RTP 包
    /// </summary>
    public void ProcessAudioRtpPacket(string consumerId, RTPPacket rtpPacket)
    {
        try
        {
            // 获取或创建 Opus 解码器
            if (!_audioDecoders.TryGetValue(consumerId, out var decoder))
            {
#pragma warning disable CS0618 // 使用旧版 API
                decoder = new OpusDecoder(OPUS_SAMPLE_RATE, OPUS_CHANNELS);
#pragma warning restore CS0618
                _audioDecoders[consumerId] = decoder;
                _logger.LogDebug("Created Opus decoder for consumer {ConsumerId}", consumerId);
            }

            // 解码 Opus 音频
            var payload = rtpPacket.Payload;
            if (payload == null || payload.Length == 0) return;

            // 解码
            var pcmBuffer = new short[OPUS_FRAME_SIZE * OPUS_CHANNELS];
#pragma warning disable CS0618 // 使用旧版 API
            var samplesDecoded = decoder.Decode(payload, 0, payload.Length, pcmBuffer, 0, OPUS_FRAME_SIZE, false);
#pragma warning restore CS0618

            if (samplesDecoded > 0)
            {
                var samples = new short[samplesDecoded * OPUS_CHANNELS];
                Array.Copy(pcmBuffer, samples, samples.Length);
                OnDecodedAudioSamples?.Invoke(consumerId, samples);
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Audio decode failed for consumer {ConsumerId}", consumerId);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // 释放视频解码器
            foreach (var decoder in _videoDecoders.Values)
            {
                decoder.Dispose();
            }
            _videoDecoders.Clear();
            
            // 清理解包器
            _vp8Depacketizers.Clear();
            _vp9Depacketizers.Clear();
            _h264Depacketizers.Clear();
            
            _audioDecoders.Clear();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// VP8 RTP 解包器 - 将 RTP 包重组为完整的 VP8 帧
/// 参考 RFC 7741
/// </summary>
public class Vp8Depacketizer
{
    private readonly List<byte[]> _fragments = new();
    private bool _isKeyFrame;

    /// <summary>
    /// 帧是否完整
    /// </summary>
    public bool IsFrameComplete { get; private set; }

    /// <summary>
    /// 是否是关键帧
    /// </summary>
    public bool IsKeyFrame => _isKeyFrame;

    /// <summary>
    /// 添加 RTP 包
    /// </summary>
    public void AddRtpPacket(byte[] payload, bool marker)
    {
        if (payload == null || payload.Length < 1) return;

        // VP8 RTP 负载描述符解析
        // 参考 RFC 7741
        var offset = 0;
        var firstByte = payload[offset++];

        // X: 扩展位
        var hasExtension = (firstByte & 0x80) != 0;
        // R: 保留位
        // N: 非参考帧
        // S: 起始位
        var isStart = (firstByte & 0x10) != 0;
        // PID: 分区索引
        var partitionIndex = firstByte & 0x0F;

        // 处理扩展字节
        if (hasExtension && offset < payload.Length)
        {
            var extByte = payload[offset++];
            // I: 画面 ID 存在
            if ((extByte & 0x80) != 0 && offset < payload.Length)
            {
                var pictureId = payload[offset++];
                // M: 长画面 ID
                if ((pictureId & 0x80) != 0 && offset < payload.Length)
                {
                    offset++; // 跳过第二个画面 ID 字节
                }
            }
            // L: TL0PICIDX 存在
            if ((extByte & 0x40) != 0 && offset < payload.Length)
            {
                offset++;
            }
            // T/K: TID/KEYIDX 存在
            if ((extByte & 0x20) != 0 && offset < payload.Length)
            {
                offset++;
            }
            if ((extByte & 0x10) != 0 && offset < payload.Length)
            {
                offset++;
            }
        }

        // 提取 VP8 负载
        if (offset < payload.Length)
        {
            var vp8Payload = new byte[payload.Length - offset];
            Array.Copy(payload, offset, vp8Payload, 0, vp8Payload.Length);

            // 检查是否是关键帧 (VP8 帧的第一个字节)
            if (isStart && vp8Payload.Length > 0)
            {
                // VP8 帧头: 第一个字节的最低位为 0 表示关键帧
                _isKeyFrame = (vp8Payload[0] & 0x01) == 0;
            }

            _fragments.Add(vp8Payload);
        }

        IsFrameComplete = marker;
    }

    /// <summary>
    /// 获取完整帧数据
    /// </summary>
    public byte[]? GetFrame()
    {
        if (_fragments.Count == 0) return null;

        var totalLength = _fragments.Sum(f => f.Length);
        var frame = new byte[totalLength];
        var offset = 0;

        foreach (var fragment in _fragments)
        {
            Array.Copy(fragment, 0, frame, offset, fragment.Length);
            offset += fragment.Length;
        }

        return frame;
    }

    /// <summary>
    /// 重置解包器
    /// </summary>
    public void Reset()
    {
        _fragments.Clear();
        IsFrameComplete = false;
        _isKeyFrame = false;
    }
}

/// <summary>
/// VP9 RTP 解包器 - 将 RTP 包重组为完整的 VP9 帧
/// 参考 RFC 7741 / draft-ietf-payload-vp9
/// </summary>
public class Vp9Depacketizer
{
    private readonly List<byte[]> _fragments = new();
    private bool _isKeyFrame;

    public bool IsFrameComplete { get; private set; }
    public bool IsKeyFrame => _isKeyFrame;

    public void AddRtpPacket(byte[] payload, bool marker)
    {
        if (payload == null || payload.Length < 1) return;

        // VP9 RTP 负载描述符解析 (draft-ietf-payload-vp9)
        var offset = 0;
        var firstByte = payload[offset++];

        // I: Picture ID 存在
        var hasPictureId = (firstByte & 0x80) != 0;
        // P: Inter-picture predicted frame
        var isInterPredicted = (firstByte & 0x40) != 0;
        // L: Layer indices present
        var hasLayerIndices = (firstByte & 0x20) != 0;
        // F: Flexible mode
        var isFlexibleMode = (firstByte & 0x10) != 0;
        // B: Start of a frame
        var isStart = (firstByte & 0x08) != 0;
        // E: End of a frame
        var isEnd = (firstByte & 0x04) != 0;
        // V: Scalability Structure present
        var hasScalabilityStructure = (firstByte & 0x02) != 0;

        // 处理 Picture ID
        if (hasPictureId && offset < payload.Length)
        {
            var pictureId = payload[offset++];
            if ((pictureId & 0x80) != 0 && offset < payload.Length)
            {
                offset++; // 跳过第二个字节
            }
        }

        // 处理 Layer indices
        if (hasLayerIndices && offset < payload.Length)
        {
            offset++;
            if (!isFlexibleMode && offset < payload.Length)
            {
                offset++;
            }
        }

        // 处理 Scalability Structure
        if (hasScalabilityStructure && isStart && offset < payload.Length)
        {
            // 简化处理，跳过可伸缩性结构
            var ssHeader = payload[offset++];
            var numSpatialLayers = ((ssHeader >> 5) & 0x07) + 1;
            var hasY = (ssHeader & 0x10) != 0;
            var hasG = (ssHeader & 0x08) != 0;
            
            if (hasY)
            {
                for (int i = 0; i < numSpatialLayers; i++)
                {
                    if (offset + 3 < payload.Length)
                    {
                        offset += 4; // WIDTH (2) + HEIGHT (2)
                    }
                }
            }
            
            if (hasG && offset < payload.Length)
            {
                var numPicInPg = payload[offset++];
                for (int i = 0; i < numPicInPg && offset + 1 < payload.Length; i++)
                {
                    var pgInfo = payload[offset++];
                    var numRefs = (pgInfo >> 4) & 0x03;
                    offset += numRefs;
                }
            }
        }

        // 提取 VP9 负载
        if (offset < payload.Length)
        {
            var vp9Payload = new byte[payload.Length - offset];
            Array.Copy(payload, offset, vp9Payload, 0, vp9Payload.Length);

            // 检查是否是关键帧 (VP9: P 位为 0 表示关键帧)
            if (isStart)
            {
                _isKeyFrame = !isInterPredicted;
            }

            _fragments.Add(vp9Payload);
        }

        IsFrameComplete = marker || isEnd;
    }

    public byte[]? GetFrame()
    {
        if (_fragments.Count == 0) return null;

        var totalLength = _fragments.Sum(f => f.Length);
        var frame = new byte[totalLength];
        var offset = 0;

        foreach (var fragment in _fragments)
        {
            Array.Copy(fragment, 0, frame, offset, fragment.Length);
            offset += fragment.Length;
        }

        return frame;
    }

    public void Reset()
    {
        _fragments.Clear();
        IsFrameComplete = false;
        _isKeyFrame = false;
    }
}

/// <summary>
/// H264 RTP 解包器 - 将 RTP 包重组为完整的 H264 NAL 单元
/// 参考 RFC 6184
/// </summary>
public class H264Depacketizer
{
    private readonly List<byte[]> _fragments = new();
    private bool _isKeyFrame;
    private byte[]? _currentNalUnit;
    private bool _fragmentationStarted;

    // NAL 起始码
    private static readonly byte[] NAL_START_CODE = { 0x00, 0x00, 0x00, 0x01 };

    public bool IsFrameComplete { get; private set; }
    public bool IsKeyFrame => _isKeyFrame;

    public void AddRtpPacket(byte[] payload, bool marker)
    {
        if (payload == null || payload.Length < 1) return;

        // H264 RTP 负载类型解析 (RFC 6184)
        var nalUnitType = payload[0] & 0x1F;

        switch (nalUnitType)
        {
            case >= 1 and <= 23:
                // 单一 NAL 单元包
                ProcessSingleNalUnit(payload);
                break;
            case 24:
                // STAP-A: 单时间聚合包
                ProcessStapA(payload);
                break;
            case 28:
                // FU-A: 分片单元
                ProcessFuA(payload);
                break;
            case 29:
                // FU-B: 分片单元（带 DON）
                ProcessFuB(payload);
                break;
            default:
                // 其他类型不支持
                break;
        }

        IsFrameComplete = marker;
    }

    private void ProcessSingleNalUnit(byte[] payload)
    {
        // 添加 NAL 起始码
        var nalUnit = new byte[NAL_START_CODE.Length + payload.Length];
        Array.Copy(NAL_START_CODE, 0, nalUnit, 0, NAL_START_CODE.Length);
        Array.Copy(payload, 0, nalUnit, NAL_START_CODE.Length, payload.Length);
        
        _fragments.Add(nalUnit);
        CheckKeyFrame(payload[0] & 0x1F);
    }

    private void ProcessStapA(byte[] payload)
    {
        var offset = 1; // 跳过 STAP-A 头
        
        while (offset + 2 < payload.Length)
        {
            // 读取 NAL 单元大小
            var nalSize = (payload[offset] << 8) | payload[offset + 1];
            offset += 2;
            
            if (offset + nalSize > payload.Length) break;
            
            // 提取 NAL 单元并添加起始码
            var nalUnit = new byte[NAL_START_CODE.Length + nalSize];
            Array.Copy(NAL_START_CODE, 0, nalUnit, 0, NAL_START_CODE.Length);
            Array.Copy(payload, offset, nalUnit, NAL_START_CODE.Length, nalSize);
            
            _fragments.Add(nalUnit);
            CheckKeyFrame(payload[offset] & 0x1F);
            
            offset += nalSize;
        }
    }

    private void ProcessFuA(byte[] payload)
    {
        if (payload.Length < 2) return;
        
        var fuIndicator = payload[0];
        var fuHeader = payload[1];
        
        var isStart = (fuHeader & 0x80) != 0;
        var isEnd = (fuHeader & 0x40) != 0;
        var nalType = fuHeader & 0x1F;
        
        if (isStart)
        {
            // 开始新的分片
            _fragmentationStarted = true;
            
            // 重建 NAL 头
            var nalHeader = (byte)((fuIndicator & 0xE0) | nalType);
            
            _currentNalUnit = new byte[NAL_START_CODE.Length + 1 + (payload.Length - 2)];
            Array.Copy(NAL_START_CODE, 0, _currentNalUnit, 0, NAL_START_CODE.Length);
            _currentNalUnit[NAL_START_CODE.Length] = nalHeader;
            Array.Copy(payload, 2, _currentNalUnit, NAL_START_CODE.Length + 1, payload.Length - 2);
            
            CheckKeyFrame(nalType);
        }
        else if (_fragmentationStarted && _currentNalUnit != null)
        {
            // 继续分片
            var newLength = _currentNalUnit.Length + (payload.Length - 2);
            var newNalUnit = new byte[newLength];
            Array.Copy(_currentNalUnit, 0, newNalUnit, 0, _currentNalUnit.Length);
            Array.Copy(payload, 2, newNalUnit, _currentNalUnit.Length, payload.Length - 2);
            _currentNalUnit = newNalUnit;
        }
        
        if (isEnd && _currentNalUnit != null)
        {
            _fragments.Add(_currentNalUnit);
            _currentNalUnit = null;
            _fragmentationStarted = false;
        }
    }

    private void ProcessFuB(byte[] payload)
    {
        if (payload.Length < 4) return;
        
        // FU-B 与 FU-A 类似，但在开始分片中包含 DON
        var fuIndicator = payload[0];
        var fuHeader = payload[1];
        // DON 在 payload[2..3]
        
        var isStart = (fuHeader & 0x80) != 0;
        var isEnd = (fuHeader & 0x40) != 0;
        var nalType = fuHeader & 0x1F;
        
        var dataOffset = isStart ? 4 : 2;
        
        if (isStart)
        {
            _fragmentationStarted = true;
            var nalHeader = (byte)((fuIndicator & 0xE0) | nalType);
            
            _currentNalUnit = new byte[NAL_START_CODE.Length + 1 + (payload.Length - dataOffset)];
            Array.Copy(NAL_START_CODE, 0, _currentNalUnit, 0, NAL_START_CODE.Length);
            _currentNalUnit[NAL_START_CODE.Length] = nalHeader;
            Array.Copy(payload, dataOffset, _currentNalUnit, NAL_START_CODE.Length + 1, payload.Length - dataOffset);
            
            CheckKeyFrame(nalType);
        }
        else if (_fragmentationStarted && _currentNalUnit != null)
        {
            var newLength = _currentNalUnit.Length + (payload.Length - 2);
            var newNalUnit = new byte[newLength];
            Array.Copy(_currentNalUnit, 0, newNalUnit, 0, _currentNalUnit.Length);
            Array.Copy(payload, 2, newNalUnit, _currentNalUnit.Length, payload.Length - 2);
            _currentNalUnit = newNalUnit;
        }
        
        if (isEnd && _currentNalUnit != null)
        {
            _fragments.Add(_currentNalUnit);
            _currentNalUnit = null;
            _fragmentationStarted = false;
        }
    }

    private void CheckKeyFrame(int nalType)
    {
        // IDR 帧 (NAL type 5) 表示关键帧
        // SPS (7), PPS (8) 通常也伴随关键帧
        if (nalType == 5 || nalType == 7 || nalType == 8)
        {
            _isKeyFrame = true;
        }
    }

    public byte[]? GetFrame()
    {
        if (_fragments.Count == 0) return null;

        var totalLength = _fragments.Sum(f => f.Length);
        var frame = new byte[totalLength];
        var offset = 0;

        foreach (var fragment in _fragments)
        {
            Array.Copy(fragment, 0, frame, offset, fragment.Length);
            offset += fragment.Length;
        }

        return frame;
    }

    public void Reset()
    {
        _fragments.Clear();
        IsFrameComplete = false;
        _isKeyFrame = false;
        _currentNalUnit = null;
        _fragmentationStarted = false;
    }
}
