using System.Text.Json;

namespace Dorisoy.Meeting.Client.Helpers;

/// <summary>
/// JSON 序列化帮助类
/// </summary>
public static class JsonHelper
{
    /// <summary>
    /// 默认序列化选项
    /// </summary>
    public static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    /// <summary>
    /// 美化输出的序列化选项
    /// </summary>
    public static readonly JsonSerializerOptions PrettyOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    /// <summary>
    /// 序列化对象
    /// </summary>
    public static string Serialize<T>(T obj, bool pretty = false)
    {
        return JsonSerializer.Serialize(obj, pretty ? PrettyOptions : DefaultOptions);
    }

    /// <summary>
    /// 反序列化对象
    /// </summary>
    public static T? Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, DefaultOptions);
    }

    /// <summary>
    /// 尝试反序列化对象
    /// </summary>
    public static bool TryDeserialize<T>(string json, out T? result)
    {
        try
        {
            result = JsonSerializer.Deserialize<T>(json, DefaultOptions);
            return true;
        }
        catch
        {
            result = default;
            return false;
        }
    }
}
