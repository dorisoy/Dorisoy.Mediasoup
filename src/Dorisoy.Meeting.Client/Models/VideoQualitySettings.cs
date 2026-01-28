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
    /// 优化说明：
    /// - 提高各档位码率以改善画质，减少马赛克
    /// - 降低 CpuUsed 值以获得更好的编码质量（值越低质量越好）
    /// - 调整关键帧间隔，高画质模式更频繁发送关键帧以便快速恢复
    /// </summary>
    public static readonly VideoQualitySettings[] Presets = 
    [
        // 低画质 - 480x360, 500Kbps (提高分辨率和码率)
        new VideoQualitySettings
        {
            Preset = VideoQualityPreset.Low,
            DisplayName = "低画质",
            Description = "适合低带宽网络 (500Kbps)",
            Width = 480,
            Height = 360,
            Bitrate = 500_000,
            FrameRate = 20,
            KeyFrameInterval = 2,
            CpuUsed = 6  // 平衡速度和质量
        },
        
        // 标准画质 - 640x480, 1.5Mbps (提高码率)
        new VideoQualitySettings
        {
            Preset = VideoQualityPreset.Standard,
            DisplayName = "标准画质",
            Description = "平衡模式 (1.5 Mbps)",
            Width = 640,
            Height = 480,
            Bitrate = 1_500_000,
            FrameRate = 25,
            KeyFrameInterval = 2,
            CpuUsed = 4  // 提高质量
        },
        
        // 高画质 - 1280x720, 4Mbps (大幅提高码率)
        new VideoQualitySettings
        {
            Preset = VideoQualityPreset.High,
            DisplayName = "高画质",
            Description = "高清 720p (4 Mbps)",
            Width = 1280,
            Height = 720,
            Bitrate = 4_000_000,
            FrameRate = 30,
            KeyFrameInterval = 1,  // 更频繁的关键帧
            CpuUsed = 3  // 高质量编码
        },
        
        // 超高画质 - 1920x1080, 8Mbps (大幅提高码率)
        new VideoQualitySettings
        {
            Preset = VideoQualityPreset.Ultra,
            DisplayName = "超高画质",
            Description = "全高清 1080p (8 Mbps)",
            Width = 1920,
            Height = 1080,
            Bitrate = 8_000_000,
            FrameRate = 30,
            KeyFrameInterval = 1,  // 每秒一个关键帧
            CpuUsed = 2  // 最高质量编码
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
