using Microsoft.Extensions.Logging;
using NAudio.Wave;
using System.Collections.Concurrent;
using System.IO;

namespace Dorisoy.Meeting.Client.Services;

/// <summary>
/// 系统音效服务 - 使用 NAudio 实现低延迟播放
/// </summary>
public class SoundService : IDisposable
{
    private readonly ILogger<SoundService> _logger;
    private readonly ConcurrentDictionary<string, byte[]> _audioCache = new();
    private readonly string _soundsBasePath;
    private bool _isMuted;
    private bool _disposed;
    private bool _isInitialized;

    /// <summary>
    /// 音效类型
    /// </summary>
    public enum SoundType
    {
        /// <summary>新消息提示音</summary>
        Message,
        /// <summary>用户加入房间</summary>
        Joined,
        /// <summary>用户离开房间</summary>
        Left,
        /// <summary>举手提示音</summary>
        RaiseHand,
        /// <summary>警告提示音</summary>
        Alert,
        /// <summary>通知提示音</summary>
        Notify,
        /// <summary>点击音效</summary>
        Click,
        /// <summary>重连提示音</summary>
        Reconnect
    }

    /// <summary>
    /// 表情音效类型
    /// </summary>
    public enum EmojiSoundType
    {
        /// <summary>鼓掌</summary>
        Applause,
        /// <summary>嘘声</summary>
        Boo,
        /// <summary>祝贺</summary>
        Congrats,
        /// <summary>爱心</summary>
        Heart,
        /// <summary>亲吻</summary>
        Kiss,
        /// <summary>笑声</summary>
        Laughs,
        /// <summary>OK</summary>
        Ok,
        /// <summary>火箭</summary>
        Rocket,
        /// <summary>微笑</summary>
        Smile,
        /// <summary>魔法</summary>
        Tinkerbell,
        /// <summary>长号</summary>
        Trombone,
        /// <summary>哇</summary>
        Woah
    }

    /// <summary>
    /// 是否静音
    /// </summary>
    public bool IsMuted
    {
        get => _isMuted;
        set => _isMuted = value;
    }

    public SoundService(ILogger<SoundService> logger)
    {
        _logger = logger;
        _soundsBasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "sounds");
        
        // 异步预加载常用音频
        Task.Run(PreloadCommonSoundsAsync);
    }

    /// <summary>
    /// 预加载常用音频到内存
    /// </summary>
    private async Task PreloadCommonSoundsAsync()
    {
        try
        {
            // 预加载系统音效
            var systemSounds = new[] { "message.wav", "joined.wav", "left.wav", "raiseHand.wav", "notify.wav" };
            foreach (var sound in systemSounds)
            {
                await PreloadSoundAsync(sound);
            }

            // 预加载表情音效
            var emojiSounds = new[] { "applause.mp3", "boo.mp3", "congrats.mp3", "heart.mp3", "laughs.mp3", 
                                      "ok.mp3", "rocket.mp3", "smile.mp3", "trombone.mp3", "woah.mp3" };
            foreach (var sound in emojiSounds)
            {
                await PreloadSoundAsync($"emoji/{sound}");
            }

            _isInitialized = true;
            _logger.LogInformation("音频预加载完成, 缓存了 {Count} 个文件", _audioCache.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "音频预加载失败");
        }
    }

    /// <summary>
    /// 预加载单个音频文件
    /// </summary>
    private async Task PreloadSoundAsync(string relativePath)
    {
        try
        {
            var fullPath = Path.Combine(_soundsBasePath, relativePath);
            if (File.Exists(fullPath) && !_audioCache.ContainsKey(relativePath))
            {
                var data = await File.ReadAllBytesAsync(fullPath);
                _audioCache.TryAdd(relativePath, data);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "预加载音频失败: {Path}", relativePath);
        }
    }

    /// <summary>
    /// 播放系统音效
    /// </summary>
    public void PlaySound(SoundType soundType)
    {
        if (_isMuted) return;

        var fileName = soundType switch
        {
            SoundType.Message => "message.wav",
            SoundType.Joined => "joined.wav",
            SoundType.Left => "left.wav",
            SoundType.RaiseHand => "raiseHand.wav",
            SoundType.Alert => "alert.wav",
            SoundType.Notify => "notify.wav",
            SoundType.Click => "click.wav",
            SoundType.Reconnect => "reconnect.wav",
            _ => "notify.wav"
        };

        PlaySoundFile($"sounds/{fileName}");
    }

    /// <summary>
    /// 播放表情音效
    /// </summary>
    public void PlayEmojiSound(EmojiSoundType emojiType)
    {
        if (_isMuted) return;

        var fileName = emojiType switch
        {
            EmojiSoundType.Applause => "applause.mp3",
            EmojiSoundType.Boo => "boo.mp3",
            EmojiSoundType.Congrats => "congrats.mp3",
            EmojiSoundType.Heart => "heart.mp3",
            EmojiSoundType.Kiss => "kiss.mp3",
            EmojiSoundType.Laughs => "laughs.mp3",
            EmojiSoundType.Ok => "ok.mp3",
            EmojiSoundType.Rocket => "rocket.mp3",
            EmojiSoundType.Smile => "smile.mp3",
            EmojiSoundType.Tinkerbell => "tinkerbell.mp3",
            EmojiSoundType.Trombone => "trombone.mp3",
            EmojiSoundType.Woah => "woah.mp3",
            _ => "smile.mp3"
        };

        PlaySoundFile($"sounds/emoji/{fileName}");
    }

    /// <summary>
    /// 根据表情字符播放对应音效
    /// </summary>
    public void PlayEmojiSoundByEmoji(string emoji)
    {
        if (_isMuted) return;
        if (string.IsNullOrEmpty(emoji)) return;

        // 根据表情映射到对应的音效
        var emojiType = emoji switch
        {
            // 举手相关
            "✋" or "👋" or "🤚" or "🖐️" or "✌️" => EmojiSoundType.Ok,
            
            // 鼓掌相关
            "👏" or "🙌" => EmojiSoundType.Applause,
            
            // 点赞/OK
            "👍" or "👌" or "🤌" or "🤏" => EmojiSoundType.Ok,
            
            // 点踩/嘘
            "👎" => EmojiSoundType.Boo,
            
            // 爱心相关
            "❤️" or "🧡" or "💛" or "💚" or "💙" or "💜" or "🖤" or "🤍" or
            "💔" or "❣️" or "💕" or "💞" or "💓" or "💗" or "💖" or "💘" or
            "🥰" or "😍" or "🤩" or "😘" or "😗" => EmojiSoundType.Heart,
            
            // 亲吻
            "😚" or "😙" => EmojiSoundType.Kiss,
            
            // 笑相关
            "😀" or "😃" or "😄" or "😁" or "😆" or "😅" or "🤣" or "😂" or
            "🙂" or "😊" or "😇" => EmojiSoundType.Laughs,
            
            // 火箭/庆祝
            "🚀" or "🎉" or "🎊" or "🥳" => EmojiSoundType.Rocket,
            
            // 祝贺/星星
            "🎆" or "🎇" or "✨" or "🌟" or "⭐" or "💫" => EmojiSoundType.Congrats,
            
            // 哇/惊讶
            "😮" or "😯" or "😲" or "🤯" or "😱" => EmojiSoundType.Woah,
            
            // 喇叭（长号）
            "🎺" => EmojiSoundType.Trombone,
            
            // 默认微笑
            _ => EmojiSoundType.Smile
        };

        PlayEmojiSound(emojiType);
    }

    /// <summary>
    /// 播放音频文件 - 使用 NAudio 实现低延迟播放
    /// </summary>
    private void PlaySoundFile(string relativePath)
    {
        if (_disposed) return;

        // 使用线程池异步播放，避免阻塞 UI
        Task.Run(() =>
        {
            try
            {
                byte[]? audioData = null;

                // 优先从缓存获取
                if (_audioCache.TryGetValue(relativePath, out var cached))
                {
                    audioData = cached;
                }
                else
                {
                    // 缓存未命中，从文件加载
                    var fullPath = Path.Combine(_soundsBasePath, relativePath);
                    if (!File.Exists(fullPath))
                    {
                        _logger.LogWarning("音频文件不存在: {Path}", fullPath);
                        return;
                    }
                    audioData = File.ReadAllBytes(fullPath);
                    _audioCache.TryAdd(relativePath, audioData);
                }

                // 使用 NAudio 播放
                using var ms = new MemoryStream(audioData);
                using var reader = relativePath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
                    ? (WaveStream)new Mp3FileReader(ms)
                    : new WaveFileReader(ms);
                using var outputDevice = new WaveOutEvent();
                
                outputDevice.Init(reader);
                outputDevice.Volume = 0.5f; // 50% 音量
                outputDevice.Play();

                // 等待播放完成
                while (outputDevice.PlaybackState == PlaybackState.Playing)
                {
                    Thread.Sleep(50);
                }

                _logger.LogDebug("播放音效完成: {Path}", relativePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "播放音效失败: {Path}", relativePath);
            }
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _audioCache.Clear();
    }
}
