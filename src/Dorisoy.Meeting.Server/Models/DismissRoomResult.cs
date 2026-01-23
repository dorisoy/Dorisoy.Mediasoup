namespace Dorisoy.Meeting.Server
{
    /// <summary>
    /// 房间解散结果（主持人离开时）
    /// </summary>
    public class DismissRoomResult
    {
        /// <summary>
        /// 主持人
        /// </summary>
        public Peer HostPeer { get; set; }

        /// <summary>
        /// 其他被踢出的用户 ID
        /// </summary>
        public string[] OtherPeerIds { get; init; } = [];
    }
}
