namespace Dorisoy.Meeting.Client.Models;

/// <summary>
/// 加入房间信息
/// </summary>
public class JoinRoomInfo
{
    /// <summary>
    /// 房间号码 (5位数字)
    /// </summary>
    public string RoomId { get; set; } = string.Empty;

    /// <summary>
    /// 用户名
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 服务器地址
    /// </summary>
    public string ServerUrl { get; set; } = "http://192.168.30.8:9000";

    /// <summary>
    /// 访问令牌 (JWT Token) - 用于 SignalR 连接认证
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// 选中的摄像头设备ID
    /// </summary>
    public string? CameraDeviceId { get; set; }

    /// <summary>
    /// 选中的麦克风设备ID
    /// </summary>
    public string? MicrophoneDeviceId { get; set; }

    /// <summary>
    /// 加入后是否禁用麦克风
    /// </summary>
    public bool MuteMicrophoneOnJoin { get; set; }

    /// <summary>
    /// 加入后是否禁用摄像头
    /// </summary>
    public bool MuteCameraOnJoin { get; set; }
}
