using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        public WebRtcServer? WebRtcServer { get; }

        public Router Router { get; }

        public AudioLevelObserver AudioLevelObserver { get; }

        public Room(
            ILoggerFactory loggerFactory,
            WebRtcServer? webRtcServer,
            Router router,
            AudioLevelObserver audioLevelObserver,
            string roomId,
            string name
        )
        {
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<Room>();
            WebRtcServer = webRtcServer;
            Router = router;
            AudioLevelObserver = audioLevelObserver;
            RoomId = roomId;
            Name = name.NullOrWhiteSpaceReplace("Default");
            _closed = false;

            HandleAudioLevelObserver();
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

<<<<<<< HEAD
=======
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

>>>>>>> pro
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
            await using (await _closeLock.WriteLockAsync())
            {
                if (_closed)
                {
                    return;
                }

                _logger.LogDebug("CloseAsync() | RoomId:{RoomId}", RoomId);

                _closed = true;

                await Router.CloseAsync();
            }
        }

        private void HandleAudioLevelObserver()
        {
            AudioLevelObserver.On(
                "volumes",
                async (_, volumes) =>
                {
                    await using (await _closeLock.ReadLockAsync())
                    {
                        if (_closed)
                        {
                            return;
                        }

                        await using (await _peersLock.ReadLockAsync())
                        {
                            foreach (var peer in _peers.Values)
                            {
                                peer.HubClient?.Notify(
                                        new MeetingNotification
                                        {
                                            Type = "activeSpeaker",
                                            // TODO: (alby)Strongly typed
                                            Data = (volumes as List<AudioLevelObserverVolume>)!.Select(m => new
                                            {
                                                PeerId = m.Producer.AppData["peerId"],
                                                m.Producer.ProducerId,
                                                m.Volume,
                                            }),
                                        }
                                    )
                                    .ContinueWithOnFaultedHandleLog(_logger);
                            }
                        }
                    }
                }
            );

            AudioLevelObserver.On(
                "silence",
                async (_, _) =>
                {
                    await using (await _closeLock.ReadLockAsync())
                    {
                        if (_closed)
                        {
                            return;
                        }

                        await using (await _peersLock.ReadLockAsync())
                        {
                            foreach (var peer in _peers.Values)
                            {
                                peer.HubClient?.Notify(new MeetingNotification { Type = "activeSpeaker" })
                                    .ContinueWithOnFaultedHandleLog(_logger);
                            }
                        }
                    }
                }
            );
        }
    }
}
