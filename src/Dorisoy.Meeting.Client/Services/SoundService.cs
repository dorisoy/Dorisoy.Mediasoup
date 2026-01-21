using Microsoft.Extensions.Logging;
using System.IO;
using System.Windows.Media;

namespace Dorisoy.Meeting.Client.Services;

/// <summary>
/// 系统音效服务 - 播放各种提示音
/// </summary>
public class SoundService : IDisposable
{
    private readonly ILogger<SoundService> _logger;
    private readonly Dictionary<string, MediaPlayer> _cachedPlayers = new();
    private readonly object _lock = new();
    private bool _isMuted;
    private bool _disposed;

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
            
            // 祝贺
            "🎆" or "🎇" or "✨" or "🌟" or "⭐" => EmojiSoundType.Congrats,
            
            // 哇/惊讶
            "😮" or "😯" or "😲" or "🤯" or "😱" => EmojiSoundType.Woah,
            
            // 默认微笑
            _ => EmojiSoundType.Smile
        };

        PlayEmojiSound(emojiType);
    }

    /// <summary>
    /// 播放音频文件
    /// </summary>
    private void PlaySoundFile(string relativePath)
    {
        try
        {
            // 获取应用程序目录
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var soundPath = Path.Combine(baseDir, "Resources", relativePath);

            if (!File.Exists(soundPath))
            {
                _logger.LogWarning("音频文件不存在: {Path}", soundPath);
                return;
            }

            // 使用 MediaPlayer 异步播放（支持 wav 和 mp3）
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var player = new MediaPlayer();
                    player.Open(new Uri(soundPath));
                    player.Volume = 0.5; // 50% 音量
                    player.Play();

                    // 播放完成后释放
                    player.MediaEnded += (s, e) =>
                    {
                        player.Close();
                    };

                    _logger.LogDebug("播放音效: {Path}", relativePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "播放音效失败: {Path}", relativePath);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "播放音效失败: {Path}", relativePath);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            foreach (var player in _cachedPlayers.Values)
            {
                player.Close();
            }
            _cachedPlayers.Clear();
        }
    }
}
