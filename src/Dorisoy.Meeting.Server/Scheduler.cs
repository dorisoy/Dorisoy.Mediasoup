using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FBS.WebRtcTransport;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Threading;
using Dorisoy.Mediasoup;

namespace Dorisoy.Meeting.Server
{
    public class Scheduler
    {
        #region Private Fields

        /// <summary>
        /// Logger factory for create logger.
        /// </summary>
        private readonly ILoggerFactory _loggerFactory;

        /// <summary>
        /// Logger
        /// </summary>
        private readonly ILogger<Scheduler> _logger;

        private readonly MediasoupOptions _mediasoupOptions;

        private readonly MediasoupServer _mediasoupServer;

        private readonly Dictionary<string, Peer> _peers = new();

        private readonly AsyncReaderWriterLock _peersLock = new();

        private readonly Dictionary<string, Room> _rooms = new();

        private readonly AsyncAutoResetEvent _roomsLock = new();

        #endregion Private Fields

        public RtpCapabilities DefaultRtpCapabilities { get; }

        public Scheduler(ILoggerFactory loggerFactory, MediasoupOptions mediasoupOptions, MediasoupServer mediasoupServer)
        {
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<Scheduler>();
            _mediasoupOptions = mediasoupOptions;
            _mediasoupServer = mediasoupServer;

            // 按创建 Route 时一样方式创建 RtpCodecCapabilities
            var rtpCodecCapabilities = mediasoupOptions.MediasoupSettings.RouterSettings.RtpCodecCapabilities;
            // This may throw.
            DefaultRtpCapabilities = ORTC.GenerateRouterRtpCapabilities(rtpCodecCapabilities);

            _roomsLock.Set();
        }

        public async Task<Peer> JoinAsync(string peerId, string connectionId, IHubClient hubClient, JoinRequest joinRequest)
        {
            await using (await _peersLock.WriteLockAsync())
            {
                if (_peers.TryGetValue(peerId, out var peer))
                {
                    // 客户端多次调用 `Join`
                    if (peer.ConnectionId == connectionId)
                    {
                        throw new PeerJoinedException("PeerJoinAsync()", peerId);
                    }
                }

                peer = new Peer(
                    _loggerFactory,
                    _mediasoupOptions.MediasoupSettings.WebRtcTransportSettings,
                    _mediasoupOptions.MediasoupSettings.PlainTransportSettings,
                    joinRequest.RtpCapabilities,
                    joinRequest.SctpCapabilities,
                    peerId,
                    connectionId,
                    hubClient,
                    joinRequest.DisplayName,
                    joinRequest.Sources,
                    joinRequest.AppData
                );

                _peers[peerId] = peer;

                return peer;
            }
        }

        public async Task<LeaveResult?> LeaveAsync(string peerId)
        {
            await using (await _peersLock.WriteLockAsync())
            {
                if (!_peers.Remove(peerId, out var peer))
                {
                    return null;
                }

                return await peer.LeaveAsync();
            }
        }

        public async Task<JoinRoomResult> JoinRoomAsync(string peerId, string connectionId, JoinRoomRequest joinRoomRequest)
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(peerId, out var peer))
                {
                    throw new PeerNotExistsException("JoinRoomAsync()", peerId);
                }

                CheckConnection(peer, connectionId);

                await _roomsLock.WaitAsync();
                try
                {
                    // Room 如果不存在则创建
                    if (!_rooms.TryGetValue(joinRoomRequest.RoomId, out var room))
                    {
                        // Router media codecs.
                        var mediaCodecs = _mediasoupOptions.MediasoupSettings.RouterSettings.RtpCodecCapabilities;

                        // Create a mediasoup Router.
                        var worker = _mediasoupServer.GetWorker();
                        var router = await worker.CreateRouterAsync(new RouterOptions { MediaCodecs = mediaCodecs });

                        // Create a mediasoup AudioLevelObserver.
                        var audioLevelObserver = await router.CreateAudioLevelObserverAsync(
                            new AudioLevelObserverOptions
                            {
                                MaxEntries = 1,
                                Threshold = -80,
                                Interval = 800,
                            }
                        );

                        var webRtcServer = worker.GetDefaultWebRtcServer();
                        room = new Room(_loggerFactory, webRtcServer, router, audioLevelObserver, joinRoomRequest.RoomId, "Default");
                        _rooms[room.RoomId] = room;
                    }

                    JoinRoomResult result;
                    try
                    {
                        result = await peer.JoinRoomAsync(room);
                    }
                    catch (PeerInRoomException)
                    {
                        // 幂等操作：如果 Peer 已经在同一个房间中，返回当前房间状态
                        _logger.LogWarning("JoinRoomAsync() | Peer:{PeerId} 已在 Room:{RoomId}，返回当前状态", peerId, room.RoomId);
                        result = await room.GetJoinRoomResultAsync(peer);
                    }

                    await peer.SetPeerInternalDataAsync(
                        new SetPeerInternalDataRequest
                        {
                            InternalData = new Dictionary<string, object> { { Peer.RoleKey, joinRoomRequest.Role } },
                        }
                    );

                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "JoinRoomAsync()");
                    throw;
                }
                finally
                {
                    _roomsLock.Set();
                }
            }
        }

        public async Task<LeaveRoomResult> LeaveRoomAsync(string peerId, string connectionId)
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(peerId, out var peer))
                {
                    throw new PeerNotExistsException("LeaveRoomAsync()", peerId);
                }

                CheckConnection(peer, connectionId);

                return await peer.LeaveRoomAsync();
            }
        }

        /// <summary>
        /// 解散房间（主持人离开时调用）
        /// 按正确的顺序清理所有用户的资源，避免 Worker 崩溃
        /// </summary>
        public async Task<DismissRoomResult> DismissRoomAsync(string hostPeerId, string connectionId, Room room)
        {
            // 使用写锁，因为我们需要从 _peers 中移除 Peer
            await using (await _peersLock.WriteLockAsync())
            {
                if (!_peers.TryGetValue(hostPeerId, out var hostPeer))
                {
                    throw new PeerNotExistsException("DismissRoomAsync()", hostPeerId);
                }

                CheckConnection(hostPeer, connectionId);

                // 获取房间中除主持人外的所有 Peer
                var allPeers = await room.GetPeersAsync();
                var otherPeers = new List<Peer>();
                var otherPeerIds = new List<string>();

                foreach (var peer in allPeers)
                {
                    if (peer.PeerId != hostPeerId)
                    {
                        otherPeers.Add(peer);
                        otherPeerIds.Add(peer.PeerId);
                    }
                }

                _logger.LogInformation("DismissRoomAsync() | 房间 {RoomId} 解散，清理 {Count} 个其他用户的资源", 
                    room.RoomId, otherPeers.Count);

                // 1. 清理所有其他用户：先清理 Transport，再从 Room 和 Scheduler 中移除
                foreach (var peer in otherPeers)
                {
                    try
                    {
                        _logger.LogDebug("DismissRoomAsync() | 清理 Peer:{PeerId} 的资源", peer.PeerId);
                        
                        // 清理 Transport（关闭 Consumer 和 Producer）
                        await peer.ForceLeaveRoomAsync();
                        
                        // 从 Room 中移除 Peer（重要！）
                        try
                        {
                            await room.ForceRemovePeerAsync(peer.PeerId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "DismissRoomAsync() | 从房间移除 Peer:{PeerId} 时出错（可能已移除）", peer.PeerId);
                        }
                        
                        // 从 Scheduler 的 _peers 中移除（让用户可以重新 Join）
                        _peers.Remove(peer.PeerId);
                        _logger.LogDebug("DismissRoomAsync() | 已从 Scheduler 移除 Peer:{PeerId}", peer.PeerId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "DismissRoomAsync() | 清理 Peer:{PeerId} 失败", peer.PeerId);
                    }
                }

                // 2. 添加小延迟，确保资源清理完成
                await Task.Delay(100);

                // 3. 最后清理主持人的资源
                _logger.LogDebug("DismissRoomAsync() | 清理主持人 {HostPeerId} 的资源", hostPeerId);
                await hostPeer.LeaveRoomAsync();
                _peers.Remove(hostPeerId);

                // 4. 从房间列表中移除空房间
                await _roomsLock.WaitAsync();
                try
                {
                    // 直接移除房间，因为所有用户都已经被清理
                    _rooms.Remove(room.RoomId);
                    _logger.LogInformation("DismissRoomAsync() | 房间 {RoomId} 已清空并移除", room.RoomId);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "DismissRoomAsync() | 移除房间时出错");
                }
                finally
                {
                    _roomsLock.Set();
                }

                return new DismissRoomResult
                {
                    HostPeer = hostPeer,
                    OtherPeerIds = otherPeerIds.ToArray()
                };
            }
        }

        public async Task<PeerAppDataResult> SetPeerAppDataAsync(
            string peerId,
            string connectionId,
            SetPeerAppDataRequest setPeerAppDataRequest
        )
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(peerId, out var peer))
                {
                    throw new PeerNotExistsException("SetPeerAppDataAsync()", peerId);
                }

                CheckConnection(peer, connectionId);

                return await peer.SetPeerAppDataAsync(setPeerAppDataRequest);
            }
        }

        public async Task<PeerAppDataResult> UnsetPeerAppDataAsync(
            string peerId,
            string connectionId,
            UnsetPeerAppDataRequest unsetPeerAppDataRequest
        )
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(peerId, out var peer))
                {
                    throw new PeerNotExistsException("UnsetPeerAppDataAsync()", peerId);
                }

                CheckConnection(peer, connectionId);

                return await peer.UnsetPeerAppDataAsync(unsetPeerAppDataRequest);
            }
        }

        public async Task<PeerAppDataResult> ClearPeerAppDataAsync(string peerId, string connectionId)
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(peerId, out var peer))
                {
                    throw new PeerNotExistsException("ClearPeerAppDataAsync()", peerId);
                }

                CheckConnection(peer, connectionId);

                return await peer.ClearPeerAppDataAsync();
            }
        }

        public async Task<PeerInternalDataResult> SetPeerInternalDataAsync(
            SetPeerInternalDataRequest setPeerInternalDataRequest
        )
        {
            await using (await _peersLock.ReadLockAsync())
            {
                return _peers.TryGetValue(setPeerInternalDataRequest.PeerId, out var peer)
                    ? await peer.SetPeerInternalDataAsync(setPeerInternalDataRequest)
                    : throw new PeerNotExistsException("SetPeerInternalDataAsync()", setPeerInternalDataRequest.PeerId);
            }
        }

        public async Task<PeerInternalDataResult> GetPeerInternalDataAsync(string peerId, string connectionId)
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(peerId, out var peer))
                {
                    throw new PeerNotExistsException("GetPeerInternalDataAsync()", peerId);
                }

                CheckConnection(peer, connectionId);

                return await peer.GetPeerInternalDataAsync();
            }
        }

        public async Task<PeerInternalDataResult> UnsetPeerInternalDataAsync(
            UnsetPeerInternalDataRequest unsetPeerInternalDataRequest
        )
        {
            await using (await _peersLock.ReadLockAsync())
            {
                return _peers.TryGetValue(unsetPeerInternalDataRequest.PeerId, out var peer)
                    ? await peer.UnsetPeerInternalDataAsync(unsetPeerInternalDataRequest)
                    : throw new PeerNotExistsException("UnsetPeerInternalDataAsync()", unsetPeerInternalDataRequest.PeerId);
            }
        }

        public async Task<PeerInternalDataResult> ClearPeerInternalDataAsync(
            string peerId,
            string connectionId,
            string targetPeerId
        )
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(peerId, out var peer))
                {
                    throw new PeerNotExistsException("ClearPeerInternalDataAsync()", peerId);
                }

                CheckConnection(peer, connectionId);

                Peer? targetPeer;
                if (peerId == targetPeerId)
                {
                    targetPeer = peer;
                }
                else if (!_peers.TryGetValue(targetPeerId, out targetPeer))
                {
                    throw new PeerNotExistsException("ClearPeerInternalDataAsync()", targetPeerId);
                }

                return await targetPeer.ClearPeerInternalDataAsync();
            }
        }

        public async Task<WebRtcTransport> CreateWebRtcTransportAsync(
            string peerId,
            string connectionId,
            CreateWebRtcTransportRequest createWebRtcTransportRequest,
            bool isSend
        )
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(peerId, out var peer))
                {
                    throw new PeerNotExistsException("CreateWebRtcTransport()", peerId);
                }

                CheckConnection(peer, connectionId);

                return await peer.CreateWebRtcTransportAsync(createWebRtcTransportRequest, isSend);
            }
        }

        public async Task<bool> ConnectWebRtcTransportAsync(
            string peerId,
            string connectionId,
            ConnectWebRtcTransportRequest connectWebRtcTransportRequest
        )
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(peerId, out var peer))
                {
                    throw new PeerNotExistsException("ConnectWebRtcTransportAsync()", peerId);
                }

                CheckConnection(peer, connectionId);

                return await peer.ConnectWebRtcTransportAsync(connectWebRtcTransportRequest);
            }
        }

        public async Task<PlainTransport> CreatePlainTransportAsync(
            string peerId,
            string connectionId,
            CreatePlainTransportRequest createPlainTransportRequest
        )
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(peerId, out var peer))
                {
                    throw new PeerNotExistsException("CreateWebRtcTransport()", peerId);
                }

                CheckConnection(peer, connectionId);

                return await peer.CreatePlainTransportAsync(createPlainTransportRequest);
            }
        }

        public async Task<PullResult> PullAsync(string peerId, string connectionId, PullRequest pullRequest)
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(peerId, out var peer))
                {
                    throw new PeerNotExistsException("PullAsync()", peerId);
                }

                CheckConnection(peer, connectionId);

                if (!_peers.TryGetValue(pullRequest.PeerId, out var producePeer))
                {
                    throw new PeerNotExistsException("PullAsync()", pullRequest.PeerId);
                }

                var pullResult = await peer.PullAsync(producePeer, pullRequest.Sources);

                return new PullResult
                {
                    ConsumePeer = peer,
                    ProducePeer = producePeer,
                    ExistsProducers = pullResult.ExistsProducers,
                    Sources = pullResult.ProduceSources,
                };
            }
        }

        public async Task<ProduceResult> ProduceAsync(string peerId, string connectionId, ProduceRequest produceRequest)
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(peerId, out var peer))
                {
                    throw new PeerNotExistsException("ProduceAsync()", peerId);
                }

                CheckConnection(peer, connectionId);

                var peerProduceResult =
                    await peer.ProduceAsync(produceRequest)
                    ?? throw new Exception($"ProduceAsync() | Peer:{peerId} produce failed.");

                // NOTE: 这里假设了 Room 存在
                var pullPaddingConsumerPeers = new List<Peer>();
                foreach (var item in peerProduceResult.PullPaddings)
                {
                    // 其他 Peer 消费本 Peer
                    if (_peers.TryGetValue(item.ConsumerPeerId, out var consumerPeer))
                    {
                        pullPaddingConsumerPeers.Add(consumerPeer);
                    }
                }

                var produceResult = new ProduceResult
                {
                    ProducerPeer = peer,
                    Producer = peerProduceResult.Producer,
                    PullPaddingConsumerPeers = pullPaddingConsumerPeers.ToArray(),
                };

                return produceResult;
            }
        }

        public async Task<Consumer?> ConsumeAsync(string producerPeerId, string consumerPeerId, string producerId)
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(producerPeerId, out var producerPeer))
                {
                    throw new PeerNotExistsException("ConsumeAsync()", producerPeerId);
                }

                if (!_peers.TryGetValue(consumerPeerId, out var consumerPeer))
                {
                    throw new PeerNotExistsException("ConsumeAsync()", consumerPeerId);
                }

                // NOTE: 这里假设了 Room 存在
                return await consumerPeer.ConsumeAsync(producerPeer, producerId);
            }
        }

        public async Task<bool> CloseProducerAsync(string peerId, string connectionId, string producerId)
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(peerId, out var peer))
                {
                    throw new PeerNotExistsException("CloseProducerAsync()", peerId);
                }

                CheckConnection(peer, connectionId);

                return await peer.CloseProducerAsync(producerId);
            }
        }

        public async Task<bool> CloseAllProducersAsync(string peerId, string connectionId)
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(peerId, out var peer))
                {
                    throw new PeerNotExistsException("CloseAllProducersAsync()", peerId);
                }

                CheckConnection(peer, connectionId);

                return await peer.CloseAllProducersAsync();
            }
        }

        public async Task<bool> CloseProducerWithSourcesAsync(
            string peerId,
            string connectionId,
            string targetPeerId,
            IEnumerable<string> sources
        )
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(peerId, out var peer))
                {
                    throw new PeerNotExistsException("CloseProducerWithSourcesAsync()", peerId);
                }

                CheckConnection(peer, connectionId);

                Peer? targetPeer;
                if (peerId == targetPeerId)
                {
                    targetPeer = peer;
                }
                else if (!_peers.TryGetValue(targetPeerId, out targetPeer))
                {
                    throw new PeerNotExistsException("CloseProducerWithSourcesAsync()", targetPeerId);
                }

                return await targetPeer.CloseProducerWithSourcesAsync(sources);
            }
        }

        public async Task<bool> PauseProducerAsync(string peerId, string connectionId, string producerId)
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(peerId, out var peer))
                {
                    throw new PeerNotExistsException("PauseProducerAsync()", peerId);
                }

                CheckConnection(peer, connectionId);

                return await peer.PauseProducerAsync(producerId);
            }
        }

        public async Task<bool> ResumeProducerAsync(string peerId, string connectionId, string producerId)
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(peerId, out var peer))
                {
                    throw new PeerNotExistsException("ResumeProducerAsync()", peerId);
                }

                CheckConnection(peer, connectionId);

                return await peer.ResumeProducerAsync(producerId);
            }
        }

        public async Task<bool> CloseConsumerAsync(string peerId, string connectionId, string consumerId)
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(peerId, out var peer))
                {
                    throw new PeerNotExistsException("CloseConsumerAsync()", peerId);
                }

                CheckConnection(peer, connectionId);

                return await peer.CloseConsumerAsync(consumerId);
            }
        }

        public async Task<bool> PauseConsumerAsync(string peerId, string connectionId, string consumerId)
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(peerId, out var peer))
                {
                    throw new PeerNotExistsException("PauseConsumerAsync()", peerId);
                }

                CheckConnection(peer, connectionId);

                return await peer.PauseConsumerAsync(consumerId);
            }
        }

        public async Task<Consumer> ResumeConsumerAsync(string peerId, string connectionId, string consumerId)
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(peerId, out var peer))
                {
                    throw new PeerNotExistsException("ResumeConsumerAsync()", peerId);
                }

                CheckConnection(peer, connectionId);

                return await peer.ResumeConsumerAsync(consumerId);
            }
        }

        public async Task<bool> SetConsumerPreferredLayersAsync(
            string peerId,
            string connectionId,
            SetConsumerPreferredLayersRequest setConsumerPreferredLayersRequest
        )
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(peerId, out var peer))
                {
                    throw new PeerNotExistsException("SetConsumerPreferredLayersAsync()", peerId);
                }

                CheckConnection(peer, connectionId);

                return await peer.SetConsumerPreferredLayersAsync(setConsumerPreferredLayersRequest);
            }
        }

        public async Task<bool> SetConsumerPriorityAsync(
            string peerId,
            string connectionId,
            SetConsumerPriorityRequest setConsumerPriorityRequest
        )
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(peerId, out var peer))
                {
                    throw new PeerNotExistsException("SetConsumerPriorityAsync()", peerId);
                }

                CheckConnection(peer, connectionId);

                return await peer.SetConsumerPriorityAsync(setConsumerPriorityRequest);
            }
        }

        public async Task<bool> RequestConsumerKeyFrameAsync(string peerId, string connectionId, string consumerId)
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(peerId, out var peer))
                {
                    throw new PeerNotExistsException("RequestConsumerKeyFrameAsync()", peerId);
                }

                CheckConnection(peer, connectionId);

                return await peer.RequestConsumerKeyFrameAsync(consumerId);
            }
        }

        public async Task<object[]> GetWebRtcTransportStatsAsync(string peerId, string connectionId, string transportId)
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(peerId, out var peer))
                {
                    throw new PeerNotExistsException("GetWebRtcTransportStatsAsync()", peerId);
                }

                CheckConnection(peer, connectionId);

                return await peer.GetWebRtcTransportStatsAsync(transportId);
            }
        }

        public async Task<object[]> GetProducerStatsAsync(string peerId, string connectionId, string producerId)
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(peerId, out var peer))
                {
                    throw new PeerNotExistsException("GetProducerStatsAsync()", peerId);
                }

                CheckConnection(peer, connectionId);

                return await peer.GetProducerStatsAsync(producerId);
            }
        }

        public async Task<object[]> GetConsumerStatsAsync(string peerId, string connectionId, string consumerId)
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(peerId, out var peer))
                {
                    throw new PeerNotExistsException("GetConsumerStatsAsync()", peerId);
                }

                CheckConnection(peer, connectionId);

                return await peer.GetConsumerStatsAsync(consumerId);
            }
        }

        public async Task<IceParametersT> RestartIceAsync(string peerId, string connectionId, string transportId)
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(peerId, out var peer))
                {
                    throw new PeerNotExistsException("RestartIceAsync()", peerId);
                }

                CheckConnection(peer, connectionId);

                return await peer.RestartIceAsync(transportId);
            }
        }

        public async Task<string[]> GetOtherPeerIdsAsync(string peerId, string connectionId, UserRole? role = null)
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(peerId, out var peer))
                {
                    throw new PeerNotExistsException("GetOtherPeerIdsAsync()", peerId);
                }

                CheckConnection(peer, connectionId);

                return await peer.GetOtherPeerIdsAsync(role);
            }
        }

        public async Task<Peer[]> GetOtherPeersAsync(string peerId, string connectionId, UserRole? role = null)
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(peerId, out var peer))
                {
                    throw new PeerNotExistsException("GetOtherPeersAsync()", peerId);
                }

                CheckConnection(peer, connectionId);

                return await peer.GetOtherPeersAsync(role);
            }
        }

        public async Task<UserRole> GetPeerRoleAsync(string peerId, string connectionId)
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(peerId, out var peer))
                {
                    throw new PeerNotExistsException("GetPeerRoleAsync()", peerId);
                }

                CheckConnection(peer, connectionId);

                return await peer.GetRoleAsync();
            }
        }

        /// <summary>
        /// 获取 Peer 信息（用于消息广播等）
        /// </summary>
        public async Task<Peer?> GetPeerAsync(string peerId, string connectionId)
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(peerId, out var peer))
                {
                    return null;
                }

                CheckConnection(peer, connectionId);

                return peer;
            }
        }

        /// <summary>
        /// 踢出用户（主持人操作）
        /// </summary>
        public async Task<KickPeerResult?> KickPeerAsync(string hostPeerId, string connectionId, string targetPeerId)
        {
            await using (await _peersLock.ReadLockAsync())
            {
                if (!_peers.TryGetValue(hostPeerId, out var hostPeer))
                {
                    throw new PeerNotExistsException("KickPeerAsync()", hostPeerId);
                }

                CheckConnection(hostPeer, connectionId);

                if (!_peers.TryGetValue(targetPeerId, out var targetPeer))
                {
                    throw new PeerNotExistsException("KickPeerAsync()", targetPeerId);
                }

                // 通过 Peer 获取 Room
                var room = await hostPeer.GetRoomAsync();
                if (room == null)
                {
                    throw new Exception("KickPeerAsync() | Host peer is not in any room.");
                }

                // 踢出用户
                var result = await room.KickPeerAsync(hostPeerId, targetPeerId);

                // 让被踢用户强制离开房间（关闭 transports，但不再调用 Room.PeerLeaveAsync）
                if (result != null)
                {
                    await targetPeer.ForceLeaveRoomAsync();
                }

                return result;
            }
        }

        /// <summary>
        /// 获取目标 Peer 信息
        /// </summary>
        public async Task<Peer?> GetTargetPeerAsync(string peerId)
        {
            await using (await _peersLock.ReadLockAsync())
            {
                return _peers.TryGetValue(peerId, out var peer) ? peer : null;
            }
        }

        private static void CheckConnection(Peer peer, string connectionId)
        {
            if (peer.ConnectionId != connectionId)
            {
                throw new DisconnectedException($"New: {connectionId} Old:{peer.ConnectionId}");
            }
        }
    }
}
