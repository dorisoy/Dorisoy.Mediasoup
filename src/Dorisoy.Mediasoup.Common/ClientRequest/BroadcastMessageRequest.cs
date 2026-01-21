namespace Dorisoy.Mediasoup
{
    /// <summary>
    /// 广播消息请求 - 用于即时聊天、表情反应等
    /// </summary>
    public class BroadcastMessageRequest
    {
        /// <summary>
        /// 消息类型：chatMessage, emojiReaction, screenShareRequest, screenShareResponse 等
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 消息数据（根据 Type 不同，内容格式不同）
        /// </summary>
        public object Data { get; set; }
    }
}
