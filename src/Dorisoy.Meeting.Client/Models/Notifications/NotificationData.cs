namespace Dorisoy.Meeting.Client.Models.Notifications;

/// <summary>
/// Peer 加入房间通知数据
/// </summary>
public class PeerJoinRoomData
{
    /// <summary>
    /// 加入的 Peer 信息
    /// </summary>
    public PeerInfo? Peer { get; set; }
}

/// <summary>
/// Peer 离开房间通知数据
/// </summary>
public class PeerLeaveRoomData
{
    /// <summary>
    /// 离开的 Peer ID
    /// </summary>
    public string? PeerId { get; set; }
}

/// <summary>
/// 新消费者通知数据
/// </summary>
public class NewConsumerData
{
    /// <summary>
    /// 生产者 Peer ID
    /// </summary>
    public string ProducerPeerId { get; set; } = string.Empty;

    /// <summary>
    /// 媒体类型: audio/video
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// 生产者 ID
    /// </summary>
    public string ProducerId { get; set; } = string.Empty;

    /// <summary>
    /// 消费者 ID
    /// </summary>
    public string ConsumerId { get; set; } = string.Empty;

    /// <summary>
    /// RTP 参数
    /// </summary>
    public object? RtpParameters { get; set; }

    /// <summary>
    /// 消费者类型
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// 生产者应用数据
    /// </summary>
    public Dictionary<string, object>? ProducerAppData { get; set; }

    /// <summary>
    /// 生产者是否暂停
    /// </summary>
    public bool ProducerPaused { get; set; }
}

/// <summary>
/// 消费者关闭通知数据
/// </summary>
public class ConsumerClosedData
{
    /// <summary>
    /// 消费者 ID
    /// </summary>
    public string ConsumerId { get; set; } = string.Empty;
}

/// <summary>
/// 生产者关闭通知数据
/// </summary>
public class ProducerClosedData
{
    /// <summary>
    /// 生产者 ID
    /// </summary>
    public string ProducerId { get; set; } = string.Empty;
}

/// <summary>
/// 请求生产源通知数据
/// </summary>
public class ProduceSourcesData
{
    /// <summary>
    /// 需要生产的源列表
    /// </summary>
    public HashSet<string> Sources { get; set; } = [];
}

/// <summary>
/// 消费者分值通知数据
/// </summary>
public class ConsumerScoreData
{
    /// <summary>
    /// 消费者 ID
    /// </summary>
    public string ConsumerId { get; set; } = string.Empty;

    /// <summary>
    /// 分值
    /// </summary>
    public object? Score { get; set; }
}

/// <summary>
/// 生产者分值通知数据
/// </summary>
public class ProducerScoreData
{
    /// <summary>
    /// 生产者 ID
    /// </summary>
    public string ProducerId { get; set; } = string.Empty;

    /// <summary>
    /// 分值
    /// </summary>
    public object? Score { get; set; }
}

/// <summary>
/// 聊天消息数据（用于反序列化）
/// </summary>
public class ChatMessageData
{
    /// <summary>
    /// 消息ID
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// 发送者ID
    /// </summary>
    public string? SenderId { get; set; }

    /// <summary>
    /// 发送者名称
    /// </summary>
    public string? SenderName { get; set; }

    /// <summary>
    /// 接收者ID（为空表示群聊）
    /// </summary>
    public string? ReceiverId { get; set; }

    /// <summary>
    /// 消息内容
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// 消息类型
    /// </summary>
    public int? MessageType { get; set; }

    /// <summary>
    /// 文件名
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// 文件大小
    /// </summary>
    public long? FileSize { get; set; }

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime? Timestamp { get; set; }
}

/// <summary>
/// 表情反应数据（用于反序列化）
/// </summary>
public class EmojiReactionData
{
    /// <summary>
    /// 发送者ID
    /// </summary>
    public string? SenderId { get; set; }

    /// <summary>
    /// 发送者名称
    /// </summary>
    public string? SenderName { get; set; }

    /// <summary>
    /// 表情
    /// </summary>
    public string? Emoji { get; set; }
}

/// <summary>
/// 屏幕共享请求数据
/// </summary>
public class ScreenShareRequestData
{
    /// <summary>
    /// 请求者ID
    /// </summary>
    public string? RequesterId { get; set; }

    /// <summary>
    /// 请求者名称
    /// </summary>
    public string? RequesterName { get; set; }

    /// <summary>
    /// 共享会话ID
    /// </summary>
    public string? SessionId { get; set; }
}

/// <summary>
/// 屏幕共享响应数据
/// </summary>
public class ScreenShareResponseData
{
    /// <summary>
    /// 响应者ID
    /// </summary>
    public string? ResponderId { get; set; }

    /// <summary>
    /// 共享会话ID
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// 是否接受
    /// </summary>
    public bool Accepted { get; set; }
}

/// <summary>
/// 广播消息数据（从服务器 BroadcastMessage 方法发送的消息）
/// </summary>
public class BroadcastMessageData
{
    /// <summary>
    /// 消息类型：chatMessage, emojiReaction, screenShareRequest, screenShareResponse 等
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// 发送者 PeerId
    /// </summary>
    public string? SenderId { get; set; }

    /// <summary>
    /// 发送者显示名
    /// </summary>
    public string? SenderName { get; set; }

    /// <summary>
    /// 消息数据（根据 Type 不同，内容格式不同）
    /// </summary>
    public object? Data { get; set; }
}
