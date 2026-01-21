namespace Dorisoy.Meeting.Client.Models;

/// <summary>
/// 服务模式响应
/// </summary>
public class ServeModeResponse
{
    /// <summary>
    /// 服务模式: Open, Pull, Invite
    /// </summary>
    public string ServeMode { get; set; } = "Open";
}

/// <summary>
/// 加入房间响应
/// </summary>
public class JoinRoomResponse
{
    /// <summary>
    /// 房间内所有 Peer 列表
    /// </summary>
    public PeerInfo[] Peers { get; set; } = [];

    /// <summary>
    /// 主持人 PeerId
    /// </summary>
    public string? HostPeerId { get; set; }

    /// <summary>
    /// 当前用户的 PeerId
    /// </summary>
    public string? SelfPeerId { get; set; }
}

/// <summary>
/// 创建 Transport 响应
/// </summary>
public class CreateTransportResponse
{
    /// <summary>
    /// Transport ID
    /// </summary>
    public string TransportId { get; set; } = string.Empty;

    /// <summary>
    /// ICE 参数
    /// </summary>
    public object? IceParameters { get; set; }

    /// <summary>
    /// ICE 候选列表
    /// </summary>
    public object[]? IceCandidates { get; set; }

    /// <summary>
    /// DTLS 参数
    /// </summary>
    public object? DtlsParameters { get; set; }

    /// <summary>
    /// SCTP 参数
    /// </summary>
    public object? SctpParameters { get; set; }
}

/// <summary>
/// 生产媒体响应
/// </summary>
public class ProduceResponse
{
    /// <summary>
    /// Producer ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 媒体源
    /// </summary>
    public string Source { get; set; } = string.Empty;
}
