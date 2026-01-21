namespace Dorisoy.Meeting.Server
{
    public class JoinRoomResponse
    {
        public Peer[] Peers { get; set; }

        /// <summary>
        /// 主持人 PeerId
        /// </summary>
        public string? HostPeerId { get; set; }
    }
}
