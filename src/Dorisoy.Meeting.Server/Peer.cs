using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using FBS.RtpParameters;
using FBS.WebRtcTransport;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Threading;
using Dorisoy.Mediasoup;

namespace Dorisoy.Meeting.Server
{
    public partial class Peer : IEquatable<Peer>
    {
        public string PeerId { get; }

        public string DisplayName { get; }

        public string[] Sources { get; }

        public ConcurrentDictionary<string, object> AppData { get; }

        [JsonIgnore]
        public ConcurrentDictionary<string, object> InternalData { get; }

        #region IEquatable<T>

        public bool Equals(Peer? other)
        {
            if (other is null)
            {
                return false;
            }

            return PeerId == other.PeerId;
        }

        public override bool Equals(object? other)
        {
            return Equals(other as Peer);
        }

        public override int GetHashCode()
        {
            return PeerId.GetHashCode();
        }

        #endregion IEquatable<T>
    }

    public partial class Peer
    {
        /// <summary>
        /// Logger factory for create logger.
        /// </summary>
        private readonly ILoggerFactory _loggerFactory;

        /// <summary>
        /// Logger
        /// </summary>
        private readonly ILogger<Peer> _logger;

        private bool _joined;

        private readonly AsyncReaderWriterLock _joinedLock = new();

        private readonly WebRtcTransportSettings _webRtcTransportSettings;

        private readonly PlainTransportSettings _plainTransportSettings;

        private readonly RtpCapabilities _rtpCapabilities;

        private readonly SctpCapabilities? _sctpCapabilities;

        private readonly Dictionary<string, Transport> _transports = new();

        private readonly AsyncReaderWriterLock _transportsLock = new();

        private readonly Dictionary<string, Consumer> _consumers = new();

        private readonly AsyncReaderWriterLock _consumersLock = new();

        private readonly Dictionary<string, Producer> _producers = new();

        public async Task<Dictionary<string, Producer>> GetProducersASync()
        {
            await using (await _producersLock.ReadLockAsync())
            {
                // 返回副本以避免并发修改问题
                return new Dictionary<string, Producer>(_producers);
            }
        }

        private readonly AsyncReaderWriterLock _producersLock = new();

        private readonly Dictionary<string, DataConsumer> _dataConsumers = new();

        private readonly AsyncReaderWriterLock _dataConsumersLock = new();

        private readonly Dictionary<string, DataProducer> _dataProducers = new();

        private readonly AsyncReaderWriterLock _dataProducersLock = new();

        private readonly List<PullPadding> _pullPaddings = new();

        private readonly AsyncAutoResetEvent _pullPaddingsLock = new();

        private Room? _room;

        private readonly AsyncReaderWriterLock _roomLock = new();

        public const string RoleKey = "role";

        [JsonIgnore]
        public string ConnectionId { get; }

        [JsonIgnore]
        public IHubClient? HubClient { get; }

        public Peer(
            ILoggerFactory loggerFactory,
            WebRtcTransportSettings webRtcTransportSettings,
            PlainTransportSettings plainTransportSettings,
            RtpCapabilities rtpCapabilities,
            SctpCapabilities? sctpCapabilities,
            string peerId,
            string connectionId,
            IHubClient hubClient,
            string displayName,
            string[]? sources,
            Dictionary<string, object>? appData
        )
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<Peer>();
            _webRtcTransportSettings = webRtcTransportSettings;
            _plainTransportSettings = plainTransportSettings;
            _rtpCapabilities = rtpCapabilities;
            _sctpCapabilities = sctpCapabilities;
            PeerId = peerId;
            ConnectionId = connectionId;
            HubClient = hubClient;
            DisplayName = displayName.NullOrWhiteSpaceReplace("User:" + peerId.PadLeft(8, '0'));
            Sources = sources ?? [];
            AppData = new ConcurrentDictionary<string, object>(appData ?? new Dictionary<string, object>());
            InternalData = new ConcurrentDictionary<string, object>(appData ?? new Dictionary<string, object>());
            _pullPaddingsLock.Set();
            _joined = true;
        }

        /// <summary>
        /// 创建 WebRtcTransport
        /// </summary>
        public async Task<WebRtcTransport> CreateWebRtcTransportAsync(
            CreateWebRtcTransportRequest createWebRtcTransportRequest,
            bool isSend
        )
        {
            await using (await _joinedLock.ReadLockAsync())
            {
                CheckJoined("CreateWebRtcTransportAsync()");

                await using (await _roomLock.ReadLockAsync())
                {
                    CheckRoom("CreateWebRtcTransportAsync()");

                    var webRtcTransportOptions = new Mediasoup.WebRtcTransportOptions
                    {
                        ListenInfos = _webRtcTransportSettings.ListenInfos,
                        InitialAvailableOutgoingBitrate = _webRtcTransportSettings.InitialAvailableOutgoingBitrate,
                        MaxSctpMessageSize = _webRtcTransportSettings.MaxSctpMessageSize,
                        EnableSctp = createWebRtcTransportRequest.SctpCapabilities != null,
                        NumSctpStreams = createWebRtcTransportRequest.SctpCapabilities?.NumStreams,
                        WebRtcServer = _room!.WebRtcServer,
                        AppData = new Dictionary<string, object> { { "Consuming", !isSend }, { "Producing", isSend } },
                    };

                    if (createWebRtcTransportRequest.ForceTcp)
                    {
                        webRtcTransportOptions.EnableUdp = false;
                        webRtcTransportOptions.EnableTcp = true;
                    }

                    var transport = await _room!.Router.CreateWebRtcTransportAsync(webRtcTransportOptions);
                    await using (await _transportsLock.WriteLockAsync())
                    {
                        // 幂等操作：只对 send transport 应用
                        // 对于 recv transport，允许创建多个（解决 SIPSorcery SRTP 限制，每个远端用户需要独立的 transport）
                        if (isSend && HasProducingTransport())
                        {
                            // 关闭新创建的 transport
                            await transport.CloseAsync();
                            // 返回现有的 producing transport
                            var existingTransport = _transports.Values.FirstOrDefault(t => 
                                t.AppData.TryGetValue("Producing", out var producing) && producing is true);
                            if (existingTransport is WebRtcTransport webRtcTransport)
                            {
                                _logger.LogWarning("CreateWebRtcTransportAsync() | 返回现有的 Producing transport: {TransportId}", webRtcTransport.TransportId);
                                return webRtcTransport;
                            }
                            throw new Exception("CreateWebRtcTransportAsync() | Producing transport exists but not found");
                        }

                        // Store the WebRtcTransport into the Peer data Object.
                        _transports[transport.TransportId] = transport;
                    }

                    transport.On(
                        "@close",
                        (_, _) =>
                        {
                            // 因为调用 transport.Close() 之前已经使用 _transportsLock 写锁，所以触发该事件的调用从 _transports 移除无需再次加锁。
                            _transports.Remove(transport.TransportId);
                            return Task.CompletedTask;
                        }
                    );
                    transport.On(
                        "routerclose",
                        async (_, _) =>
                        {
                            await using (await _transportsLock.WriteLockAsync())
                            {
                                _transports.Remove(transport.TransportId);
                            }
                        }
                    );

                    // If set, apply max incoming bitrate limit.
                    if (_webRtcTransportSettings.MaximumIncomingBitrate > 0)
                    {
                        // Fire and forget
                        transport
                            .SetMaxIncomingBitrateAsync(_webRtcTransportSettings.MaximumIncomingBitrate.Value)
                            .ContinueWithOnFaultedHandleLog(_logger);
                    }

                    return transport;
                }
            }
        }

        /// <summary>
        /// 连接 WebRtcTransport
        /// </summary>
        public async Task<bool> ConnectWebRtcTransportAsync(ConnectWebRtcTransportRequest connectWebRtcTransportRequest)
        {
            await using (await _joinedLock.ReadLockAsync())
            {
                CheckJoined("ConnectWebRtcTransportAsync()");

                await using (await _roomLock.ReadLockAsync())
                {
                    CheckRoom("ConnectWebRtcTransportAsync()");

                    await using (await _transportsLock.ReadLockAsync())
                    {
                        if (!_transports.TryGetValue(connectWebRtcTransportRequest.TransportId, out var transport))
                        {
                            throw new Exception(
                                $"ConnectWebRtcTransportAsync() | Transport:{connectWebRtcTransportRequest.TransportId} is not exists"
                            );
                        }

                        await transport.ConnectAsync(connectWebRtcTransportRequest);
                        return true;
                    }
                }
            }
        }

        /// <summary>
        /// 创建 PlainTransport
        /// </summary>
        public async Task<PlainTransport> CreatePlainTransportAsync(CreatePlainTransportRequest createPlainTransportRequest)
        {
            var plainTransportOptions = new PlainTransportOptions
            {
                ListenInfo = _plainTransportSettings.ListenInfo,
                MaxSctpMessageSize = _plainTransportSettings.MaxSctpMessageSize,
                RtcpMux = createPlainTransportRequest.RtcpMux, // 一般为 false
                Comedia = createPlainTransportRequest.Comedia, // 一般为 true
                AppData = new Dictionary<string, object>
                {
                    { "Consuming", createPlainTransportRequest.Consuming },
                    { "Producing", createPlainTransportRequest.Producing },
                },
            };

            await using (await _joinedLock.ReadLockAsync())
            {
                CheckJoined("CreatePlainTransportAsync()");

                await using (await _roomLock.ReadLockAsync())
                {
                    CheckRoom("CreatePlainTransportAsync()");

                    var transport =
                        await _room!.Router.CreatePlainTransportAsync(plainTransportOptions)
                        ?? throw new Exception("CreatePlainTransportAsync() | Router.CreatePlainTransport failed");

                    await using (await _transportsLock.WriteLockAsync())
                    {
                        // Store the PlainTransport into the Peer data Object.
                        _transports[transport.TransportId] = transport;
                    }

                    transport.On(
                        "@close",
                        (_, _) =>
                        {
                            // 因为调用 transport.CloseAsync() 之前已经使用 _transportsLock 写锁，所以触发该事件的调用从 _transports 移除无需再次加锁。
                            _transports.Remove(transport.TransportId);
                            return Task.CompletedTask;
                        }
                    );
                    transport.On(
                        "routerclose",
                        async (_, _) =>
                        {
                            await using (await _transportsLock.WriteLockAsync())
                            {
                                _transports.Remove(transport.TransportId);
                            }
                        }
                    );

                    return transport;
                }
            }
        }

        /// <summary>
        /// 拉取
        /// </summary>
        public async Task<PeerPullResult> PullAsync(Peer producerPeer, IEnumerable<string> sources)
        {
            await using (await _joinedLock.ReadLockAsync())
            {
                CheckJoined("PullAsync()");

                await using (await _roomLock.ReadLockAsync())
                {
                    CheckRoom("PullAsync()");

                    var consumerRoom = _room!;

                    await using (await _consumersLock.ReadLockAsync())
                    {
                        // producerPeer 也有可能是本 Peer
                        if (producerPeer.PeerId == PeerId)
                        {
                            return await PullInternalAsync(producerPeer, sources);
                        }
                        else
                        {
                            await using (await producerPeer._joinedLock.ReadLockAsync())
                            {
                                producerPeer.CheckJoined("PullAsync()");

                                await using (await producerPeer._roomLock.ReadLockAsync())
                                {
                                    producerPeer.CheckRoom("PullAsync()");

                                    var producerRoom = producerPeer._room!;

                                    if (producerRoom.RoomId != consumerRoom.RoomId)
                                    {
                                        throw new Exception(
                                            $"PullAsync() | Peer:{producerPeer.PeerId} and Peer:{PeerId} are not in the same room."
                                        );
                                    }

                                    var sourcesArray = sources as string[] ?? sources.ToArray();
                                    if (sourcesArray.Except(producerPeer.Sources).Any())
                                    {
                                        throw new Exception(
                                            $"PullAsync() | Peer:{producerPeer.PeerId} can't produce some sources."
                                        );
                                    }

                                    return await PullInternalAsync(producerPeer, sourcesArray);
                                }
                            }
                        }
                    }
                }
            }
        }

        private async Task<PeerPullResult> PullInternalAsync(Peer producerPeer, IEnumerable<string> sources)
        {
            var consumerActiveRoom = _room!;

            var roomId = consumerActiveRoom.RoomId;

            await using (await producerPeer._producersLock.ReadLockAsync())
            {
                var producerProducers = producerPeer._producers.Values.Where(m => sources.Contains(m.Source)).ToArray();

                var existsProducers = new HashSet<Producer>();
                var produceSources = new HashSet<string>();
                foreach (var source in sources)
                {
                    foreach (var existsProducer in producerProducers)
                    {
                        // 忽略重复消费
                        if (_consumers.Values.Any(m => m.ProducerId == existsProducer.ProducerId))
                        {
                            continue;
                        }

                        existsProducers.Add(existsProducer);
                    }

                    await producerPeer._pullPaddingsLock.WaitAsync();
                    // 如果 Source 没有对应的 Producer，通知 otherPeer 生产；生产成功后又要通知本 Peer 去对应的 Room 消费。
                    if (producerPeer._pullPaddings.All(m => m.Source != source))
                    {
                        produceSources.Add(source);
                    }

                    if (
                        !producerPeer._pullPaddings.Any(m =>
                            m.Source == source && m.RoomId == roomId && m.ConsumerPeerId == PeerId
                        )
                    )
                    {
                        producerPeer._pullPaddings.Add(
                            new PullPadding
                            {
                                RoomId = roomId,
                                ConsumerPeerId = PeerId,
                                Source = source,
                            }
                        );
                    }

                    producerPeer._pullPaddingsLock.Set();
                }

                return new PeerPullResult { ExistsProducers = existsProducers.ToArray(), ProduceSources = produceSources };
            }
        }

        /// <summary>
        /// 生产
        /// </summary>
        public async Task<PeerProduceResult> ProduceAsync(ProduceRequest produceRequest)
        {
            _logger.LogInformation(
                "ProduceAsync() | Peer:{PeerId} attempting to produce Source:\"{Source}\", Kind:{Kind}",
                PeerId, produceRequest.Source, produceRequest.Kind
            );

            if (produceRequest.Source.IsNullOrWhiteSpace())
            {
                throw new Exception($"ProduceAsync() | Peer:{PeerId} AppData[\"source\"] is null or white space.");
            }

            if (!Sources.Contains(produceRequest.Source))
            {
                throw new Exception($"ProduceAsync() | Source:\"{produceRequest.Source}\" cannot be produce.");
            }

            // Add peerId into appData to later get the associated Peer during
            // the 'loudest' event of the audioLevelObserver.
            produceRequest.AppData["peerId"] = PeerId;

            await using (await _joinedLock.ReadLockAsync())
            {
                CheckJoined("ProduceAsync()");

                await using (await _roomLock.ReadLockAsync())
                {
                    CheckRoom("ProduceAsync()");

                    await using (await _transportsLock.ReadLockAsync())
                    {
                        var transport =
                            GetProducingTransport()
                            ?? throw new Exception("ProduceAsync() | Transport:Producing is not exists.");

                        await using (await _producersLock.WriteLockAsync())
                        {
                            _logger.LogDebug(
                                "ProduceAsync() | Peer:{PeerId} checking existing producers, count:{Count}, sources:[{Sources}]",
                                PeerId, _producers.Count, string.Join(", ", _producers.Values.Select(p => $"{p.ProducerId}:{p.Source}"))
                            );

                            // 检查是否已存在相同 Source 的 Producer
                            var existingProducer = _producers.Values.FirstOrDefault(m => m.Source == produceRequest.Source);
                            if (existingProducer != null)
                            {
                                // 重要修复：切换摄像头场景下，先关闭旧的 Producer 再创建新的
                                // 而不是直接返回现有的，因为现有的可能已经是无效状态
                                _logger.LogWarning(
                                    "ProduceAsync() | Peer:{PeerId} Source:\"{Source}\" already exists with ProducerId:{ProducerId}, closing old producer first",
                                    PeerId, produceRequest.Source, existingProducer.ProducerId
                                );
                                
                                try
                                {
                                    await existingProducer.CloseAsync();
                                    _logger.LogInformation(
                                        "ProduceAsync() | Peer:{PeerId} old Producer:{ProducerId} closed successfully for Source:\"{Source}\"",
                                        PeerId, existingProducer.ProducerId, produceRequest.Source
                                    );
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, 
                                        "ProduceAsync() | Peer:{PeerId} failed to close old Producer:{ProducerId}, will continue creating new one",
                                        PeerId, existingProducer.ProducerId
                                    );
                                }
                            }

                            _logger.LogInformation(
                                "ProduceAsync() | Peer:{PeerId} creating new producer for Source:\"{Source}\"",
                                PeerId, produceRequest.Source
                            );

                            var producer = await transport.ProduceAsync(
                                new ProducerOptions
                                {
                                    Kind = produceRequest.Kind,
                                    RtpParameters = produceRequest.RtpParameters,
                                    AppData = produceRequest.AppData,
                                }
                            );

                            // Store producer source
                            producer.Source = produceRequest.Source;

                            producer.On(
                                "@close",
                                async (_, _) =>
                                {
                                    // 因为调用 producer.Close() 之前已经使用 _producersLock 写锁，所以触发该事件的调用从 _producers 移除无需再次加锁。
                                    _producers.Remove(producer.ProducerId);

                                    await _pullPaddingsLock.WaitAsync();
                                    _pullPaddings.Clear();
                                    _pullPaddingsLock.Set();
                                }
                            );
                            producer.On(
                                "transportclose",
                                async (_, _) =>
                                {
                                    await using (await _producersLock.WriteLockAsync())
                                    {
                                        _producers.Remove(producer.ProducerId);
                                    }

                                    await _pullPaddingsLock.WaitAsync();
                                    _pullPaddings.Clear();
                                    _pullPaddingsLock.Set();
                                }
                            );

                            await _pullPaddingsLock.WaitAsync();
                            var matchedPullPaddings = _pullPaddings.Where(m => m.Source == producer.Source).ToArray();
                            foreach (var item in matchedPullPaddings)
                            {
                                _pullPaddings.Remove(item);
                            }

                            _pullPaddingsLock.Set();

                            // Store the Producer into the Peer data Object.
                            _producers[producer.ProducerId] = producer;

                            _logger.LogInformation(
                                "ProduceAsync() | Peer:{PeerId} producer created successfully: ProducerId:{ProducerId}, Source:\"{Source}\", producers now: [{Producers}]",
                                PeerId, producer.ProducerId, producer.Source, string.Join(", ", _producers.Keys)
                            );

                            // Add into the audioLevelObserver.
                            if (producer.Data.Kind == MediaKind.AUDIO)
                            {
                                // Fire and forget
                                _room!
                                    .AudioLevelObserver.AddProducerAsync(
                                        new RtpObserverAddRemoveProducerOptions { ProducerId = producer.ProducerId }
                                    )
                                    .ContinueWithOnFaultedHandleLog(_logger);
                            }

                            return new PeerProduceResult { Producer = producer, PullPaddings = matchedPullPaddings };
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 消费
        /// </summary>
        public async Task<Consumer?> ConsumeAsync(Peer producerPeer, string producerId)
        {
            await using (await _joinedLock.ReadLockAsync())
            {
                CheckJoined("ConsumeAsync()");

                await using (await _roomLock.ReadLockAsync())
                {
                    CheckRoom("ConsumeAsync()");

                    await using (await _transportsLock.ReadLockAsync())
                    {
                        var transport =
                            GetConsumingTransport()
                            ?? throw new Exception($"ConsumeAsync() | Peer:{PeerId} Transport for consuming not found.");

                        await using (await _consumersLock.WriteLockAsync())
                        {
                            // 已经在消费
                            if (_consumers.Any(m => m.Value.ProducerId == producerId))
                            {
                                _logger.LogDebug(
                                    "ConsumeAsync() | Peer:{PeerId} already consuming ProducerId:{ProducerId} from ProducerPeer:{ProducerPeerId}",
                                    PeerId, producerId, producerPeer.PeerId
                                );
                                return null;
                            }

                            await using (await producerPeer._producersLock.ReadLockAsync())
                            {
                                if (!producerPeer._producers.TryGetValue(producerId, out var producer))
                                {
                                    _logger.LogWarning(
                                        "ConsumeAsync() | Peer:{PeerId} - ProducerPeer:{ProducerPeerId} has no Producer:{ProducerId}, available producers: [{Producers}]",
                                        PeerId, producerPeer.PeerId, producerId, 
                                        string.Join(", ", producerPeer._producers.Keys)
                                    );
                                    throw new Exception(
                                        $"ConsumeAsync() | Peer:{PeerId} - ProducerPeer:{producerPeer.PeerId} has no Producer:{producerId}"
                                    );
                                }

                                var canConsume = await _room!.Router.CanConsumeAsync(producer.ProducerId, _rtpCapabilities);
                                if (!canConsume)
                                {
                                    _logger.LogWarning(
                                        "ConsumeAsync() | Peer:{PeerId} cannot consume ProducerId:{ProducerId} from ProducerPeer:{ProducerPeerId} - RTP capabilities mismatch",
                                        PeerId, producerId, producerPeer.PeerId
                                    );
                                    throw new Exception($"ConsumeAsync() | Peer:{PeerId} Can not consume.");
                                }

                                // Create the Consumer in paused mode.
                                var consumer = await transport.ConsumeAsync(
                                    new ConsumerOptions
                                    {
                                        ProducerId = producer.ProducerId,
                                        RtpCapabilities = _rtpCapabilities,
                                        Paused = true, // Or: producer.Kind == MediaKind.Video
                                    }
                                );

                                consumer.Source = producer.Source;

                                consumer.On(
                                    "@close",
                                    async (_, _) =>
                                    {
                                        // 因为调用 consumer.CloseAsync() 之前已经使用 _consumersLock 写锁，所以触发该事件的调用从 _consumers 移除无需再次加锁。
                                        _consumers.Remove(consumer.ConsumerId);
                                        await producer.RemoveConsumerAsync(consumer.ConsumerId);
                                    }
                                );
                                consumer.On(
                                    "producerclose,transportclose",
                                    async (_, _) =>
                                    {
                                        await using (await _consumersLock.WriteLockAsync())
                                        {
                                            _consumers.Remove(consumer.ConsumerId);
                                        }

                                        await producer.RemoveConsumerAsync(consumer.ConsumerId);
                                    }
                                );

                                await producer.AddConsumerAsync(consumer);

                                // Store the Consumer into the consumerPeer data Object.
                                _consumers[consumer.ConsumerId] = consumer;

                                return consumer;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 停止生产
        /// </summary>
        public async Task<bool> CloseProducerAsync(string producerId)
        {
            _logger.LogInformation(
                "CloseProducerAsync() | Peer:{PeerId} attempting to close Producer:{ProducerId}",
                PeerId, producerId
            );

            await using (await _joinedLock.ReadLockAsync())
            {
                CheckJoined("CloseProducerAsync()");

                await using (await _roomLock.ReadLockAsync())
                {
                    CheckRoom("CloseProducerAsync()");

                    // NOTE: 因为 Close 会触发 Emit("@close")，而 @close 的事件处理需要写锁。故使用写锁。
                    await using (await _producersLock.WriteLockAsync())
                    {
                        if (!_producers.TryGetValue(producerId, out var producer))
                        {
                            throw new Exception($"CloseProducerAsync() | Peer:{PeerId} has no Producer:{producerId}.");
                        }

                        var source = producer.Source;
                        _logger.LogDebug(
                            "CloseProducerAsync() | Peer:{PeerId} closing Producer:{ProducerId}, Source:\"{Source}\", producers before close: [{Producers}]",
                            PeerId, producerId, source, string.Join(", ", _producers.Keys)
                        );

                        await producer.CloseAsync();

                        _logger.LogInformation(
                            "CloseProducerAsync() | Peer:{PeerId} Producer:{ProducerId} closed successfully, Source:\"{Source}\", producers after close: [{Producers}]",
                            PeerId, producerId, source, string.Join(", ", _producers.Keys)
                        );

                        return true;
                    }
                }
            }
        }

        /// <summary>
        /// 停止生产全部
        /// </summary>
        public async Task<bool> CloseAllProducersAsync()
        {
            await using (await _joinedLock.ReadLockAsync())
            {
                CheckJoined("CloseAllProducersAsync()");

                await using (await _roomLock.ReadLockAsync())
                {
                    CheckRoom("CloseAllProducersAsync()");

                    // NOTE: 因为 Close 会触发 Emit("@close")，而 @close 的事件处理需要写锁。故使用写锁。
                    await using (await _producersLock.WriteLockAsync())
                    {
                        var producers = _producers.Values.ToArray();
                        foreach (var producer in producers)
                        {
                            await producer.CloseAsync();
                        }

                        return true;
                    }
                }
            }
        }

        /// <summary>
        /// 停止生产指定 Source
        /// </summary>
        public async Task<bool> CloseProducerWithSourcesAsync(IEnumerable<string> sources)
        {
            await using (await _joinedLock.ReadLockAsync())
            {
                CheckJoined("CloseProducerWithSourcesAsync()");

                await using (await _roomLock.ReadLockAsync())
                {
                    CheckRoom("CloseProducerWithSourcesAsync()");

                    // NOTE: 因为 Close 会触发 Emit("@close")，而 @close 的事件处理需要写锁。故使用写锁。
                    await using (await _producersLock.WriteLockAsync())
                    {
                        var producers = _producers.Values.Where(m => sources.Contains(m.Source)).ToArray();
                        foreach (var producer in producers)
                        {
                            await producer.CloseAsync();
                        }

                        return true;
                    }
                }
            }
        }

        /// <summary>
        /// 暂停生产
        /// </summary>
        public async Task<bool> PauseProducerAsync(string producerId)
        {
            await using (await _joinedLock.ReadLockAsync())
            {
                CheckJoined("PauseProducerAsync()");

                await using (await _roomLock.ReadLockAsync())
                {
                    CheckRoom("PauseProducerAsync()");

                    await using (await _producersLock.ReadLockAsync())
                    {
                        if (!_producers.TryGetValue(producerId, out var producer))
                        {
                            throw new Exception($"PauseProducerAsync() | Peer:{PeerId} has no Producer:{producerId}.");
                        }

                        await producer.PauseAsync();
                        return true;
                    }
                }
            }
        }

        /// <summary>
        /// 恢复生产
        /// </summary>
        public async Task<bool> ResumeProducerAsync(string producerId)
        {
            await using (await _joinedLock.ReadLockAsync())
            {
                CheckJoined("ResumeProducerAsync()");

                await using (await _roomLock.ReadLockAsync())
                {
                    CheckRoom("ResumeProducerAsync()");

                    await using (await _producersLock.ReadLockAsync())
                    {
                        if (!_producers.TryGetValue(producerId, out var producer))
                        {
                            throw new Exception($"ResumeProducerAsync() | Peer:{PeerId} has no Producer:{producerId}.");
                        }

                        await producer.ResumeAsync();
                        return true;
                    }
                }
            }
        }

        /// <summary>
        /// 停止消费
        /// </summary>
        public async Task<bool> CloseConsumerAsync(string consumerId)
        {
            await using (await _joinedLock.ReadLockAsync())
            {
                CheckJoined("CloseConsumerAsync()");

                await using (await _roomLock.ReadLockAsync())
                {
                    CheckRoom("CloseConsumerAsync()");

                    // NOTE: 因为 Close 会触发 Emit("@close")，而 @close 的事件处理需要写锁。故使用写锁。
                    await using (await _consumersLock.WriteLockAsync())
                    {
                        if (!_consumers.TryGetValue(consumerId, out var consumer))
                        {
                            throw new Exception($"CloseConsumerAsync() | Peer:{PeerId} has no Consumer:{consumerId}.");
                        }

                        await consumer.CloseAsync();
                        return true;
                    }
                }
            }
        }

        /// <summary>
        /// 暂停消费
        /// </summary>
        public async Task<bool> PauseConsumerAsync(string consumerId)
        {
            await using (await _joinedLock.ReadLockAsync())
            {
                CheckJoined("PauseConsumerAsync()");

                await using (await _roomLock.ReadLockAsync())
                {
                    CheckRoom("PauseConsumerAsync()");

                    await using (await _consumersLock.ReadLockAsync())
                    {
                        if (!_consumers.TryGetValue(consumerId, out var consumer))
                        {
                            throw new Exception($"PauseConsumerAsync() | Peer:{PeerId} has no Consumer:{consumerId}.");
                        }

                        await consumer.PauseAsync();
                        return true;
                    }
                }
            }
        }

        /// <summary>
        /// 恢复消费
        /// </summary>
        public async Task<Consumer> ResumeConsumerAsync(string consumerId)
        {
            await using (await _joinedLock.ReadLockAsync())
            {
                CheckJoined("ResumeConsumerAsync()");

                await using (await _roomLock.ReadLockAsync())
                {
                    CheckRoom("ResumeConsumerAsync()");

                    await using (await _consumersLock.ReadLockAsync())
                    {
                        if (!_consumers.TryGetValue(consumerId, out var consumer))
                        {
                            throw new Exception($"ResumeConsumerAsync() | Peer:{PeerId} has no Consumer:{consumerId}.");
                        }

                        await consumer.ResumeAsync();
                        return consumer;
                    }
                }
            }
        }

        /// <summary>
        /// 设置消费建议 Layers
        /// </summary>
        public async Task<bool> SetConsumerPreferredLayersAsync(
            SetConsumerPreferredLayersRequest setConsumerPreferredLayersRequest
        )
        {
            await using (await _joinedLock.ReadLockAsync())
            {
                CheckJoined("SetConsumerPreferredLayersAsync()");

                await using (await _roomLock.ReadLockAsync())
                {
                    CheckRoom("SetConsumerPreferredLayersAsync()");

                    await using (await _consumersLock.ReadLockAsync())
                    {
                        if (!_consumers.TryGetValue(setConsumerPreferredLayersRequest.ConsumerId, out var consumer))
                        {
                            throw new Exception(
                                $"SetConsumerPreferredLayersAsync() | Peer:{PeerId} has no Consumer:{setConsumerPreferredLayersRequest.ConsumerId}."
                            );
                        }

                        await consumer.SetPreferredLayersAsync(setConsumerPreferredLayersRequest);
                        return true;
                    }
                }
            }
        }

        /// <summary>
        /// 设置消费 Priority
        /// </summary>
        public async Task<bool> SetConsumerPriorityAsync(SetConsumerPriorityRequest setConsumerPriorityRequest)
        {
            await using (await _joinedLock.ReadLockAsync())
            {
                CheckJoined("SetConsumerPriorityAsync()");

                await using (await _roomLock.ReadLockAsync())
                {
                    CheckRoom("SetConsumerPriorityAsync()");

                    await using (await _consumersLock.ReadLockAsync())
                    {
                        if (!_consumers.TryGetValue(setConsumerPriorityRequest.ConsumerId, out var consumer))
                        {
                            throw new Exception(
                                $"SetConsumerPriorityAsync() | Peer:{PeerId} has no Consumer:{setConsumerPriorityRequest.ConsumerId}."
                            );
                        }

                        await consumer.SetPriorityAsync(setConsumerPriorityRequest);
                        return true;
                    }
                }
            }
        }

        /// <summary>
        /// 请求关键帧
        /// </summary>
        public async Task<bool> RequestConsumerKeyFrameAsync(string consumerId)
        {
            await using (await _joinedLock.ReadLockAsync())
            {
                CheckJoined("RequestConsumerKeyFrameAsync()");

                await using (await _roomLock.ReadLockAsync())
                {
                    CheckRoom("RequestConsumerKeyFrameAsync()");

                    await using (await _consumersLock.ReadLockAsync())
                    {
                        if (!_consumers.TryGetValue(consumerId, out var consumer))
                        {
                            throw new Exception(
                                $"RequestConsumerKeyFrameAsync() | Peer:{PeerId} has no Producer:{consumerId}."
                            );
                        }

                        await consumer.RequestKeyFrameAsync();
                        return true;
                    }
                }
            }
        }

        /// <summary>
        /// 获取 WebRtcTransport 状态
        /// </summary>
        public async Task<object[]> GetWebRtcTransportStatsAsync(string transportId)
        {
            await using (await _joinedLock.ReadLockAsync())
            {
                CheckJoined("GetWebRtcTransportStatsAsync()");

                await using (await _roomLock.ReadLockAsync())
                {
                    CheckRoom("GetWebRtcTransportStatsAsync()");

                    await using (await _transportsLock.ReadLockAsync())
                    {
                        if (_transports.TryGetValue(transportId, out var transport))
                        {
                            throw new Exception(
                                $"GetWebRtcTransportStatsAsync() | Peer:{PeerId} has no Transport:{transportId}."
                            );
                        }

                        var stats = await transport!.GetStatsAsync();
                        return stats;
                    }
                }
            }
        }

        /// <summary>
        /// 获取生产者状态
        /// </summary>
        public async Task<object[]> GetProducerStatsAsync(string producerId)
        {
            await using (await _joinedLock.ReadLockAsync())
            {
                CheckJoined("GetProducerStatsAsync()");

                await using (await _roomLock.ReadLockAsync())
                {
                    CheckRoom("GetProducerStatsAsync()");

                    await using (await _producersLock.ReadLockAsync())
                    {
                        if (!_producers.TryGetValue(producerId, out var producer))
                        {
                            throw new Exception($"GetProducerStatsAsync() | Peer:{PeerId} has no Producer:{producerId}.");
                        }

                        var fbsStats = await producer.GetStatsAsync();
                        var stats = fbsStats.Select(m => m.Data.Value).ToArray();
                        return stats;
                    }
                }
            }
        }

        /// <summary>
        /// 获取消费者状态
        /// </summary>
        public async Task<object[]> GetConsumerStatsAsync(string consumerId)
        {
            await using (await _joinedLock.ReadLockAsync())
            {
                CheckJoined("GetConsumerStatsAsync()");

                await using (await _roomLock.ReadLockAsync())
                {
                    CheckRoom("GetConsumerStatsAsync()");

                    await using (await _consumersLock.ReadLockAsync())
                    {
                        if (!_consumers.TryGetValue(consumerId, out var consumer))
                        {
                            throw new Exception($"GetConsumerStatsAsync() | Peer:{PeerId} has no Consumer:{consumerId}.");
                        }

                        var fbsStats = await consumer.GetStatsAsync();
                        var stats = fbsStats.Select(m => m.Data.Value).ToArray();
                        return stats;
                    }
                }
            }
        }

        /// <summary>
        /// 重置 Ice
        /// </summary>
        public async Task<IceParametersT> RestartIceAsync(string transportId)
        {
            await using (await _joinedLock.ReadLockAsync())
            {
                CheckJoined("RestartIceAsync()");

                await using (await _roomLock.ReadLockAsync())
                {
                    CheckRoom("RestartIceAsync()");

                    await using (await _transportsLock.ReadLockAsync())
                    {
                        if (_transports.TryGetValue(transportId, out var transport))
                        {
                            throw new Exception($"RestartIceAsync() | Peer:{PeerId} has no Transport:{transportId}.");
                        }

                        if (transport is not WebRtcTransport webRtcTransport)
                        {
                            throw new Exception(
                                $"RestartIceAsync() | Peer:{PeerId} Transport:{transportId} is not WebRtcTransport."
                            );
                        }

                        var iceParameters = await webRtcTransport.RestartIceAsync();
                        return iceParameters;
                    }
                }
            }
        }

        /// <summary>
        /// 加入房间
        /// </summary>
        public async Task<JoinRoomResult> JoinRoomAsync(Room room)
        {
            await using (await _joinedLock.ReadLockAsync())
            {
                CheckJoined("JoinRoomAsync()");
        
                await using (await _roomLock.WriteLockAsync())
                {
                    // 如果已经在同一个房间中，返回当前房间的信息（幂等操作）
                    if (_room != null)
                    {
                        if (_room.RoomId == room.RoomId)
                        {
                            // 已在同一个房间，返回当前状态（幂等）
                            return await _room.GetJoinRoomResultAsync(this);
                        }
                        // 在不同的房间中，抛出异常
                        throw new PeerInRoomException("JoinRoomAsync()", PeerId, room.RoomId);
                    }
        
                    _room = room;
        
                    return await _room.PeerJoinAsync(this);
                }
            }
        }

        /// <summary>
        /// 离开房间
        /// </summary>
        public async Task<LeaveRoomResult> LeaveRoomAsync()
        {
            await using (await _joinedLock.ReadLockAsync())
            {
                CheckJoined("LeaveRoomAsync()");

                await using (await _roomLock.WriteLockAsync())
                {
                    CheckRoom("LeaveRoomAsync()");

                    // NOTE: 因为 Close 会触发 Emit("@close")，而 @close 的事件处理需要写锁。故使用写锁。
                    await using (await _transportsLock.WriteLockAsync())
                    {
                        // Iterate and close all mediasoup Transport associated to this Peer, so all
                        // its Producers and Consumers will also be closed.
                        foreach (var transport in _transports.Values)
                        {
                            await transport.CloseAsync();
                        }
                    }

                    var result = await _room!.PeerLeaveAsync(PeerId);
                    _room = null;
                    return result;
                }
            }
        }

        /// <summary>
        /// 强制离开房间（用于被踢出场景，不再调用 Room.PeerLeaveAsync 因为已经被移除）
        /// </summary>
        public async Task ForceLeaveRoomAsync()
        {
            await using (await _joinedLock.ReadLockAsync())
            {
                if (!_joined)
                {
                    return;
                }

                await using (await _roomLock.WriteLockAsync())
                {
                    if (_room == null)
                    {
                        return;
                    }

                    // NOTE: 因为 Close 会触发 Emit("@close")，而 @close 的事件处理需要写锁。故使用写锁。
                    await using (await _transportsLock.WriteLockAsync())
                    {
                        // Iterate and close all mediasoup Transport associated to this Peer, so all
                        // its Producers and Consumers will also be closed.
                        foreach (var transport in _transports.Values)
                        {
                            await transport.CloseAsync();
                        }
                    }

                    // 不调用 Room.PeerLeaveAsync，因为在被踢出场景中 peer 已经被 Room.KickPeerAsync 移除
                    _room = null;
                }
            }
        }

        /// <summary>
        /// 离开
        /// </summary>
        public async Task<LeaveResult> LeaveAsync()
        {
            var leaveResult = new LeaveResult { SelfPeer = this, OtherPeerIds = [] };

            if (!_joined)
            {
                return leaveResult;
            }

            await using (await _joinedLock.WriteLockAsync())
            {
                if (!_joined)
                {
                    return leaveResult;
                }

                _joined = false;

                await using (await _roomLock.WriteLockAsync())
                {
                    // NOTE: 因为 Close 会触发 Emit("@close")，而 @close 的事件处理需要写锁。故使用写锁。
                    await using (await _transportsLock.WriteLockAsync())
                    {
                        // Iterate and close all mediasoup Transport associated to this Peer, so all
                        // its Producers and Consumers will also be closed.
                        foreach (var transport in _transports.Values)
                        {
                            await transport.CloseAsync();
                        }
                    }

                    if (_room != null)
                    {
                        var leaveRoomResult = await _room.PeerLeaveAsync(PeerId);
                        leaveResult.OtherPeerIds = leaveRoomResult.OtherPeerIds;
                        _room = null;
                    }

                    return leaveResult;
                }
            }
        }

        /// <summary>
        /// 设置 AppData
        /// </summary>
        public async Task<PeerAppDataResult> SetPeerAppDataAsync(SetPeerAppDataRequest setPeerAppDataRequest)
        {
            var peerAppDataResult = new PeerAppDataResult { SelfPeerId = PeerId, OtherPeerIds = [] };

            await using (await _joinedLock.ReadLockAsync())
            {
                CheckJoined("SetPeerAppDataAsync()");

                foreach (var item in setPeerAppDataRequest.AppData)
                {
                    AppData.AddOrUpdate(item.Key, item.Value, (oldKey, oldValue) => item.Value);
                }

                peerAppDataResult.AppData = AppData.ToDictionary(x => x.Key, x => x.Value);

                await using (await _roomLock.ReadLockAsync())
                {
                    var allPeerIds = await GetPeerIdsInternalAsync();
                    peerAppDataResult.OtherPeerIds = allPeerIds.Where(m => m != PeerId).ToArray();
                    return peerAppDataResult;
                }
            }
        }

        /// <summary>
        /// 移除 AppData
        /// </summary>
        public async Task<PeerAppDataResult> UnsetPeerAppDataAsync(UnsetPeerAppDataRequest unsetPeerAppDataRequest)
        {
            var peerAppDataResult = new PeerAppDataResult { SelfPeerId = PeerId, OtherPeerIds = [] };

            await using (await _joinedLock.ReadLockAsync())
            {
                CheckJoined("UnsetPeerAppDataAsync()");

                foreach (var item in unsetPeerAppDataRequest.Keys)
                {
                    AppData.TryRemove(item, out var _);
                }

                peerAppDataResult.AppData = AppData.ToDictionary(x => x.Key, x => x.Value);

                await using (await _roomLock.ReadLockAsync())
                {
                    var allPeerIds = await GetPeerIdsInternalAsync();
                    peerAppDataResult.OtherPeerIds = allPeerIds.Where(m => m != PeerId).ToArray();
                    return peerAppDataResult;
                }
            }
        }

        /// <summary>
        /// 清空 AppData
        /// </summary>
        public async Task<PeerAppDataResult> ClearPeerAppDataAsync()
        {
            var peerAppDataResult = new PeerAppDataResult
            {
                SelfPeerId = PeerId,
                OtherPeerIds = [],
                AppData = new Dictionary<string, object>(),
            };

            await using (await _joinedLock.ReadLockAsync())
            {
                CheckJoined("ClearPeerAppDataAsync()");

                AppData.Clear();

                await using (await _roomLock.ReadLockAsync())
                {
                    var allPeerIds = await GetPeerIdsInternalAsync();
                    peerAppDataResult.OtherPeerIds = allPeerIds.Where(m => m != PeerId).ToArray();
                    return peerAppDataResult;
                }
            }
        }

        /// <summary>
        /// 设置 InternalData
        /// </summary>
        public async Task<PeerInternalDataResult> SetPeerInternalDataAsync(
            SetPeerInternalDataRequest setPeerInternalDataRequest
        )
        {
            var peerInternalDataResult = new PeerInternalDataResult();

            await using (await _joinedLock.ReadLockAsync())
            {
                CheckJoined("SetPeerInternalDataAsync()");

                foreach (var item in setPeerInternalDataRequest.InternalData)
                {
                    InternalData.AddOrUpdate(item.Key, item.Value, (oldKey, oldValue) => item.Value);
                }

                peerInternalDataResult.InternalData = InternalData.ToDictionary(x => x.Key, x => x.Value);
                return peerInternalDataResult;
            }
        }

        /// <summary>
        /// 获取 InternalData
        /// </summary>
        public async Task<PeerInternalDataResult> GetPeerInternalDataAsync()
        {
            var peerInternalDataResult = new PeerInternalDataResult();

            await using (await _joinedLock.ReadLockAsync())
            {
                CheckJoined("GetPeerInternalDataAsync()");

                peerInternalDataResult.InternalData = InternalData.ToDictionary(x => x.Key, x => x.Value);
                return peerInternalDataResult;
            }
        }

        /// <summary>
        /// 移除 InternalData
        /// </summary>
        public async Task<PeerInternalDataResult> UnsetPeerInternalDataAsync(
            UnsetPeerInternalDataRequest unsetPeerInternalDataRequest
        )
        {
            var peerInternalDataResult = new PeerInternalDataResult();

            await using (await _joinedLock.ReadLockAsync())
            {
                CheckJoined("UnsetPeerInternalDataAsync()");

                foreach (var item in unsetPeerInternalDataRequest.Keys)
                {
                    AppData.TryRemove(item, out var _);
                }

                peerInternalDataResult.InternalData = InternalData.ToDictionary(x => x.Key, x => x.Value);
                return peerInternalDataResult;
            }
        }

        /// <summary>
        /// 清空 InternalData
        /// </summary>
        public async Task<PeerInternalDataResult> ClearPeerInternalDataAsync()
        {
            var peerInternalDataResult = new PeerInternalDataResult { InternalData = new Dictionary<string, object>() };

            await using (await _joinedLock.ReadLockAsync())
            {
                CheckJoined("ClearPeerInternalDataAsync()");

                InternalData.Clear();

                return peerInternalDataResult;
            }
        }

        /// <summary>
        /// 获取 Room 内其他 Peer 的 Id
        /// </summary>
        public async Task<string[]> GetOtherPeerIdsAsync(UserRole? role = null)
        {
            var peers = await GetOtherPeersAsync(role);
            return peers.Select(m => m.PeerId).ToArray();
        }

        /// <summary>
        /// 获取 Room 内其他 Peer
        /// </summary>
        public async Task<Peer[]> GetOtherPeersAsync(UserRole? role = null)
        {
            await using (await _joinedLock.ReadLockAsync())
            {
                CheckJoined("GetOtherPeersAsync()");

                await using (await _roomLock.ReadLockAsync())
                {
                    CheckRoom("GetOtherPeersAsync()");

                    var allPeers = await GetPeersInternalAsync();
                    var query = allPeers.Where(m => m.PeerId != PeerId);
                    if (role.HasValue)
                    {
                        query = query.Where(m =>
                            m.InternalData.TryGetValue(RoleKey, out var r)
                            && r.GetType() == typeof(UserRole)
                            && (UserRole)r == role.Value
                        );
                    }

                    return query.ToArray();
                }
            }
        }

        /// <summary>
        /// 获取用户角色
        /// </summary>
        public async Task<UserRole> GetRoleAsync()
        {
            await using (await _joinedLock.ReadLockAsync())
            {
                CheckJoined("GetRoleAsync()");

                return InternalData.TryGetValue(RoleKey, out var role) && role.GetType() == typeof(UserRole)
                    ? (UserRole)role
                    : UserRole.Normal;
            }
        }

        #region Private Methods

        private Transport? GetProducingTransport()
        {
            return _transports.Values.FirstOrDefault(m =>
                m.AppData.TryGetValue("Producing", out var value) && (bool)value
            );
        }

        private Transport? GetConsumingTransport()
        {
            return _transports.Values.FirstOrDefault(m =>
                m.AppData.TryGetValue("Consuming", out var value) && (bool)value
            );
        }

        private bool HasProducingTransport()
        {
            return _transports.Values.Any(m =>
                m.AppData.TryGetValue("Producing", out var value) && (bool)value
            );
        }

        private bool HasConsumingTransport()
        {
            return _transports.Values.Any(m =>
                m.AppData.TryGetValue("Consuming", out var value) && (bool)value
            );
        }

        /// <summary>
        /// 检查 Peer 是否已创建 Recv Transport（用于判断是否可以消费其他 Peer 的视频）
        /// </summary>
        public async Task<bool> HasRecvTransportAsync()
        {
            await using (await _transportsLock.ReadLockAsync())
            {
                return HasConsumingTransport();
            }
        }

        private void CheckJoined(string tag)
        {
            if (!_joined)
            {
                throw new PeerNotJoinedException(tag, PeerId);
            }
        }

        private void CheckRoom(string tag)
        {
            if (_room == null)
            {
                throw new PeerNotInRoomException(tag, PeerId);
            }
        }

        private async Task<string[]> GetPeerIdsInternalAsync()
        {
            return _room != null ? await _room.GetPeerIdsAsync() : [];
        }

        private async Task<Peer[]> GetPeersInternalAsync()
        {
            return _room != null ? await _room.GetPeersAsync() : [];
        }

        #endregion Private Methods

        #region Public Room Access

        /// <summary>
        /// 获取 Peer 所在的房间
        /// </summary>
        public async Task<Room?> GetRoomAsync()
        {
            await using (await _roomLock.ReadLockAsync())
            {
                return _room;
            }
        }

        #endregion
    }
}
