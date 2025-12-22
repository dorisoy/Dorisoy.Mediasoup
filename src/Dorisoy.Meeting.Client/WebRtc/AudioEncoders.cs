using Concentus.Structs;
using Concentus.Enums;
using Microsoft.Extensions.Logging;

namespace Dorisoy.Meeting.Client.WebRtc;

/// <summary>
/// Opus 音频编码器 - 使用 Concentus 编码 Opus
/// 用于将本地采集的音频编码后发送到 mediasoup 服务器
/// </summary>
public class OpusEncoder : IDisposable
{
    private readonly ILogger _logger;
    
#pragma warning disable CS0618 // 使用旧版 Concentus API
    private Concentus.Structs.OpusEncoder? _encoder;
#pragma warning restore CS0618
    
    private bool _initialized;
    private bool _disposed;

    /// <summary>
    /// 采样率 (Hz)
    /// </summary>
    public int SampleRate { get; }

    /// <summary>
    /// 通道数
    /// </summary>
    public int Channels { get; }

    /// <summary>
    /// 帧大小 (采样数)
    /// </summary>
    public int FrameSize { get; }

    /// <summary>
    /// 比特率 (bps)
    /// </summary>
    public int Bitrate { get; set; } = 64000;

    /// <summary>
    /// 编码后的帧事件 (Opus 数据)
    /// </summary>
    public event Action<byte[]>? OnFrameEncoded;

    /// <summary>
    /// 创建 Opus 编码器
    /// </summary>
    /// <param name="logger">日志</param>
    /// <param name="sampleRate">采样率，默认 48000 Hz</param>
    /// <param name="channels">通道数，默认 1 (单声道)</param>
    /// <param name="frameSize">帧大小，默认 960 (20ms @ 48kHz)</param>
    public OpusEncoder(
        ILogger logger,
        int sampleRate = 48000,
        int channels = 1,
        int frameSize = 960)
    {
        _logger = logger;
        SampleRate = sampleRate;
        Channels = channels;
        FrameSize = frameSize;
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
#pragma warning disable CS0618 // 使用旧版 Concentus API
            _encoder = new Concentus.Structs.OpusEncoder(SampleRate, Channels, OpusApplication.OPUS_APPLICATION_VOIP);
            _encoder.Bitrate = Bitrate;
            _encoder.Complexity = 5; // 0-10, 平衡质量和 CPU 使用
            _encoder.SignalType = OpusSignal.OPUS_SIGNAL_VOICE;
            _encoder.UseVBR = true;
            _encoder.UseInbandFEC = true;
#pragma warning restore CS0618

            _initialized = true;
            _logger.LogInformation("Opus encoder initialized: {SampleRate}Hz, {Channels}ch, {FrameSize} samples/frame, {Bitrate}bps",
                SampleRate, Channels, FrameSize, Bitrate);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Opus encoder");
            return false;
        }
    }

    /// <summary>
    /// 编码 PCM 音频数据 (short[])
    /// </summary>
    /// <param name="pcmData">PCM 采样数据</param>
    /// <returns>是否编码成功</returns>
    public bool Encode(short[] pcmData)
    {
        if (!_initialized || _disposed || _encoder == null)
            return false;

        try
        {
            // 验证输入大小
            var expectedSamples = FrameSize * Channels;
            if (pcmData.Length < expectedSamples)
            {
                _logger.LogTrace("PCM data too short: {Actual} < {Expected}", pcmData.Length, expectedSamples);
                return false;
            }

            // 编码缓冲区
            var outputBuffer = new byte[4000]; // Opus 最大帧大小

#pragma warning disable CS0618 // 使用旧版 Concentus API
            var encodedLength = _encoder.Encode(pcmData, 0, FrameSize, outputBuffer, 0, outputBuffer.Length);
#pragma warning restore CS0618

            if (encodedLength > 0)
            {
                var encodedData = new byte[encodedLength];
                Array.Copy(outputBuffer, encodedData, encodedLength);
                OnFrameEncoded?.Invoke(encodedData);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error encoding Opus frame");
            return false;
        }
    }

    /// <summary>
    /// 编码 PCM 音频数据 (byte[] - 16-bit little-endian)
    /// </summary>
    /// <param name="pcmBytes">PCM 字节数据 (16-bit little-endian)</param>
    /// <returns>是否编码成功</returns>
    public bool EncodeBytes(byte[] pcmBytes)
    {
        if (!_initialized || _disposed)
            return false;

        try
        {
            // 转换 byte[] 到 short[]
            var samples = new short[pcmBytes.Length / 2];
            Buffer.BlockCopy(pcmBytes, 0, samples, 0, pcmBytes.Length);
            return Encode(samples);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error encoding Opus frame from bytes");
            return false;
        }
    }

    /// <summary>
    /// 设置编码器比特率
    /// </summary>
    public void SetBitrate(int bitrate)
    {
        Bitrate = bitrate;
        if (_encoder != null)
        {
#pragma warning disable CS0618
            _encoder.Bitrate = bitrate;
#pragma warning restore CS0618
            _logger.LogInformation("Opus encoder bitrate changed to {Bitrate}bps", bitrate);
        }
    }

    /// <summary>
    /// 设置编码器复杂度 (0-10)
    /// </summary>
    public void SetComplexity(int complexity)
    {
        if (_encoder != null && complexity >= 0 && complexity <= 10)
        {
#pragma warning disable CS0618
            _encoder.Complexity = complexity;
#pragma warning restore CS0618
            _logger.LogInformation("Opus encoder complexity changed to {Complexity}", complexity);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _encoder = null;
            _disposed = true;
            _logger.LogInformation("Opus encoder disposed");
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 音频帧打包器 - 将 NAudio 的音频数据打包成固定大小的帧
/// </summary>
public class AudioFramePacketizer : IDisposable
{
    private readonly ILogger _logger;
    private readonly OpusEncoder _encoder;
    private readonly List<short> _buffer = new();
    private readonly int _frameSize;
    private readonly int _channels;
    private bool _disposed;

    /// <summary>
    /// 编码后的帧事件
    /// </summary>
    public event Action<byte[]>? OnFrameReady;

    public AudioFramePacketizer(ILogger logger, int sampleRate = 48000, int channels = 1)
    {
        _logger = logger;
        _channels = channels;
        _frameSize = sampleRate * 20 / 1000; // 20ms 帧
        
        _encoder = new OpusEncoder(logger, sampleRate, channels, _frameSize);
        _encoder.OnFrameEncoded += data => OnFrameReady?.Invoke(data);
    }

    /// <summary>
    /// 初始化
    /// </summary>
    public bool Initialize()
    {
        return _encoder.Initialize();
    }

    /// <summary>
    /// 添加 PCM 采样数据
    /// </summary>
    public void AddSamples(short[] samples)
    {
        if (_disposed) return;

        _buffer.AddRange(samples);
        
        // 处理完整的帧
        var samplesPerFrame = _frameSize * _channels;
        while (_buffer.Count >= samplesPerFrame)
        {
            var frame = _buffer.Take(samplesPerFrame).ToArray();
            _buffer.RemoveRange(0, samplesPerFrame);
            _encoder.Encode(frame);
        }
    }

    /// <summary>
    /// 添加 PCM 字节数据
    /// </summary>
    public void AddBytes(byte[] bytes)
    {
        if (_disposed) return;

        var samples = new short[bytes.Length / 2];
        Buffer.BlockCopy(bytes, 0, samples, 0, bytes.Length);
        AddSamples(samples);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _encoder.Dispose();
            _buffer.Clear();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
