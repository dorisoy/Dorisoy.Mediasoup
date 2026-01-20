using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Dorisoy.Meeting.Client.Services;

/// <summary>
/// 消息分块器 - 用于处理超过 SignalR 消息大小限制的大消息
/// SignalR 默认限制为 32KB，本类将大消息分块传输并在接收端重组
/// </summary>
public class MessageChunker
{
    private readonly ILogger _logger;
    
    // 分块大小：20KB（留出余量给分块元数据）
    public const int ChunkSize = 20 * 1024;
    
    // 消息大小阈值：超过此值则分块（25KB，考虑 JSON 序列化开销）
    public const int MessageSizeThreshold = 25 * 1024;
    
    // 正在重组的消息缓存 - key: messageId, value: 分块列表
    private readonly ConcurrentDictionary<string, ChunkAssembly> _pendingAssemblies = new();
    
    // 分块过期时间（秒）
    private const int ChunkExpirationSeconds = 60;

    public MessageChunker(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 检查消息是否需要分块
    /// </summary>
    public bool NeedsChunking(object message)
    {
        var json = JsonSerializer.Serialize(message);
        var size = Encoding.UTF8.GetByteCount(json);
        return size > MessageSizeThreshold;
    }

    /// <summary>
    /// 获取消息的 JSON 大小（字节）
    /// </summary>
    public int GetMessageSize(object message)
    {
        var json = JsonSerializer.Serialize(message);
        return Encoding.UTF8.GetByteCount(json);
    }

    /// <summary>
    /// 将消息分块
    /// </summary>
    /// <param name="message">原始消息对象</param>
    /// <returns>分块列表</returns>
    public List<MessageChunk> SplitIntoChunks(object message)
    {
        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        var messageId = Guid.NewGuid().ToString("N");
        var totalChunks = (int)Math.Ceiling((double)bytes.Length / ChunkSize);
        
        var chunks = new List<MessageChunk>();
        
        for (int i = 0; i < totalChunks; i++)
        {
            var offset = i * ChunkSize;
            var length = Math.Min(ChunkSize, bytes.Length - offset);
            var chunkData = new byte[length];
            Array.Copy(bytes, offset, chunkData, 0, length);
            
            chunks.Add(new MessageChunk
            {
                MessageId = messageId,
                ChunkIndex = i,
                TotalChunks = totalChunks,
                Data = Convert.ToBase64String(chunkData),
                TotalSize = bytes.Length
            });
        }
        
        _logger.LogDebug("消息分块完成: MessageId={MessageId}, TotalChunks={TotalChunks}, TotalSize={TotalSize}",
            messageId, totalChunks, bytes.Length);
        
        return chunks;
    }

    /// <summary>
    /// 接收分块并尝试重组
    /// </summary>
    /// <param name="chunk">接收到的分块</param>
    /// <returns>如果重组完成返回完整消息 JSON，否则返回 null</returns>
    public string? ReceiveChunk(MessageChunk chunk)
    {
        // 清理过期的分块
        CleanupExpiredAssemblies();
        
        var assembly = _pendingAssemblies.GetOrAdd(chunk.MessageId, _ => new ChunkAssembly
        {
            MessageId = chunk.MessageId,
            TotalChunks = chunk.TotalChunks,
            TotalSize = chunk.TotalSize,
            ReceivedChunks = new ConcurrentDictionary<int, byte[]>(),
            CreatedAt = DateTime.UtcNow
        });
        
        // 存储分块数据
        var chunkData = Convert.FromBase64String(chunk.Data);
        assembly.ReceivedChunks[chunk.ChunkIndex] = chunkData;
        
        _logger.LogDebug("接收分块: MessageId={MessageId}, ChunkIndex={ChunkIndex}/{TotalChunks}",
            chunk.MessageId, chunk.ChunkIndex + 1, chunk.TotalChunks);
        
        // 检查是否所有分块都已接收
        if (assembly.ReceivedChunks.Count == assembly.TotalChunks)
        {
            // 移除并重组
            if (_pendingAssemblies.TryRemove(chunk.MessageId, out _))
            {
                return AssembleMessage(assembly);
            }
        }
        
        return null;
    }

    /// <summary>
    /// 重组消息
    /// </summary>
    private string AssembleMessage(ChunkAssembly assembly)
    {
        var totalBytes = new byte[assembly.TotalSize];
        var offset = 0;
        
        for (int i = 0; i < assembly.TotalChunks; i++)
        {
            if (assembly.ReceivedChunks.TryGetValue(i, out var chunkData))
            {
                Array.Copy(chunkData, 0, totalBytes, offset, chunkData.Length);
                offset += chunkData.Length;
            }
            else
            {
                _logger.LogError("分块重组失败: 缺少分块 {ChunkIndex}", i);
                throw new InvalidOperationException($"Missing chunk {i}");
            }
        }
        
        var json = Encoding.UTF8.GetString(totalBytes);
        _logger.LogDebug("消息重组完成: MessageId={MessageId}, TotalSize={TotalSize}",
            assembly.MessageId, assembly.TotalSize);
        
        return json;
    }

    /// <summary>
    /// 清理过期的分块
    /// </summary>
    private void CleanupExpiredAssemblies()
    {
        var expiredKeys = _pendingAssemblies
            .Where(kvp => (DateTime.UtcNow - kvp.Value.CreatedAt).TotalSeconds > ChunkExpirationSeconds)
            .Select(kvp => kvp.Key)
            .ToList();
        
        foreach (var key in expiredKeys)
        {
            if (_pendingAssemblies.TryRemove(key, out var assembly))
            {
                _logger.LogWarning("分块消息过期被清理: MessageId={MessageId}, ReceivedChunks={Received}/{Total}",
                    key, assembly.ReceivedChunks.Count, assembly.TotalChunks);
            }
        }
    }
}

/// <summary>
/// 消息分块
/// </summary>
public class MessageChunk
{
    /// <summary>
    /// 消息唯一 ID
    /// </summary>
    public string MessageId { get; set; } = string.Empty;
    
    /// <summary>
    /// 分块索引（从 0 开始）
    /// </summary>
    public int ChunkIndex { get; set; }
    
    /// <summary>
    /// 总分块数
    /// </summary>
    public int TotalChunks { get; set; }
    
    /// <summary>
    /// 分块数据（Base64 编码）
    /// </summary>
    public string Data { get; set; } = string.Empty;
    
    /// <summary>
    /// 原始消息总大小（字节）
    /// </summary>
    public int TotalSize { get; set; }
}

/// <summary>
/// 分块重组状态
/// </summary>
internal class ChunkAssembly
{
    public string MessageId { get; set; } = string.Empty;
    public int TotalChunks { get; set; }
    public int TotalSize { get; set; }
    public ConcurrentDictionary<int, byte[]> ReceivedChunks { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}
