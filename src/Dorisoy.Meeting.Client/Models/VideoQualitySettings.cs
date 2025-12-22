namespace Dorisoy.Meeting.Client.Models;

/// <summary>
/// 视频质量预设枚举
/// </summary>
public enum VideoQualityPreset
{
    /// <summary>
    /// 低画质 - 适合低带宽环境
    /// </summary>
    Low,
    
    /// <summary>
    /// 标准画质 - 平衡模式
    /// </summary>
    Standard,
    
    /// <summary>
    /// 高画质 - 适合良好网络
    /// </summary>
    High,
    
    /// <summary>
    /// 超高画质 - 最佳视觉效果
    /// </summary>
    Ultra
}

/// <summary>
/// 视频质量配置
/// </summary>
public class VideoQualitySettings
{
    /// <summary>
    /// 预设名称
    /// </summary>
    public VideoQualityPreset Preset { get; init; }
    
    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;
    
    /// <summary>
    /// 描述
    /// </summary>
    public string Description { get; init; } = string.Empty;
    
    /// <summary>
    /// 视频宽度
    /// </summary>
    public int Width { get; init; }
    
    /// <summary>
    /// 视频高度
    /// </summary>
    public int Height { get; init; }
    
    /// <summary>
    /// 目标比特率 (bps)
    /// </summary>
    public int Bitrate { get; init; }
    
    /// <summary>
    /// 目标帧率
    /// </summary>
    public int FrameRate { get; init; }
    
    /// <summary>
    /// 关键帧间隔（秒）
    /// </summary>
    public int KeyFrameInterval { get; init; }
    
    /// <summary>
    /// CPU 使用率 (0-8, 越低质量越好但 CPU 消耗更高)
    /// </summary>
    public int CpuUsed { get; init; }
    
    /// <summary>
    /// 分辨率描述
    /// </summary>
    public string Resolution => $"{Width}x{Height}";
    
    /// <summary>
    /// 比特率描述
    /// </summary>
    public string BitrateDescription => Bitrate >= 1000000 
        ? $"{Bitrate / 1000000.0:F1} Mbps" 
        : $"{Bitrate / 1000} Kbps";
    
    /// <summary>
    /// 预定义的质量档位
    /// </summary>
    public static readonly VideoQualitySettings[] Presets = 
    [
        // 低画质 - 320x240, 300Kbps
        new VideoQualitySettings
        {
            Preset = VideoQualityPreset.Low,
            DisplayName = "低画质",
            Description = "适合低带宽网络 (300Kbps)",
            Width = 320,
            Height = 240,
            Bitrate = 300_000,
            FrameRate = 15,
            KeyFrameInterval = 2,
            CpuUsed = 8  // 最快编码
        },
        
        // 标准画质 - 640x480, 1Mbps
        new VideoQualitySettings
        {
            Preset = VideoQualityPreset.Standard,
            DisplayName = "标准画质",
            Description = "平衡模式 (1 Mbps)",
            Width = 640,
            Height = 480,
            Bitrate = 1_000_000,
            FrameRate = 25,
            KeyFrameInterval = 2,
            CpuUsed = 5
        },
        
        // 高画质 - 1280x720, 2.5Mbps
        new VideoQualitySettings
        {
            Preset = VideoQualityPreset.High,
            DisplayName = "高画质",
            Description = "高清 720p (2.5 Mbps)",
            Width = 1280,
            Height = 720,
            Bitrate = 2_500_000,
            FrameRate = 30,
            KeyFrameInterval = 1,
            CpuUsed = 4
        },
        
        // 超高画质 - 1920x1080, 5Mbps
        new VideoQualitySettings
        {
            Preset = VideoQualityPreset.Ultra,
            DisplayName = "超高画质",
            Description = "全高清 1080p (5 Mbps)",
            Width = 1920,
            Height = 1080,
            Bitrate = 5_000_000,
            FrameRate = 30,
            KeyFrameInterval = 1,
            CpuUsed = 2  // 最低为 2，避免实时编码问题
        }
    ];
    
    /// <summary>
    /// 根据预设获取配置
    /// </summary>
    public static VideoQualitySettings GetPreset(VideoQualityPreset preset)
    {
        return Presets.FirstOrDefault(p => p.Preset == preset) ?? Presets[1]; // 默认标准画质
    }
}
