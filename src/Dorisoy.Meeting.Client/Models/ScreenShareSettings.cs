namespace Dorisoy.Meeting.Client.Models;

/// <summary>
/// 屏幕共享质量预设枚举
/// </summary>
public enum ScreenShareQualityPreset
{
    /// <summary>
    /// 流畅 - 低分辨率，高帧率，适合动态内容
    /// </summary>
    Smooth,

    /// <summary>
    /// 标准 - 平衡模式
    /// </summary>
    Standard,

    /// <summary>
    /// 清晰 - 高分辨率，适合文档和静态内容
    /// </summary>
    Clear,

    /// <summary>
    /// 超清 - 最高分辨率
    /// </summary>
    Ultra,

    /// <summary>
    /// 自定义
    /// </summary>
    Custom
}

/// <summary>
/// 屏幕共享设置配置
/// </summary>
public class ScreenShareSettings
{
    /// <summary>
    /// 预设名称
    /// </summary>
    public ScreenShareQualityPreset Preset { get; init; }

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
    public int Width { get; set; }

    /// <summary>
    /// 视频高度
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// 目标帧率
    /// </summary>
    public int FrameRate { get; set; }

    /// <summary>
    /// 是否显示鼠标指针
    /// </summary>
    public bool ShowCursor { get; set; } = true;

    /// <summary>
    /// 分辨率描述
    /// </summary>
    public string Resolution => $"{Width}x{Height}";

    /// <summary>
    /// 帧率描述
    /// </summary>
    public string FrameRateDescription => $"{FrameRate} fps";

    /// <summary>
    /// 预定义的质量档位
    /// </summary>
    public static readonly ScreenShareSettings[] Presets =
    [
        // 流畅模式 - 720p, 30fps
        new ScreenShareSettings
        {
            Preset = ScreenShareQualityPreset.Smooth,
            DisplayName = "流畅模式",
            Description = "720p 30fps - 适合动态内容、演示视频",
            Width = 1280,
            Height = 720,
            FrameRate = 30,
            ShowCursor = true
        },

        // 标准模式 - 1080p, 15fps
        new ScreenShareSettings
        {
            Preset = ScreenShareQualityPreset.Standard,
            DisplayName = "标准模式",
            Description = "1080p 15fps - 平衡画质与流畅度",
            Width = 1920,
            Height = 1080,
            FrameRate = 15,
            ShowCursor = true
        },

        // 清晰模式 - 1080p, 10fps
        new ScreenShareSettings
        {
            Preset = ScreenShareQualityPreset.Clear,
            DisplayName = "清晰模式",
            Description = "1080p 10fps - 适合文档、代码展示",
            Width = 1920,
            Height = 1080,
            FrameRate = 10,
            ShowCursor = true
        },

        // 超清模式 - 原始分辨率, 15fps
        new ScreenShareSettings
        {
            Preset = ScreenShareQualityPreset.Ultra,
            DisplayName = "超清模式",
            Description = "原始分辨率 15fps - 最高画质",
            Width = 0, // 0 表示使用原始屏幕分辨率
            Height = 0,
            FrameRate = 15,
            ShowCursor = true
        }
    ];

    /// <summary>
    /// 可选的分辨率列表
    /// </summary>
    public static readonly (int Width, int Height, string Name)[] AvailableResolutions =
    [
        (1920, 1080, "1920x1080 (1080p)"),
        (1280, 720, "1280x720 (720p)"),
        (1024, 768, "1024x768"),
        (800, 600, "800x600"),
        (0, 0, "原始分辨率")
    ];

    /// <summary>
    /// 可选的帧率列表
    /// </summary>
    public static readonly int[] AvailableFrameRates = [5, 10, 15, 20, 25, 30];

    /// <summary>
    /// 根据预设获取配置
    /// </summary>
    public static ScreenShareSettings GetPreset(ScreenShareQualityPreset preset)
    {
        return Presets.FirstOrDefault(p => p.Preset == preset) ?? Presets[1]; // 默认标准模式
    }

    /// <summary>
    /// 创建自定义配置
    /// </summary>
    public static ScreenShareSettings CreateCustom(int width, int height, int frameRate, bool showCursor)
    {
        return new ScreenShareSettings
        {
            Preset = ScreenShareQualityPreset.Custom,
            DisplayName = "自定义",
            Description = $"{(width == 0 ? "原始" : $"{width}x{height}")} {frameRate}fps",
            Width = width,
            Height = height,
            FrameRate = frameRate,
            ShowCursor = showCursor
        };
    }

    /// <summary>
    /// 克隆当前设置
    /// </summary>
    public ScreenShareSettings Clone()
    {
        return new ScreenShareSettings
        {
            Preset = Preset,
            DisplayName = DisplayName,
            Description = Description,
            Width = Width,
            Height = Height,
            FrameRate = FrameRate,
            ShowCursor = ShowCursor
        };
    }
}
