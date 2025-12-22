using Microsoft.Extensions.Logging;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using Concentus.Structs;
using Concentus.Enums;
using System.Collections.Concurrent;

namespace Dorisoy.Meeting.Client.WebRtc;

/// <summary>
/// RTP 媒体解码器 - 处理视频/音频 RTP 包的解包和解码
/// </summary>
public class RtpMediaDecoder : IDisposable
{
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private bool _disposed;

    // VP8 解包器
    private readonly ConcurrentDictionary<string, Vp8Depacketizer> _videoDepacketizers = new();
    
    // VP8 解码器 (使用 FFmpeg)
    private readonly ConcurrentDictionary<string, Vp8Decoder> _videoDecoders = new();

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
    /// 处理视频 RTP 包
    /// </summary>
    public void ProcessVideoRtpPacket(string consumerId, RTPPacket rtpPacket)
    {
        try
        {
            _logger.LogDebug("Processing video RTP: ConsumerId={ConsumerId}, Seq={Seq}, Marker={Marker}",
                consumerId, rtpPacket.Header.SequenceNumber, rtpPacket.Header.MarkerBit);
            // 获取或创建解包器
            if (!_videoDepacketizers.TryGetValue(consumerId, out var depacketizer))
            {
                depacketizer = new Vp8Depacketizer();
                _videoDepacketizers[consumerId] = depacketizer;
                _logger.LogDebug("Created VP8 depacketizer for consumer {ConsumerId}", consumerId);
            }

            // 解包 RTP 负载
            var payload = rtpPacket.Payload;
            var marker = rtpPacket.Header.MarkerBit == 1;

            // 添加 RTP 包到解包器
            depacketizer.AddRtpPacket(payload, marker);

            // 如果帧完整，解码
            if (marker && depacketizer.IsFrameComplete)
            {
                var frameData = depacketizer.GetFrame();
                if (frameData != null && frameData.Length > 0)
                {
                    DecodeVideoFrame(consumerId, frameData);
                }
                depacketizer.Reset();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing video RTP packet for consumer {ConsumerId}", consumerId);
        }
    }

    /// <summary>
    /// 解码视频帧 - 使用 FFmpeg VP8 解码器
    /// </summary>
    private void DecodeVideoFrame(string consumerId, byte[] frameData)
    {
        try
        {
            // 发送 VP8 帧数据事件
            var depacketizer = _videoDepacketizers.GetValueOrDefault(consumerId);
            OnVp8FrameReceived?.Invoke(consumerId, frameData, depacketizer?.IsKeyFrame ?? false);
            
            // 获取或创建 VP8 解码器
            if (!_videoDecoders.TryGetValue(consumerId, out var decoder))
            {
                decoder = new Vp8Decoder(_loggerFactory?.CreateLogger<Vp8Decoder>() ?? _logger);
                decoder.OnFrameDecoded += (bgrData, width, height) =>
                {
                    OnDecodedVideoFrame?.Invoke(consumerId, bgrData, width, height);
                };
                _videoDecoders[consumerId] = decoder;
                _logger.LogInformation("Created VP8 decoder for consumer {ConsumerId}", consumerId);
            }
            
            // 解码 VP8 帧
            var success = decoder.Decode(frameData);
            if (success)
            {
                _logger.LogTrace("VP8 frame decoded for consumer {ConsumerId}", consumerId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Video decode failed for consumer {ConsumerId}", consumerId);
        }
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
            // 释放 VP8 解码器
            foreach (var decoder in _videoDecoders.Values)
            {
                decoder.Dispose();
            }
            _videoDecoders.Clear();
            
            _videoDepacketizers.Clear();
            _audioDecoders.Clear();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// VP8 RTP 解包器 - 将 RTP 包重组为完整的 VP8 帧
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
