namespace Dorisoy.Meeting.Server
{
    public class LeaveRoomResult
    {
        public Peer SelfPeer { get; set; }

        public string[] OtherPeerIds { get; init; }

        /// <summary>
        /// 离开的用户是否是主持人
        /// </summary>
        public bool IsHost { get; set; }

        /// <summary>
        /// 房间 ID
        /// </summary>
        public string? RoomId { get; set; }
    }
}
