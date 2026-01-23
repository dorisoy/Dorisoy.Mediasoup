using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dorisoy.Meeting.Client.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Dorisoy.Meeting.Client.Services;

/// <summary>
/// SignalR 服务实现 - 处理与服务器的信令通信
/// </summary>
public class SignalRService : ISignalRService
{
    private readonly ILogger<SignalRService> _logger;
    private HubConnection? _connection;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        // 使用 JsonStringEnumMemberConverter 与服务端保持一致
        Converters = { new JsonStringEnumMemberConverter() }
    };

    /// <summary>
    /// 是否已连接
    /// </summary>
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

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
            .AddJsonProtocol(options =>
            {
                // 与服务端保持一致的 JSON 序列化配置（必须使用相同的枚举转换器）
                options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
                // 重要：必须使用 JsonStringEnumMemberConverter 与服务端一致！
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumMemberConverter());
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
            OnConnected?.Invoke();
            await Task.CompletedTask;
        };

        // 重连中事件
        _connection.Reconnecting += async error =>
        {
            _logger.LogWarning(error, "Reconnecting...");
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
