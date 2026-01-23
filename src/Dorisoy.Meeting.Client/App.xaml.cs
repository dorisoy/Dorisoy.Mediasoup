using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Windows;
using Dorisoy.Meeting.Client.Services;
using Dorisoy.Meeting.Client.ViewModels;
using Dorisoy.Meeting.Client.WebRtc;
using Application = System.Windows.Application;

namespace Dorisoy.Meeting.Client;

/// <summary>
/// 应用程序入口
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    /// <summary>
    /// 服务提供者
    /// </summary>
    public static ServiceProvider? ServiceProvider { get; private set; }

    /// <summary>
    /// 应用程序启动
    /// </summary>
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // 配置 Serilog 日志
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File("logs/meeting-client-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        // 配置依赖注入
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        ServiceProvider = _serviceProvider;

        // 初始化 FFmpeg
        var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
        if (!FFmpegConfig.Initialize(logger))
        {
            logger.LogWarning("FFmpeg initialization failed. Video encoding/decoding may not work.");
        }

        // 显示主窗口
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    /// <summary>
    /// 配置服务
    /// </summary>
    private static void ConfigureServices(IServiceCollection services)
    {
        // 日志
        services.AddLogging(builder =>
        {
            builder.AddSerilog(dispose: true);
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // 服务 - 单例
        services.AddSingleton<ISignalRService, SignalRService>();
        services.AddSingleton<IWebRtcService, WebRtcService>();

        // ViewModels
        services.AddTransient<MainViewModel>();

        // Views
        services.AddTransient<MainWindow>();
    }

    /// <summary>
    /// 应用程序退出
    /// </summary>
    private void Application_Exit(object sender, ExitEventArgs e)
    {
        Log.Information("Application exiting...");

        try
        {
            // 先显式关闭各服务的连接（在 Dispose 之前）
            if (_serviceProvider != null)
            {
                // 关闭 WebRTC 服务
                var webRtcService = _serviceProvider.GetService<IWebRtcService>();
                webRtcService?.Dispose();

                // 关闭 SignalR 服务（异步转同步）
                var signalRService = _serviceProvider.GetService<ISignalRService>();
                if (signalRService != null)
                {
                    try
                    {
                        // 使用 Task.Run 避免 UI 线程死锁
                        Task.Run(async () => await signalRService.DisposeAsync()).Wait(TimeSpan.FromSeconds(3));
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Error disposing SignalR service");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error during service cleanup");
        }
        finally
        {
            try
            {
                // 最后释放 ServiceProvider
                _serviceProvider?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error disposing ServiceProvider");
            }

            Log.CloseAndFlush();
        }
    }
}
