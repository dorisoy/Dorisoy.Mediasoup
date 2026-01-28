using Microsoft.Extensions.Logging;
using SIPSorcery.Net;
using System.Collections.Concurrent;

namespace Dorisoy.Meeting.Client.WebRtc;

/// <summary>
/// 多 SSRC SRTP 解密器
/// 解决 SIPSorcery 只能为每种媒体类型维护一个解密上下文的限制
/// 为每个 SSRC 创建独立的解密上下文，支持同时解密多个远端用户的视频流
/// </summary>
public class MultiSsrcSrtpDecryptor : IDisposable
{
    private readonly ILogger _logger;
    
    // SRTP 引擎 - 使用从 DTLS 获取的 master key 和 salt
    private SrtpTransformEngine? _srtpEngine;
    
    // Per-SSRC 解密上下文
    private readonly ConcurrentDictionary<uint, SrtpCryptoContext> _ssrcContexts = new();
    
    // 默认上下文 - 用于派生新的 SSRC 上下文
    private SrtpCryptoContext? _defaultContext;
    
    // Master Key 和 Salt（保存用于创建新上下文）
    private byte[]? _masterKey;
    private byte[]? _masterSalt;
    private SrtpPolicy? _srtpPolicy;
    
    // 是否已初始化
    private bool _initialized;
    private bool _disposed;
    
    // 统计
    private long _decryptedCount;
    private long _failedCount;

    public MultiSsrcSrtpDecryptor(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 使用从 DTLS 握手获取的密钥初始化解密器
    /// </summary>
    /// <param name="masterKey">SRTP Master Key (通常 16 bytes for AES-128)</param>
    /// <param name="masterSalt">SRTP Master Salt (通常 14 bytes)</param>
    /// <param name="srtpPolicy">SRTP 策略（加密算法、认证算法等）</param>
    public void Initialize(byte[] masterKey, byte[] masterSalt, SrtpPolicy srtpPolicy)
    {
        if (_initialized)
        {
            _logger.LogWarning("MultiSsrcSrtpDecryptor already initialized");
            return;
        }

        _masterKey = masterKey;
        _masterSalt = masterSalt;
        _srtpPolicy = srtpPolicy;

        // 创建 SRTP 引擎
        _srtpEngine = new SrtpTransformEngine(masterKey, masterSalt, srtpPolicy, srtpPolicy);
        
        // 获取默认上下文（用于派生新的 SSRC 上下文）
        _defaultContext = _srtpEngine.GetDefaultContext();

        _initialized = true;
        _logger.LogInformation("MultiSsrcSrtpDecryptor initialized with master key ({KeyLen} bytes) and salt ({SaltLen} bytes)",
            masterKey.Length, masterSalt.Length);
    }

    /// <summary>
    /// 解密 SRTP 包
    /// </summary>
    /// <param name="srtpData">加密的 SRTP 数据</param>
    /// <returns>解密后的 RTP 包，失败返回 null</returns>
    public RTPPacket? DecryptSrtpPacket(byte[] srtpData)
    {
        if (!_initialized || _defaultContext == null || _srtpPolicy == null)
        {
            return null;
        }

        try
        {
            // 解析 SSRC（位于 RTP 头的第 8-11 字节）
            if (srtpData.Length < 12)
            {
                return null; // 太短，不是有效的 RTP 包
            }

            uint ssrc = (uint)((srtpData[8] << 24) | (srtpData[9] << 16) | (srtpData[10] << 8) | srtpData[11]);

            // 获取或创建该 SSRC 的解密上下文
            var context = GetOrCreateContext(ssrc);
            if (context == null)
            {
                _failedCount++;
                return null;
            }

            // 创建 RawPacket 用于解密
            var rawPacket = new RawPacket(srtpData, 0, srtpData.Length);

            // 使用该 SSRC 的上下文解密
            bool success = context.ReverseTransformPacket(rawPacket);
            if (!success)
            {
                _failedCount++;
                if (_failedCount % 100 == 1)
                {
                    _logger.LogWarning("SRTP decrypt failed for SSRC={Ssrc:X8}, failed count={Count}", ssrc, _failedCount);
                }
                return null;
            }

            _decryptedCount++;

            // 解密成功，构造 RTPPacket
            // 注意：ReverseTransformPacket 会修改 rawPacket 的内容和长度（移除 auth tag）
            var decryptedData = rawPacket.GetData();
            if (decryptedData == null || decryptedData.Length < 12)
            {
                return null;
            }

            // 解析 RTP 包
            var rtpPacket = new RTPPacket(decryptedData);
            
            if (_decryptedCount % 100 == 1)
            {
                _logger.LogDebug("MultiSsrc decrypt stats: total={Total}, failed={Failed}, SSRC contexts={Contexts}",
                    _decryptedCount, _failedCount, _ssrcContexts.Count);
            }

            return rtpPacket;
        }
        catch (Exception ex)
        {
            _failedCount++;
            if (_failedCount % 100 == 1)
            {
                _logger.LogError(ex, "SRTP decrypt exception, failed count={Count}", _failedCount);
            }
            return null;
        }
    }

    /// <summary>
    /// 获取或创建指定 SSRC 的解密上下文
    /// </summary>
    private SrtpCryptoContext? GetOrCreateContext(uint ssrc)
    {
        // 先尝试从缓存获取
        if (_ssrcContexts.TryGetValue(ssrc, out var existingContext))
        {
            return existingContext;
        }

        // 创建新的上下文
        if (_defaultContext == null || _masterKey == null || _masterSalt == null || _srtpPolicy == null)
        {
            return null;
        }

        try
        {
            // 使用默认上下文派生新的 SSRC 上下文
            // deriveContext(ssrc, roc=0, deriveRate=0) - 初始 ROC 为 0
            var newContext = _defaultContext.deriveContext(ssrc, 0, 0);
            
            if (newContext != null)
            {
                // 派生 SRTP 会话密钥
                newContext.DeriveSrtpKeys(0);
                
                // 添加到缓存
                if (_ssrcContexts.TryAdd(ssrc, newContext))
                {
                    _logger.LogInformation("Created new SRTP context for SSRC={Ssrc:X8}, total contexts={Count}",
                        ssrc, _ssrcContexts.Count);
                }
                else
                {
                    // 另一个线程已经创建了，使用已有的
                    newContext.Close();
                    _ssrcContexts.TryGetValue(ssrc, out newContext);
                }
            }

            return newContext;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create SRTP context for SSRC={Ssrc:X8}", ssrc);
            return null;
        }
    }

    /// <summary>
    /// 移除指定 SSRC 的解密上下文
    /// </summary>
    public void RemoveContext(uint ssrc)
    {
        if (_ssrcContexts.TryRemove(ssrc, out var context))
        {
            context.Close();
            _logger.LogInformation("Removed SRTP context for SSRC={Ssrc:X8}", ssrc);
        }
    }

    /// <summary>
    /// 清除所有 SSRC 上下文
    /// </summary>
    public void ClearContexts()
    {
        foreach (var kvp in _ssrcContexts)
        {
            kvp.Value.Close();
        }
        _ssrcContexts.Clear();
        _logger.LogInformation("Cleared all SRTP contexts");
    }

    /// <summary>
    /// 获取解密统计
    /// </summary>
    public (long decrypted, long failed, int contexts) GetStats()
    {
        return (_decryptedCount, _failedCount, _ssrcContexts.Count);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        ClearContexts();
        
        _defaultContext?.Close();
        _defaultContext = null;
        
        _srtpEngine?.Close();
        _srtpEngine = null;

        _logger.LogInformation("MultiSsrcSrtpDecryptor disposed");
    }
}
