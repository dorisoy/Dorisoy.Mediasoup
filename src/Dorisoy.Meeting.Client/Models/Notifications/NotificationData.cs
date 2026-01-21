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
