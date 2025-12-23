using System.Text;
using Dorisoy.Meeting.Client.Models;

namespace Dorisoy.Meeting.Client.WebRtc;

/// <summary>
/// Mediasoup SDP 构建器 - 将 mediasoup RTP 参数转换为标准 SDP
/// 实现 mediasoup-client 的核心 SDP 转换逻辑
/// </summary>
public static class MediasoupSdpBuilder
{
    /// <summary>
    /// 为 Recv Transport 生成 SDP Offer
    /// 用于接收远端媒体 (Consumer)
    /// </summary>
    public static string BuildRecvSdpOffer(
        IceParameters iceParameters,
        List<IceCandidate> iceCandidates,
        DtlsParameters dtlsParameters,
        List<ConsumerInfo> consumers)
    {
        var sb = new StringBuilder();

        // SDP 版本
        sb.AppendLine("v=0");

        // Origin
        var sessionId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        sb.AppendLine($"o=- {sessionId} 1 IN IP4 0.0.0.0");

        // Session name
        sb.AppendLine("s=mediasoup-client");

        // Timing
        sb.AppendLine("t=0 0");

        // ICE options
        if (iceParameters.IceLite)
        {
            sb.AppendLine("a=ice-lite");
        }

        // Bundle - 所有 mid 放在一起
        var mids = consumers.Select((c, i) => i.ToString()).ToList();
        if (mids.Any())
        {
            sb.AppendLine($"a=group:BUNDLE {string.Join(" ", mids)}");
        }

        // msid-semantic
        sb.AppendLine("a=msid-semantic: WMS *");

        // 为每个 Consumer 生成 m= 行
        for (int i = 0; i < consumers.Count; i++)
        {
            var consumer = consumers[i];
            var mid = i.ToString();

            AppendMediaSection(sb, consumer, mid, iceParameters, iceCandidates, dtlsParameters);
        }

        return sb.ToString();
    }

    /// <summary>
    /// 为 Send Transport 生成 SDP Answer
    /// 用于发送本地媒体 (Producer)
    /// </summary>
    public static string BuildSendSdpAnswer(
        IceParameters iceParameters,
        DtlsParameters dtlsParameters,
        string localSdpOffer)
    {
        // 解析本地 offer，修改为 answer
        var lines = localSdpOffer.Split('\n');
        var sb = new StringBuilder();

        foreach (var line in lines)
        {
            var trimmedLine = line.TrimEnd('\r');

            // 修改 a=setup 为 passive (因为我们是客户端)
            if (trimmedLine.StartsWith("a=setup:"))
            {
                sb.AppendLine("a=setup:passive");
            }
            // 替换 ICE 凭证
            else if (trimmedLine.StartsWith("a=ice-ufrag:"))
            {
                sb.AppendLine($"a=ice-ufrag:{iceParameters.UsernameFragment}");
            }
            else if (trimmedLine.StartsWith("a=ice-pwd:"))
            {
                sb.AppendLine($"a=ice-pwd:{iceParameters.Password}");
            }
            else
            {
                sb.AppendLine(trimmedLine);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// 生成远端 Answer SDP (服务器回复)
    /// </summary>
    public static string BuildRemoteAnswer(
        IceParameters iceParameters,
        List<IceCandidate> iceCandidates,
        DtlsParameters dtlsParameters,
        List<ConsumerInfo> consumers)
    {
        var sb = new StringBuilder();

        // SDP 版本
        sb.AppendLine("v=0");

        // Origin
        var sessionId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        sb.AppendLine($"o=- {sessionId} 1 IN IP4 0.0.0.0");

        // Session name
        sb.AppendLine("s=mediasoup-server");

        // Timing
        sb.AppendLine("t=0 0");

        // ICE lite
        if (iceParameters.IceLite)
        {
            sb.AppendLine("a=ice-lite");
        }

        // Bundle
        var mids = consumers.Select((c, i) => i.ToString()).ToList();
        if (mids.Any())
        {
            sb.AppendLine($"a=group:BUNDLE {string.Join(" ", mids)}");
        }

        sb.AppendLine("a=msid-semantic: WMS *");

        // 为每个 Consumer 生成 m= 行
        for (int i = 0; i < consumers.Count; i++)
        {
            var consumer = consumers[i];
            var mid = i.ToString();

            AppendMediaSectionAnswer(sb, consumer, mid, iceParameters, iceCandidates, dtlsParameters);
        }

        return sb.ToString();
    }

    /// <summary>
    /// 添加媒体节 (Offer)
    /// </summary>
    private static void AppendMediaSection(
        StringBuilder sb,
        ConsumerInfo consumer,
        string mid,
        IceParameters iceParameters,
        List<IceCandidate> iceCandidates,
        DtlsParameters dtlsParameters)
    {
        var isVideo = consumer.Kind == "video";
        var codec = consumer.RtpParameters?.Codecs?.FirstOrDefault();
        var encoding = consumer.RtpParameters?.Encodings?.FirstOrDefault();

        if (codec == null) return;

        // m= 行
        var payloadTypes = consumer.RtpParameters!.Codecs!.Select(c => c.PayloadType).ToList();
        var port = iceCandidates.FirstOrDefault()?.Port ?? 9;

        if (isVideo)
        {
            sb.AppendLine($"m=video {port} UDP/TLS/RTP/SAVPF {string.Join(" ", payloadTypes)}");
        }
        else
        {
            sb.AppendLine($"m=audio {port} UDP/TLS/RTP/SAVPF {string.Join(" ", payloadTypes)}");
        }

        // Connection
        var ip = iceCandidates.FirstOrDefault()?.Ip ?? "0.0.0.0";
        sb.AppendLine($"c=IN IP4 {ip}");

        // ICE credentials
        sb.AppendLine($"a=ice-ufrag:{iceParameters.UsernameFragment}");
        sb.AppendLine($"a=ice-pwd:{iceParameters.Password}");

        // ICE candidates
        foreach (var candidate in iceCandidates)
        {
            var candidateStr = BuildIceCandidateString(candidate);
            sb.AppendLine($"a={candidateStr}");
        }

        // DTLS fingerprint
        var fingerprint = dtlsParameters.Fingerprints?.FirstOrDefault();
        if (fingerprint != null)
        {
            sb.AppendLine($"a=fingerprint:{fingerprint.Algorithm} {fingerprint.Value}");
        }

        // DTLS setup
        var role = dtlsParameters.Role ?? "actpass";
        var setup = role == "client" ? "active" : (role == "server" ? "passive" : "actpass");
        sb.AppendLine($"a=setup:{setup}");

        // Mid
        sb.AppendLine($"a=mid:{mid}");

        // Direction - recvonly for Consumer
        sb.AppendLine("a=recvonly");

        // RTP/RTCP mux
        sb.AppendLine("a=rtcp-mux");
        sb.AppendLine("a=rtcp-rsize");

        // Codecs
        foreach (var c in consumer.RtpParameters!.Codecs!)
        {
            AppendCodecLines(sb, c, isVideo);
        }

        // SSRC
        if (encoding?.Ssrc > 0)
        {
            var cname = consumer.RtpParameters.Rtcp?.Cname ?? $"consumer-{consumer.ConsumerId}";
            sb.AppendLine($"a=ssrc:{encoding.Ssrc} cname:{cname}");
            sb.AppendLine($"a=ssrc:{encoding.Ssrc} msid:mediasoup {mid}");
        }

        // Header extensions
        if (consumer.RtpParameters.HeaderExtensions != null)
        {
            foreach (var ext in consumer.RtpParameters.HeaderExtensions)
            {
                sb.AppendLine($"a=extmap:{ext.Id} {ext.Uri}");
            }
        }
    }

    /// <summary>
    /// 添加媒体节 (Answer)
    /// </summary>
    private static void AppendMediaSectionAnswer(
        StringBuilder sb,
        ConsumerInfo consumer,
        string mid,
        IceParameters iceParameters,
        List<IceCandidate> iceCandidates,
        DtlsParameters dtlsParameters)
    {
        var isVideo = consumer.Kind == "video";
        var codec = consumer.RtpParameters?.Codecs?.FirstOrDefault();
        var encoding = consumer.RtpParameters?.Encodings?.FirstOrDefault();

        if (codec == null) return;

        // m= 行
        var payloadTypes = consumer.RtpParameters!.Codecs!.Select(c => c.PayloadType).ToList();
        var port = iceCandidates.FirstOrDefault()?.Port ?? 9;

        if (isVideo)
        {
            sb.AppendLine($"m=video {port} UDP/TLS/RTP/SAVPF {string.Join(" ", payloadTypes)}");
        }
        else
        {
            sb.AppendLine($"m=audio {port} UDP/TLS/RTP/SAVPF {string.Join(" ", payloadTypes)}");
        }

        // Connection
        var ip = iceCandidates.FirstOrDefault()?.Ip ?? "0.0.0.0";
        sb.AppendLine($"c=IN IP4 {ip}");

        // ICE credentials
        sb.AppendLine($"a=ice-ufrag:{iceParameters.UsernameFragment}");
        sb.AppendLine($"a=ice-pwd:{iceParameters.Password}");

        // ICE candidates
        foreach (var candidate in iceCandidates)
        {
            var candidateStr = BuildIceCandidateString(candidate);
            sb.AppendLine($"a={candidateStr}");
        }

        // DTLS fingerprint
        var fingerprint = dtlsParameters.Fingerprints?.FirstOrDefault();
        if (fingerprint != null)
        {
            sb.AppendLine($"a=fingerprint:{fingerprint.Algorithm} {fingerprint.Value}");
        }

        // DTLS setup - server side is passive
        sb.AppendLine("a=setup:passive");

        // Mid
        sb.AppendLine($"a=mid:{mid}");

        // Direction - sendonly for server (Consumer's perspective: server sends to client)
        sb.AppendLine("a=sendonly");

        // RTP/RTCP mux
        sb.AppendLine("a=rtcp-mux");
        sb.AppendLine("a=rtcp-rsize");

        // Codecs
        foreach (var c in consumer.RtpParameters!.Codecs!)
        {
            AppendCodecLines(sb, c, isVideo);
        }

        // SSRC
        if (encoding?.Ssrc > 0)
        {
            var cname = consumer.RtpParameters.Rtcp?.Cname ?? $"consumer-{consumer.ConsumerId}";
            sb.AppendLine($"a=ssrc:{encoding.Ssrc} cname:{cname}");
            sb.AppendLine($"a=ssrc:{encoding.Ssrc} msid:mediasoup {mid}");
        }

        // Header extensions
        if (consumer.RtpParameters.HeaderExtensions != null)
        {
            foreach (var ext in consumer.RtpParameters.HeaderExtensions)
            {
                sb.AppendLine($"a=extmap:{ext.Id} {ext.Uri}");
            }
        }
    }

    /// <summary>
    /// 添加编解码器行
    /// </summary>
    private static void AppendCodecLines(StringBuilder sb, ConsumerRtpCodec codec, bool isVideo)
    {
        var pt = codec.PayloadType;
        var mimeType = codec.MimeType ?? (isVideo ? "video/VP8" : "audio/opus");
        var codecName = mimeType.Split('/').LastOrDefault() ?? "VP8";
        var clockRate = codec.ClockRate;

        if (isVideo)
        {
            sb.AppendLine($"a=rtpmap:{pt} {codecName}/{clockRate}");
        }
        else
        {
            var channels = codec.Channels ?? 2;
            sb.AppendLine($"a=rtpmap:{pt} {codecName}/{clockRate}/{channels}");
        }

        // fmtp 参数
        if (codec.Parameters != null && codec.Parameters.Any())
        {
            var fmtpParams = string.Join(";", codec.Parameters.Select(p => $"{p.Key}={p.Value}"));
            sb.AppendLine($"a=fmtp:{pt} {fmtpParams}");
        }

        // RTCP feedback (仅视频)
        if (isVideo)
        {
            sb.AppendLine($"a=rtcp-fb:{pt} nack");
            sb.AppendLine($"a=rtcp-fb:{pt} nack pli");
            sb.AppendLine($"a=rtcp-fb:{pt} ccm fir");
            sb.AppendLine($"a=rtcp-fb:{pt} goog-remb");
            sb.AppendLine($"a=rtcp-fb:{pt} transport-cc");
        }
    }

    /// <summary>
    /// 构建 ICE candidate 字符串
    /// </summary>
    private static string BuildIceCandidateString(IceCandidate candidate)
    {
        var foundation = candidate.Foundation ?? "0";
        var priority = candidate.Priority ?? 1;
        var protocol = candidate.Protocol?.ToLower() ?? "udp";
        var type = candidate.Type?.ToLower() ?? "host";

        var candidateStr = $"candidate:{foundation} 1 {protocol} {priority} {candidate.Ip} {candidate.Port} typ {type}";

        if (!string.IsNullOrEmpty(candidate.TcpType))
        {
            candidateStr += $" tcptype {candidate.TcpType}";
        }

        return candidateStr;
    }

    /// <summary>
    /// 构建本地 Offer SDP (用于 Send Transport)
    /// </summary>
    public static string BuildLocalOffer(
        string mid,
        string kind,
        uint ssrc,
        string codecName,
        int payloadType,
        int clockRate,
        int? channels = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine("v=0");
        sb.AppendLine($"o=- {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()} 1 IN IP4 0.0.0.0");
        sb.AppendLine("s=wpf-client");
        sb.AppendLine("t=0 0");
        sb.AppendLine($"a=group:BUNDLE {mid}");
        sb.AppendLine("a=msid-semantic: WMS *");

        // m= 行
        if (kind == "video")
        {
            sb.AppendLine($"m=video 9 UDP/TLS/RTP/SAVPF {payloadType}");
            sb.AppendLine("c=IN IP4 0.0.0.0");
            sb.AppendLine("a=sendonly");
            sb.AppendLine($"a=mid:{mid}");
            sb.AppendLine($"a=rtpmap:{payloadType} {codecName}/{clockRate}");
            sb.AppendLine($"a=rtcp-fb:{payloadType} nack");
            sb.AppendLine($"a=rtcp-fb:{payloadType} nack pli");
            sb.AppendLine($"a=rtcp-fb:{payloadType} ccm fir");
            sb.AppendLine($"a=rtcp-fb:{payloadType} goog-remb");
        }
        else
        {
            var ch = channels ?? 2;
            sb.AppendLine($"m=audio 9 UDP/TLS/RTP/SAVPF {payloadType}");
            sb.AppendLine("c=IN IP4 0.0.0.0");
            sb.AppendLine("a=sendonly");
            sb.AppendLine($"a=mid:{mid}");
            sb.AppendLine($"a=rtpmap:{payloadType} {codecName}/{clockRate}/{ch}");
            if (codecName.ToLower() == "opus")
            {
                sb.AppendLine($"a=fmtp:{payloadType} minptime=10;useinbandfec=1");
            }
        }

        sb.AppendLine("a=rtcp-mux");
        sb.AppendLine("a=rtcp-rsize");
        sb.AppendLine($"a=ssrc:{ssrc} cname:wpf-{Guid.NewGuid():N}");
        sb.AppendLine("a=setup:actpass");
        sb.AppendLine("a=ice-ufrag:placeholder");
        sb.AppendLine("a=ice-pwd:placeholder");

        return sb.ToString();
    }

    /// <summary>
    /// 为 Send Transport 生成远端 SDP Offer
    /// 服务器作为 recvonly，客户端作为 sendonly
    /// 
    /// 关键：PayloadType 必须与以下保持一致：
    /// - RtpModels.cs 中的 CreateVideoProduceRequest 和 CreateAudioProduceRequest
    /// - MediasoupTransport.cs 中的 RTP 发送方法
    /// - MediasoupTransport.cs 中的 AddSendTracks() 创建的 track
    /// </summary>
    public static string BuildSendTransportRemoteOffer(
        IceParameters iceParameters,
        List<IceCandidate> iceCandidates,
        DtlsParameters dtlsParameters,
        VideoCodecType videoCodec = VideoCodecType.VP8)
    {
        // 根据编解码器类型选择 Payload Type 和名称
        var (videoPayloadType, videoCodecName, videoRtxPayloadType) = GetVideoCodecInfo(videoCodec);
        const int AUDIO_PAYLOAD_TYPE = 100;  // Opus

        var sb = new StringBuilder();

        // SDP 版本
        sb.AppendLine("v=0");
        sb.AppendLine($"o=- {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()} 1 IN IP4 0.0.0.0");
        sb.AppendLine("s=mediasoup-server");
        sb.AppendLine("t=0 0");
        sb.AppendLine("a=ice-lite");
        sb.AppendLine("a=group:BUNDLE 0 1");
        sb.AppendLine("a=msid-semantic: WMS *");

        // 获取端口
        var port = iceCandidates?.FirstOrDefault()?.Port ?? 44444;
        var ip = iceCandidates?.FirstOrDefault()?.Ip ?? "0.0.0.0";

        // Video m= section (recvonly from server perspective)
        sb.AppendLine($"m=video {port} UDP/TLS/RTP/SAVPF {videoPayloadType} {videoRtxPayloadType}");
        sb.AppendLine($"c=IN IP4 {ip}");
        AppendIceAndDtls(sb, iceParameters, iceCandidates, dtlsParameters);
        sb.AppendLine("a=mid:0");
        sb.AppendLine("a=recvonly");
        sb.AppendLine("a=rtcp-mux");
        sb.AppendLine("a=rtcp-rsize");
        sb.AppendLine($"a=rtpmap:{videoPayloadType} {videoCodecName}/90000");
        
        // 添加 fmtp 参数（VP9 和 H264 需要）
        AppendVideoFmtp(sb, videoCodec, videoPayloadType);
        
        sb.AppendLine($"a=rtcp-fb:{videoPayloadType} nack");
        sb.AppendLine($"a=rtcp-fb:{videoPayloadType} nack pli");
        sb.AppendLine($"a=rtcp-fb:{videoPayloadType} ccm fir");
        sb.AppendLine($"a=rtcp-fb:{videoPayloadType} goog-remb");
        sb.AppendLine($"a=rtcp-fb:{videoPayloadType} transport-cc");
        sb.AppendLine($"a=rtpmap:{videoRtxPayloadType} rtx/90000");
        sb.AppendLine($"a=fmtp:{videoRtxPayloadType} apt={videoPayloadType}");
        sb.AppendLine("a=extmap:4 http://www.webrtc.org/experiments/rtp-hdrext/abs-send-time");

        // Audio m= section (recvonly from server perspective)
        sb.AppendLine($"m=audio {port} UDP/TLS/RTP/SAVPF {AUDIO_PAYLOAD_TYPE}");
        sb.AppendLine($"c=IN IP4 {ip}");
        AppendIceAndDtls(sb, iceParameters, iceCandidates, dtlsParameters);
        sb.AppendLine("a=mid:1");
        sb.AppendLine("a=recvonly");
        sb.AppendLine("a=rtcp-mux");
        sb.AppendLine("a=rtcp-rsize");
        sb.AppendLine($"a=rtpmap:{AUDIO_PAYLOAD_TYPE} opus/48000/2");
        sb.AppendLine($"a=fmtp:{AUDIO_PAYLOAD_TYPE} minptime=10;useinbandfec=1");
        sb.AppendLine("a=extmap:4 http://www.webrtc.org/experiments/rtp-hdrext/abs-send-time");

        return sb.ToString();
    }

    /// <summary>
    /// 获取视频编解码器信息
    /// </summary>
    private static (int payloadType, string codecName, int rtxPayloadType) GetVideoCodecInfo(VideoCodecType codecType)
    {
        return codecType switch
        {
            VideoCodecType.VP8 => (96, "VP8", 97),
            VideoCodecType.VP9 => (103, "VP9", 104),
            VideoCodecType.H264 => (105, "H264", 106),
            _ => (96, "VP8", 97)
        };
    }

    /// <summary>
    /// 添加视频 fmtp 参数
    /// </summary>
    private static void AppendVideoFmtp(StringBuilder sb, VideoCodecType codecType, int payloadType)
    {
        switch (codecType)
        {
            case VideoCodecType.VP9:
                sb.AppendLine($"a=fmtp:{payloadType} profile-id=0");
                break;
            case VideoCodecType.H264:
                sb.AppendLine($"a=fmtp:{payloadType} level-asymmetry-allowed=1;packetization-mode=1;profile-level-id=42e01f");
                break;
            // VP8 不需要 fmtp
        }
    }

    /// <summary>
    /// 辅助方法：添加 ICE 和 DTLS 参数
    /// </summary>
    private static void AppendIceAndDtls(
        StringBuilder sb,
        IceParameters iceParameters,
        List<IceCandidate>? iceCandidates,
        DtlsParameters dtlsParameters)
    {
        // ICE 参数
        sb.AppendLine($"a=ice-ufrag:{iceParameters.UsernameFragment}");
        sb.AppendLine($"a=ice-pwd:{iceParameters.Password}");

        // ICE candidates
        if (iceCandidates != null)
        {
            foreach (var candidate in iceCandidates)
            {
                sb.AppendLine($"a={BuildIceCandidateString(candidate)}");
            }
        }

        // DTLS fingerprint
        var fingerprint = dtlsParameters.Fingerprints?.FirstOrDefault();
        if (fingerprint != null)
        {
            sb.AppendLine($"a=fingerprint:{fingerprint.Algorithm} {fingerprint.Value}");
        }

        sb.AppendLine("a=setup:passive");
    }
}

/// <summary>
/// Consumer 信息 (用于 SDP 构建)
/// </summary>
public class ConsumerInfo
{
    public string ConsumerId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public ConsumerRtpParameters? RtpParameters { get; set; }
}
