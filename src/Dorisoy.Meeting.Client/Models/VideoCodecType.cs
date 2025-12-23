namespace Dorisoy.Meeting.Client.Models;

/// <summary>
/// 视频编解码器类型枚举
/// 定义支持的视频编解码格式
/// </summary>
public enum VideoCodecType
{
    /// <summary>
    /// VP8 编解码器 - Google 开发，广泛兼容
    /// </summary>
    VP8 = 0,
    
    /// <summary>
    /// VP9 编解码器 - VP8 的继任者，更高压缩效率
    /// </summary>
    VP9 = 1,
    
    /// <summary>
    /// H.264/AVC 编解码器 - 行业标准，硬件加速支持最广
    /// </summary>
    H264 = 2
}

/// <summary>
/// 视频编解码器信息
/// 用于 UI 显示和绑定
/// </summary>
public class VideoCodecInfo
{
    /// <summary>
    /// 编解码器类型
    /// </summary>
    public VideoCodecType CodecType { get; set; }
    
    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// 编解码器描述
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// MIME 类型
    /// </summary>
    public string MimeType { get; set; } = string.Empty;
    
    /// <summary>
    /// RTP Payload Type
    /// </summary>
    public int PayloadType { get; set; }

    /// <summary>
    /// 预定义的编解码器列表
    /// </summary>
    public static VideoCodecInfo[] AvailableCodecs { get; } = new[]
    {
        new VideoCodecInfo
        {
            CodecType = VideoCodecType.VP8,
            DisplayName = "VP8",
            Description = "Google VP8 - 兼容性好，CPU 占用适中",
            MimeType = "video/VP8",
            PayloadType = 96
        },
        new VideoCodecInfo
        {
            CodecType = VideoCodecType.VP9,
            DisplayName = "VP9",
            Description = "Google VP9 - 高压缩率，支持 SVC",
            MimeType = "video/VP9",
            PayloadType = 98
        },
        new VideoCodecInfo
        {
            CodecType = VideoCodecType.H264,
            DisplayName = "H.264",
            Description = "H.264/AVC - 硬件加速支持最广",
            MimeType = "video/H264",
            PayloadType = 100
        }
    };

    /// <summary>
    /// 根据类型获取编解码器信息
    /// </summary>
    public static VideoCodecInfo GetByType(VideoCodecType type)
    {
        return AvailableCodecs.FirstOrDefault(c => c.CodecType == type) ?? AvailableCodecs[0];
    }
    
    public override string ToString() => DisplayName;
}
