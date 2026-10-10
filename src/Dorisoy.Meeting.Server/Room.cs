using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FBS.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Threading;
using Dorisoy.Mediasoup;

namespace Dorisoy.Meeting.Server
{
    public partial class Room : IEquatable<Room>
    {
        public string RoomId { get; }

        public string Name { get; }

        /// <summary>
        /// 主持人 PeerId（房间创建者）
        /// </summary>
        public string? HostPeerId { get; private set; }

        #region IEquatable<T>

        public bool Equals(Room? other)
        {
            if (other is null)
            {
                return false;
            }

            return RoomId == other.RoomId;
        }

        public override bool Equals(object? other)
        {
            return Equals(other as Room);
        }

        public override int GetHashCode()
        {
            return RoomId.GetHashCode();
        }

        #endregion IEquatable<T>
    }

    public partial class Room
    {
        /// <summary>
        /// Logger factory for create logger.
        /// </summary>
        private readonly ILoggerFactory _loggerFactory;

        /// <summary>
        /// Logger.
        /// </summary>
        private readonly ILogger<Room> _logger;

        /// <summary>
        /// Whether the Room is closed.
        /// </summary>
        private bool _closed;

        private readonly AsyncReaderWriterLock _closeLock = new();

        private readonly Dictionary<string, Peer> _peers = new();

        private readonly AsyncReaderWriterLock _peersLock = new();

        /// <summary>
        /// 分片、Peer 分配和 Producer 管道映射只在内存中变更，统一用普通锁保护，锁内不 await。
        /// </summary>
        private readonly object _mediaSync = new();

        private RoomMediaShard[] _shards;

        private readonly Dictionary<string, RoomMediaShard> _peerShards = new();

        private readonly Dictionary<string, RoomProducerRoute> _producerRoutes = new();

        private readonly Dictionary<int, (AudioLevelObserverVolume Volume, DateTime UpdatedAt)> _shardLoudest = new();

        private readonly Func<int, IReadOnlyCollection<IWorker>, Task<RoomMediaShard?>>? _shardFactory;

        private readonly int _maxShardCount;

        private readonly ListenInfoT _pipeListenInfo;

        private Task? _expandShardsTask;

        private static readonly TimeSpan ShardLoudestExpiration = TimeSpan.FromSeconds(2);

        private const string PrimaryVideoSource = "video:cam";

        public RoomMediaShard PrimaryShard { get; }

        public WebRtcServer? WebRtcServer => PrimaryShard.WebRtcServer;

        public Router Router => PrimaryShard.Router;

        public AudioLevelObserver AudioLevelObserver => PrimaryShard.AudioLevelObserver;

        public Room(
            ILoggerFactory loggerFactory,
            RoomMediaShard primaryShard,
            int maxShardCount,
            Func<int, IReadOnlyCollection<IWorker>, Task<RoomMediaShard?>>? shardFactory,
            ListenInfoT pipeListenInfo,
            string roomId,
            string name
        )
        {
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<Room>();
            PrimaryShard = primaryShard;
            _shards = [primaryShard];
            _maxShardCount = Math.Clamp(maxShardCount, 1, RoomMediaShardPolicy.MaxShardsPerRoom);
            _shardFactory = shardFactory;
            _pipeListenInfo = pipeListenInfo;
            RoomId = roomId;
            Name = name.NullOrWhiteSpaceReplace("Default");
            _closed = false;

            HandleAudioLevelObserver(primaryShard);
        }

        /// <summary>
        /// 管道只在本机 Worker 间传输，固定监听回环地址。
        /// </summary>
        public static ListenInfoT CreateDefaultPipeListenInfo(PortRangeT? portRange)
        {
            return new ListenInfoT
            {
                Protocol = Protocol.UDP,
                Ip = "127.0.0.1",
                PortRange = new PortRangeT { Min = portRange?.Min ?? 0, Max = portRange?.Max ?? 0 },
                Flags = new SocketFlagsT(),
            };
        }

        public async Task<JoinRoomResult> PeerJoinAsync(Peer peer)
        {
            await using (await _closeLock.ReadLockAsync())
            {
                if (_closed)
                {
                    throw new Exception($"PeerJoinAsync() | RoomId:{RoomId} was closed.");
                }

                await using (await _peersLock.WriteLockAsync())
                {
                    if (!_peers.TryAdd(peer.PeerId, peer))
                    {
                        throw new Exception($"PeerJoinAsync() | Peer:{peer.PeerId} was in RoomId:{RoomId} already.");
                    }

                    AssignPeerShard(peer.PeerId);
                    TryScheduleShardExpansion(_peers.Count);

                    // 第一个加入房间的人成为主持人
                    if (HostPeerId == null)
                    {
                        HostPeerId = peer.PeerId;
                        _logger.LogInformation("Room {RoomId}: Host set to {PeerId}", RoomId, peer.PeerId);
                    }

                    return new JoinRoomResult 
                    { 
                        SelfPeer = peer, 
                        Peers = _peers.Values.ToArray(),
                        HostPeerId = HostPeerId
                    };
                }
            }
        }

        /// <summary>
        /// 获取当前加入房间结果（用于幂等操作）
        /// </summary>
        public async Task<JoinRoomResult> GetJoinRoomResultAsync(Peer peer)
        {
            await using (await _closeLock.ReadLockAsync())
            {
                if (_closed)
                {
                    throw new Exception($"GetJoinRoomResultAsync() | RoomId:{RoomId} was closed.");
                }

                await using (await _peersLock.ReadLockAsync())
                {
                    return new JoinRoomResult 
                    { 
                        SelfPeer = peer, 
                        Peers = _peers.Values.ToArray(),
                        HostPeerId = HostPeerId
                    };
                }
            }
        }

        /// <summary>
        /// 踢出用户（主持人操作）
        /// </summary>
        public async Task<KickPeerResult?> KickPeerAsync(string hostPeerId, string targetPeerId)
        {
            await using (await _closeLock.ReadLockAsync())
            {
                if (_closed)
                {
                    throw new Exception($"KickPeerAsync() | RoomId:{RoomId} was closed.");
                }

                // 验证主持人权限
                if (HostPeerId != hostPeerId)
                {
                    throw new Exception($"KickPeerAsync() | Peer:{hostPeerId} is not the host of RoomId:{RoomId}.");
                }

                // 不能踢自己
                if (hostPeerId == targetPeerId)
                {
                    throw new Exception($"KickPeerAsync() | Host cannot kick themselves.");
                }

                await using (await _peersLock.WriteLockAsync())
                {
                    if (!_peers.Remove(targetPeerId, out var peer))
                    {
                        throw new Exception($"KickPeerAsync() | Peer:{targetPeerId} is not in RoomId:{RoomId}.");
                    }

                    ReleasePeerShard(targetPeerId);

                    _logger.LogInformation("Room {RoomId}: Peer {TargetPeerId} was kicked by host {HostPeerId}", 
                        RoomId, targetPeerId, hostPeerId);

                    return new KickPeerResult 
                    { 
                        KickedPeer = peer, 
                        OtherPeerIds = _peers.Keys.ToArray() 
                    };
                }
            }
        }

        public async Task<LeaveRoomResult> PeerLeaveAsync(string peerId)
        {
            await using (await _closeLock.ReadLockAsync())
            {
                if (_closed)
                {
                    throw new Exception($"PeerLeaveAsync() | RoomId:{RoomId} was closed.");
                }

                await using (await _peersLock.WriteLockAsync())
                {
                    if (!_peers.Remove(peerId, out var peer))
                    {
                        throw new Exception($"PeerLeaveAsync() | Peer:{peerId} is not in RoomId:{RoomId}.");
                    }

                    ReleasePeerShard(peerId);

                    return new LeaveRoomResult { SelfPeer = peer, OtherPeerIds = _peers.Keys.ToArray() };
                }
            }
        }

        /// <summary>
        /// 强制从房间中移除 Peer（用于房间解散场景，不抛出异常）
        /// </summary>
        public async Task ForceRemovePeerAsync(string peerId)
        {
            await using (await _closeLock.ReadLockAsync())
            {
                if (_closed)
                {
                    return;
                }

                await using (await _peersLock.WriteLockAsync())
                {
                    _peers.Remove(peerId);
                    ReleasePeerShard(peerId);
                    _logger.LogDebug("ForceRemovePeerAsync() | Peer:{PeerId} removed from Room:{RoomId}", peerId, RoomId);
                }
            }
        }

        public async Task<string[]> GetPeerIdsAsync()
        {
            await using (await _closeLock.ReadLockAsync())
            {
                if (_closed)
                {
                    throw new Exception($"GetPeerIdsAsync() | RoomId:{RoomId} was closed.");
                }

                await using (await _peersLock.ReadLockAsync())
                {
                    return _peers.Keys.ToArray();
                }
            }
        }

        public async Task<Peer[]> GetPeersAsync()
        {
            await using (await _closeLock.ReadLockAsync())
            {
                if (_closed)
                {
                    throw new Exception($"GetPeersAsync() | RoomId:{RoomId} was closed.");
                }

                await using (await _peersLock.ReadLockAsync())
                {
                    return _peers.Values.ToArray();
                }
            }
        }

        public async Task CloseAsync()
        {
            RoomMediaShard[] shards;
            await using (await _closeLock.WriteLockAsync())
            {
                if (_closed)
                {
                    return;
                }

                _logger.LogDebug("CloseAsync() | RoomId:{RoomId}", RoomId);

                _closed = true;

                lock (_mediaSync)
                {
                    shards = _shards;
                    _shards = [];
                    _peerShards.Clear();
                    _producerRoutes.Clear();
                    _shardLoudest.Clear();
                }
            }

            // Router 关闭会级联关闭其上的 Transport、PipeTransport、PipeProducer 与 PipeConsumer。
            foreach (var shard in shards)
            {
                try
                {
                    await shard.Router.CloseAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "CloseAsync() | RoomId:{RoomId} close shard:{ShardIndex} failed", RoomId, shard.Index);
                }
            }
        }

        #region Media shards

        /// <summary>
        /// 获取 Peer 所属分片；未分配时返回主分片（兼容旧路径）。
        /// </summary>
        public RoomMediaShard GetPeerShard(string peerId)
        {
            lock (_mediaSync)
            {
                return _peerShards.TryGetValue(peerId, out var shard) ? shard : PrimaryShard;
            }
        }

        public RoomMediaShard[] GetShards()
        {
            lock (_mediaSync)
            {
                return _shards;
            }
        }

        private void AssignPeerShard(string peerId)
        {
            lock (_mediaSync)
            {
                if (_peerShards.ContainsKey(peerId) || _shards.Length == 0)
                {
                    return;
                }

                var counts = _shards.Select(shard => _peerShards.Values.Count(item => item.Index == shard.Index)).ToArray();
                var shardIndex = RoomMediaShardPolicy.SelectShardIndex(counts);
                var selected = _shards[shardIndex];
                _peerShards[peerId] = selected;

                _logger.LogInformation(
                    "Room {RoomId}: Peer {PeerId} assigned to shard {ShardIndex} (worker {WorkerIndex}, router {RouterId}), shard peers: [{Counts}]",
                    RoomId, peerId, selected.Index, selected.WorkerIndex, selected.Router.RouterId, string.Join(",", counts)
                );
            }
        }

        private void ReleasePeerShard(string peerId)
        {
            lock (_mediaSync)
            {
                _peerShards.Remove(peerId);
            }
        }

        private void TryScheduleShardExpansion(int peerCount)
        {
            if (_shardFactory == null || peerCount < RoomMediaShardPolicy.PrebuildPeerThreshold)
            {
                return;
            }

            lock (_mediaSync)
            {
                if (_expandShardsTask != null || _shards.Length >= _maxShardCount)
                {
                    return;
                }

                _logger.LogInformation(
                    "Room {RoomId}: peer count {PeerCount} reached {Threshold}, prebuilding media shards up to {MaxShards}",
                    RoomId, peerCount, RoomMediaShardPolicy.PrebuildPeerThreshold, _maxShardCount
                );
                _expandShardsTask = Task.Run(ExpandShardsAsync);
            }
        }

        private async Task ExpandShardsAsync()
        {
            try
            {
                while (true)
                {
                    RoomMediaShard[] current;
                    lock (_mediaSync)
                    {
                        current = _shards;
                    }

                    if (current.Length == 0 || current.Length >= _maxShardCount)
                    {
                        return;
                    }

                    var usedWorkers = current.Select(item => item.Worker).ToArray();
                    var shard = await _shardFactory!(current.Length, usedWorkers);
                    if (shard == null)
                    {
                        _logger.LogWarning(
                            "Room {RoomId}: no idle worker for shard {ShardIndex}, keep {ShardCount} shard(s)",
                            RoomId, current.Length, current.Length
                        );
                        return;
                    }

                    var accepted = false;
                    await using (await _closeLock.ReadLockAsync())
                    {
                        if (!_closed)
                        {
                            lock (_mediaSync)
                            {
                                _shards = [.. _shards, shard];
                            }

                            HandleAudioLevelObserver(shard);
                            accepted = true;
                        }
                    }

                    if (!accepted)
                    {
                        await shard.Router.CloseAsync();
                        return;
                    }

                    _logger.LogInformation(
                        "Room {RoomId}: shard {ShardIndex} ready on worker {WorkerIndex}, router {RouterId}",
                        RoomId, shard.Index, shard.WorkerIndex, shard.Router.RouterId
                    );

                    PipeExistingProducers(shard);
                }
            }
            catch (Exception ex)
            {
                // 扩容失败不影响已有会议，保持现有分片继续运行。
                _logger.LogWarning(ex, "Room {RoomId}: media shard expansion failed, keep current shards", RoomId);
            }
        }

        /// <summary>
        /// 登记 Producer 并把音频与缩略流预先管道到其他分片；高清流按需管道，避免 4 Mbps 流在无人观看的分片间复制。
        /// </summary>
        public void RegisterProducer(Producer producer, RoomMediaShard sourceShard)
        {
            RoomMediaShard[] targets;
            lock (_mediaSync)
            {
                if (_shards.Length == 0)
                {
                    return;
                }

                _producerRoutes[producer.ProducerId] = new RoomProducerRoute(producer, sourceShard);
                targets = _shards.Where(item => item.Index != sourceShard.Index).ToArray();
            }

            producer.Observer.On(
                "close",
                (_, _) =>
                {
                    UnregisterProducer(producer.ProducerId);
                    return Task.CompletedTask;
                }
            );

            if (!ShouldPipeEagerly(producer))
            {
                return;
            }

            foreach (var target in targets)
            {
                EnsureProducerOnShardAsync(producer.ProducerId, target).ContinueWithOnFaultedHandleLog(_logger);
            }
        }

        /// <summary>
        /// 确保 Producer 在目标分片可被消费：同分片直接返回，否则复用或创建 PipeProducer。
        /// </summary>
        public Task EnsureProducerOnShardAsync(string producerId, RoomMediaShard targetShard)
        {
            Task<Producer> pipeTask;
            lock (_mediaSync)
            {
                if (!_producerRoutes.TryGetValue(producerId, out var route) || route.SourceShard.Index == targetShard.Index)
                {
                    return Task.CompletedTask;
                }

                if (!route.PipeTasks.TryGetValue(targetShard.Index, out pipeTask!))
                {
                    pipeTask = PipeProducerAsync(route, targetShard);
                    route.PipeTasks[targetShard.Index] = pipeTask;
                }
            }

            return pipeTask;
        }

        private async Task<Producer> PipeProducerAsync(RoomProducerRoute route, RoomMediaShard targetShard)
        {
            // 确保后续代码不在调用方持有的 _mediaSync 内同步执行，失败时才能正确移除缓存的任务。
            await Task.Yield();

            try
            {
                var result = await route.SourceShard.Router.PipeToRouteAsync(
                    new PipeToRouterOptions
                    {
                        ProducerId = route.Producer.ProducerId,
                        Router = targetShard.Router,
                        ListenInfo = _pipeListenInfo,
                        EnableSctp = false,
                        EnableRtx = false,
                        EnableSrtp = false,
                    }
                );

                var pipeProducer = result.PipeProducer ?? throw new Exception("PipeToRouteAsync() returned no PipeProducer.");
                var keep = false;
                lock (_mediaSync)
                {
                    if (_producerRoutes.TryGetValue(route.Producer.ProducerId, out var currentRoute) && ReferenceEquals(currentRoute, route))
                    {
                        route.PipeProducers[targetShard.Index] = pipeProducer;
                        keep = true;
                    }
                }

                if (!keep)
                {
                    // 原 Producer 在管道建立期间已关闭，立即释放，避免残留 SSRC。
                    await pipeProducer.CloseAsync();
                    throw new InvalidStateException("original Producer closed");
                }

                pipeProducer.Observer.On(
                    "close",
                    (_, _) =>
                    {
                        lock (_mediaSync)
                        {
                            route.PipeProducers.Remove(targetShard.Index);
                            route.PipeTasks.Remove(targetShard.Index);
                        }

                        return Task.CompletedTask;
                    }
                );

                _logger.LogDebug(
                    "Room {RoomId}: piped Producer {ProducerId} ({Source}) shard {FromShard} -> {ToShard}",
                    RoomId, route.Producer.ProducerId, route.Producer.Source, route.SourceShard.Index, targetShard.Index
                );

                return pipeProducer;
            }
            catch
            {
                lock (_mediaSync)
                {
                    // 失败后允许下次 Consume 重试。
                    route.PipeTasks.Remove(targetShard.Index);
                }

                throw;
            }
        }

        private void UnregisterProducer(string producerId)
        {
            Producer[] pipeProducers;
            lock (_mediaSync)
            {
                if (!_producerRoutes.Remove(producerId, out var route))
                {
                    return;
                }

                pipeProducers = route.PipeProducers.Values.ToArray();
                route.PipeProducers.Clear();
                route.PipeTasks.Clear();
            }

            // PipeConsumer 会随原 Producer 关闭级联关闭 PipeProducer；这里显式关闭作为兜底（CloseAsync 幂等）。
            foreach (var pipeProducer in pipeProducers)
            {
                pipeProducer.CloseAsync().ContinueWithOnFaultedHandleLog(_logger);
            }
        }

        private void PipeExistingProducers(RoomMediaShard shard)
        {
            RoomProducerRoute[] routes;
            lock (_mediaSync)
            {
                routes = _producerRoutes.Values.Where(item => item.SourceShard.Index != shard.Index).ToArray();
            }

            foreach (var route in routes.Where(item => ShouldPipeEagerly(item.Producer)))
            {
                EnsureProducerOnShardAsync(route.Producer.ProducerId, shard).ContinueWithOnFaultedHandleLog(_logger);
            }
        }

        private static bool ShouldPipeEagerly(Producer producer)
        {
            return producer.Source != PrimaryVideoSource;
        }

        public RoomMediaSnapshot GetMediaSnapshot()
        {
            lock (_mediaSync)
            {
                return new RoomMediaSnapshot
                {
                    Shards = _shards
                        .Select(shard => new RoomMediaShardSnapshot
                        {
                            Index = shard.Index,
                            WorkerIndex = shard.WorkerIndex,
                            RouterId = shard.Router.RouterId,
                            PeerCount = _peerShards.Values.Count(item => item.Index == shard.Index),
                        })
                        .ToArray(),
                    ProducerCount = _producerRoutes.Count,
                    PipeProducerCount = _producerRoutes.Values.Sum(item => item.PipeProducers.Count),
                };
            }
        }

        private sealed class RoomProducerRoute
        {
            public RoomProducerRoute(Producer producer, RoomMediaShard sourceShard)
            {
                Producer = producer;
                SourceShard = sourceShard;
            }

            public Producer Producer { get; }

            public RoomMediaShard SourceShard { get; }

            public Dictionary<int, Task<Producer>> PipeTasks { get; } = new();

            public Dictionary<int, Producer> PipeProducers { get; } = new();
        }

        #endregion Media shards

        #region Active speaker

        private void HandleAudioLevelObserver(RoomMediaShard shard)
        {
            shard.AudioLevelObserver.On(
                "volumes",
                async (_, volumes) =>
                {
                    var loudest = (volumes as List<AudioLevelObserverVolume>)?.OrderByDescending(m => m.Volume).FirstOrDefault();
                    AudioLevelObserverVolume? roomLoudest;
                    lock (_mediaSync)
                    {
                        if (loudest != null)
                        {
                            _shardLoudest[shard.Index] = (loudest, DateTime.UtcNow);
                        }
                        else
                        {
                            _shardLoudest.Remove(shard.Index);
                        }

                        roomLoudest = SelectRoomLoudest();
                    }

                    await NotifyActiveSpeakerAsync(roomLoudest);
                }
            );

            shard.AudioLevelObserver.On(
                "silence",
                async (_, _) =>
                {
                    AudioLevelObserverVolume? roomLoudest;
                    lock (_mediaSync)
                    {
                        _shardLoudest.Remove(shard.Index);
                        roomLoudest = SelectRoomLoudest();
                    }

                    await NotifyActiveSpeakerAsync(roomLoudest);
                }
            );
        }

        /// <summary>
        /// 汇总各分片最近一次的最响发言者（调用方持有 _mediaSync）。
        /// </summary>
        private AudioLevelObserverVolume? SelectRoomLoudest()
        {
            var expiredBefore = DateTime.UtcNow - ShardLoudestExpiration;
            foreach (var expired in _shardLoudest.Where(item => item.Value.UpdatedAt < expiredBefore).Select(item => item.Key).ToArray())
            {
                _shardLoudest.Remove(expired);
            }

            return _shardLoudest.Values.OrderByDescending(item => item.Volume.Volume).Select(item => item.Volume).FirstOrDefault();
        }

        private async Task NotifyActiveSpeakerAsync(AudioLevelObserverVolume? loudest)
        {
            await using (await _closeLock.ReadLockAsync())
            {
                if (_closed)
                {
                    return;
                }

                await using (await _peersLock.ReadLockAsync())
                {
                    var notification = loudest == null
                        ? new MeetingNotification { Type = "activeSpeaker" }
                        : new MeetingNotification
                        {
                            Type = "activeSpeaker",
                            // TODO: (alby)Strongly typed
                            Data = new[]
                            {
                                new
                                {
                                    PeerId = loudest.Producer.AppData["peerId"],
                                    loudest.Producer.ProducerId,
                                    loudest.Volume,
                                },
                            },
                        };

                    foreach (var peer in _peers.Values)
                    {
                        peer.HubClient?.Notify(notification).ContinueWithOnFaultedHandleLog(_logger);
                    }
                }
            }
        }

        #endregion Active speaker
    }

    public sealed class RoomMediaSnapshot
    {
        public RoomMediaShardSnapshot[] Shards { get; init; } = [];

        public int ProducerCount { get; init; }

        public int PipeProducerCount { get; init; }
    }

    public sealed class RoomMediaShardSnapshot
    {
        public int Index { get; init; }

        public int WorkerIndex { get; init; }

        public string RouterId { get; init; } = string.Empty;

        public int PeerCount { get; init; }
    }
}
