using Dorisoy.Meeting.Client.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace Dorisoy.Meeting.Client.Services;

/// <summary>
/// SignalR 服务接口 - 处理与服务器的信令通信
/// </summary>
public interface ISignalRService : IAsyncDisposable
{
    /// <summary>
    /// 是否已连接
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 连接状态
    /// </summary>
    HubConnectionState ConnectionState { get; }

    /// <summary>
    /// 收到服务器通知事件
    /// </summary>
    event Action<MeetingNotification>? OnNotification;

    /// <summary>
    /// 连接成功事件
    /// </summary>
    event Action? OnConnected;

    /// <summary>
    /// 连接断开事件
    /// </summary>
    event Action<Exception?>? OnDisconnected;

    /// <summary>
    /// 正在重连事件 - 参数：第几次重试
    /// </summary>
    event Action<int>? OnReconnecting;

    /// <summary>
    /// 重连成功事件
    /// </summary>
    event Action? OnReconnected;

    /// <summary>
    /// 分块消息重组完成事件
    /// 参数: (type: 消息类型, json: 重组后的完整 JSON)
    /// </summary>
    event Action<string, string>? OnChunkedMessageReceived;

    /// <summary>
    /// 连接到服务器
    /// </summary>
    /// <param name="serverUrl">服务器地址</param>
    /// <param name="accessToken">访问令牌</param>
    Task ConnectAsync(string serverUrl, string accessToken);

    /// <summary>
    /// 断开连接
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// 调用 Hub 方法（带返回数据）
    /// </summary>
    /// <typeparam name="T">返回数据类型</typeparam>
    /// <param name="methodName">方法名</param>
    /// <param name="arg">参数</param>
    /// <returns>响应消息</returns>
    Task<MeetingMessage<T>> InvokeAsync<T>(string methodName, object? arg = null);

    /// <summary>
    /// 调用 Hub 方法（无返回数据）
    /// </summary>
    /// <param name="methodName">方法名</param>
    /// <param name="arg">参数</param>
    /// <returns>响应消息</returns>
    Task<MeetingMessage> InvokeAsync(string methodName, object? arg = null);
}
