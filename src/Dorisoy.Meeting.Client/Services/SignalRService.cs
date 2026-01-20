using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Dorisoy.Meeting.Client.Models;

namespace Dorisoy.Meeting.Client.Services;

/// <summary>
/// SignalR 服务实现 - 处理与服务器的信令通信
/// </summary>
public class SignalRService : ISignalRService
{
    private readonly ILogger<SignalRService> _logger;
    private HubConnection? _connection;
    private int _reconnectAttempt;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// 是否已连接
    /// </summary>
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    /// <summary>
    /// 连接状态
    /// </summary>
    public HubConnectionState ConnectionState => _connection?.State ?? HubConnectionState.Disconnected;

    /// <summary>
    /// 收到服务器通知事件
    /// </summary>
    public event Action<MeetingNotification>? OnNotification;

    /// <summary>
    /// 连接成功事件
    /// </summary>
    public event Action? OnConnected;

    /// <summary>
    /// 连接断开事件
    /// </summary>
    public event Action<Exception?>? OnDisconnected;

    /// <summary>
    /// 正在重连事件 - 参数：第几次重试
    /// </summary>
    public event Action<int>? OnReconnecting;

    /// <summary>
    /// 重连成功事件
    /// </summary>
    public event Action? OnReconnected;

    public SignalRService(ILogger<SignalRService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 连接到服务器
    /// </summary>
    public async Task ConnectAsync(string serverUrl, string accessToken)
    {
        if (_connection != null)
        {
            await DisconnectAsync();
        }

        _logger.LogInformation("Connecting to server: {ServerUrl}", serverUrl);

        _connection = new HubConnectionBuilder()
            .WithUrl($"{serverUrl}/hubs/meetingHub", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                options.SkipNegotiation = true;
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
            })
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.FromSeconds(0),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)
            })
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Debug);
            })
            .Build();

        // 注册通知处理
        _connection.On<MeetingNotification>("Notify", notification =>
        {
            _logger.LogDebug("Received notification: {Type}", notification.Type);
            OnNotification?.Invoke(notification);
        });

        // 连接关闭事件
        _connection.Closed += async error =>
        {
            _logger.LogWarning(error, "Connection closed");
            OnDisconnected?.Invoke(error);
            await Task.CompletedTask;
        };

        // 重连成功事件
        _connection.Reconnected += async connectionId =>
        {
            _logger.LogInformation("Reconnected with connectionId: {ConnectionId}", connectionId);
            _reconnectAttempt = 0;  // 重置重连计数
            OnReconnected?.Invoke();
            OnConnected?.Invoke();
            await Task.CompletedTask;
        };

        // 重连中事件
        _connection.Reconnecting += async error =>
        {
            _reconnectAttempt++;
            _logger.LogWarning(error, "Reconnecting... Attempt: {Attempt}", _reconnectAttempt);
            OnReconnecting?.Invoke(_reconnectAttempt);
            await Task.CompletedTask;
        };

        await _connection.StartAsync();
        _logger.LogInformation("Connected to server successfully");
        OnConnected?.Invoke();
    }

    /// <summary>
    /// 断开连接
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_connection != null)
        {
            _logger.LogInformation("Disconnecting from server");
            await _connection.StopAsync();
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    /// <summary>
    /// 调用 Hub 方法（带返回数据）
    /// </summary>
    public async Task<MeetingMessage<T>> InvokeAsync<T>(string methodName, object? arg = null)
    {
        if (_connection == null || !IsConnected)
        {
            _logger.LogWarning("Cannot invoke {MethodName}: Not connected", methodName);
            return new MeetingMessage<T> { Code = 500, Message = "Not connected" };
        }

        try
        {
            _logger.LogDebug("Invoking {MethodName} with arg: {Arg}", methodName, arg);

            var result = arg == null
                ? await _connection.InvokeAsync<JsonElement>(methodName)
                : await _connection.InvokeAsync<JsonElement>(methodName, arg);

            var response = JsonSerializer.Deserialize<MeetingMessage<T>>(result.GetRawText(), JsonOptions);
            
            if (response == null)
            {
                _logger.LogError("Failed to deserialize response for {MethodName}", methodName);
                return new MeetingMessage<T> { Code = 500, Message = "Deserialization failed" };
            }

            _logger.LogDebug("{MethodName} completed with code: {Code}", methodName, response.Code);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invoke {MethodName}", methodName);
            return new MeetingMessage<T> { Code = 500, Message = ex.Message };
        }
    }

    /// <summary>
    /// 调用 Hub 方法（无返回数据）
    /// </summary>
    public async Task<MeetingMessage> InvokeAsync(string methodName, object? arg = null)
    {
        if (_connection == null || !IsConnected)
        {
            _logger.LogWarning("Cannot invoke {MethodName}: Not connected", methodName);
            return new MeetingMessage { Code = 500, Message = "Not connected" };
        }

        try
        {
            _logger.LogDebug("Invoking {MethodName} with arg: {Arg}", methodName, arg);

            var result = arg == null
                ? await _connection.InvokeAsync<JsonElement>(methodName)
                : await _connection.InvokeAsync<JsonElement>(methodName, arg);

            var response = JsonSerializer.Deserialize<MeetingMessage>(result.GetRawText(), JsonOptions);
            
            if (response == null)
            {
                _logger.LogError("Failed to deserialize response for {MethodName}", methodName);
                return new MeetingMessage { Code = 500, Message = "Deserialization failed" };
            }

            _logger.LogDebug("{MethodName} completed with code: {Code}", methodName, response.Code);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invoke {MethodName}", methodName);
            return new MeetingMessage { Code = 500, Message = ex.Message };
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        GC.SuppressFinalize(this);
    }
}
