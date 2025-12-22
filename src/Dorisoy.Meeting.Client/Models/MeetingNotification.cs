using System.Text.Json.Serialization;

namespace Dorisoy.Meeting.Client.Models;

/// <summary>
/// 服务器推送通知
/// </summary>
public class MeetingNotification
{
    /// <summary>
    /// 通知类型
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 通知数据
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }
}
