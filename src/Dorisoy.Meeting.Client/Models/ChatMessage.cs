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
}
