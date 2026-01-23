using System.IO;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;

namespace Dorisoy.Meeting.Client.WebRtc;

/// <summary>
/// FFmpeg 配置助手 - 管理 FFmpeg 库的加载和初始化
/// </summary>
public static class FFmpegConfig
{
    private static bool _initialized;
    private static bool _initializationAttempted; // 防止初始化失败后反复重试
    private static readonly object _lock = new();
    private static string? _libraryPath;

    // Windows API 用于设置 DLL 搜索路径
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDllDirectory(string lpPathName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AddDllDirectory(string lpPathName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetDefaultDllDirectories(uint directoryFlags);

    private const uint LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000;

    /// <summary>
    /// FFmpeg 库路径
    /// </summary>
    public static string? LibraryPath => _libraryPath;

    /// <summary>
    /// 是否已初始化
    /// </summary>
    public static bool IsInitialized => _initialized;

    /// <summary>
    /// 初始化 FFmpeg
    /// </summary>
    /// <param name="logger">日志</param>
    /// <param name="customPath">自定义路径 (可选)</param>
    /// <returns>是否初始化成功</returns>
    public static bool Initialize(ILogger? logger = null, string? customPath = null)
    {
        if (_initialized)
            return true;

        // 如果已经尝试过初始化但失败，不再重试
        if (_initializationAttempted)
            return false;

        lock (_lock)
        {
            if (_initialized)
                return true;
            
            if (_initializationAttempted)
                return false;

            _initializationAttempted = true;

            try
            {
                _libraryPath = FindFFmpegPath(customPath);

                if (!string.IsNullOrEmpty(_libraryPath))
                {
                    // 设置 FFmpeg.AutoGen 的根路径
                    ffmpeg.RootPath = _libraryPath;
                    
                    // 同时设置 Windows DLL 搜索路径，确保 P/Invoke 能找到 DLL
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        SetDllDirectory(_libraryPath);
                        logger?.LogDebug("Set DLL directory to: {Path}", _libraryPath);
                    }
                    
                    logger?.LogInformation("FFmpeg library path set to: {Path}", _libraryPath);
                }
                else
                {
                    logger?.LogWarning("FFmpeg library path not found, using system default");
                    return false;
                }

                // 注意：FFmpeg.AutoGen 6.0.0 默认使用静态 P/Invoke 绑定
                // 不调用 DynamicallyLoadedBindings.Initialize() 以避免函数不匹配导致的 NotSupportedException
                logger?.LogDebug("Using static P/Invoke bindings for FFmpeg");

                // 验证 FFmpeg 是否可用
                try
                {
                    var version = ffmpeg.av_version_info();
                    logger?.LogInformation("FFmpeg version: {Version}", version);
                    _initialized = true;
                    return true;
                }
                catch (DllNotFoundException ex)
                {
                    logger?.LogError(ex, "FFmpeg libraries not found. Please ensure FFmpeg DLLs are available.");
                    return false;
                }
                catch (NotSupportedException ex)
                {
                    // FFmpeg.AutoGen 动态绑定在某些函数不存在时会抛出此异常
                    logger?.LogError(ex, "FFmpeg function not supported. This may indicate version mismatch.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to initialize FFmpeg");
                return false;
            }
        }
    }

    /// <summary>
    /// 查找 FFmpeg6.0 库路径
    /// </summary>
    private static string? FindFFmpegPath(string? customPath)
    {
        var searchPaths = new List<string>();

        // 1. 自定义路径优先
        if (!string.IsNullOrEmpty(customPath))
        {
            searchPaths.Add(customPath);
        }

        // 2. 应用程序目录 - FFmpeg.AutoGen.Redist NuGet 包会将 DLL 复制到这里
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        searchPaths.Add(baseDir); // 最高优先级！
        
        // 3. runtimes 目录 (NuGet 包的标准位置)
        searchPaths.Add(Path.Combine(baseDir, "runtimes", "win-x64", "native"));

        // 4. 开发时：从 bin/Debug 向上查找 src/FFmpeg/bin/x64
        searchPaths.Add(Path.Combine(baseDir, "..", "..", "..", "..", "..", "src", "FFmpeg", "bin", "x64"));
        searchPaths.Add(Path.Combine(baseDir, "..", "..", "..", "..", "FFmpeg", "bin", "x64"));
        searchPaths.Add(Path.Combine(baseDir, "..", "..", "..", "FFmpeg", "bin", "x64"));
        
        // 也尝试不带 bin/x64 的路径
        searchPaths.Add(Path.Combine(baseDir, "..", "..", "..", "..", "..", "src", "FFmpeg"));
        searchPaths.Add(Path.Combine(baseDir, "..", "..", "..", "..", "FFmpeg"));

        // 5. 应用程序目录下的 FFmpeg 子目录
        searchPaths.Add(Path.Combine(baseDir, "FFmpeg", "bin", "x64"));
        searchPaths.Add(Path.Combine(baseDir, "FFmpeg"));
        searchPaths.Add(Path.Combine(baseDir, "ffmpeg"));

        // 6. 环境变量
        var envPath = Environment.GetEnvironmentVariable("FFMPEG_PATH");
        if (!string.IsNullOrEmpty(envPath))
        {
            searchPaths.Add(envPath);
        }

        // 查找第一个存在的路径
        foreach (var path in searchPaths)
        {
            try
            {
                var fullPath = Path.GetFullPath(path);
                if (Directory.Exists(fullPath))
                {
                    // 检查是否包含 FFmpeg DLL
                    if (ContainsFFmpegLibs(fullPath))
                    {
                        return fullPath;
                    }
                }
            }
            catch
            {
                // 忽略路径解析错误
            }
        }

        return null;
    }

    /// <summary>
    /// 检查目录是否包含 FFmpeg 库文件
    /// </summary>
    private static bool ContainsFFmpegLibs(string path)
    {
        var requiredLibs = new[]
        {
            "avcodec-*.dll",
            "avcodec*.dll",
            "libavcodec*.dll"
        };

        foreach (var pattern in requiredLibs)
        {
            try
            {
                var files = Directory.GetFiles(path, pattern);
                if (files.Length > 0)
                    return true;
            }
            catch
            {
                // 忽略
            }
        }

        // 也检查没有版本号的情况
        return File.Exists(Path.Combine(path, "avcodec.dll")) ||
               File.Exists(Path.Combine(path, "libavcodec.dll")) ||
               File.Exists(Path.Combine(path, "avcodec-60.dll")) ||
               File.Exists(Path.Combine(path, "avcodec-61.dll"));
    }

    /// <summary>
    /// 获取 FFmpeg 版本信息
    /// </summary>
    public static string GetVersionInfo()
    {
        if (!_initialized)
            return "Not initialized";

        try
        {
            return ffmpeg.av_version_info() ?? "Unknown";
        }
        catch
        {
            return "Error getting version";
        }
    }

    /// <summary>
    /// 获取支持的编解码器列表
    /// </summary>
    public static IEnumerable<string> GetSupportedCodecs()
    {
        var codecs = new List<string>();
        
        if (!_initialized)
            return codecs;

        try
        {
            unsafe
            {
                void* opaque = null;
                AVCodec* codec;
                while ((codec = ffmpeg.av_codec_iterate(&opaque)) != null)
                {
                    var name = Marshal.PtrToStringAnsi((IntPtr)codec->name);
                    if (name != null)
                    {
                        var type = ffmpeg.av_codec_is_encoder(codec) != 0 ? "encoder" : "decoder";
                        codecs.Add($"{name} ({type})");
                    }
                }
            }
        }
        catch
        {
            // 忽略
        }

        return codecs;
    }
}
