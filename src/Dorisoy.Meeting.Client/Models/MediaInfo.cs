namespace Dorisoy.Meeting.Client.Models;

/// <summary>
/// 消费者信息
/// </summary>
public class ConsumerInfo
{
    /// <summary>
    /// 消费者 ID
    /// </summary>
    public string ConsumerId { get; set; } = string.Empty;

    /// <summary>
    /// 生产者 ID
    /// </summary>
    public string ProducerId { get; set; } = string.Empty;

    /// <summary>
    /// 生产者 Peer ID
    /// </summary>
    public string ProducerPeerId { get; set; } = string.Empty;

    /// <summary>
    /// 媒体类型
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// 媒体源
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// 是否暂停
    /// </summary>
    public bool Paused { get; set; }
}

/// <summary>
/// 生产者信息
/// </summary>
public class ProducerInfo
{
    /// <summary>
    /// 生产者 ID
    /// </summary>
    public string ProducerId { get; set; } = string.Empty;

    /// <summary>
    /// 媒体类型
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// 媒体源
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// 是否暂停
    /// </summary>
    public bool Paused { get; set; }
}
