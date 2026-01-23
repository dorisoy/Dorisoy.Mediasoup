using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using FBS.RtpParameters;

namespace Dorisoy.Meeting.Server
{
    #region 业务类通知

    /// <summary>
    /// 用户进入房间，通知其他用户
    /// </summary>
    public class PeerJoinRoomNotification
    {
        public Peer Peer { get; set; }
    }

    /// <summary>
    /// 用户离开房间，通知其他用户
    /// </summary>
    public class PeerLeaveRoomNotification
    {
        /// <summary>
        /// PeerId
        /// </summary>
        public string PeerId { get; set; }
    }

    /// <summary>
    /// 产生新消费者，通知客户端进行消费。客户端可根据该通知的信息创建本地消费者。
    /// </summary>
    public class NewConsumerNotification
    {
        /// <summary>
        /// 生产者的 PeerId
        /// </summary>
        public string ProducerPeerId { get; set; }

        /// <summary>
        /// 媒体类型，音频或视频
        /// </summary>
        public MediaKind Kind { get; set; }

        /// <summary>
        /// 生产者 Id
        /// </summary>
        public string ProducerId { get; set; }

        /// <summary>
        /// 消费者 Id
        /// </summary>
        public string ConsumerId { get; set; }

        /// <summary>
        /// Rtp 参数
        /// </summary>
        public Mediasoup.RtpParameters RtpParameters { get; set; }

        /// <summary>
        /// 消费者类型，如 SVC, Simulcast 等。
        /// </summary>
        public FBS.RtpParameters.Type Type { get; set; }

        /// <summary>
        /// 生产者的 AppData
        /// </summary>
        public Dictionary<string, object>? ProducerAppData { get; set; }

        /// <summary>
        /// 生产者是否暂停中
        /// </summary>
        public bool ProducerPaused { get; set; }
    }

    /// <summary>
    /// Pull 或 Invite 模式下，请求用户生产源。
    /// </summary>
    public class ProduceSourcesNotification
    {
        public HashSet<string> Sources { get; set; }
    }

    /// <summary>
    /// Invite 模式下，管理员请出发言后，用户收到的通知。
    /// </summary>
    public class CloseSourcesNotification
    {
        public HashSet<string> Sources { get; set; }
    }

    /// <summary>
    /// Invite 模式下，用户向管理员申请生产，管理员收到的通知。
    /// </summary>
    public class RequestProduceNotification
    {
        /// <summary>
        /// 申请者 PeerId
        /// </summary>
        public string PeerId { get; set; }

        /// <summary>
        /// 申请生产源
        /// </summary>
        public HashSet<string> Sources { get; set; }
    }

    /// <summary>
    /// 用户 AppData 改变后，通知其他用户。
    /// </summary>
    public class PeerAppDataChangedNotification
    {
        /// <summary>
        /// PeerId
        /// </summary>
        public string PeerId { get; set; }

        /// <summary>
        /// AppData
        /// </summary>
        public Dictionary<string, object> AppData { get; set; }
    }

    /// <summary>
    /// 文本消息通知。
    /// </summary>
    public class NewMessageNotification
    {
        /// <summary>
        /// 消息
        /// </summary>
        public string Message { get; set; }
    }

    /// <summary>
    /// 广播消息通知 - 支持即时聊天
    /// </summary>
    public class BroadcastMessageNotification
    {
        /// <summary>
        /// 消息类型（chatMessage, emojiReaction 等）
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 发送者 PeerId
        /// </summary>
        public string SenderId { get; set; }

        /// <summary>
        /// 发送者显示名
        /// </summary>
        public string SenderName { get; set; }

        /// <summary>
        /// 消息数据
        /// </summary>
        public object Data { get; set; }
    }

    /// <summary>
    /// 用户被踢出房间通知
    /// </summary>
    public class PeerKickedNotification
    {
        /// <summary>
        /// 被踢出的用户 PeerId
        /// </summary>
        public string PeerId { get; set; }

        /// <summary>
        /// 被踢出的用户名称
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 踢人的主持人 PeerId
        /// </summary>
        public string HostPeerId { get; set; }
    }

    /// <summary>
    /// 用户被远程静音通知
    /// </summary>
    public class PeerMutedNotification
    {
        /// <summary>
        /// 被静音的用户 PeerId
        /// </summary>
        public string PeerId { get; set; }

        /// <summary>
        /// 被静音的用户名称
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 是否静音
        /// </summary>
        public bool IsMuted { get; set; }

        /// <summary>
        /// 操作的主持人 PeerId
        /// </summary>
        public string HostPeerId { get; set; }
    }

    /// <summary>
    /// 房间解散通知（主持人离开时）
    /// </summary>
    public class RoomDismissedNotification
    {
        /// <summary>
        /// 房间 ID
        /// </summary>
        public string RoomId { get; set; }

        /// <summary>
        /// 主持人 PeerId
        /// </summary>
        public string HostPeerId { get; set; }

        /// <summary>
        /// 解散原因
        /// </summary>
        public string Reason { get; set; }
    }

    #endregion

    #region Consumer

    /// <summary>
    /// 消费者通知
    /// </summary>
    public class ConsumerNotificationBase
    {
        /// <summary>
        /// 消费者 Id
        /// </summary>
        public string ProducerPeerId { get; set; }

        /// <summary>
        /// 媒体类型
        /// </summary>
        public MediaKind Kind { get; set; }

        /// <summary>
        /// 消费者 Id
        /// </summary>
        public string ConsumerId { get; set; }
    }

    /// <summary>
    /// 消费者分值通知
    /// </summary>
    public class ConsumerScoreNotification : ConsumerNotificationBase
    {
        /// <summary>
        /// 分值
        /// </summary>
        public object? Score { get; set; } // 透传
    }

    /// <summary>
    /// 消费者关闭通知
    /// </summary>
    public class ConsumerClosedNotification : ConsumerNotificationBase { }

    /// <summary>
    /// 消费者暂停通知
    /// </summary>
    public class ConsumerPausedNotification : ConsumerNotificationBase { }

    /// <summary>
    /// 消费者恢复通知
    /// </summary>
    public class ConsumerResumedNotification : ConsumerNotificationBase { }

    /// <summary>
    /// 消费者 Layers 改变通知
    /// </summary>
    public class ConsumerLayersChangedNotification : ConsumerNotificationBase
    {
        /// <summary>
        /// Layers
        /// </summary>
        public object? Layers { get; set; } // 透传
    }

    #endregion

    #region Producer

    /// <summary>
    /// 生产者通知
    /// </summary>
    public class ProducerNotificationBase
    {
        /// <summary>
        /// 生产者 Id
        /// </summary>
        public string ProducerId { get; set; }
    }

    /// <summary>
    /// 生产者分值通知
    /// </summary>
    public class ProducerScoreNotification : ProducerNotificationBase
    {
        /// <summary>
        /// 生产者分值
        /// </summary>
        public object? Score { get; set; } // 透传
    }

    /// <summary>
    /// 生产者视频方向改变通知
    /// </summary>
    public class ProducerVideoOrientationChangedNotification : ProducerNotificationBase
    {
        /// <summary>
        /// 视频方向
        /// </summary>
        public object? VideoOrientation { get; set; } // 透传
    }

    /// <summary>
    /// 生产者关闭通知
    /// </summary>
    public class ProducerClosedNotification : ProducerNotificationBase { }

    #endregion

    #region Vote

    /// <summary>
    /// 创建投票请求
    /// </summary>
    public class CreateVoteRequest
    {
        /// <summary>
        /// 投票ID
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 投票问题
        /// </summary>
        public string Question { get; set; } = string.Empty;

        /// <summary>
        /// 选项列表
        /// </summary>
        public List<string> Options { get; set; } = new();

        /// <summary>
        /// 创建者ID
        /// </summary>
        public string CreatorId { get; set; } = string.Empty;

        /// <summary>
        /// 创建者名称
        /// </summary>
        public string CreatorName { get; set; } = string.Empty;

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedTime { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 提交投票请求
    /// </summary>
    public class SubmitVoteRequest
    {
        /// <summary>
        /// 投票ID
        /// </summary>
        public string VoteId { get; set; } = string.Empty;

        /// <summary>
        /// 选中的选项索引
        /// </summary>
        public int OptionIndex { get; set; }

        /// <summary>
        /// 投票人ID
        /// </summary>
        public string VoterId { get; set; } = string.Empty;

        /// <summary>
        /// 投票人名称
        /// </summary>
        public string VoterName { get; set; } = string.Empty;
    }

    #endregion

    #region 编辑器相关

    /// <summary>
    /// 打开编辑器请求
    /// </summary>
    public class OpenEditorRequest
    {
        /// <summary>
        /// 会话ID
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// 主持人 ID（用于权限控制）
        /// </summary>
        public string HostId { get; set; } = string.Empty;

        /// <summary>
        /// 发起者ID
        /// </summary>
        public string InitiatorId { get; set; } = string.Empty;

        /// <summary>
        /// 发起者名称
        /// </summary>
        public string InitiatorName { get; set; } = string.Empty;
    }

    /// <summary>
    /// 编辑器内容更新
    /// </summary>
    public class EditorContentUpdate
    {
        /// <summary>
        /// 会话ID
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// 纯文本内容
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// RTF 富文本内容
        /// </summary>
        public string RtfContent { get; set; } = string.Empty;

        /// <summary>
        /// 编辑者ID
        /// </summary>
        public string EditorId { get; set; } = string.Empty;

        /// <summary>
        /// 编辑者名称
        /// </summary>
        public string EditorName { get; set; } = string.Empty;

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdateTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 光标位置
        /// </summary>
        public int CursorPosition { get; set; }
    }

    /// <summary>
    /// 关闭编辑器请求
    /// </summary>
    public class CloseEditorRequest
    {
        /// <summary>
        /// 会话ID
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// 关闭者ID
        /// </summary>
        public string CloserId { get; set; } = string.Empty;

        /// <summary>
        /// 关闭者名称
        /// </summary>
        public string CloserName { get; set; } = string.Empty;
    }

    #endregion

    #region 白板相关

    /// <summary>
    /// 打开白板请求
    /// </summary>
    public class OpenWhiteboardRequest
    {
        /// <summary>
        /// 会话ID
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// 发起者（主持人）ID
        /// </summary>
        public string HostId { get; set; } = string.Empty;

        /// <summary>
        /// 发起者（主持人）名称
        /// </summary>
        public string HostName { get; set; } = string.Empty;

        /// <summary>
        /// 画布宽度
        /// </summary>
        public double CanvasWidth { get; set; } = 1920;

        /// <summary>
        /// 画布高度
        /// </summary>
        public double CanvasHeight { get; set; } = 1080;
    }

    /// <summary>
    /// 白板绘制工具类型
    /// 注意：全局使用 JsonStringEnumMemberConverter，无需单独标记
    /// </summary>
    public enum WhiteboardTool
    {
        Pen,
        Rectangle,
        Ellipse,
        Text,
        Eraser,
        Select
    }

    /// <summary>
    /// 白板笔触数据
    /// </summary>
    public class WhiteboardStroke
    {
        public string Id { get; set; } = string.Empty;
        public WhiteboardTool Tool { get; set; } = WhiteboardTool.Pen;
        public string Color { get; set; } = "#FF000000";
        public double StrokeWidth { get; set; } = 2.0;
        public List<double> Points { get; set; } = new();
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double EndX { get; set; }
        public double EndY { get; set; }
        public string Text { get; set; } = string.Empty;
        public double FontSize { get; set; } = 16.0;
        public bool IsFilled { get; set; }
        public DateTime CreatedTime { get; set; } = DateTime.Now;
        public string CreatorId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 白板笔触更新
    /// </summary>
    public class WhiteboardStrokeUpdate
    {
        public string SessionId { get; set; } = string.Empty;
        public string Action { get; set; } = "add";
        public WhiteboardStroke? Stroke { get; set; }
        public List<string>? StrokeIds { get; set; }
        public string DrawerId { get; set; } = string.Empty;
        public string DrawerName { get; set; } = string.Empty;
        public DateTime UpdateTime { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 关闭白板请求
    /// </summary>
    public class CloseWhiteboardRequest
    {
        public string SessionId { get; set; } = string.Empty;
        public string CloserId { get; set; } = string.Empty;
        public string CloserName { get; set; } = string.Empty;
        public bool SaveImage { get; set; }
        public string? ImageData { get; set; }
    }

    #endregion
}
