namespace Dorisoy.Meeting.Server
{
    public class JoinRoomResult
    {
        public Peer SelfPeer { get; init; }

        public Peer[] Peers { get; init; }

        /// <summary>
        /// 主持人 PeerId
        /// </summary>
        public string? HostPeerId { get; init; }
    }

    /// <summary>
    /// 踢出用户结果
    /// </summary>
    public class KickPeerResult
    {
        /// <summary>
        /// 被踢出的用户
        /// </summary>
        public Peer KickedPeer { get; init; }

        /// <summary>
        /// 房间内其他用户 ID
        /// </summary>
        public string[] OtherPeerIds { get; init; }
    }
}
