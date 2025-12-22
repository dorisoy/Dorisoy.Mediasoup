using System.Text.Json.Serialization;

namespace Dorisoy.Meeting.Client.Models;

/// <summary>
/// Peer 信息
/// </summary>
public class PeerInfo
{
    /// <summary>
    /// Peer ID
    /// </summary>
    public string PeerId { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 媒体源列表
    /// </summary>
    public string[] Sources { get; set; } = [];

    /// <summary>
    /// 应用数据
    /// </summary>
    public Dictionary<string, object> AppData { get; set; } = [];
}

/// <summary>
/// 用户角色
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UserRole
{
    /// <summary>
    /// 普通用户
    /// </summary>
    Normal,

    /// <summary>
    /// 管理员
    /// </summary>
    Admin
}
