using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dorisoy.Meeting.Client.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Dorisoy.Meeting.Client.Services;

/// <summary>
/// SignalR 服务实现 - 处理与服务器的信令通信
/// 支持自动分块传输大消息，避免超过 SignalR 消息大小限制 (32KB)
/// </summary>
public class SignalRService : ISignalRService
{
    private readonly ILogger<SignalRService> _logger;
    private HubConnection? _connection;
    private int _reconnectAttempt;
    
    // 消息分块器 - 用于处理大消息
    private readonly MessageChunker _chunker;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
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

    /// <summary>
    /// 接收到分块消息事件 - 重组完成后触发
    /// </summary>
    public event Action<string, string>? OnChunkedMessageReceived;

    public SignalRService(ILogger<SignalRService> logger)
    {
        _logger = logger;
        _chunker = new MessageChunker(logger);
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
                // 与服务端保持一致的 JSON 序列化配置
                options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
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

        // 注册分块消息处理
        _connection.On<MessageChunk>("ReceiveChunk", chunk =>
        {
            try
            {
                var assembledJson = _chunker.ReceiveChunk(chunk);
                if (assembledJson != null)
                {
                    // 重组完成，解析并触发事件
                    _logger.LogDebug("分块消息重组完成: MessageId={MessageId}", chunk.MessageId);
                    
                    // 解析重组后的消息
                    using var doc = JsonDocument.Parse(assembledJson);
                    if (doc.RootElement.TryGetProperty("type", out var typeElement))
                    {
                        var type = typeElement.GetString() ?? "unknown";
                        OnChunkedMessageReceived?.Invoke(type, assembledJson);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理分块消息失败: MessageId={MessageId}", chunk.MessageId);
            }
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

            // 详细日志：输出原始 JSON 响应
            var rawJson = result.GetRawText();
            _logger.LogDebug("{MethodName} 原始响应: {RawJson}", methodName, rawJson.Length > 500 ? rawJson[..500] + "..." : rawJson);
            
            var response = JsonSerializer.Deserialize<MeetingMessage<T>>(rawJson, JsonOptions);
            
            if (response == null)
            {
                _logger.LogError("Failed to deserialize response for {MethodName}", methodName);
                return new MeetingMessage<T> { Code = 500, Message = "Deserialization failed" };
            }

            // 详细日志：输出反序列化后的数据
            _logger.LogDebug("{MethodName} 反序列化成功: Code={Code}, IsSuccess={IsSuccess}, Data={Data}", 
                methodName, response.Code, response.IsSuccess, response.Data != null ? "not null" : "null");
            
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
    /// 自动检测消息大小，超过阈值时分块发送
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
            // 检查是否需要分块发送
            if (arg != null && _chunker.NeedsChunking(arg))
            {
                return await InvokeWithChunkingAsync(methodName, arg);
            }

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
    /// 分块发送大消息
    /// </summary>
    private async Task<MeetingMessage> InvokeWithChunkingAsync(string methodName, object arg)
    {
        var messageSize = _chunker.GetMessageSize(arg);
        _logger.LogInformation("消息超过阈值 ({Size} bytes)，使用分块发送: {MethodName}", messageSize, methodName);

        var chunks = _chunker.SplitIntoChunks(arg);
        
        foreach (var chunk in chunks)
        {
            try
            {
                // 发送每个分块到服务器
                await _connection!.InvokeAsync("ReceiveChunk", new
                {
                    methodName,  // 原始方法名
                    chunk = new
                    {
                        chunk.MessageId,
                        chunk.ChunkIndex,
                        chunk.TotalChunks,
                        chunk.Data,
                        chunk.TotalSize
                    }
                });

                _logger.LogDebug("分块发送成功: {ChunkIndex}/{TotalChunks}", chunk.ChunkIndex + 1, chunk.TotalChunks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "分块发送失败: {ChunkIndex}/{TotalChunks}", chunk.ChunkIndex + 1, chunk.TotalChunks);
                return new MeetingMessage { Code = 500, Message = $"分块发送失败: {ex.Message}" };
            }
        }

        _logger.LogInformation("分块消息发送完成: {MethodName}, TotalChunks={TotalChunks}", methodName, chunks.Count);
        return new MeetingMessage { Code = 200, Message = "Chunked message sent" };
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
