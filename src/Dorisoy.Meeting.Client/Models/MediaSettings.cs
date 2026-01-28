namespace Dorisoy.Meeting.Client.Models;

/// <summary>
/// 媒体设置模型 - 用于持久化保存用户的媒体配置
/// </summary>
public class MediaSettings
{
    #region 设备设置

    /// <summary>
    /// 选中的摄像头设备 ID
    /// </summary>
    public string? CameraDeviceId { get; set; }

    /// <summary>
    /// 选中的麦克风设备 ID
    /// </summary>
    public string? MicrophoneDeviceId { get; set; }

    #endregion

    #region 视频质量设置

    /// <summary>
    /// 视频质量预设 (Low/Medium/High/Ultra)
    /// </summary>
    public string VideoQualityPreset { get; set; } = "High";

    /// <summary>
    /// 视频编解码器类型 (VP8/VP9/H264)
    /// </summary>
    public string VideoCodec { get; set; } = "VP9";

    #endregion

    #region 屏幕共享设置

    /// <summary>
    /// 屏幕共享质量预设 (Fluent/Standard/HighDefinition/Ultra)
    /// </summary>
    public string ScreenSharePreset { get; set; } = "Standard";

    /// <summary>
    /// 屏幕共享时是否显示鼠标指针
    /// </summary>
    public bool ScreenShareShowCursor { get; set; } = true;

    #endregion

    #region 录制设置

    /// <summary>
    /// 录制视频保存目录
    /// </summary>
    public string? RecordingSavePath { get; set; }

    /// <summary>
    /// 录制格式 (MP4/WebM/MKV)
    /// </summary>
    public string RecordingFormat { get; set; } = "MP4";

    /// <summary>
    /// 开始录制时显示倒计时
    /// </summary>
    public bool RecordingShowCountdown { get; set; } = true;

    /// <summary>
    /// 录制时显示视频边框高亮
    /// </summary>
    public bool RecordingShowBorder { get; set; } = true;

    #endregion
}
