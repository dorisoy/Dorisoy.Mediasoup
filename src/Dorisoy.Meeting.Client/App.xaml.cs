using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Windows;
using Dorisoy.Meeting.Client.Services;
using Dorisoy.Meeting.Client.ViewModels;
using Dorisoy.Meeting.Client.Views;
using Dorisoy.Meeting.Client.WebRtc;

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

        try
        {
            // 显示加入房间窗口
            Log.Information("正在创建 JoinRoomViewModel...");
            var joinRoomViewModel = _serviceProvider.GetRequiredService<JoinRoomViewModel>();
            
            Log.Information("正在创建 JoinRoomWindow...");
            var joinRoomWindow = new JoinRoomWindow(joinRoomViewModel);
            
            Log.Information("正在显示 JoinRoomWindow...");
            joinRoomWindow.ShowDialog();
            
            Log.Information("加入窗口关闭: IsConfirmed={IsConfirmed}, JoinRoomInfo={JoinRoomInfo}", 
                joinRoomWindow.IsConfirmed, 
                joinRoomWindow.JoinRoomInfo != null ? "not null" : "null");
            
            if (joinRoomWindow.IsConfirmed && joinRoomWindow.JoinRoomInfo != null)
            {
                // 用户确认加入，显示主窗口
                Log.Information("正在创建 MainViewModel...");
                var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
                var joinInfo = joinRoomWindow.JoinRoomInfo;
                
                // 应用加入房间信息到主视图模型
                mainViewModel.ServerUrl = joinInfo.ServerUrl;
                mainViewModel.CurrentUserName = joinInfo.UserName;
                mainViewModel.RoomId = joinInfo.RoomId;
                
                Log.Information("正在显示主窗口: UserName={UserName}, ServerUrl={ServerUrl}, RoomId={RoomId}", 
                    joinInfo.UserName, joinInfo.ServerUrl, joinInfo.RoomId);
                
                // 显示主窗口并最大化
                var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                mainWindow.WindowState = System.Windows.WindowState.Maximized;
                mainWindow.Show();
                
                // 自动加入房间
                Log.Information("开始自动加入房间...");
                _ = mainViewModel.AutoJoinAsync(joinInfo);
            }
            else
            {
                // 用户取消，退出应用
                Log.Information("用户取消加入，退出应用");
                Shutdown();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "启动过程中发生异常");
            MessageBox.Show($"启动失败: {ex.Message}\n\n详细信息: {ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
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
        services.AddTransient<JoinRoomViewModel>();

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
                // 使用异步方式释放 ServiceProvider（因为 SignalRService 只实现了 IAsyncDisposable）
                if (_serviceProvider != null)
                {
                    Task.Run(async () => await _serviceProvider.DisposeAsync()).Wait(TimeSpan.FromSeconds(3));
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error disposing ServiceProvider");
            }

            Log.CloseAndFlush();
        }
    }
}
