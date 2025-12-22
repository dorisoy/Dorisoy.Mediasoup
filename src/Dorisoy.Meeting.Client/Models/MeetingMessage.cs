using System.Text.Json.Serialization;

namespace Dorisoy.Meeting.Client.Models;

/// <summary>
/// 服务器响应消息基类
/// </summary>
public class MeetingMessage
{
    /// <summary>
    /// 状态码，200 表示成功
    /// </summary>
    public int Code { get; set; } = 200;

    /// <summary>
    /// 内部错误码
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InternalCode { get; set; }

    /// <summary>
    /// 响应消息
    /// </summary>
    public string Message { get; set; } = "Success";

    /// <summary>
    /// 是否成功
    /// </summary>
    [JsonIgnore]
    public bool IsSuccess => Code == 200;

    /// <summary>
    /// 创建失败消息
    /// </summary>
    public static MeetingMessage Failure(string? message = null)
    {
        return new MeetingMessage { Code = 400, Message = message ?? "Failure" };
    }

    /// <summary>
    /// 创建成功消息
    /// </summary>
    public static MeetingMessage Success(string? message = null)
    {
        return new MeetingMessage { Code = 200, Message = message ?? "Success" };
    }
}

/// <summary>
/// 带数据的服务器响应消息
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public class MeetingMessage<T> : MeetingMessage
{
    /// <summary>
    /// 响应数据
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public T? Data { get; set; }

    /// <summary>
    /// 创建成功消息
    /// </summary>
    public static MeetingMessage<T> Success(T data, string? message = null)
    {
        return new MeetingMessage<T>
        {
            Code = 200,
            Message = message ?? "Success",
            Data = data,
        };
    }

    /// <summary>
    /// 创建失败消息
    /// </summary>
    public new static MeetingMessage<T> Failure(string? message = null)
    {
        return new MeetingMessage<T> { Code = 400, Message = message ?? "Failure" };
    }
}
