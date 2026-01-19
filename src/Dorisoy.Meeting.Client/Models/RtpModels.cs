using System.Collections.Generic;
using System.Text.Json.Serialization;
using FBS.RtpParameters;
using MediasoupTypes = Dorisoy.Mediasoup;

namespace Dorisoy.Meeting.Client.Models;

/// <summary>
/// RTP 参数 - 用于调用 Produce API
/// </summary>
public class ClientRtpParameters
{
    /// <summary>
    /// MID 值
    /// </summary>
    [JsonPropertyName("mid")]
    public string? Mid { get; set; }

    /// <summary>
    /// 编解码器列表
    /// </summary>
    [JsonPropertyName("codecs")]
    public List<ClientRtpCodecParameters> Codecs { get; set; } = new();

    /// <summary>
    /// RTP Header Extensions
    /// </summary>
    [JsonPropertyName("headerExtensions")]
    public List<ClientRtpHeaderExtension>? HeaderExtensions { get; set; }

    /// <summary>
    /// 编码参数
    /// </summary>
    [JsonPropertyName("encodings")]
    public List<ClientRtpEncodingParameters> Encodings { get; set; } = new();

    /// <summary>
    /// RTCP 参数
    /// </summary>
    [JsonPropertyName("rtcp")]
    public ClientRtcpParameters Rtcp { get; set; } = new();
}

/// <summary>
/// RTP 编解码器参数
/// </summary>
public class ClientRtpCodecParameters
{
    /// <summary>
    /// MIME 类型 (如 "video/VP8", "video/H264", "audio/opus")
    /// </summary>
    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = string.Empty;

    /// <summary>
    /// 有效载荷类型
    /// </summary>
    [JsonPropertyName("payloadType")]
    public int PayloadType { get; set; }

    /// <summary>
    /// 时钟频率
    /// </summary>
    [JsonPropertyName("clockRate")]
    public int ClockRate { get; set; }

    /// <summary>
    /// 通道数 (音频)
    /// </summary>
    [JsonPropertyName("channels")]
    public int? Channels { get; set; }

    /// <summary>
    /// 编解码器特定参数
    /// </summary>
    [JsonPropertyName("parameters")]
    public Dictionary<string, object>? Parameters { get; set; }

    /// <summary>
    /// RTCP 反馈
    /// </summary>
    [JsonPropertyName("rtcpFeedback")]
    public List<ClientRtcpFeedback>? RtcpFeedback { get; set; }
}

/// <summary>
/// RTCP 反馈
/// </summary>
public class ClientRtcpFeedback
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("parameter")]
    public string? Parameter { get; set; }
}

/// <summary>
/// RTP Header Extension
/// </summary>
public class ClientRtpHeaderExtension
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("encrypt")]
    public bool Encrypt { get; set; }

    [JsonPropertyName("parameters")]
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// RTP 编码参数
/// </summary>
public class ClientRtpEncodingParameters
{
    /// <summary>
    /// SSRC
    /// </summary>
    [JsonPropertyName("ssrc")]
    public uint Ssrc { get; set; }

    /// <summary>
    /// RTX 信息
    /// </summary>
    [JsonPropertyName("rtx")]
    public ClientRtxInfo? Rtx { get; set; }

    /// <summary>
    /// 最大比特率
    /// </summary>
    [JsonPropertyName("maxBitrate")]
    public int? MaxBitrate { get; set; }

    /// <summary>
    /// DTX (非连续传输)
    /// </summary>
    [JsonPropertyName("dtx")]
    public bool? Dtx { get; set; }
}

/// <summary>
/// RTX 信息
/// </summary>
public class ClientRtxInfo
{
    [JsonPropertyName("ssrc")]
    public uint Ssrc { get; set; }
}

/// <summary>
/// RTCP 参数
/// </summary>
public class ClientRtcpParameters
{
    /// <summary>
    /// CNAME
    /// </summary>
    [JsonPropertyName("cname")]
    public string Cname { get; set; } = string.Empty;

    /// <summary>
    /// 减少尺寸
    /// </summary>
    [JsonPropertyName("reducedSize")]
    public bool ReducedSize { get; set; } = true;
}

/// <summary>
/// Produce 请求
/// </summary>
public class ClientProduceRequest
{
    /// <summary>
    /// 媒体类型: "audio" 或 "video"
    /// </summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// RTP 参数
    /// </summary>
    [JsonPropertyName("rtpParameters")]
    public ClientRtpParameters RtpParameters { get; set; } = new();

    /// <summary>
    /// 媒体源标识 (如 "video:cam", "audio:mic")
    /// </summary>
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// 应用数据
    /// </summary>
    [JsonPropertyName("appData")]
    public Dictionary<string, object>? AppData { get; set; }
}

/// <summary>
/// RTP 参数工厂 - 生成用于 Produce 的 RTP 参数
/// </summary>
public static class RtpParametersFactory
{
    private static readonly Random _random = new();

    /// <summary>
    /// 创建视频 RTP 参数 (VP8)
    /// </summary>
    public static ClientRtpParameters CreateVideoRtpParameters()
    {
        var ssrc = (uint)_random.Next(100000000, 999999999);
        var rtxSsrc = ssrc + 1;

        return new ClientRtpParameters
        {
            Mid = "0",
            Codecs = new List<ClientRtpCodecParameters>
            {
                new ClientRtpCodecParameters
                {
                    MimeType = "video/VP8",
                    PayloadType = 96,
                    ClockRate = 90000,
                    RtcpFeedback = new List<ClientRtcpFeedback>
                    {
                        new ClientRtcpFeedback { Type = "nack" },
                        new ClientRtcpFeedback { Type = "nack", Parameter = "pli" },
                        new ClientRtcpFeedback { Type = "ccm", Parameter = "fir" },
                        new ClientRtcpFeedback { Type = "goog-remb" },
                        new ClientRtcpFeedback { Type = "transport-cc" }
                    }
                },
                new ClientRtpCodecParameters
                {
                    MimeType = "video/rtx",
                    PayloadType = 97,
                    ClockRate = 90000,
                    Parameters = new Dictionary<string, object> { { "apt", 96 } }
                }
            },
            HeaderExtensions = new List<ClientRtpHeaderExtension>
            {
                new ClientRtpHeaderExtension
                {
                    Uri = "urn:ietf:params:rtp-hdrext:sdes:mid",
                    Id = 1
                },
                new ClientRtpHeaderExtension
                {
                    Uri = "http://www.webrtc.org/experiments/rtp-hdrext/abs-send-time",
                    Id = 2
                },
                new ClientRtpHeaderExtension
                {
                    Uri = "http://www.ietf.org/id/draft-holmer-rmcat-transport-wide-cc-extensions-01",
                    Id = 3
                },
                new ClientRtpHeaderExtension
                {
                    Uri = "urn:ietf:params:rtp-hdrext:toffset",
                    Id = 4
                }
            },
            Encodings = new List<ClientRtpEncodingParameters>
            {
                new ClientRtpEncodingParameters
                {
                    Ssrc = ssrc,
                    Rtx = new ClientRtxInfo { Ssrc = rtxSsrc },
                    MaxBitrate = 1500000
                }
            },
            Rtcp = new ClientRtcpParameters
            {
                Cname = $"wpf-client-{Guid.NewGuid():N}",
                ReducedSize = true
            }
        };
    }

    /// <summary>
    /// 创建音频 RTP 参数 (Opus)
    /// </summary>
    public static ClientRtpParameters CreateAudioRtpParameters()
    {
        var ssrc = (uint)_random.Next(100000000, 999999999);

        return new ClientRtpParameters
        {
            Mid = "1",
            Codecs = new List<ClientRtpCodecParameters>
            {
                new ClientRtpCodecParameters
                {
                    MimeType = "audio/opus",
                    PayloadType = 100,
                    ClockRate = 48000,
                    Channels = 2,
                    Parameters = new Dictionary<string, object>
                    {
                        { "minptime", 10 },
                        { "useinbandfec", 1 }
                    },
                    RtcpFeedback = new List<ClientRtcpFeedback>
                    {
                        new ClientRtcpFeedback { Type = "transport-cc" }
                    }
                }
            },
            HeaderExtensions = new List<ClientRtpHeaderExtension>
            {
                new ClientRtpHeaderExtension
                {
                    Uri = "urn:ietf:params:rtp-hdrext:sdes:mid",
                    Id = 1
                },
                new ClientRtpHeaderExtension
                {
                    Uri = "http://www.webrtc.org/experiments/rtp-hdrext/abs-send-time",
                    Id = 2
                },
                new ClientRtpHeaderExtension
                {
                    Uri = "http://www.ietf.org/id/draft-holmer-rmcat-transport-wide-cc-extensions-01",
                    Id = 3
                },
                new ClientRtpHeaderExtension
                {
                    Uri = "urn:ietf:params:rtp-hdrext:ssrc-audio-level",
                    Id = 5
                }
            },
            Encodings = new List<ClientRtpEncodingParameters>
            {
                new ClientRtpEncodingParameters
                {
                    Ssrc = ssrc,
                    Dtx = true
                }
            },
            Rtcp = new ClientRtcpParameters
            {
                Cname = $"wpf-client-{Guid.NewGuid():N}",
                ReducedSize = true
            }
        };
    }

    #region 服务器兼容的 ProduceRequest 工厂方法

    /// <summary>
    /// 创建视频 ProduceRequest (使用服务器端共享类型)
    /// </summary>
    /// <param name="ssrc">视频 SSRC，如果为 0 则自动生成</param>
    /// <param name="codecType">视频编解码器类型</param>
    public static MediasoupTypes.ProduceRequest CreateVideoProduceRequest(uint ssrc = 0, VideoCodecType codecType = VideoCodecType.VP8)
    {
        if (ssrc == 0)
            ssrc = (uint)_random.Next(100000000, 999999999);
        var rtxSsrc = ssrc + 1;

        // 根据编解码器类型获取参数
        var (mimeType, payloadType, rtxPayloadType, parameters) = GetVideoCodecParameters(codecType);

        return new MediasoupTypes.ProduceRequest
        {
            Kind = MediaKind.VIDEO,
            Source = "video:cam",
            AppData = new Dictionary<string, object> { { "source", "video:cam" } },
            RtpParameters = new MediasoupTypes.RtpParameters
            {
                Mid = "0",
                Codecs = new List<MediasoupTypes.RtpCodecParameters>
                {
                    new MediasoupTypes.RtpCodecParameters
                    {
                        MimeType = mimeType,
                        PayloadType = payloadType,
                        ClockRate = 90000,
                        Parameters = parameters,
                        RtcpFeedback = new List<RtcpFeedbackT>
                        {
                            new RtcpFeedbackT { Type = "nack", Parameter = "" },
                            new RtcpFeedbackT { Type = "nack", Parameter = "pli" },
                            new RtcpFeedbackT { Type = "ccm", Parameter = "fir" },
                            new RtcpFeedbackT { Type = "goog-remb", Parameter = "" },
                            new RtcpFeedbackT { Type = "transport-cc", Parameter = "" }
                        }
                    },
                    new MediasoupTypes.RtpCodecParameters
                    {
                        MimeType = "video/rtx",
                        PayloadType = rtxPayloadType,
                        ClockRate = 90000,
                        Parameters = new Dictionary<string, object> { { "apt", payloadType } }
                    }
                },
                HeaderExtensions = new List<MediasoupTypes.RtpHeaderExtensionParameters>
                {
                    new MediasoupTypes.RtpHeaderExtensionParameters { Uri = RtpHeaderExtensionUri.Mid, Id = 1 },
                    new MediasoupTypes.RtpHeaderExtensionParameters { Uri = RtpHeaderExtensionUri.AbsSendTime, Id = 2 },
                    new MediasoupTypes.RtpHeaderExtensionParameters { Uri = RtpHeaderExtensionUri.TransportWideCcDraft01, Id = 3 },
                    new MediasoupTypes.RtpHeaderExtensionParameters { Uri = RtpHeaderExtensionUri.TimeOffset, Id = 4 }
                },
                Encodings = new List<RtpEncodingParametersT>
                {
                    new RtpEncodingParametersT
                    {
                        Ssrc = ssrc,
                        Rtx = new RtxT { Ssrc = rtxSsrc },
                        MaxBitrate = 1500000
                    }
                },
                Rtcp = new RtcpParametersT
                {
                    Cname = $"wpf-client-{Guid.NewGuid():N}",
                    ReducedSize = true
                }
            }
        };
    }

    /// <summary>
    /// 获取视频编解码器参数
    /// </summary>
    private static (string mimeType, byte payloadType, byte rtxPayloadType, Dictionary<string, object>? parameters) GetVideoCodecParameters(VideoCodecType codecType)
    {
        return codecType switch
        {
            VideoCodecType.VP8 => ("video/VP8", 96, 97, null),
            VideoCodecType.VP9 => ("video/VP9", 103, 104, new Dictionary<string, object> { { "profile-id", 0 } }),
            VideoCodecType.H264 => ("video/H264", 105, 106, new Dictionary<string, object>
            {
                { "level-asymmetry-allowed", 1 },
                { "packetization-mode", 1 },
                { "profile-level-id", "42e01f" }
            }),
            _ => ("video/VP8", 96, 97, null)
        };
    }

    /// <summary>
    /// 创建音频 ProduceRequest (使用服务器端共享类型)
    /// </summary>
    /// <param name="ssrc">音频 SSRC，如果为 0 则自动生成</param>
    public static MediasoupTypes.ProduceRequest CreateAudioProduceRequest(uint ssrc = 0)
    {
        if (ssrc == 0)
            ssrc = (uint)_random.Next(100000000, 999999999);

        return new MediasoupTypes.ProduceRequest
        {
            Kind = MediaKind.AUDIO,
            Source = "audio:mic",
            AppData = new Dictionary<string, object> { { "source", "audio:mic" } },
            RtpParameters = new MediasoupTypes.RtpParameters
            {
                Mid = "1",
                Codecs = new List<MediasoupTypes.RtpCodecParameters>
                {
                    new MediasoupTypes.RtpCodecParameters
                    {
                        MimeType = "audio/opus",
                        PayloadType = 100,
                        ClockRate = 48000,
                        Channels = 2,
                        Parameters = new Dictionary<string, object>
                        {
                            { "minptime", 10 },
                            { "useinbandfec", 1 }
                        },
                        RtcpFeedback = new List<RtcpFeedbackT>
                        {
                            new RtcpFeedbackT { Type = "transport-cc", Parameter = "" }
                        }
                    }
                },
                HeaderExtensions = new List<MediasoupTypes.RtpHeaderExtensionParameters>
                {
                    new MediasoupTypes.RtpHeaderExtensionParameters { Uri = RtpHeaderExtensionUri.Mid, Id = 1 },
                    new MediasoupTypes.RtpHeaderExtensionParameters { Uri = RtpHeaderExtensionUri.AbsSendTime, Id = 2 },
                    new MediasoupTypes.RtpHeaderExtensionParameters { Uri = RtpHeaderExtensionUri.TransportWideCcDraft01, Id = 3 },
                    new MediasoupTypes.RtpHeaderExtensionParameters { Uri = RtpHeaderExtensionUri.AudioLevel, Id = 5 }
                },
                Encodings = new List<RtpEncodingParametersT>
                {
                    new RtpEncodingParametersT
                    {
                        Ssrc = ssrc,
                        Dtx = true
                    }
                },
                Rtcp = new RtcpParametersT
                {
                    Cname = $"wpf-client-{Guid.NewGuid():N}",
                    ReducedSize = true
                }
            }
        };
    }

    #endregion
}
