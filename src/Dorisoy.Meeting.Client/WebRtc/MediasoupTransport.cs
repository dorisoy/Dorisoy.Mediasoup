using Microsoft.Extensions.Logging;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Concurrent;

namespace Dorisoy.Meeting.Client.WebRtc;

/// <summary>
/// Mediasoup Transport - 封装 SIPSorcery RTCPeerConnection
/// 用于处理与 mediasoup 服务器的 WebRTC 连接
/// 实现 mediasoup-client 核心信令协议
/// </summary>
public class MediasoupTransport : IDisposable
{
    private readonly ILogger _logger;
    private readonly TransportDirection _direction;

    private RTCPeerConnection? _peerConnection;
    private bool _connected;
    private bool _disposed;

    // 远端 Consumer 信息
    private readonly ConcurrentDictionary<string, ConsumerInfo> _consumers = new();
    private readonly ConcurrentDictionary<string, RemoteConsumerInfo> _remoteConsumers = new();
    private readonly object _consumersLock = new();
    
    // SSRC 到 ConsumerId 的动态映射 - 用于在 SSRC 不匹配时建立正确的路由
    private readonly ConcurrentDictionary<uint, string> _ssrcToConsumerMap = new();
    // 记录已收到 RTP 包的 Consumer，用于判断哪些 Consumer 尚未分配 SSRC
    private readonly HashSet<string> _consumersWithRtp = new();
    private readonly object _ssrcMapLock = new();
    
    // SDP 状态 (预留用于后续 SDP 重新协商)
    #pragma warning disable CS0414
    private bool _sdpNegotiated;
    #pragma warning restore CS0414

    // 接收 track 状态
    private bool _hasAddedVideoTrack;
    private bool _hasAddedAudioTrack;
    
    // PeerConnection 启动状态
    private bool _peerConnectionStarted;
    
    // SDP 协商延迟处理
    private System.Threading.Timer? _negotiateTimer;
    private readonly object _negotiateLock = new();

    /// <summary>
    /// Transport ID
    /// </summary>
    public string TransportId { get; }

    /// <summary>
    /// ICE 参数
    /// </summary>
    public IceParameters? IceParameters { get; private set; }

    /// <summary>
    /// ICE Candidates
    /// </summary>
    public List<IceCandidate>? IceCandidates { get; private set; }

    /// <summary>
    /// DTLS 参数
    /// </summary>
    public DtlsParameters? DtlsParameters { get; private set; }

    /// <summary>
    /// 是否已连接
    /// </summary>
    public bool IsConnected => _connected;

    /// <summary>
    /// 连接状态变更事件
    /// </summary>
    public event Action<string>? OnConnectionStateChanged;

    /// <summary>
    /// 需要连接事件 - 当 Transport 需要连接时触发
    /// </summary>
    public event Func<DtlsParameters, Task>? OnConnect;

    /// <summary>
    /// 需要生产事件 - 当 Transport 需要向服务器注册 Producer 时触发
    /// </summary>
    public event Func<string, object, string, Task<string>>? OnProduce;

    /// <summary>
    /// SDP 协商完成事件 - 当 recv transport 的 SDP 协商完成后触发
    /// 这是调用 ConnectWebRtcTransport 的正确时机
    /// </summary>
    public event Func<Task>? OnNegotiationCompleted;

    /// <summary>
    /// 关键帧请求事件 - 当收到 PLI/FIR RTCP 反馈时触发
    /// 编码器应响应此事件发送关键帧
    /// </summary>
    public event Action? OnKeyFrameRequested;

    /// <summary>
    /// 收到远端视频帧事件 (预留用于 Transport 层解码)
    /// </summary>
    #pragma warning disable CS0067 // 预留事件
    public event Action<string, byte[], int, int>? OnRemoteVideoFrame;

    /// <summary>
    /// 收到远端音频数据事件 (预留用于 Transport 层解码)
    /// </summary>
    public event Action<string, byte[]>? OnRemoteAudioData;
    #pragma warning restore CS0067

    /// <summary>
    /// 收到视频 RTP 包事件 - 用于解码
    /// </summary>
    public event Action<string, RTPPacket>? OnVideoRtpPacketReceived;

    /// <summary>
    /// 收到音频 RTP 包事件 - 用于解码
    /// </summary>
    public event Action<string, RTPPacket>? OnAudioRtpPacketReceived;

    public MediasoupTransport(
        ILogger logger,
        string transportId,
        TransportDirection direction)
    {
        _logger = logger;
        TransportId = transportId;
        _direction = direction;
    }

    /// <summary>
    /// 初始化 Transport
    /// </summary>
    public void Initialize(
        IceParameters iceParameters,
        List<IceCandidate> iceCandidates,
        DtlsParameters dtlsParameters)
    {
        IceParameters = iceParameters;
        IceCandidates = iceCandidates;
        DtlsParameters = dtlsParameters;

        InitializePeerConnection();
    }

    /// <summary>
    /// 初始化 PeerConnection
    /// </summary>
    private void InitializePeerConnection()
    {
        var config = new RTCConfiguration();

        // 添加服务器提供的 ICE Candidates 作为 ICE servers (如果适用)
        // 对于 mediasoup，服务器直接提供 candidates，不需要 STUN/TURN

        _peerConnection = new RTCPeerConnection(config);
        
        // 关键：允许接收来自任意远端的 RTP 包
        // 这对于 mediasoup 作为 SFU 发送 RTP 数据是必需的
        _peerConnection.AcceptRtpFromAny = true;
        _logger.LogDebug("PeerConnection AcceptRtpFromAny enabled");

        _peerConnection.onconnectionstatechange += (state) =>
        {
            _logger.LogDebug("Transport {TransportId} connection state: {State}", TransportId, state);
            OnConnectionStateChanged?.Invoke(state.ToString());

            if (state == RTCPeerConnectionState.connected)
            {
                _connected = true;
                // 输出连接成功时的详细信息
                _logger.LogInformation("Transport {TransportId} DTLS connected, AcceptRtpFromAny={AcceptRtpFromAny}", 
                    TransportId, _peerConnection.AcceptRtpFromAny);
            }
            else if (state == RTCPeerConnectionState.disconnected ||
                     state == RTCPeerConnectionState.failed ||
                     state == RTCPeerConnectionState.closed)
            {
                _connected = false;
            }
        };

        _peerConnection.oniceconnectionstatechange += (state) =>
        {
            _logger.LogDebug("Transport {TransportId} ICE connection state: {State}", TransportId, state);
        };

        // 监听 RTCP 报告 - 处理 PLI/FIR 请求
        _peerConnection.OnReceiveReport += (ep, mediaType, rtcpReport) =>
        {
            _logger.LogDebug("RTCP report received from {Endpoint} for {MediaType}", ep, mediaType);
            
            // 只处理视频的 RTCP 报告
            // SIPSorcery 的 OnReceiveReport 可能包含 SR/RR/SDES 等，不一定是 PLI/FIR
            // 但当服务器发送 PLI 时，也会通过此回调通知
            if (mediaType == SDPMediaTypesEnum.video && rtcpReport != null)
            {
                // 节流逻辑：避免过于频繁地请求关键帧
                var now = DateTime.UtcNow;
                if ((now - _lastKeyFrameRequestTime).TotalMilliseconds >= KEYFRAME_REQUEST_THROTTLE_MS)
                {
                    _lastKeyFrameRequestTime = now;
                    // 请求关键帧 - 触发事件
                    OnKeyFrameRequested?.Invoke();
                    _logger.LogInformation("PLI/FIR received, requesting keyframe (throttled)");
                }
            }
        };
        
        // 注意：SIPSorcery 不直接暴露 PLI/FIR 回调
        // 但 OnReceiveReport 可以用于此目的

        // 监听 RTP 包接收事件 - 这是接收远端媒体的关键
        _peerConnection.OnRtpPacketReceived += OnRtpPacketReceived;
        
        // 监听所有传入的 RTP 数据（包括 SRTP 解密前）
        var rtpChannel = _peerConnection.GetRtpChannel();
        if (rtpChannel != null)
        {
            rtpChannel.OnRTPDataReceived += (localEP, remoteEP, data) =>
            {
                // 这是原始的 SRTP 数据，用于诊断是否收到网络包
                _rawRtpDataCount++;
                
                // 记录前 20 个包的详细信息，用于诊断
                if (_rawRtpDataCount <= 20 || _rawRtpDataCount % 100 == 1)
                {
                    // 分析第一个字节来确定数据类型
                    string dataType = "unknown";
                    if (data.Length > 0)
                    {
                        var firstByte = data[0];
                        if (firstByte >= 20 && firstByte <= 63)
                            dataType = "DTLS";
                        else if (firstByte >= 128 && firstByte <= 191)
                        {
                            var secondByte = data.Length > 1 ? data[1] : 0;
                            var pt = secondByte & 0x7F;
                            if (pt >= 64 && pt <= 95)
                                dataType = $"RTCP(PT={pt})";
                            else
                                dataType = $"RTP(PT={pt})";
                        }
                    }
                    
                    if (data.Length >= 12)
                    {
                        var pt = data[1] & 0x7F;
                        var ssrc = (uint)(data[8] << 24 | data[9] << 16 | data[10] << 8 | data[11]);
                        _logger.LogInformation("Raw data #{Count}: type={Type}, from {RemoteEP}, size={Size}, PT={PT}, SSRC={Ssrc:X8}", 
                            _rawRtpDataCount, dataType, remoteEP, data.Length, pt, ssrc);
                    }
                    else
                    {
                        _logger.LogInformation("Raw data #{Count}: type={Type}, from {RemoteEP}, size={Size}", 
                            _rawRtpDataCount, dataType, remoteEP, data.Length);
                    }
                }
            };
            _logger.LogInformation("RTP channel event subscribed for raw data diagnosis");
        }
        
        // 记录回调注册成功
        _logger.LogInformation("OnRtpPacketReceived callback registered for transport {TransportId}", TransportId);

        // 添加服务器提供的 ICE Candidates
        if (IceCandidates != null)
        {
            foreach (var candidate in IceCandidates)
            {
                try
                {
                    var iceCandidateInit = CreateRtcIceCandidateInit(candidate);
                    if (iceCandidateInit != null)
                    {
                        _peerConnection.addIceCandidate(iceCandidateInit);
                        _logger.LogDebug("Added ICE candidate: {Candidate}", candidate.Ip);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to add ICE candidate");
                }
            }
        }

        _logger.LogInformation("Transport {TransportId} initialized for {Direction}", TransportId, _direction);
    }

    /// <summary>
    /// 处理接收到的 RTP 包
    /// 关键修复：支持多个视频 Consumer，通过 SSRC 动态映射正确路由 RTP 包
    /// </summary>
    private void OnRtpPacketReceived(IPEndPoint remoteEndPoint, SDPMediaTypesEnum mediaType, RTPPacket rtpPacket)
    {
        try
        {
            _recvRtpPacketCount++;
            var ssrc = rtpPacket.Header.SyncSource;
            var payloadType = rtpPacket.Header.PayloadType;
            var payload = rtpPacket.Payload;
    
            // 过滤 mediasoup/libwebrtc 的带宽探测包 (padding packets for bandwidth probing)
            // 探测包通常使用 PT=127 和 SSRC=1234
            if (payloadType == 127 && ssrc == 1234)
            {
                // 这是带宽探测包，忽略它
                return;
            }
    
            // 每100个包输出一次统计
            if (_recvRtpPacketCount % 100 == 1)
            {
                _logger.LogInformation("RTP recv stats: count={Count}, SSRC={Ssrc}, PT={PayloadType}, Size={Size}, MediaType={MediaType}", 
                    _recvRtpPacketCount, ssrc, payloadType, payload.Length, mediaType);
            }
    
            // 1. 首先检查 SSRC 映射表
            if (_ssrcToConsumerMap.TryGetValue(ssrc, out var mappedConsumerId))
            {
                if (_remoteConsumers.TryGetValue(mappedConsumerId, out var mappedConsumer))
                {
                    DispatchRtpToConsumer(mappedConsumer, rtpPacket);
                    return;
                }
            }
    
            // 2. 尝试根据预期 SSRC 查找 Consumer（服务端在 newConsumer 通知中提供的 SSRC）
            var consumer = _remoteConsumers.Values.FirstOrDefault(c => c.Ssrc == ssrc);
            if (consumer != null)
            {
                // 建立映射并分发
                EstablishSsrcMapping(ssrc, consumer);
                DispatchRtpToConsumer(consumer, rtpPacket);
                return;
            }
    
            // 3. SSRC 不匹配，需要动态分配给尚未收到 RTP 的 Consumer
            var kind = mediaType == SDPMediaTypesEnum.video ? "video" : "audio";
            lock (_ssrcMapLock)
            {
                // 找到同类型但尚未收到 RTP 包的 Consumer
                var unassignedConsumer = _remoteConsumers.Values
                    .Where(c => c.Kind == kind && !_consumersWithRtp.Contains(c.ConsumerId))
                    .FirstOrDefault();
                    
                if (unassignedConsumer != null)
                {
                    _logger.LogInformation(
                        "Dynamic SSRC mapping: SSRC={Ssrc} -> Consumer {ConsumerId} (Kind={Kind}, expected SSRC={ExpectedSsrc})",
                        ssrc, unassignedConsumer.ConsumerId, kind, unassignedConsumer.Ssrc);
                    
                    // 更新 Consumer 的实际 SSRC
                    unassignedConsumer.Ssrc = ssrc;
                    EstablishSsrcMapping(ssrc, unassignedConsumer);
                    DispatchRtpToConsumer(unassignedConsumer, rtpPacket);
                    return;
                }
            }
    
            // 4. 所有同类型 Consumer 都已分配，可能是新的 SSRC（重传或新流）
            // 尝试用 PayloadType 匹配
            consumer = _remoteConsumers.Values.FirstOrDefault(c => c.PayloadType == payloadType && c.Kind == kind);
            if (consumer != null)
            {
                if (_recvRtpPacketCount <= 20)
                {
                    _logger.LogWarning(
                        "RTP SSRC mismatch, routing by PT: SSRC={Ssrc}, PT={PayloadType} -> Consumer {ConsumerId}",
                        ssrc, payloadType, consumer.ConsumerId);
                }
                DispatchRtpToConsumer(consumer, rtpPacket);
                return;
            }
    
            if (_recvRtpPacketCount % 100 == 1)
            {
                _logger.LogWarning(
                    "Received RTP for unknown SSRC: {Ssrc}, PT: {PayloadType}, MediaType: {MediaType}. Known consumers: {Consumers}",
                    ssrc, payloadType, mediaType,
                    string.Join(", ", _remoteConsumers.Values.Select(c => $"{c.Kind}:SSRC={c.Ssrc},PT={c.PayloadType},Id={c.ConsumerId}")));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing RTP packet");
        }
    }
    
    /// <summary>
    /// 建立 SSRC 到 Consumer 的映射
    /// </summary>
    private void EstablishSsrcMapping(uint ssrc, RemoteConsumerInfo consumer)
    {
        lock (_ssrcMapLock)
        {
            _ssrcToConsumerMap[ssrc] = consumer.ConsumerId;
            _consumersWithRtp.Add(consumer.ConsumerId);
        }
    }
    
    /// <summary>
    /// 分发 RTP 包到指定 Consumer
    /// </summary>
    private void DispatchRtpToConsumer(RemoteConsumerInfo consumer, RTPPacket rtpPacket)
    {
        if (consumer.Kind == "video")
        {
            OnVideoRtpPacketReceived?.Invoke(consumer.ConsumerId, rtpPacket);
        }
        else if (consumer.Kind == "audio")
        {
            OnAudioRtpPacketReceived?.Invoke(consumer.ConsumerId, rtpPacket);
        }
    }

    // 接收 RTP 包计数
    private long _recvRtpPacketCount;
    
    // 原始 RTP 数据计数（诊断用）
    private long _rawRtpDataCount;
    
    // 上次关键帧请求时间 - 用于节流
    private DateTime _lastKeyFrameRequestTime = DateTime.MinValue;
    private const int KEYFRAME_REQUEST_THROTTLE_MS = 1000; // 最小请求间隔 1 秒

    /// <summary>
    /// 创建 RTC ICE Candidate
    /// </summary>
    private RTCIceCandidateInit? CreateRtcIceCandidateInit(IceCandidate candidate)
    {
        try
        {
            // 构建标准的 ICE candidate 字符串
            var protocol = candidate.Protocol?.ToLower() ?? "udp";
            var type = candidate.Type?.ToLower() ?? "host";
            var priority = candidate.Priority ?? 1;

            var candidateStr = $"candidate:{candidate.Foundation} 1 {protocol} {priority} {candidate.Ip} {candidate.Port} typ {type}";

            return new RTCIceCandidateInit
            {
                candidate = candidateStr,
                sdpMid = "0",
                sdpMLineIndex = 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create ICE candidate from {Ip}:{Port}", candidate.Ip, candidate.Port);
            return null;
        }
    }

    /// <summary>
    /// 连接 Transport - 完成 DTLS 握手
    /// </summary>
    public async Task ConnectAsync()
    {
        if (_connected)
        {
            _logger.LogWarning("Transport {TransportId} already connected", TransportId);
            return;
        }

        if (_peerConnection == null)
        {
            throw new InvalidOperationException("PeerConnection not initialized");
        }

        try
        {
            // 创建本地 DTLS 参数
            var localDtlsParameters = GetLocalDtlsParameters();

            // 通知外部连接事件，让调用者发送 ConnectWebRtcTransport 请求
            if (OnConnect != null)
            {
                await OnConnect.Invoke(localDtlsParameters);
            }

            _logger.LogInformation("Transport {TransportId} connect initiated", TransportId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect transport {TransportId}", TransportId);
            throw;
        }
    }

    /// <summary>
    /// 获取本地 DTLS 参数
    /// </summary>
    public DtlsParameters GetLocalDtlsParameters()
    {
        if (_peerConnection == null)
        {
            throw new InvalidOperationException("PeerConnection not initialized");
        }

        // 获取本地证书指纹
        var fingerprint = _peerConnection.DtlsCertificateFingerprint;

        return new DtlsParameters
        {
            Role = "client", // 客户端作为 DTLS client
            Fingerprints = new List<DtlsFingerprint>
            {
                new DtlsFingerprint
                {
                    Algorithm = fingerprint.algorithm,
                    Value = fingerprint.value
                }
            }
        };
    }

    /// <summary>
    /// 设置为已连接状态
    /// </summary>
    public void SetConnected()
    {
        _connected = true;
        _logger.LogInformation("Transport {TransportId} marked as connected", TransportId);
    }

    /// <summary>
    /// 启动 Send Transport 的 SDP 协商和 DTLS 连接
    /// 在 mediasoup 中，Send Transport 也需要 DTLS 握手才能发送加密的 RTP
    /// </summary>
    public async Task StartSendTransportAsync()
    {
        if (_peerConnection == null)
        {
            throw new InvalidOperationException("PeerConnection not initialized");
        }

        if (_direction != TransportDirection.Send)
        {
            throw new InvalidOperationException("StartSendTransportAsync is only for Send transport");
        }

        if (_peerConnectionStarted)
        {
            _logger.LogDebug("Send transport already started");
            return;
        }

        try
        {
            _logger.LogInformation("Starting Send Transport SDP negotiation and DTLS...");

            // 关键：在设置远端 SDP 前，先添加本地 SendOnly 的 video 和 audio track
            // 这样 SIPSorcery 生成的 answer 才会是 sendonly 而不是 inactive
            AddSendTracks();

            // 生成远端 SDP offer（服务器作为 recvonly）
            var remoteSdp = MediasoupSdpBuilder.BuildSendTransportRemoteOffer(
                IceParameters,
                IceCandidates,
                DtlsParameters);

            _logger.LogDebug("Generated remote SDP for Send Transport (length={Length}):\n{Sdp}", remoteSdp.Length, remoteSdp);

            // 设置远端 SDP 为 Offer
            var offerSdp = new RTCSessionDescriptionInit
            {
                type = RTCSdpType.offer,
                sdp = remoteSdp
            };

            _logger.LogDebug("Setting remote description for Send Transport...");
            var setRemoteResult = _peerConnection.setRemoteDescription(offerSdp);
            _logger.LogDebug("Send Transport setRemoteDescription result: {Result}", setRemoteResult);

            if (setRemoteResult != SetDescriptionResultEnum.OK)
            {
                _logger.LogWarning("Failed to set Send Transport remote description: {Result}", setRemoteResult);
                return;
            }

            // 创建 Answer
            var answer = _peerConnection.createAnswer();
            if (answer == null)
            {
                _logger.LogWarning("Failed to create Send Transport SDP answer");
                return;
            }

            _logger.LogDebug("Created Send Transport local answer:\n{Sdp}", answer.sdp);

            // 设置本地 SDP
            _peerConnection.setLocalDescription(answer);

            // 关键：从 SIPSorcery track 获取实际使用的 SSRC，确保与 Producer 注册使用的 SSRC 一致
            UpdateSsrcFromTracks();

            // 启动 PeerConnection - 开始 DTLS 握手
            await _peerConnection.Start();
            _peerConnectionStarted = true;

            var rtpChannel = _peerConnection.GetRtpChannel();
            if (rtpChannel != null)
            {
                _logger.LogInformation("Send Transport started, RTP channel LocalEndPoint: {Local}",
                    rtpChannel.RTPLocalEndPoint);
            }

            _logger.LogInformation("Send Transport SDP negotiation completed, waiting for DTLS...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Send Transport");
            throw;
        }
    }

    // 发送 track 状态
    private bool _hasAddedSendVideoTrack;
    private bool _hasAddedSendAudioTrack;

    /// <summary>
    /// 为 Send Transport 添加本地发送 track - 这是让 Answer 方向变成 sendonly 的关键
    /// </summary>
    private void AddSendTracks()
    {
        if (_peerConnection == null) return;

        try
        {
            // 添加视频发送 track
            if (!_hasAddedSendVideoTrack)
            {
                // 创建 VP8 视频格式 - PT 必须与 Producer 注册和 RTP 发送一致
                var videoFormat = new SDPAudioVideoMediaFormat(
                    SDPMediaTypesEnum.video,
                    VIDEO_PAYLOAD_TYPE,  // VP8 payload type = 96，与 CreateVideoProduceRequest 一致
                    "VP8",
                    VIDEO_CLOCK_RATE);

                // 创建 SendOnly 的视频 track
                var videoTrack = new MediaStreamTrack(
                    SDPMediaTypesEnum.video,
                    false,
                    new List<SDPAudioVideoMediaFormat> { videoFormat },
                    MediaStreamStatusEnum.SendOnly);

                _peerConnection.addTrack(videoTrack);
                _hasAddedSendVideoTrack = true;
                _logger.LogInformation("Added video track for sending (SendOnly), PT={PayloadType}", VIDEO_PAYLOAD_TYPE);
            }

            // 添加音频发送 track
            if (!_hasAddedSendAudioTrack)
            {
                // 创建 Opus 音频格式 - PT 必须与 Producer 注册和 RTP 发送一致
                var audioFormat = new SDPAudioVideoMediaFormat(
                    SDPMediaTypesEnum.audio,
                    AUDIO_PAYLOAD_TYPE,  // Opus payload type = 100，与 CreateAudioProduceRequest 一致
                    "opus",
                    AUDIO_CLOCK_RATE,
                    2); // channels

                // 创建 SendOnly 的音频 track
                var audioTrack = new MediaStreamTrack(
                    SDPMediaTypesEnum.audio,
                    false,
                    new List<SDPAudioVideoMediaFormat> { audioFormat },
                    MediaStreamStatusEnum.SendOnly);

                _peerConnection.addTrack(audioTrack);
                _hasAddedSendAudioTrack = true;
                _logger.LogInformation("Added audio track for sending (SendOnly), PT={PayloadType}", AUDIO_PAYLOAD_TYPE);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add send tracks");
        }
    }

    /// <summary>
    /// 从 SIPSorcery track 获取实际使用的 SSRC，更新内部变量
    /// 这是确保 Producer 注册使用的 SSRC 与实际 RTP 发送的 SSRC 一致的关键
    /// </summary>
    private void UpdateSsrcFromTracks()
    {
        if (_peerConnection == null) return;

        try
        {
            // 获取视频 track 的 SSRC
            var videoTrack = _peerConnection.VideoLocalTrack;
            if (videoTrack != null && videoTrack.Ssrc > 0)
            {
                _videoSsrc = videoTrack.Ssrc;
                _logger.LogInformation("Updated video SSRC from track: {Ssrc}", _videoSsrc);
            }

            // 获取音频 track 的 SSRC
            var audioTrack = _peerConnection.AudioLocalTrack;
            if (audioTrack != null && audioTrack.Ssrc > 0)
            {
                _audioSsrc = audioTrack.Ssrc;
                _logger.LogInformation("Updated audio SSRC from track: {Ssrc}", _audioSsrc);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update SSRC from tracks");
        }
    }

    /// <summary>
    /// 生产媒体（用于 Send Transport）
    /// </summary>
    public async Task<string?> ProduceAsync(MediaStreamTrack track, string kind, string source)
    {
        if (_peerConnection == null)
        {
            throw new InvalidOperationException("PeerConnection not initialized");
        }

        if (_direction != TransportDirection.Send)
        {
            throw new InvalidOperationException("Cannot produce on receive transport");
        }

        try
        {
            // 添加轨道到 PeerConnection
            _peerConnection.addTrack(track);

            // 创建 RTP 参数
            var rtpParameters = CreateRtpParametersForTrack(track, kind);

            // 触发生产事件，通知服务器创建 Producer
            if (OnProduce != null)
            {
                var producerId = await OnProduce.Invoke(kind, rtpParameters, source);
                _logger.LogInformation("Produced {Kind} with ID: {ProducerId}", kind, producerId);
                return producerId;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to produce {Kind}", kind);
            throw;
        }
    }

    /// <summary>
    /// 消费媒体（用于 Recv Transport）
    /// 实现 mediasoup-client 的 Consumer 创建和 SDP 协商
    /// </summary>
    public async Task ConsumeAsync(string consumerId, string kind, object rtpParameters)
    {
        if (_peerConnection == null)
        {
            throw new InvalidOperationException("PeerConnection not initialized");
        }

        if (_direction != TransportDirection.Recv)
        {
            throw new InvalidOperationException("Cannot consume on send transport");
        }

        try
        {
            _logger.LogInformation("Consuming {Kind} with consumer ID: {ConsumerId}", kind, consumerId);

            // 解析 RTP 参数
            var json = JsonSerializer.Serialize(rtpParameters);
            var consumerRtpParams = JsonSerializer.Deserialize<ConsumerRtpParameters>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (consumerRtpParams == null)
            {
                throw new InvalidOperationException("Failed to parse consumer RTP parameters");
            }

            // 创建 ConsumerInfo
            var consumerInfo = new ConsumerInfo
            {
                ConsumerId = consumerId,
                Kind = kind,
                RtpParameters = consumerRtpParams
            };

            _consumers[consumerId] = consumerInfo;

            // 存储 RemoteConsumerInfo
            var encoding = consumerRtpParams.Encodings?.FirstOrDefault();
            var codec = consumerRtpParams.Codecs?.FirstOrDefault();

            _remoteConsumers[consumerId] = new RemoteConsumerInfo
            {
                ConsumerId = consumerId,
                Kind = kind,
                Ssrc = encoding?.Ssrc ?? 0,
                PayloadType = codec?.PayloadType ?? (kind == "video" ? 96 : 100),
                ClockRate = codec?.ClockRate ?? (kind == "video" ? 90000 : 48000),
                MimeType = codec?.MimeType ?? (kind == "video" ? "video/VP8" : "audio/opus")
            };

            _logger.LogInformation("Consumer {ConsumerId} registered, SSRC: {Ssrc}, Codec: {Codec}",
                consumerId, encoding?.Ssrc, codec?.MimeType);

            // 触发延迟 SDP 协商 - 等待更多 consumer 到达后再统一协商
            TriggerDelayedNegotiation();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to consume {Kind} with ID {ConsumerId}", kind, consumerId);
            throw;
        }
    }

    /// <summary>
    /// 触发延迟 SDP 协商 - 等待 200ms 后执行，允许多个 consumer 批量处理
    /// </summary>
    private void TriggerDelayedNegotiation()
    {
        lock (_negotiateLock)
        {
            // 重置定时器
            _negotiateTimer?.Dispose();
            _negotiateTimer = new System.Threading.Timer(_ =>
            {
                // 使用 Task.Run 避免在 Timer 回调中直接调用异步方法
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ExecuteNegotiationAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in delayed negotiation");
                    }
                });
            }, null, 200, Timeout.Infinite);
        }
    }

    /// <summary>
    /// 执行实际的 SDP 协商
    /// </summary>
    private async Task ExecuteNegotiationAsync()
    {
        try
        {
            await NegotiateSdpForConsumersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during SDP negotiation");
        }
    }

    /// <summary>
    /// 执行 SDP 协商 - 为所有 Consumer 生成 SDP 并应用
    /// </summary>
    private async Task NegotiateSdpForConsumersAsync()
    {
        if (_peerConnection == null || IceParameters == null || IceCandidates == null || DtlsParameters == null)
        {
            _logger.LogWarning("Cannot negotiate SDP: missing parameters");
            return;
        }

        try
        {
            // 收集所有 Consumer
            var consumers = _consumers.Values.ToList();
            if (!consumers.Any())
            {
                _logger.LogDebug("No consumers to negotiate");
                return;
            }

            // 关键：在设置 remote SDP 前，为每个媒体类型添加接收 track
            // 这样 SIP Sorcery 生成的 Answer 才会是 recvonly 而不是 inactive
            AddReceiveTracks(consumers);

            // 生成远端 SDP Answer (从 mediasoup 服务器角度)
            var remoteSdp = MediasoupSdpBuilder.BuildRemoteAnswer(
                IceParameters,
                IceCandidates,
                DtlsParameters,
                consumers);

            _logger.LogDebug("Generated remote SDP (length={Length}):\n{Sdp}", remoteSdp.Length, remoteSdp);

            // 设置远端 SDP 为 Offer
            var offerSdp = new RTCSessionDescriptionInit
            {
                type = RTCSdpType.offer,
                sdp = remoteSdp
            };

            _logger.LogDebug("Calling setRemoteDescription...");
            SetDescriptionResultEnum setRemoteResult;
            try
            {
                setRemoteResult = _peerConnection.setRemoteDescription(offerSdp);
            }
            catch (Exception sdpEx)
            {
                _logger.LogError(sdpEx, "Exception in setRemoteDescription");
                return;
            }
            _logger.LogDebug("setRemoteDescription result: {Result}", setRemoteResult);
            
            if (setRemoteResult != SetDescriptionResultEnum.OK)
            {
                _logger.LogWarning("Failed to set remote description: {Result}", setRemoteResult);
                return;
            }

            _logger.LogInformation("Remote SDP offer set successfully");

            // 创建 Answer
            var answer = _peerConnection.createAnswer();
            if (answer == null)
            {
                _logger.LogWarning("Failed to create SDP answer");
                return;
            }

            _logger.LogDebug("Created local answer:\n{Sdp}", answer.sdp);

            // 设置本地 SDP
            #pragma warning disable CS4014 // setLocalDescription 是同步方法，警告可忽略
            _peerConnection.setLocalDescription(answer);
            #pragma warning restore CS4014

            // 启动 PeerConnection - 确保只启动一次
            if (!_peerConnectionStarted)
            {
                await _peerConnection.Start();
                _peerConnectionStarted = true;
                
                // 打印 RTP 通道信息
                var rtpChannel = _peerConnection.GetRtpChannel();
                if (rtpChannel != null)
                {
                    _logger.LogInformation("PeerConnection started for receiving media, RTP channel LocalEndPoint: {Local}", 
                        rtpChannel.RTPLocalEndPoint);
                }
                else
                {
                    _logger.LogWarning("PeerConnection started but RTP channel is null!");
                }
            }
            else
            {
                _logger.LogDebug("PeerConnection already started, skipping Start()");
            }

            _sdpNegotiated = true;
            _logger.LogInformation("SDP negotiation completed for {Count} consumers", consumers.Count);

            // 触发协商完成事件 - 这是调用 ConnectWebRtcTransport 的正确时机
            if (OnNegotiationCompleted != null)
            {
                try
                {
                    _logger.LogDebug("Triggering OnNegotiationCompleted event...");
                    await OnNegotiationCompleted.Invoke();
                }
                catch (Exception negEx)
                {
                    _logger.LogError(negEx, "Error in OnNegotiationCompleted handler");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SDP negotiation failed");
        }
    }

    /// <summary>
    /// 为接收远端媒体添加本地 track - 这是让 Answer 方向变成 recvonly 的关键
    /// </summary>
    private void AddReceiveTracks(List<ConsumerInfo> consumers)
    {
        if (_peerConnection == null) return;

        bool hasVideo = consumers.Any(c => c.Kind == "video");
        bool hasAudio = consumers.Any(c => c.Kind == "audio");

        try
        {
            // 为视频添加接收能力
            if (hasVideo && !_hasAddedVideoTrack)
            {
                // 获取视频 consumer 的实际 codec 信息
                var videoConsumer = consumers.First(c => c.Kind == "video");
                var videoCodec = videoConsumer.RtpParameters?.Codecs?.FirstOrDefault();
                int payloadType = videoCodec?.PayloadType ?? 101;
                int clockRate = videoCodec?.ClockRate ?? 90000;

                // 创建 SDP 格式的视频 track
                var videoFormat = new SDPAudioVideoMediaFormat(
                    SDPMediaTypesEnum.video,
                    payloadType,
                    "VP8",
                    clockRate);

                var videoTrack = new MediaStreamTrack(
                    SDPMediaTypesEnum.video,
                    false,
                    new List<SDPAudioVideoMediaFormat> { videoFormat },
                    MediaStreamStatusEnum.RecvOnly);

                _peerConnection.addTrack(videoTrack);
                _hasAddedVideoTrack = true;
                _logger.LogInformation("Added video track for receiving (RecvOnly), PT={PayloadType}", payloadType);
            }

            // 为音频添加接收能力
            if (hasAudio && !_hasAddedAudioTrack)
            {
                // 获取音频 consumer 的实际 codec 信息
                var audioConsumer = consumers.First(c => c.Kind == "audio");
                var audioCodec = audioConsumer.RtpParameters?.Codecs?.FirstOrDefault();
                int payloadType = audioCodec?.PayloadType ?? 100;
                int clockRate = audioCodec?.ClockRate ?? 48000;

                // 创建 SDP 格式的音频 track
                var audioFormat = new SDPAudioVideoMediaFormat(
                    SDPMediaTypesEnum.audio,
                    payloadType,
                    "opus",
                    clockRate,
                    2); // channels

                var audioTrack = new MediaStreamTrack(
                    SDPMediaTypesEnum.audio,
                    false,
                    new List<SDPAudioVideoMediaFormat> { audioFormat },
                    MediaStreamStatusEnum.RecvOnly);

                _peerConnection.addTrack(audioTrack);
                _hasAddedAudioTrack = true;
                _logger.LogInformation("Added audio track for receiving (RecvOnly), PT={PayloadType}", payloadType);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add receive tracks");
        }
    }

    /// <summary>
    /// 获取当前活动的视频 Consumer ID
    /// </summary>
    public string? GetActiveVideoConsumerId()
    {
        lock (_consumersLock)
        {
            return _remoteConsumers.Values.FirstOrDefault(c => c.Kind == "video")?.ConsumerId;
        }
    }

    /// <summary>
    /// 获取当前活动的音频 Consumer ID
    /// </summary>
    public string? GetActiveAudioConsumerId()
    {
        lock (_consumersLock)
        {
            return _remoteConsumers.Values.FirstOrDefault(c => c.Kind == "audio")?.ConsumerId;
        }
    }

    /// <summary>
    /// 获取所有远端 Consumer 信息
    /// </summary>
    public IReadOnlyDictionary<string, RemoteConsumerInfo> GetRemoteConsumers()
    {
        lock (_consumersLock)
        {
            return new Dictionary<string, RemoteConsumerInfo>(_remoteConsumers);
        }
    }
<<<<<<< HEAD
=======
    
    /// <summary>
    /// 移除指定 Consumer
    /// 当用户离开房间或 Consumer 关闭时调用
    /// </summary>
    /// <param name="consumerId">Consumer ID</param>
    public void RemoveConsumer(string consumerId)
    {
        lock (_consumersLock)
        {
            if (_consumers.TryRemove(consumerId, out _))
            {
                _logger.LogDebug("已移除 Consumer 信息: {ConsumerId}", consumerId);
            }
            
            if (_remoteConsumers.TryRemove(consumerId, out var removedConsumer))
            {
                _logger.LogDebug("已移除远端 Consumer 信息: {ConsumerId}", consumerId);
                
                // 清理 SSRC 映射
                lock (_ssrcMapLock)
                {
                    // 移除 SSRC -> ConsumerId 映射
                    var ssrcToRemove = _ssrcToConsumerMap
                        .Where(kv => kv.Value == consumerId)
                        .Select(kv => kv.Key)
                        .ToList();
                    foreach (var ssrc in ssrcToRemove)
                    {
                        _ssrcToConsumerMap.TryRemove(ssrc, out _);
                    }
                    
                    // 从已收到 RTP 的 Consumer 集合中移除
                    _consumersWithRtp.Remove(consumerId);
                }
            }
        }
        
        _logger.LogInformation("Consumer 已从 Transport 移除: {ConsumerId}", consumerId);
    }
>>>>>>> pro

    /// <summary>
    /// 为轨道创建 RTP 参数
    /// </summary>
    private static object CreateRtpParametersForTrack(MediaStreamTrack track, string kind)
    {
        var ssrc = (uint)Random.Shared.Next(100000000, 999999999);

        if (kind == "video")
        {
            return new
            {
                mid = "0",
                codecs = new object[]
                {
                    new
                    {
                        mimeType = "video/VP8",
                        payloadType = 96,
                        clockRate = 90000,
                        rtcpFeedback = new object[]
                        {
                            new { type = "nack", parameter = (string?)null },
                            new { type = "nack", parameter = "pli" },
                            new { type = "ccm", parameter = "fir" },
                            new { type = "goog-remb", parameter = (string?)null },
                            new { type = "transport-cc", parameter = (string?)null }
                        }
                    }
                },
                encodings = new object[]
                {
                    new { ssrc, maxBitrate = 1500000 }
                },
                rtcp = new
                {
                    cname = $"wpf-{Guid.NewGuid():N}",
                    reducedSize = true
                }
            };
        }
        else
        {
            return new
            {
                mid = "1",
                codecs = new object[]
                {
                    new
                    {
                        mimeType = "audio/opus",
                        payloadType = 100,
                        clockRate = 48000,
                        channels = 2,
                        parameters = new { minptime = 10, useinbandfec = 1 }
                    }
                },
                encodings = new object[]
                {
                    new { ssrc, dtx = true }
                },
                rtcp = new
                {
                    cname = $"wpf-{Guid.NewGuid():N}",
                    reducedSize = true
                }
            };
        }
    }

    #region RTP 发送

    // RTP 配置常量
    private const int MTU_SIZE = 1200; // 安全的 MTU 大小，留有余量
    private const int VP8_PAYLOAD_DESCRIPTOR_SIZE = 1; // 简化版 VP8 负载描述符大小
    private const int RTP_HEADER_SIZE = 12; // RTP 头固定大小
    private const int MAX_VP8_PAYLOAD_SIZE = MTU_SIZE - RTP_HEADER_SIZE - VP8_PAYLOAD_DESCRIPTOR_SIZE;
    
    // PayloadType 必须与 Producer 注册时使用的 PT 一致
    // 参见 RtpModels.cs 中的 CreateVideoProduceRequest 和 CreateAudioProduceRequest
    private const int VIDEO_PAYLOAD_TYPE = 96; // VP8 - 与 CreateVideoProduceRequest 一致
    private const int AUDIO_PAYLOAD_TYPE = 100; // Opus - 与 CreateAudioProduceRequest 一致
    private const int VIDEO_CLOCK_RATE = 90000; // 90kHz
    private const int AUDIO_CLOCK_RATE = 48000; // 48kHz

<<<<<<< HEAD
=======
    // 当前视频编解码器类型
    private VideoCodecType _currentVideoCodec = VideoCodecType.VP9;
    
    /// <summary>
    /// 视频目标比特率 (bps)，用于 RTP 参数协商
    /// </summary>
    public int VideoBitrate { get; set; } = 5_000_000;

    /// <summary>
    /// 设置当前视频编解码器类型
    /// </summary>
    public void SetVideoCodecType(VideoCodecType codecType)
    {
        if (_currentVideoCodec != codecType)
        {
            _logger.LogInformation("Transport 编解码器类型切换: {Old} -> {New}", _currentVideoCodec, codecType);
            _currentVideoCodec = codecType;
        }
    }

    /// <summary>
    /// 获取当前视频编解码器类型
    /// </summary>
    public VideoCodecType CurrentVideoCodec => _currentVideoCodec;

    /// <summary>
    /// 获取当前编解码器的 Payload Type
    /// </summary>
    private int GetVideoPayloadType()
    {
        return _currentVideoCodec switch
        {
            VideoCodecType.VP8 => VP8_PAYLOAD_TYPE,
            VideoCodecType.VP9 => VP9_PAYLOAD_TYPE,
            VideoCodecType.H264 => H264_PAYLOAD_TYPE,
            _ => VP8_PAYLOAD_TYPE
        };
    }

>>>>>>> pro
    // RTP 发送状态
    private uint _videoSsrc = (uint)Random.Shared.Next(100000000, 999999999);
    private uint _audioSsrc = (uint)Random.Shared.Next(100000000, 999999999);
    private ushort _videoSeqNum;
    private ushort _audioSeqNum;
    private uint _videoTimestamp;
    private uint _audioTimestamp;
    private long _videoFrameCount;
    private long _audioFrameCount;

    /// <summary>
    /// 发送视频 RTP 包 - 支持 MTU 分片
    /// </summary>
    public void SendVideoRtpPacketAsync(byte[] vp8Data, bool isKeyFrame)
    {
        if (_peerConnection == null || !_connected)
            return;

        try
        {
            _videoFrameCount++;

            // 记录关键帧发送信息
            if (isKeyFrame || _videoFrameCount <= 3)
            {
                // 检查 VP8 帧数据的 P 位
                bool isVp8KeyFrame = vp8Data.Length > 0 && (vp8Data[0] & 0x01) == 0;
                _logger.LogInformation("VP8 RTP send: frame={Frame}, size={Size}, isKeyFrame={Key}, vp8PBit={PBit}, pictureId={PicId}",
                    _videoFrameCount, vp8Data.Length, isKeyFrame, isVp8KeyFrame ? 0 : 1, _vp8PictureId);
            }

            // 检查是否需要分片
            if (vp8Data.Length <= MAX_VP8_PAYLOAD_SIZE)
            {
                // 单包发送
                SendSingleVp8Packet(vp8Data, isKeyFrame, isFirstPacket: true, isLastPacket: true);
            }
            else
            {
                // 分片发送
                SendFragmentedVp8Frame(vp8Data, isKeyFrame);
            }

            // 更新 PictureID（每帧增加，7-bit 范围 0-127 循环）
            _vp8PictureId = (ushort)((_vp8PictureId + 1) & 0x7F);

            // 更新时间戳 (90kHz 时钟，30fps = 3000 增量)
            _videoTimestamp += VIDEO_CLOCK_RATE / 30; // 假设 30fps

            if (_videoFrameCount % 100 == 0)
            {
                _logger.LogDebug("Video stats: frames={Frames}, ssrc={Ssrc}, seq={Seq}, pictureId={PicId}", 
                    _videoFrameCount, _videoSsrc, _videoSeqNum, _vp8PictureId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Error sending video RTP packet");
        }
    }

    /// <summary>
    /// 发送单个 VP8 RTP 包（不需要分片）
    /// </summary>
    private void SendSingleVp8Packet(byte[] vp8Data, bool isKeyFrame, bool isFirstPacket, bool isLastPacket)
    {
        var payload = CreateVp8RtpPayload(vp8Data, 0, vp8Data.Length, isFirstPacket);
        var seqNum = _videoSeqNum++;
        var marker = isLastPacket ? 1 : 0;

        // 实际发送 RTP 包
        SendRtpPacket(SDPMediaTypesEnum.video, VIDEO_PAYLOAD_TYPE, seqNum, _videoTimestamp, _videoSsrc, marker, payload);

        _logger.LogTrace("Sent VP8 packet: seq={Seq}, size={Size}, keyFrame={Key}, marker={Marker}", 
            seqNum, payload.Length, isKeyFrame, marker);
    }

    /// <summary>
    /// 分片发送 VP8 帧 - 按 RFC 7741
    /// </summary>
    private void SendFragmentedVp8Frame(byte[] vp8Data, bool isKeyFrame)
    {
        int offset = 0;
        int remaining = vp8Data.Length;
        int packetCount = 0;

        while (remaining > 0)
        {
            int chunkSize = Math.Min(remaining, MAX_VP8_PAYLOAD_SIZE);
            bool isFirstPacket = (offset == 0);
            bool isLastPacket = (offset + chunkSize >= vp8Data.Length);

            // 创建分片负载
            var payload = CreateVp8RtpPayload(vp8Data, offset, chunkSize, isFirstPacket);
            var seqNum = _videoSeqNum++;
            var marker = isLastPacket ? 1 : 0;

            // 发送 RTP 包
            SendRtpPacket(SDPMediaTypesEnum.video, VIDEO_PAYLOAD_TYPE, seqNum, _videoTimestamp, _videoSsrc, marker, payload);

            offset += chunkSize;
            remaining -= chunkSize;
            packetCount++;
        }

        _logger.LogTrace("Sent fragmented VP8 frame: size={Size}, packets={Packets}, keyFrame={Key}", 
            vp8Data.Length, packetCount, isKeyFrame);
    }

    /// <summary>
    /// 发送音频 RTP 包
    /// </summary>
    public void SendAudioRtpPacketAsync(byte[] opusData)
    {
        if (_peerConnection == null || !_connected)
            return;

        try
        {
            _audioFrameCount++;
            var seqNum = _audioSeqNum++;

            // Opus 帧通常很小，不需要分片
            SendRtpPacket(SDPMediaTypesEnum.audio, AUDIO_PAYLOAD_TYPE, seqNum, _audioTimestamp, _audioSsrc, 1, opusData);

            // 更新时间戳 (48kHz 时钟，20ms = 960 增量)
            _audioTimestamp += AUDIO_CLOCK_RATE * 20 / 1000; // 20ms

            if (_audioFrameCount % 500 == 0)
            {
                _logger.LogDebug("Audio stats: frames={Frames}, ssrc={Ssrc}, seq={Seq}", 
                    _audioFrameCount, _audioSsrc, _audioSeqNum);
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Error sending audio RTP packet");
        }
    }

    /// <summary>
    /// 实际发送 RTP 包 - 调用 SIPSorcery API
    /// 注意：SendRtpRaw 期望只接收 payload，它会自动添加 RTP 头
    /// </summary>
    private void SendRtpPacket(SDPMediaTypesEnum mediaType, int payloadType, ushort seqNum, 
        uint timestamp, uint ssrc, int marker, byte[] payload)
    {
        if (_peerConnection == null)
            return;

        try
        {
            // SendRtpRaw 期望只接收 payload（不含 RTP 头）
            // 它会自动使用 PeerConnection 内部管理的 SSRC 和序列号
            _peerConnection.SendRtpRaw(mediaType, payload, timestamp, marker, payloadType);

            // 详细日志（用于调试）
            if (_videoFrameCount <= 5 || marker == 1)
            {
                _logger.LogDebug("RTP sent via SendRtpRaw: type={Type}, pt={PT}, ts={TS}, size={Size}, marker={Marker}",
                    mediaType, payloadType, timestamp, payload.Length, marker);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send RTP packet: type={Type}, pt={PT}", mediaType, payloadType);
        }
    }

    // VP8 PictureID 计数器（用于 VP8 Payload Descriptor 扩展）
    private ushort _vp8PictureId;

    /// <summary>
    /// 创建 VP8 RTP 负载（添加 VP8 负载描述符）- RFC 7741
    /// 增强版本：包含 PictureID 扩展，便于服务器识别帧边界
    /// </summary>
    /// <param name="vp8Data">VP8 原始数据</param>
    /// <param name="offset">数据偏移</param>
    /// <param name="length">数据长度</param>
    /// <param name="isFirstPacket">是否是帧的第一个包</param>
    private byte[] CreateVp8RtpPayload(byte[] vp8Data, int offset, int length, bool isFirstPacket)
    {
        // VP8 RTP Payload Descriptor (RFC 7741)
        // 带扩展的版本：3 字节描述符 (含 PictureID)
        //
        //  0 1 2 3 4 5 6 7
        // +-+-+-+-+-+-+-+-+
        // |X|R|N|S|R| PID |  <- 第一个字节
        // +-+-+-+-+-+-+-+-+
        // |I|L|T|K| RSV   |  <- X=1 时的扩展字节
        // +-+-+-+-+-+-+-+-+
        // |M| PictureID   |  <- I=1 时的 PictureID (M=0: 7-bit, M=1: 15-bit)
        // +-+-+-+-+-+-+-+-+
        //
        // X: Extension bit (1 = has extension)
        // R: Reserved (must be 0)
        // N: Non-reference frame (0 = reference frame)
        // S: Start of VP8 partition (1 = start)
        // PID: Partition index (0 for simple case)
        // I: PictureID present
        // L: TL0PICIDX present
        // T: TID present
        // K: KEYIDX present
        // M: PictureID extended (1 = 15-bit)

        // 使用 3 字节描述符（含 7-bit PictureID）
        byte firstByte = 0x80; // X=1 (有扩展)
        if (isFirstPacket)
        {
            firstByte |= 0x10; // S=1 (帧/分区开始)
        }
        // PID = 0 (单分区)

        byte extensionByte = 0x80; // I=1 (有 PictureID)
        
        // 7-bit PictureID (M=0)，值范围 0-127
        byte pictureIdByte = (byte)(_vp8PictureId & 0x7F);

        var payload = new byte[3 + length];
        payload[0] = firstByte;
        payload[1] = extensionByte;
        payload[2] = pictureIdByte;
        Array.Copy(vp8Data, offset, payload, 3, length);

        return payload;
    }

    /// <summary>
    /// 重置 RTP 状态（例如当重新连接时）
    /// </summary>
    public void ResetRtpState()
    {
        _videoSsrc = (uint)Random.Shared.Next(100000000, 999999999);
        _audioSsrc = (uint)Random.Shared.Next(100000000, 999999999);
        _videoSeqNum = (ushort)Random.Shared.Next(0, 65535);
        _audioSeqNum = (ushort)Random.Shared.Next(0, 65535);
        _videoTimestamp = (uint)Random.Shared.Next(0, int.MaxValue);
        _audioTimestamp = (uint)Random.Shared.Next(0, int.MaxValue);
        _videoFrameCount = 0;
        _audioFrameCount = 0;

        _logger.LogInformation("RTP state reset: videoSsrc={VideoSsrc}, audioSsrc={AudioSsrc}", 
            _videoSsrc, _audioSsrc);
    }

    /// <summary>
    /// 获取视频 SSRC
    /// </summary>
    public uint VideoSsrc => _videoSsrc;

    /// <summary>
    /// 获取音频 SSRC
    /// </summary>
    public uint AudioSsrc => _audioSsrc;

    #endregion

    /// <summary>
    /// 关闭 Transport
    /// </summary>
    public void Close()
    {
        if (_peerConnection != null)
        {
            try
            {
                _peerConnection.close();
            }
            catch { }
            _peerConnection = null;
        }

        // 清理 Consumer 信息
        lock (_consumersLock)
        {
            _remoteConsumers.Clear();
            _consumers.Clear();
        }
        
        // 清理 SSRC 映射
        lock (_ssrcMapLock)
        {
            _ssrcToConsumerMap.Clear();
            _consumersWithRtp.Clear();
        }

        _connected = false;
        _logger.LogInformation("Transport {TransportId} closed", TransportId);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Close();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Transport 方向
/// </summary>
public enum TransportDirection
{
    Send,
    Recv
}

#region Transport 参数模型

/// <summary>
/// ICE 参数
/// </summary>
public class IceParameters
{
    [JsonPropertyName("usernameFragment")]
    public string? UsernameFragment { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("iceLite")]
    public bool IceLite { get; set; }
}

/// <summary>
/// ICE Candidate
/// </summary>
public class IceCandidate
{
    [JsonPropertyName("foundation")]
    public string? Foundation { get; set; }

    [JsonPropertyName("priority")]
    public uint? Priority { get; set; }

    /// <summary>
    /// IP 地址 - 服务器可能返回 "address" 或 "ip"
    /// </summary>
    [JsonPropertyName("address")]
    public string? Address { get; set; }

    /// <summary>
    /// 兼容旧的 "ip" 字段
    /// </summary>
    [JsonPropertyName("ip")]
    public string? IpLegacy { get; set; }

    /// <summary>
    /// 获取实际的 IP 地址（优先使用 Address，否则使用 IpLegacy）
    /// </summary>
    [JsonIgnore]
    public string? Ip => Address ?? IpLegacy;

    [JsonPropertyName("protocol")]
    public string? Protocol { get; set; }

    [JsonPropertyName("port")]
    public ushort Port { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("tcpType")]
    public string? TcpType { get; set; }
}

/// <summary>
/// DTLS 参数
/// </summary>
public class DtlsParameters
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("fingerprints")]
    public List<DtlsFingerprint>? Fingerprints { get; set; }
}

/// <summary>
/// DTLS 指纹
/// </summary>
public class DtlsFingerprint
{
    [JsonPropertyName("algorithm")]
    public string? Algorithm { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }
}

/// <summary>
/// Consumer RTP 参数
/// </summary>
public class ConsumerRtpParameters
{
    [JsonPropertyName("codecs")]
    public List<ConsumerRtpCodec>? Codecs { get; set; }

    [JsonPropertyName("headerExtensions")]
    public List<ConsumerHeaderExtension>? HeaderExtensions { get; set; }

    [JsonPropertyName("encodings")]
    public List<ConsumerEncoding>? Encodings { get; set; }

    [JsonPropertyName("rtcp")]
    public ConsumerRtcp? Rtcp { get; set; }
}

/// <summary>
/// Consumer RTP 编解码器
/// </summary>
public class ConsumerRtpCodec
{
    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }

    [JsonPropertyName("payloadType")]
    public int PayloadType { get; set; }

    [JsonPropertyName("clockRate")]
    public int ClockRate { get; set; }

    [JsonPropertyName("channels")]
    public int? Channels { get; set; }

    [JsonPropertyName("parameters")]
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// Consumer Header Extension
/// </summary>
public class ConsumerHeaderExtension
{
    [JsonPropertyName("uri")]
    public string? Uri { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }
}

/// <summary>
/// Consumer Encoding
/// </summary>
public class ConsumerEncoding
{
    [JsonPropertyName("ssrc")]
    public uint Ssrc { get; set; }

    [JsonPropertyName("rtx")]
    public ConsumerRtx? Rtx { get; set; }
}

/// <summary>
/// Consumer RTX
/// </summary>
public class ConsumerRtx
{
    [JsonPropertyName("ssrc")]
    public uint Ssrc { get; set; }
}

/// <summary>
/// Consumer RTCP
/// </summary>
public class ConsumerRtcp
{
    [JsonPropertyName("cname")]
    public string? Cname { get; set; }

    [JsonPropertyName("reducedSize")]
    public bool ReducedSize { get; set; }
}

/// <summary>
/// 远端 Consumer 信息
/// </summary>
public class RemoteConsumerInfo
{
    public string ConsumerId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public uint Ssrc { get; set; }
    public int PayloadType { get; set; }
    public int ClockRate { get; set; }
    public string MimeType { get; set; } = string.Empty;
}

#endregion
