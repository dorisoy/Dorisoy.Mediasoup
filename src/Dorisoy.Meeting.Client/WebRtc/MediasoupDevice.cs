using Microsoft.Extensions.Logging;
using System.Text.Json;
using Dorisoy.Meeting.Client.Models;

namespace Dorisoy.Meeting.Client.WebRtc;

/// <summary>
/// Mediasoup 设备 - 管理 RTP 能力和设备加载
/// 模拟 mediasoup-client 的 Device 类
/// </summary>
public class MediasoupDevice
{
    private readonly ILogger<MediasoupDevice> _logger;

    /// <summary>
    /// 是否已加载
    /// </summary>
    public bool IsLoaded { get; private set; }

    /// <summary>
    /// Router RTP 能力 (从服务器获取)
    /// </summary>
    public RouterRtpCapabilities? RouterRtpCapabilities { get; private set; }

    /// <summary>
    /// 本地设备的 RTP 能力
    /// </summary>
    public DeviceRtpCapabilities? RtpCapabilities { get; private set; }

    /// <summary>
    /// SCTP 能力
    /// </summary>
    public DeviceSctpCapabilities? SctpCapabilities { get; private set; }

    public MediasoupDevice(ILogger<MediasoupDevice> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 加载设备能力 - 基于 Router 的 RTP 能力
    /// </summary>
    /// <param name="routerRtpCapabilities">从服务器获取的 Router RTP 能力</param>
    public void Load(object routerRtpCapabilities)
    {
        if (IsLoaded)
        {
            _logger.LogWarning("Device already loaded");
            return;
        }

        try
        {
            // 反序列化 Router RTP 能力
            var json = JsonSerializer.Serialize(routerRtpCapabilities);
            RouterRtpCapabilities = JsonSerializer.Deserialize<RouterRtpCapabilities>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (RouterRtpCapabilities == null)
            {
                throw new InvalidOperationException("Failed to parse router RTP capabilities");
            }

            // 生成本地设备的 RTP 能力
            RtpCapabilities = GenerateDeviceRtpCapabilities(RouterRtpCapabilities);
            SctpCapabilities = GenerateSctpCapabilities();

            IsLoaded = true;
            _logger.LogInformation("Device loaded successfully with {CodecCount} codecs",
                RouterRtpCapabilities.Codecs?.Count ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load device");
            throw;
        }
    }

    /// <summary>
    /// 检查设备是否可以生产指定类型的媒体
    /// </summary>
    public bool CanProduce(string kind)
    {
        if (!IsLoaded)
        {
            _logger.LogWarning("Device not loaded");
            return false;
        }

        if (RouterRtpCapabilities?.Codecs == null)
        {
            return false;
        }

        return RouterRtpCapabilities.Codecs.Any(c =>
            c.Kind?.Equals(kind, StringComparison.OrdinalIgnoreCase) == true);
    }

    /// <summary>
    /// 生成本地设备的 RTP 能力
    /// </summary>
    private DeviceRtpCapabilities GenerateDeviceRtpCapabilities(RouterRtpCapabilities routerCapabilities)
    {
        var codecs = new List<DeviceRtpCodecCapability>();

        if (routerCapabilities.Codecs != null)
        {
            foreach (var codec in routerCapabilities.Codecs)
            {
                // 支持的编解码器：VP8, VP9, H264, Opus
                if (IsSupportedCodec(codec.MimeType))
                {
                    codecs.Add(new DeviceRtpCodecCapability
                    {
                        Kind = codec.Kind,
                        MimeType = codec.MimeType,
                        ClockRate = codec.ClockRate,
                        Channels = codec.Channels,
                        Parameters = codec.Parameters,
                        PreferredPayloadType = codec.PreferredPayloadType,
                        RtcpFeedback = codec.RtcpFeedback
                    });
                }
            }
        }

        return new DeviceRtpCapabilities
        {
            Codecs = codecs,
            HeaderExtensions = routerCapabilities.HeaderExtensions?
                .Where(IsSupportedHeaderExtension)
                .ToList() ?? new List<RouterRtpHeaderExtension>()
        };
    }

    /// <summary>
    /// 检查是否支持编解码器
    /// </summary>
    private static bool IsSupportedCodec(string? mimeType)
    {
        if (string.IsNullOrEmpty(mimeType))
            return false;

        var supported = new[]
        {
            "audio/opus",
            "video/VP8",
            "video/VP9",
            "video/H264",
            "video/rtx",
            "audio/rtx"
        };

        return supported.Any(s => mimeType.Equals(s, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 检查是否支持 Header Extension
    /// </summary>
    private bool IsSupportedHeaderExtension(RouterRtpHeaderExtension ext)
    {
        var supported = new[]
        {
            "urn:ietf:params:rtp-hdrext:sdes:mid",
            "urn:ietf:params:rtp-hdrext:sdes:rtp-stream-id",
            "urn:ietf:params:rtp-hdrext:sdes:repaired-rtp-stream-id",
            "http://www.webrtc.org/experiments/rtp-hdrext/abs-send-time",
            "http://www.ietf.org/id/draft-holmer-rmcat-transport-wide-cc-extensions-01",
            "urn:ietf:params:rtp-hdrext:toffset",
            "urn:ietf:params:rtp-hdrext:ssrc-audio-level",
            "urn:3gpp:video-orientation"
        };

        return ext.Uri != null && supported.Any(s => ext.Uri.Equals(s, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 生成 SCTP 能力
    /// </summary>
    private static DeviceSctpCapabilities GenerateSctpCapabilities()
    {
        return new DeviceSctpCapabilities
        {
            NumStreams = new NumSctpStreams
            {
                Os = 1024,
                Mis = 1024
            }
        };
    }
}

#region RTP 能力模型

/// <summary>
/// Router RTP 能力
/// </summary>
public class RouterRtpCapabilities
{
    public List<RouterRtpCodecCapability>? Codecs { get; set; }
    public List<RouterRtpHeaderExtension>? HeaderExtensions { get; set; }
}

/// <summary>
/// Router RTP 编解码器能力
/// </summary>
public class RouterRtpCodecCapability
{
    public string? Kind { get; set; }
    public string? MimeType { get; set; }
    public int? PreferredPayloadType { get; set; }
    public int ClockRate { get; set; }
    public int? Channels { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
    public List<RtcpFeedbackCapability>? RtcpFeedback { get; set; }
}

/// <summary>
/// RTCP 反馈能力
/// </summary>
public class RtcpFeedbackCapability
{
    public string? Type { get; set; }
    public string? Parameter { get; set; }
}

/// <summary>
/// Router RTP Header Extension
/// </summary>
public class RouterRtpHeaderExtension
{
    public string? Kind { get; set; }
    public string? Uri { get; set; }
    public int PreferredId { get; set; }
    public bool PreferredEncrypt { get; set; }
    public string? Direction { get; set; }
}

/// <summary>
/// 设备 RTP 能力
/// </summary>
public class DeviceRtpCapabilities
{
    public List<DeviceRtpCodecCapability> Codecs { get; set; } = new();
    public List<RouterRtpHeaderExtension> HeaderExtensions { get; set; } = new();
}

/// <summary>
/// 设备 RTP 编解码器能力
/// </summary>
public class DeviceRtpCodecCapability
{
    public string? Kind { get; set; }
    public string? MimeType { get; set; }
    public int? PreferredPayloadType { get; set; }
    public int ClockRate { get; set; }
    public int? Channels { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
    public List<RtcpFeedbackCapability>? RtcpFeedback { get; set; }
}

/// <summary>
/// 设备 SCTP 能力
/// </summary>
public class DeviceSctpCapabilities
{
    public NumSctpStreams NumStreams { get; set; } = new();
}

/// <summary>
/// SCTP 流数量
/// </summary>
public class NumSctpStreams
{
    public int Os { get; set; }
    public int Mis { get; set; }
}

#endregion
