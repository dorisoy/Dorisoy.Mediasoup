namespace Dorisoy.Mediasoup.Common
{
    /// <summary>
    /// 踢出用户请求（主持人操作）
    /// </summary>
    public class KickPeerRequest
    {
        /// <summary>
        /// 目标用户 PeerId
        /// </summary>
        public string TargetPeerId { get; set; } = string.Empty;
    }
}
