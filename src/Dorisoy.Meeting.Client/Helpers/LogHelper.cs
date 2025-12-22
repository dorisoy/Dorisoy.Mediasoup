using Microsoft.Extensions.Logging;

namespace Dorisoy.Meeting.Client.Helpers;

/// <summary>
/// 日志工具类
/// </summary>
public static class LogHelper
{
    private static ILoggerFactory? _loggerFactory;

    /// <summary>
    /// 初始化日志工厂
    /// </summary>
    public static void Initialize(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// 获取日志记录器
    /// </summary>
    public static ILogger<T> GetLogger<T>()
    {
        if (_loggerFactory == null)
        {
            throw new InvalidOperationException("LogHelper has not been initialized. Call Initialize first.");
        }
        return _loggerFactory.CreateLogger<T>();
    }

    /// <summary>
    /// 获取指定名称的日志记录器
    /// </summary>
    public static ILogger GetLogger(string categoryName)
    {
        if (_loggerFactory == null)
        {
            throw new InvalidOperationException("LogHelper has not been initialized. Call Initialize first.");
        }
        return _loggerFactory.CreateLogger(categoryName);
    }
}
