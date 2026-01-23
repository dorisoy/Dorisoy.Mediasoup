namespace Dorisoy.Mediasoup.Common
{
    /// <summary>
    /// 远程静音用户请求（主持人操作）
    /// </summary>
    public class RemoteMutePeerRequest
    {
        /// <summary>
        /// 目标用户 PeerId
        /// </summary>
        public string TargetPeerId { get; set; } = string.Empty;

        /// <summary>
        /// 是否静音
        /// </summary>
        public bool IsMuted { get; set; } = true;
    }
}
