using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Dorisoy.Meeting.Server;

/// <summary>
/// 服务端消息分块处理器 - 用于处理客户端发送的分块消息
/// </summary>
public class ServerMessageChunker
{
    private readonly ILogger<ServerMessageChunker> _logger;
    
    // 正在重组的消息缓存 - key: messageId, value: 分块重组状态
    private readonly ConcurrentDictionary<string, ChunkAssembly> _pendingAssemblies = new();
    
    // 分块过期时间（秒）
    private const int ChunkExpirationSeconds = 60;

    public ServerMessageChunker(ILogger<ServerMessageChunker> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 接收分块并尝试重组
    /// </summary>
    /// <param name="chunk">接收到的分块</param>
    /// <returns>如果重组完成返回完整消息 JSON，否则返回 null</returns>
    public string? ReceiveChunk(MessageChunkDto chunk)
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
        
        // 分块大小（与客户端一致）
        const int chunkSize = 20 * 1024;
        
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
        _logger.LogInformation("服务端消息重组完成: MessageId={MessageId}, TotalSize={TotalSize}",
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
                _logger.LogWarning("服务端分块消息过期被清理: MessageId={MessageId}, ReceivedChunks={Received}/{Total}",
                    key, assembly.ReceivedChunks.Count, assembly.TotalChunks);
            }
        }
    }

    /// <summary>
    /// 分块重组状态
    /// </summary>
    private class ChunkAssembly
    {
        public string MessageId { get; set; } = string.Empty;
        public int TotalChunks { get; set; }
        public int TotalSize { get; set; }
        public ConcurrentDictionary<int, byte[]> ReceivedChunks { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }
}

/// <summary>
/// 消息分块 DTO
/// </summary>
public class MessageChunkDto
{
    public string MessageId { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public int TotalChunks { get; set; }
    public string Data { get; set; } = string.Empty;
    public int TotalSize { get; set; }
}

/// <summary>
/// 接收分块请求
/// </summary>
public class ReceiveChunkRequest
{
    public string MethodName { get; set; } = string.Empty;
    public MessageChunkDto Chunk { get; set; } = new();
}
