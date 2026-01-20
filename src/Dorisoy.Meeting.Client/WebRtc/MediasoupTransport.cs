using Microsoft.Extensions.Logging;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Concurrent;
using Dorisoy.Meeting.Client.Models;

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
    
    // SDP 状态 (预留用于后续 SDP 重新协商)
    #pragma warning disable CS0414
    private bool _sdpNegotiated;
    #pragma warning restore CS0414

    // 接收 track 状态
    private bool _hasAddedVideoTrack;
    private bool _hasAddedAudioTrack;
    
    // PeerConnection 启动状态
    private bool _peerConnectionStarted;
    
    // ICE 连接状态 - 用于等待 DTLS 就绪
    private RTCIceConnectionState _iceConnectionState = RTCIceConnectionState.@new;
    private readonly SemaphoreSlim _iceReadySignal = new(0, 1);
    
    // DTLS 连接状态 - 用于等待 DTLS 握手完成
    private RTCPeerConnectionState _dtlsConnectionState = RTCPeerConnectionState.@new;
    private readonly SemaphoreSlim _dtlsConnectedSignal = new(0, 1);
    
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
            _dtlsConnectionState = state;
            OnConnectionStateChanged?.Invoke(state.ToString());

            if (state == RTCPeerConnectionState.connected)
            {
                _connected = true;
                // DTLS 连接成功，释放信号
                try
                {
                    if (_dtlsConnectedSignal.CurrentCount == 0)
                    {
                        _dtlsConnectedSignal.Release();
                    }
                }
                catch { /* 忽略多次释放 */ }
                
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
            _iceConnectionState = state;
            
            // 当 ICE 状态变为 checking 或 connected 时，表示可以开始 DTLS
            if (state == RTCIceConnectionState.checking || 
                state == RTCIceConnectionState.connected)
            {
                try
                {
                    if (_iceReadySignal.CurrentCount == 0)
                    {
                        _iceReadySignal.Release();
                    }
                }
                catch { /* 忽略多次释放 */ }
            }
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
    
            // 查找对应的 Consumer
            var consumer = _remoteConsumers.Values.FirstOrDefault(c => c.Ssrc == ssrc);
            if (consumer == null)
            {
                // 尝试根据 PayloadType 查找
                consumer = _remoteConsumers.Values.FirstOrDefault(c => c.PayloadType == payloadType);
            }
    
            if (consumer == null)
            {
                // 尝试根据 MediaType 查找
                var kind = mediaType == SDPMediaTypesEnum.video ? "video" : "audio";
                consumer = _remoteConsumers.Values.FirstOrDefault(c => c.Kind == kind);
                    
                if (consumer != null)
                {
                    // 找到了，可能是 SSRC 不匹配（服务器端可能使用了不同的 SSRC）
                    // 更新 Consumer 的 SSRC
                    if (_recvRtpPacketCount <= 10)
                    {
                        _logger.LogInformation("Mapping RTP SSRC {ReceivedSsrc} -> Consumer {ConsumerId} (expected SSRC={ExpectedSsrc})", 
                            ssrc, consumer.ConsumerId, consumer.Ssrc);
                        consumer.Ssrc = ssrc; // 更新 SSRC 以便后续匹配
                    }
                }
            }
    
            if (consumer == null)
            {
                if (_recvRtpPacketCount % 100 == 1)
                {
                    _logger.LogWarning("Received RTP for unknown SSRC: {Ssrc}, PT: {PayloadType}, MediaType: {MediaType}. Known consumers: {Consumers}",
                        ssrc, payloadType, mediaType,
                        string.Join(", ", _remoteConsumers.Values.Select(c => $"{c.Kind}:SSRC={c.Ssrc},PT={c.PayloadType}")));
                }
                return;
            }
    
            if (consumer.Kind == "video")
            {
                // 触发视频 RTP 包事件
                OnVideoRtpPacketReceived?.Invoke(consumer.ConsumerId, rtpPacket);
            }
            else if (consumer.Kind == "audio")
            {
                // 触发音频 RTP 包事件
                OnAudioRtpPacketReceived?.Invoke(consumer.ConsumerId, rtpPacket);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing RTP packet");
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
                DtlsParameters,
                _currentVideoCodec);

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

            // 等待 ICE 候选者初始化完成（给 SIPSorcery 内部一些时间初始化）
            // DTLS 等待时间会在 StartPeerConnectionWithRetryAsync 中处理
            _logger.LogDebug("Waiting for ICE initialization...");
            await Task.Delay(300);

            // 启动 PeerConnection - 开始 DTLS 握手
            // 使用增强的重试机制，包含 ICE 状态检查和更长的延迟
            await StartPeerConnectionWithRetryAsync();
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

    /// <summary>
    /// 带重试的 PeerConnection 启动
    /// SIPSorcery 在 DTLS 初始化未完成时可能抛出 NullReferenceException
    /// 这通常发生在 DoDtlsHandshake 方法中尝试获取远程证书时
    /// 
    /// 修复策略：
    /// 1. 首先等待 ICE 连接状态就绪（checking/connected）
    /// 2. 然后使用延迟重试机制启动 PeerConnection
    /// 3. 在每次重试前增加延迟，给 DTLS 握手更多时间
    /// 4. 启动后等待 DTLS 连接完成
    /// </summary>
    private async Task StartPeerConnectionWithRetryAsync()
    {
        const int maxRetries = 10;  // 增加重试次数
        const int baseDelayMs = 300;  // 基础延迟
        const int iceWaitTimeoutMs = 8000;  // ICE 等待超时
        const int dtlsWaitTimeoutMs = 10000;  // DTLS 连接等待超时

        // 步骤 1：等待 ICE 连接状态就绪
        _logger.LogDebug("Waiting for ICE connection to be ready...");
        try
        {
            if (_iceConnectionState != RTCIceConnectionState.checking && 
                _iceConnectionState != RTCIceConnectionState.connected)
            {
                var iceReady = await _iceReadySignal.WaitAsync(iceWaitTimeoutMs);
                if (!iceReady)
                {
                    _logger.LogWarning("ICE connection did not become ready within {Timeout}ms, proceeding anyway", iceWaitTimeoutMs);
                }
                else
                {
                    _logger.LogDebug("ICE connection is ready (state: {State})", _iceConnectionState);
                }
            }
            else
            {
                _logger.LogDebug("ICE already in ready state: {State}", _iceConnectionState);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Error waiting for ICE ready: {Message}", ex.Message);
        }

        // 步骤 2：使用重试机制启动 PeerConnection
        bool startedSuccessfully = false;
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                // 在每次尝试前等待，给 SIPSorcery 内部状态时间初始化
                int delayMs = baseDelayMs * attempt;
                _logger.LogDebug("Waiting {DelayMs}ms before PeerConnection.Start() (attempt {Attempt}/{MaxRetries})...", 
                    delayMs, attempt, maxRetries);
                await Task.Delay(delayMs);
                
                _logger.LogDebug("Starting PeerConnection (attempt {Attempt}/{MaxRetries}), ICE={IceState}, DTLS={DtlsState}...", 
                    attempt, maxRetries, _iceConnectionState, _dtlsConnectionState);
                
                await _peerConnection!.Start();
                _logger.LogInformation("PeerConnection.Start() succeeded on attempt {Attempt}", attempt);
                startedSuccessfully = true;
                break;
            }
            catch (NullReferenceException ex)
            {
                // 这通常是 DTLS 初始化未完成导致的
                _logger.LogWarning("PeerConnection.Start() NullReferenceException (attempt {Attempt}/{MaxRetries}): {Message}", 
                    attempt, maxRetries, ex.Message);

                if (attempt == maxRetries)
                {
                    // 最后一次尝试：等待更长时间
                    _logger.LogInformation("Final attempt: waiting 3 seconds for DTLS to initialize...");
                    await Task.Delay(3000);
                    try
                    {
                        await _peerConnection!.Start();
                        _logger.LogInformation("PeerConnection started successfully on final attempt");
                        startedSuccessfully = true;
                    }
                    catch (Exception finalEx)
                    {
                        _logger.LogWarning("Final attempt failed: {Message}", finalEx.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("PeerConnection.Start() {ExceptionType} (attempt {Attempt}/{MaxRetries}): {Message}", 
                    ex.GetType().Name, attempt, maxRetries, ex.Message);

                if (attempt == maxRetries)
                {
                    _logger.LogWarning("All retry attempts exhausted");
                }
            }
        }

        // 步骤 3：等待 DTLS 连接完成
        if (startedSuccessfully)
        {
            _logger.LogDebug("Waiting for DTLS connection to complete...");
            try
            {
                if (_dtlsConnectionState != RTCPeerConnectionState.connected)
                {
                    var dtlsConnected = await _dtlsConnectedSignal.WaitAsync(dtlsWaitTimeoutMs);
                    if (dtlsConnected)
                    {
                        _logger.LogInformation("DTLS connection established successfully");
                    }
                    else
                    {
                        _logger.LogWarning("DTLS connection did not complete within {Timeout}ms, but may connect later", dtlsWaitTimeoutMs);
                    }
                }
                else
                {
                    _logger.LogDebug("DTLS already connected");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error waiting for DTLS connection: {Message}", ex.Message);
            }
        }
        else
        {
            _logger.LogWarning("PeerConnection.Start() failed after all retries, connection may work later via ICE callbacks");
        }
    }

    // 发送 track 状态
    private bool _hasAddedSendVideoTrack;
    private bool _hasAddedSendAudioTrack;

    /// <summary>
    /// 为 Send Transport 添加本地发送 track - 这是让 Answer 方向变成 sendonly 的关键
    /// 注意：SIPSorcery 库对 VP9/H264 的 SDP 支持不完善，因此 SDP 协商层统一使用 VP8 名称
    /// 实际 RTP 发送时会使用正确的 Payload Type 和打包格式
    /// </summary>
    private void AddSendTracks()
    {
        if (_peerConnection == null) return;

        try
        {
            // 添加视频发送 track
            if (!_hasAddedSendVideoTrack)
            {
                // SDP 协商层统一使用 VP8 名称（SIPSorcery 原生支持）
                // 使用实际编解码器的 Payload Type，这样 RTP 发送时会使用正确的 PT
                var videoPayloadType = GetVideoPayloadType();
                var videoCodecName = GetVideoCodecName();
                
                // SIPSorcery 对 VP9/H264 的 SDP 处理可能有问题，因此 SDP 层统一使用 VP8
                // 但保留实际的 Payload Type、这样 SendRtpRaw 会使用正确的 PT
                var sdpCodecName = "VP8"; // SDP 协商统一用 VP8
                
                // 创建视频格式 - PT 使用实际编解码器的 Payload Type
                var videoFormat = new SDPAudioVideoMediaFormat(
                    SDPMediaTypesEnum.video,
                    videoPayloadType,
                    sdpCodecName,
                    VIDEO_CLOCK_RATE);

                // 创建 SendOnly 的视频 track
                var videoTrack = new MediaStreamTrack(
                    SDPMediaTypesEnum.video,
                    false,
                    new List<SDPAudioVideoMediaFormat> { videoFormat },
                    MediaStreamStatusEnum.SendOnly);

                _peerConnection.addTrack(videoTrack);
                _hasAddedSendVideoTrack = true;
                _logger.LogInformation("Added video track for sending (SendOnly), ActualCodec={Codec}, SdpCodec={SdpCodec}, PT={PayloadType}", 
                    videoCodecName, sdpCodecName, videoPayloadType);
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
    /// 获取当前编解码器的名称
    /// </summary>
    private string GetVideoCodecName()
    {
        return _currentVideoCodec switch
        {
            VideoCodecType.VP8 => "VP8",
            VideoCodecType.VP9 => "VP9",
            VideoCodecType.H264 => "H264",
            _ => "VP8"
        };
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
                // 等待 ICE 候选者初始化完成
                // DTLS 等待时间和 ICE 状态检查会在 StartPeerConnectionWithRetryAsync 中处理
                _logger.LogDebug("Waiting for ICE initialization...");
                await Task.Delay(300);
                
                // 使用增强的重试机制启动 PeerConnection（包含 ICE 状态等待和更长的 DTLS 延迟）
                await StartPeerConnectionWithRetryAsync();
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
                
                // 从 MimeType 提取编解码器名称，但 SIPSorcery 对 VP9/H264 支持有限，需要使用 VP8
                // 注意：这仅影响 SDP 协商层的名称，实际 PayloadType 仍然正确
                var codecName = GetCodecNameFromMimeType(videoCodec?.MimeType ?? "video/VP8");
                _logger.LogDebug("Creating video track: PT={PT}, Codec={Codec}, ClockRate={Rate}", 
                    payloadType, codecName, clockRate);

                // 创建 SDP 格式的视频 track
                var videoFormat = new SDPAudioVideoMediaFormat(
                    SDPMediaTypesEnum.video,
                    payloadType,
                    codecName,  // 使用从 MimeType 提取的编解码器名称
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
            
            if (_remoteConsumers.TryRemove(consumerId, out _))
            {
                _logger.LogDebug("已移除远端 Consumer 信息: {ConsumerId}", consumerId);
            }
        }
        
        _logger.LogInformation("Consumer 已从 Transport 移除: {ConsumerId}", consumerId);
    }

    /// <summary>
    /// 从 MimeType 提取编解码器名称（用于 SDP 协商）
    /// SIPSorcery 对 VP9/H264 的 SDP 支持有限，统一使用 VP8 名称以确保兼容性
    /// </summary>
    private static string GetCodecNameFromMimeType(string mimeType)
    {
        // SIPSorcery 对 VP9/H264 处理有问题，SDP 层统一使用 VP8 名称
        // 但实际的 PayloadType 是正确的，这不影响 RTP 传输
        return "VP8";
    }

    /// <summary>
    /// 为轨道创建 RTP 参数（支持多编解码器）
    /// </summary>
    private object CreateRtpParametersForTrack(MediaStreamTrack track, string kind)
    {
        var ssrc = (uint)Random.Shared.Next(100000000, 999999999);

        if (kind == "video")
        {
            // 根据当前编解码器类型选择正确的参数
            var (mimeType, payloadType) = _currentVideoCodec switch
            {
                VideoCodecType.VP8 => ("video/VP8", 96),
                VideoCodecType.VP9 => ("video/VP9", 103),
                VideoCodecType.H264 => ("video/H264", 105),
                _ => ("video/VP8", 96)
            };

            return new
            {
                mid = "0",
                codecs = new object[]
                {
                    new
                    {
                        mimeType = mimeType,
                        payloadType = payloadType,
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
                    new { ssrc, maxBitrate = VideoBitrate }
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
    private const int VP9_PAYLOAD_DESCRIPTOR_SIZE = 1; // 简化版 VP9 负载描述符大小
    private const int RTP_HEADER_SIZE = 12; // RTP 头固定大小
    private const int MAX_VP8_PAYLOAD_SIZE = MTU_SIZE - RTP_HEADER_SIZE - VP8_PAYLOAD_DESCRIPTOR_SIZE;
    private const int MAX_VP9_PAYLOAD_SIZE = MTU_SIZE - RTP_HEADER_SIZE - VP9_PAYLOAD_DESCRIPTOR_SIZE;
    private const int MAX_H264_PAYLOAD_SIZE = MTU_SIZE - RTP_HEADER_SIZE - 2; // NAL unit header
    
    // PayloadType 必须与 Producer 注册时使用的 PT 一致
    // 参见 RtpModels.cs 中的 CreateVideoProduceRequest 和 CreateAudioProduceRequest
    private const int VP8_PAYLOAD_TYPE = 96;   // VP8
    private const int VP9_PAYLOAD_TYPE = 103;  // VP9
    private const int H264_PAYLOAD_TYPE = 105; // H264
    private const int AUDIO_PAYLOAD_TYPE = 100; // Opus - 与 CreateAudioProduceRequest 一致
    private const int VIDEO_CLOCK_RATE = 90000; // 90kHz
    private const int AUDIO_CLOCK_RATE = 48000; // 48kHz

    // 当前视频编解码器类型
    private VideoCodecType _currentVideoCodec = VideoCodecType.VP8;
    
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
    /// 发送视频 RTP 包 - 支持 MTU 分片，自动选择编解码器
    /// </summary>
    public void SendVideoRtpPacketAsync(byte[] videoData, bool isKeyFrame)
    {
        if (_peerConnection == null || !_connected)
            return;

        try
        {
            _videoFrameCount++;

            // 根据当前编解码器类型选择打包方法
            switch (_currentVideoCodec)
            {
                case VideoCodecType.VP8:
                    SendVp8Frame(videoData, isKeyFrame);
                    break;
                case VideoCodecType.VP9:
                    SendVp9Frame(videoData, isKeyFrame);
                    break;
                case VideoCodecType.H264:
                    SendH264Frame(videoData, isKeyFrame);
                    break;
                default:
                    SendVp8Frame(videoData, isKeyFrame);
                    break;
            }

            // 更新时间戳 (90kHz 时钟，30fps = 3000 增量)
            _videoTimestamp += VIDEO_CLOCK_RATE / 30; // 假设 30fps

            if (_videoFrameCount % 100 == 0)
            {
                _logger.LogDebug("Video stats: codec={Codec}, frames={Frames}, ssrc={Ssrc}, seq={Seq}", 
                    _currentVideoCodec, _videoFrameCount, _videoSsrc, _videoSeqNum);
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Error sending video RTP packet");
        }
    }

    /// <summary>
    /// 发送 VP8 帧
    /// </summary>
    private void SendVp8Frame(byte[] vp8Data, bool isKeyFrame)
    {
        // 记录关键帧发送信息
        if (isKeyFrame || _videoFrameCount <= 3)
        {
            bool isVp8KeyFrame = vp8Data.Length > 0 && (vp8Data[0] & 0x01) == 0;
            _logger.LogInformation("VP8 RTP send: frame={Frame}, size={Size}, isKeyFrame={Key}, vp8PBit={PBit}, pictureId={PicId}",
                _videoFrameCount, vp8Data.Length, isKeyFrame, isVp8KeyFrame ? 0 : 1, _vp8PictureId);
        }

        // 检查是否需要分片
        if (vp8Data.Length <= MAX_VP8_PAYLOAD_SIZE)
        {
            SendSingleVp8Packet(vp8Data, isKeyFrame, isFirstPacket: true, isLastPacket: true);
        }
        else
        {
            SendFragmentedVp8Frame(vp8Data, isKeyFrame);
        }

        // 更新 PictureID（每帧增加，7-bit 范围 0-127 循环）
        _vp8PictureId = (ushort)((_vp8PictureId + 1) & 0x7F);
    }

    /// <summary>
    /// 发送 VP9 帧
    /// </summary>
    private void SendVp9Frame(byte[] vp9Data, bool isKeyFrame)
    {
        // 记录关键帧发送信息
        if (isKeyFrame || _videoFrameCount <= 3)
        {
            _logger.LogInformation("VP9 RTP send: frame={Frame}, size={Size}, isKeyFrame={Key}, pictureId={PicId}",
                _videoFrameCount, vp9Data.Length, isKeyFrame, _vp9PictureId);
        }

        // 检查是否需要分片
        if (vp9Data.Length <= MAX_VP9_PAYLOAD_SIZE)
        {
            SendSingleVp9Packet(vp9Data, isKeyFrame, isFirstPacket: true, isLastPacket: true);
        }
        else
        {
            SendFragmentedVp9Frame(vp9Data, isKeyFrame);
        }

        // 更新 PictureID（每帧增加）
        _vp9PictureId = (ushort)((_vp9PictureId + 1) & 0x7FFF);
    }

    /// <summary>
    /// 发送 H264 帧 - 正确处理 Annex B 格式
    /// FFmpeg H264 编码器输出 Annex B 格式，包含 NAL 起始码 (00 00 00 01 或 00 00 01)
    /// 需要分离每个 NAL 单元并分别发送
    /// </summary>
    private void SendH264Frame(byte[] h264Data, bool isKeyFrame)
    {
        // 记录关键帧发送信息
        if (isKeyFrame || _videoFrameCount <= 3)
        {
            _logger.LogInformation("H264 RTP send: frame={Frame}, size={Size}, isKeyFrame={Key}",
                _videoFrameCount, h264Data.Length, isKeyFrame);
        }

        // 解析 Annex B 格式，提取所有 NAL 单元
        var nalUnits = ExtractNalUnits(h264Data);
        
        if (nalUnits.Count == 0)
        {
            _logger.LogWarning("H264: No NAL units found in frame data");
            return;
        }
        
        _logger.LogDebug("H264: Found {Count} NAL units in frame", nalUnits.Count);
        
        // 发送每个 NAL 单元
        for (int i = 0; i < nalUnits.Count; i++)
        {
            var nalUnit = nalUnits[i];
            bool isLastNal = (i == nalUnits.Count - 1);
            
            if (nalUnit.Length == 0) continue;
            
            // 获取 NAL 类型用于日志
            int nalType = nalUnit[0] & 0x1F;
            _logger.LogDebug("H264: NAL[{Index}] type={Type}, size={Size}", i, nalType, nalUnit.Length);
            
            // 根据大小决定是单包发送还是分片发送
            if (nalUnit.Length <= MAX_H264_PAYLOAD_SIZE)
            {
                // 单包发送 (Single NAL Unit Packet)
                SendSingleH264NalUnit(nalUnit, isLastNal);
            }
            else
            {
                // 分片发送 (FU-A)
                SendFragmentedH264NalUnit(nalUnit, isLastNal);
            }
        }
    }
    
    /// <summary>
    /// 从 Annex B 格式数据中提取 NAL 单元
    /// Annex B 格式使用起始码 00 00 00 01 或 00 00 01 分隔 NAL 单元
    /// </summary>
    private List<byte[]> ExtractNalUnits(byte[] h264Data)
    {
        var nalUnits = new List<byte[]>();
        int offset = 0;
        int length = h264Data.Length;
        
        while (offset < length)
        {
            // 查找起始码
            int startCodeLength = 0;
            int startPos = FindStartCode(h264Data, offset, out startCodeLength);
            
            if (startPos < 0)
            {
                // 没有找到起始码
                if (offset == 0 && length > 0)
                {
                    // 可能是不带起始码的裸 NAL 数据
                    nalUnits.Add(h264Data);
                }
                break;
            }
            
            // NAL 单元开始位置（跳过起始码）
            int nalStart = startPos + startCodeLength;
            
            // 查找下一个起始码或数据结束
            int nextStartCodeLength = 0;
            int nextStartPos = FindStartCode(h264Data, nalStart, out nextStartCodeLength);
            
            int nalEnd = (nextStartPos >= 0) ? nextStartPos : length;
            int nalLength = nalEnd - nalStart;
            
            if (nalLength > 0)
            {
                var nalUnit = new byte[nalLength];
                Array.Copy(h264Data, nalStart, nalUnit, 0, nalLength);
                nalUnits.Add(nalUnit);
            }
            
            offset = nalEnd;
        }
        
        return nalUnits;
    }
    
    /// <summary>
    /// 在数据中查找 NAL 起始码 (00 00 00 01 或 00 00 01)
    /// </summary>
    private int FindStartCode(byte[] data, int offset, out int startCodeLength)
    {
        startCodeLength = 0;
        
        for (int i = offset; i < data.Length - 3; i++)
        {
            // 检查 4 字节起始码: 00 00 00 01
            if (data[i] == 0 && data[i + 1] == 0 && data[i + 2] == 0 && data[i + 3] == 1)
            {
                startCodeLength = 4;
                return i;
            }
            // 检查 3 字节起始码: 00 00 01
            if (data[i] == 0 && data[i + 1] == 0 && data[i + 2] == 1)
            {
                startCodeLength = 3;
                return i;
            }
        }
        
        return -1;
    }
    
    /// <summary>
    /// 发送单个 H264 NAL 单元 (Single NAL Unit Packet)
    /// RFC 6184: 单包模式，NAL 数据直接作为 RTP 负载
    /// </summary>
    private void SendSingleH264NalUnit(byte[] nalUnit, bool isLastInFrame)
    {
        var seqNum = _videoSeqNum++;
        var marker = isLastInFrame ? 1 : 0;  // 帧的最后一个 NAL 设置 marker
        
        // 单包模式：NAL 数据直接作为 RTP 负载（不需要额外封装）
        SendRtpPacket(SDPMediaTypesEnum.video, H264_PAYLOAD_TYPE, seqNum, _videoTimestamp, _videoSsrc, marker, nalUnit);
        
        int nalType = nalUnit.Length > 0 ? (nalUnit[0] & 0x1F) : 0;
        _logger.LogTrace("H264: Sent single NAL: type={Type}, size={Size}, marker={Marker}", 
            nalType, nalUnit.Length, marker);
    }
    
    /// <summary>
    /// 分片发送 H264 NAL 单元 (FU-A Fragmentation Unit)
    /// RFC 6184: 当 NAL 单元大于 MTU 时使用
    /// </summary>
    private void SendFragmentedH264NalUnit(byte[] nalUnit, bool isLastInFrame)
    {
        if (nalUnit.Length < 1) return;
        
        // NAL header 是第一个字节
        byte nalHeader = nalUnit[0];
        byte nalType = (byte)(nalHeader & 0x1F);
        byte nalNri = (byte)(nalHeader & 0x60);  // NRI bits
        
        int offset = 1;  // 跳过 NAL header
        int remaining = nalUnit.Length - 1;
        int packetCount = 0;
        bool isFirst = true;
        
        while (remaining > 0)
        {
            int chunkSize = Math.Min(remaining, MAX_H264_PAYLOAD_SIZE);
            bool isLast = (offset + chunkSize >= nalUnit.Length);
            
            var payload = CreateH264FuAPayload(nalUnit, offset, chunkSize, nalNri, nalType, isFirst, isLast);
            var seqNum = _videoSeqNum++;
            
            // 只有最后一个 NAL 的最后一个分片才设置 marker
            var marker = (isLast && isLastInFrame) ? 1 : 0;
            
            SendRtpPacket(SDPMediaTypesEnum.video, H264_PAYLOAD_TYPE, seqNum, _videoTimestamp, _videoSsrc, marker, payload);
            
            offset += chunkSize;
            remaining -= chunkSize;
            packetCount++;
            isFirst = false;
        }
        
        _logger.LogTrace("H264: Sent fragmented NAL: type={Type}, size={Size}, packets={Packets}", 
            nalType, nalUnit.Length, packetCount);
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
        SendRtpPacket(SDPMediaTypesEnum.video, VP8_PAYLOAD_TYPE, seqNum, _videoTimestamp, _videoSsrc, marker, payload);

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
            SendRtpPacket(SDPMediaTypesEnum.video, VP8_PAYLOAD_TYPE, seqNum, _videoTimestamp, _videoSsrc, marker, payload);

            offset += chunkSize;
            remaining -= chunkSize;
            packetCount++;
        }

        _logger.LogTrace("Sent fragmented VP8 frame: size={Size}, packets={Packets}, keyFrame={Key}", 
            vp8Data.Length, packetCount, isKeyFrame);
    }

    // VP9 PictureID 计数器
    private ushort _vp9PictureId;

    /// <summary>
    /// 发送单个 VP9 RTP 包
    /// </summary>
    private void SendSingleVp9Packet(byte[] vp9Data, bool isKeyFrame, bool isFirstPacket, bool isLastPacket)
    {
        var payload = CreateVp9RtpPayload(vp9Data, 0, vp9Data.Length, isFirstPacket, isLastPacket, isKeyFrame);
        var seqNum = _videoSeqNum++;
        var marker = isLastPacket ? 1 : 0;

        SendRtpPacket(SDPMediaTypesEnum.video, VP9_PAYLOAD_TYPE, seqNum, _videoTimestamp, _videoSsrc, marker, payload);

        _logger.LogTrace("Sent VP9 packet: seq={Seq}, size={Size}, keyFrame={Key}, marker={Marker}", 
            seqNum, payload.Length, isKeyFrame, marker);
    }

    /// <summary>
    /// 分片发送 VP9 帧 - 按 draft-ietf-payload-vp9
    /// </summary>
    private void SendFragmentedVp9Frame(byte[] vp9Data, bool isKeyFrame)
    {
        int offset = 0;
        int remaining = vp9Data.Length;
        int packetCount = 0;

        while (remaining > 0)
        {
            int chunkSize = Math.Min(remaining, MAX_VP9_PAYLOAD_SIZE);
            bool isFirstPacket = (offset == 0);
            bool isLastPacket = (offset + chunkSize >= vp9Data.Length);

            var payload = CreateVp9RtpPayload(vp9Data, offset, chunkSize, isFirstPacket, isLastPacket, isKeyFrame);
            var seqNum = _videoSeqNum++;
            var marker = isLastPacket ? 1 : 0;

            SendRtpPacket(SDPMediaTypesEnum.video, VP9_PAYLOAD_TYPE, seqNum, _videoTimestamp, _videoSsrc, marker, payload);

            offset += chunkSize;
            remaining -= chunkSize;
            packetCount++;
        }

        _logger.LogTrace("Sent fragmented VP9 frame: size={Size}, packets={Packets}, keyFrame={Key}", 
            vp9Data.Length, packetCount, isKeyFrame);
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
    /// 创建 VP9 RTP 负载（添加 VP9 负载描述符）- draft-ietf-payload-vp9
    /// </summary>
    /// <param name="vp9Data">VP9 原始数据</param>
    /// <param name="offset">数据偏移</param>
    /// <param name="length">数据长度</param>
    /// <param name="isFirstPacket">是否是帧的第一个包</param>
    /// <param name="isLastPacket">是否是帧的最后一个包</param>
    /// <param name="isKeyFrame">是否是关键帧</param>
    private byte[] CreateVp9RtpPayload(byte[] vp9Data, int offset, int length, bool isFirstPacket, bool isLastPacket, bool isKeyFrame)
    {
        // VP9 RTP Payload Descriptor (draft-ietf-payload-vp9)
        //
        //  0 1 2 3 4 5 6 7
        // +-+-+-+-+-+-+-+-+
        // |I|P|L|F|B|E|V|Z|  <- 第一个字节（必需）
        // +-+-+-+-+-+-+-+-+
        // |M| PictureID   |  <- I=1 时的 PictureID (M=0: 7-bit, M=1: 15-bit)
        // +-+-+-+-+-+-+-+-+
        // |  [PictureID]  |  <- M=1 时的 PictureID 高位
        // +-+-+-+-+-+-+-+-+
        //
        // I: PictureID present (1 = 有 PictureID)
        // P: Inter-picture predicted (0 = 关键帧, 1 = 预测帧)
        // L: Layer indices present
        // F: Flexible mode (1 = 灵活模式)
        // B: Start of frame (1 = 帧开始)
        // E: End of frame (1 = 帧结束)
        // V: Scalability Structure present
        // Z: Not a reference frame for upper spatial layers

        // 使用简化的 2 字节描述符（含 7-bit PictureID）
        // 或 3 字节（含 15-bit PictureID）
        
        // 使用 15-bit PictureID 以支持更大范围
        byte firstByte = 0x80; // I=1 (有 PictureID)
        
        if (!isKeyFrame)
        {
            firstByte |= 0x40; // P=1 (预测帧，非关键帧)
        }
        
        if (isFirstPacket)
        {
            firstByte |= 0x08; // B=1 (帧开始)
        }
        
        if (isLastPacket)
        {
            firstByte |= 0x04; // E=1 (帧结束)
        }

        // 使用 15-bit PictureID (M=1)
        byte pictureIdHigh = (byte)(0x80 | ((_vp9PictureId >> 8) & 0x7F)); // M=1 + 高 7 位
        byte pictureIdLow = (byte)(_vp9PictureId & 0xFF); // 低 8 位

        var payload = new byte[3 + length];
        payload[0] = firstByte;
        payload[1] = pictureIdHigh;
        payload[2] = pictureIdLow;
        Array.Copy(vp9Data, offset, payload, 3, length);

        return payload;
    }

    /// <summary>
    /// 创建 H264 FU-A 分片负载 (RFC 6184)
    /// </summary>
    /// <param name="h264Data">H264 NAL 数据（含 NAL header）</param>
    /// <param name="offset">数据偏移（不含 NAL header）</param>
    /// <param name="length">分片长度</param>
    /// <param name="nalNri">NAL NRI 值</param>
    /// <param name="nalType">NAL Type 值</param>
    /// <param name="isFirst">是否是第一个分片</param>
    /// <param name="isLast">是否是最后一个分片</param>
    private byte[] CreateH264FuAPayload(byte[] h264Data, int offset, int length, byte nalNri, byte nalType, bool isFirst, bool isLast)
    {
        // H264 FU-A (Fragmentation Unit A) - RFC 6184
        //
        // FU indicator:
        //  0 1 2 3 4 5 6 7
        // +-+-+-+-+-+-+-+-+
        // |F|NRI|  Type   |  <- Type = 28 (FU-A)
        // +-+-+-+-+-+-+-+-+
        //
        // FU header:
        //  0 1 2 3 4 5 6 7
        // +-+-+-+-+-+-+-+-+
        // |S|E|R|  Type   |  <- 原始 NAL Type
        // +-+-+-+-+-+-+-+-+
        //
        // S: Start bit (1 = 第一个分片)
        // E: End bit (1 = 最后一个分片)
        // R: Reserved (must be 0)
        // Type: 原始 NAL unit type

        // FU indicator: F=0, NRI=原始值, Type=28
        byte fuIndicator = (byte)(nalNri | 28); // Type 28 = FU-A

        // FU header
        byte fuHeader = nalType;
        if (isFirst)
        {
            fuHeader |= 0x80; // S=1
        }
        if (isLast)
        {
            fuHeader |= 0x40; // E=1
        }

        var payload = new byte[2 + length];
        payload[0] = fuIndicator;
        payload[1] = fuHeader;
        Array.Copy(h264Data, offset, payload, 2, length);

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
        }

        // 重置所有状态标志
        _connected = false;
        _peerConnectionStarted = false;
        _hasAddedSendVideoTrack = false;
        _hasAddedSendAudioTrack = false;
        
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
