using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media.Imaging;

namespace Dorisoy.Meeting.Client.Models;

/// <summary>
/// 聊天消息类型
/// </summary>
public enum ChatMessageType
{
    /// <summary>
    /// 文本消息
    /// </summary>
    Text,
    
    /// <summary>
    /// 图片消息
    /// </summary>
    Image,
    
    /// <summary>
    /// 文件消息
    /// </summary>
    File,
    
    /// <summary>
    /// 表情消息
    /// </summary>
    Emoji,
    
    /// <summary>
    /// 系统消息
    /// </summary>
    System
}

/// <summary>
/// 聊天消息
/// </summary>
public class ChatMessage : ObservableObject
{
    private string _id = Guid.NewGuid().ToString();
    private string _senderId = string.Empty;
    private string _senderName = string.Empty;
    private string _receiverId = string.Empty; // 为空表示群聊消息
    private string _content = string.Empty;
    private ChatMessageType _messageType = ChatMessageType.Text;
    private DateTime _timestamp = DateTime.Now;
    private bool _isFromSelf;
    private string? _filePath;
    private string? _fileName;
    private long _fileSize;
    private BitmapImage? _imageSource;

    /// <summary>
    /// 消息ID
    /// </summary>
    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    /// <summary>
    /// 发送者ID
    /// </summary>
    public string SenderId
    {
        get => _senderId;
        set => SetProperty(ref _senderId, value);
    }

    /// <summary>
    /// 发送者名称
    /// </summary>
    public string SenderName
    {
        get => _senderName;
        set => SetProperty(ref _senderName, value);
    }

    /// <summary>
    /// 接收者ID（为空表示群聊消息）
    /// </summary>
    public string ReceiverId
    {
        get => _receiverId;
        set => SetProperty(ref _receiverId, value);
    }

    /// <summary>
    /// 消息内容
    /// </summary>
    public string Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    /// <summary>
    /// 消息类型
    /// </summary>
    public ChatMessageType MessageType
    {
        get => _messageType;
        set => SetProperty(ref _messageType, value);
    }

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp
    {
        get => _timestamp;
        set => SetProperty(ref _timestamp, value);
    }

    /// <summary>
    /// 是否来自自己
    /// </summary>
    public bool IsFromSelf
    {
        get => _isFromSelf;
        set => SetProperty(ref _isFromSelf, value);
    }

    /// <summary>
    /// 文件路径（文件/图片消息）
    /// </summary>
    public string? FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }

    /// <summary>
    /// 文件名
    /// </summary>
    public string? FileName
    {
        get => _fileName;
        set => SetProperty(ref _fileName, value);
    }

    /// <summary>
    /// 文件大小
    /// </summary>
    public long FileSize
    {
        get => _fileSize;
        set => SetProperty(ref _fileSize, value);
    }

    /// <summary>
    /// 图片源（图片消息）
    /// </summary>
    public BitmapImage? ImageSource
    {
        get => _imageSource;
        set => SetProperty(ref _imageSource, value);
    }

    private string? _fileData;
    private string? _downloadUrl;
    
    /// <summary>
    /// 文件数据 (Base64 编码)
    /// 用于接收方保存文件
    /// </summary>
    public string? FileData
    {
        get => _fileData;
        set => SetProperty(ref _fileData, value);
    }

    /// <summary>
    /// 文件下载 URL（大文件分片上传后的下载链接）
    /// </summary>
    public string? DownloadUrl
    {
        get => _downloadUrl;
        set => SetProperty(ref _downloadUrl, value);
    }

    /// <summary>
    /// 是否有可下载的文件（有 Base64 数据或下载链接）
    /// </summary>
    public bool HasDownloadableFile => !string.IsNullOrEmpty(FileData) || !string.IsNullOrEmpty(DownloadUrl);

    /// <summary>
    /// 格式化的时间
    /// </summary>
    public string FormattedTime => Timestamp.ToString("HH:mm");

    /// <summary>
    /// 格式化的文件大小
    /// </summary>
    public string FormattedFileSize
    {
        get
        {
            if (FileSize < 1024) return $"{FileSize} B";
            if (FileSize < 1024 * 1024) return $"{FileSize / 1024.0:F1} KB";
            return $"{FileSize / (1024.0 * 1024.0):F1} MB";
        }
    }

    /// <summary>
    /// 是否为私聊消息
    /// </summary>
    public bool IsPrivate => !string.IsNullOrEmpty(ReceiverId);
}

/// <summary>
/// 聊天用户
/// </summary>
public class ChatUser : ObservableObject
{
    private string _peerId = string.Empty;
    private string _displayName = string.Empty;
    private bool _isOnline = true;
    private int _unreadCount;
    private ChatMessage? _lastMessage;
    private bool _isMutedByHost;
    private bool _isHost;

    /// <summary>
    /// 用户ID
    /// </summary>
    public string PeerId
    {
        get => _peerId;
        set => SetProperty(ref _peerId, value);
    }

    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    /// <summary>
    /// 是否在线
    /// </summary>
    public bool IsOnline
    {
        get => _isOnline;
        set => SetProperty(ref _isOnline, value);
    }

    /// <summary>
    /// 是否被主持人静音
    /// </summary>
    public bool IsMutedByHost
    {
        get => _isMutedByHost;
        set => SetProperty(ref _isMutedByHost, value);
    }

    /// <summary>
    /// 是否是主持人（房主）
    /// </summary>
    public bool IsHost
    {
        get => _isHost;
        set => SetProperty(ref _isHost, value);
    }

    /// <summary>
    /// 未读消息数
    /// </summary>
    public int UnreadCount
    {
        get => _unreadCount;
        set
        {
            if (SetProperty(ref _unreadCount, value))
            {
                OnPropertyChanged(nameof(HasUnread));
            }
        }
    }

    /// <summary>
    /// 是否有未读消息
    /// </summary>
    public bool HasUnread => _unreadCount > 0;

    /// <summary>
    /// 最后一条消息
    /// </summary>
    public ChatMessage? LastMessage
    {
        get => _lastMessage;
        set
        {
            if (SetProperty(ref _lastMessage, value))
            {
                OnPropertyChanged(nameof(LastMessagePreview));
            }
        }
    }

    /// <summary>
    /// 最后一条消息预览文本
    /// </summary>
    public string LastMessagePreview
    {
        get
        {
            if (_lastMessage == null) return "";
            return _lastMessage.MessageType switch
            {
                ChatMessageType.Image => "[图片]",
                ChatMessageType.File => $"[文件] {_lastMessage.FileName}",
                ChatMessageType.Emoji => _lastMessage.Content,
                _ => _lastMessage.Content ?? ""
            };
        }
    }
}

/// <summary>
/// 表情反应
/// </summary>
public class EmojiReaction : ObservableObject
{
    private string _id = Guid.NewGuid().ToString();
    private string _senderId = string.Empty;
    private string _senderName = string.Empty;
    private string _emoji = string.Empty;
    private DateTime _timestamp = DateTime.Now;

    /// <summary>
    /// 反应ID
    /// </summary>
    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    /// <summary>
    /// 发送者ID
    /// </summary>
    public string SenderId
    {
        get => _senderId;
        set => SetProperty(ref _senderId, value);
    }

    /// <summary>
    /// 发送者名称
    /// </summary>
    public string SenderName
    {
        get => _senderName;
        set => SetProperty(ref _senderName, value);
    }

    /// <summary>
    /// 表情
    /// </summary>
    public string Emoji
    {
        get => _emoji;
        set => SetProperty(ref _emoji, value);
    }

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp
    {
        get => _timestamp;
        set => SetProperty(ref _timestamp, value);
    }
}

/// <summary>
/// 常用表情列表
/// </summary>
public static class CommonEmojis
{
    /// <summary>
    /// 举手/互动表情
    /// </summary>
    public static readonly string[] HandEmojis =
    [
        "✋", "👋", "👍", "👎", "👏", "🙌", "🤝", "✌️"
    ];

    /// <summary>
    /// 表情符号
    /// </summary>
    public static readonly string[] FaceEmojis =
    [
        "😀", "😃", "😄", "😁", "😆", "😅", "🤣", "😂",
        "🙂", "😊", "😇", "🥰", "😍", "🤩", "😘", "😗",
        "😋", "😛", "😜", "🤪", "😝", "🤑", "🤗", "🤭",
        "🤫", "🤔", "🤐", "🤨", "😐", "😑", "😶", "😏",
        "😒", "🙄", "😬", "🤥", "😌", "😔", "😪", "🤤",
        "😴", "😷", "🤒", "🤕", "🤢", "🤮", "🤧", "🥵",
        "🥶", "😵", "🤯", "🤠", "🥳", "😎", "🤓", "🧐"
    ];

    /// <summary>
    /// 动作/手势
    /// </summary>
    public static readonly string[] GestureEmojis =
    [
        "👍", "👎", "👌", "🤌", "🤏", "✌️", "🤞", "🤟",
        "🤘", "🤙", "👈", "👉", "👆", "👇", "☝️", "👋",
        "🤚", "🖐️", "✋", "🖖", "👏", "🙌", "🤲", "🙏"
    ];

    /// <summary>
    /// 心形
    /// </summary>
    public static readonly string[] HeartEmojis =
    [
        "❤️", "🧡", "💛", "💚", "💙", "💜", "🖤", "🤍",
        "💔", "❣️", "💕", "💞", "💓", "💗", "💖", "💘"
    ];

    /// <summary>
    /// 声音类表情（播放对应音效）
    /// </summary>
    public static readonly string[] SoundEmojis =
    [
        "👍", "👎", "👌", "😀", "😃", "😂", "😘", "❤️",
        "🎺", "🎉", "😮", "👏", "✨", "⭐", "🌟", "💫", "🚀"
    ];

    /// <summary>
    /// 所有常用表情
    /// </summary>
    public static string[] All => [.. HandEmojis, .. FaceEmojis, .. GestureEmojis, .. HeartEmojis, .. SoundEmojis];

    /// <summary>
    /// 聊天表情目录：显示名称用于提示，关键词用于搜索。
    /// </summary>
    public static IReadOnlyList<EmojiOption> Catalog { get; } =
    [
        .. CreateCategory("表情", """
            😀|开心|happy smile
            😃|大笑|laugh happy
            😄|笑脸|smile happy
            😁|露齿笑|grin
            😆|眯眼笑|laugh
            😅|苦笑|sweat
            🤣|笑翻了|rofl
            😂|笑哭|joy laugh
            🙂|微笑|smile
            😊|害羞|blush smile
            😇|天使|angel
            🥰|喜欢|love
            😍|爱慕|love heart eyes
            🤩|崇拜|star eyes
            😘|飞吻|kiss
            😗|亲亲|kiss
            😋|美味|yummy
            😛|吐舌|tongue
            😜|调皮|wink tongue
            🤪|搞怪|crazy
            😝|鬼脸|tongue
            🤑|发财|money
            🤗|拥抱|hug
            🤭|偷笑|giggle
            🤫|嘘|quiet shush
            🤔|思考|think
            🤐|闭嘴|zipper
            🤨|怀疑|doubt
            😐|无语|neutral
            😑|无奈|expressionless
            😶|沉默|silent
            😏|得意|smirk
            😒|不高兴|unamused
            🙄|白眼|eye roll
            😬|尴尬|grimace
            🤥|说谎|lie
            😌|安心|relieved
            😔|难过|sad
            😪|困了|sleepy
            🤤|流口水|drool
            😴|睡觉|sleep
            😷|口罩|mask
            🤒|生病|sick
            🤕|受伤|hurt
            🤢|恶心|nausea
            🤮|呕吐|vomit
            🤧|感冒|sneeze
            🥵|好热|hot
            🥶|好冷|cold
            😵|晕了|dizzy
            🤯|震惊|mind blown
            🤠|牛仔|cowboy
            🥳|庆祝|party
            😎|酷|cool sunglasses
            🤓|学霸|nerd
            🧐|观察|monocle
            😕|困惑|confused
            😟|担心|worried
            🙁|不开心|sad
            😮|惊讶|surprise
            😲|吃惊|astonished
            😳|脸红|flushed
            🥺|可怜|please
            😢|流泪|cry
            😭|大哭|sob
            😤|哼|huff
            😠|生气|angry
            😡|愤怒|rage
            😱|惊恐|scream
            😰|紧张|anxious
            😓|冷汗|sweat
            🤡|小丑|clown
            💀|骷髅|skull
            👻|幽灵|ghost
            🤖|机器人|robot
            """),
        .. CreateCategory("手势", """
            🤝|握手|handshake
            👍|赞|thumbs up like
            👎|踩|thumbs down
            👌|好的|ok
            ✌️|胜利|victory peace
            🤞|好运|luck
            🤟|爱你|love
            🤘|摇滚|rock
            🤙|打电话|call
            👈|向左|left
            👉|向右|right
            👆|向上|up
            👇|向下|down
            ☝️|注意|point
            👋|挥手|wave hello bye
            🤚|停一下|stop
            🖐️|五指|hand
            ✋|举手|raise hand
            🖖|敬礼|salute
            👏|鼓掌|clap applause
            🙌|欢呼|hooray
            🤲|捧手|palms
            🙏|感谢|thanks pray
            💪|加油|strong muscle
            ✍️|写字|write
            """),
        .. CreateCategory("心形", """
            ❤️|红心|red love heart
            🧡|橙心|orange
            💛|黄心|yellow
            💚|绿心|green
            💙|蓝心|blue
            💜|紫心|purple
            🖤|黑心|black
            🤍|白心|white
            💔|心碎|broken
            ❣️|心意|heart
            💕|两颗心|love
            💞|心心相印|love
            💓|心动|beat
            💗|爱心|growing
            💖|闪亮的心|sparkle
            💘|一见钟情|cupid
            """),
        .. CreateCategory("动物", """
            🐶|小狗|dog
            🐱|小猫|cat
            🐭|老鼠|mouse
            🐹|仓鼠|hamster
            🐰|兔子|rabbit
            🦊|狐狸|fox
            🐻|小熊|bear
            🐼|熊猫|panda
            🐨|考拉|koala
            🐯|老虎|tiger
            🦁|狮子|lion
            🐮|奶牛|cow
            🐷|小猪|pig
            🐸|青蛙|frog
            🐵|猴子|monkey
            🐧|企鹅|penguin
            🐥|小鸡|chick
            🦋|蝴蝶|butterfly
            🐬|海豚|dolphin
            🐲|龙|dragon
            """),
        .. CreateCategory("美食", """
            ☕|咖啡|coffee
            🍵|茶|tea
            🍺|啤酒|beer
            🍻|干杯|cheers
            🍎|苹果|apple
            🍊|橘子|orange
            🍉|西瓜|watermelon
            🍓|草莓|strawberry
            🍒|樱桃|cherry
            🍑|桃子|peach
            🍔|汉堡|burger
            🍟|薯条|fries
            🍕|披萨|pizza
            🍜|面条|noodles
            🍚|米饭|rice
            🍰|蛋糕|cake
            🍦|冰淇淋|ice cream
            🍫|巧克力|chocolate
            """),
        .. CreateCategory("庆祝", """
            🎉|礼花|party tada
            🎊|庆贺|confetti
            🎁|礼物|gift
            🎂|生日|birthday
            🎈|气球|balloon
            🎺|喇叭|trumpet
            🎵|音乐|music
            ✨|闪亮|sparkle
            ⭐|星星|star
            🌟|闪耀|glowing star
            💫|光环|dizzy
            🚀|火箭|rocket
            🔥|火热|fire
            💯|满分|hundred
            🏆|奖杯|trophy
            🥇|金牌|medal
            🌹|玫瑰|rose
            🌻|向日葵|sunflower
            🌈|彩虹|rainbow
            ☀️|太阳|sun
            """)
    ];

    private static IEnumerable<EmojiOption> CreateCategory(string category, string entries)
    {
        foreach (var line in entries.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split('|');
            yield return new EmojiOption(parts[0], parts[1], category, parts[2]);
        }
    }
}

/// <summary>
/// 可搜索的表情项，发送时只使用 Unicode 文本。
/// </summary>
public sealed record EmojiOption(string Text, string Name, string Category, string Keywords);
