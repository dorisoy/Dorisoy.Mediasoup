using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Dorisoy.Meeting.Client.Models
{
    /// <summary>
    /// 协同编辑器内容
    /// </summary>
    public class EditorContent : INotifyPropertyChanged
    {
        private string _sessionId = string.Empty;
        private string _content = string.Empty;
        private string _rtfContent = string.Empty;
        private string _lastEditorId = string.Empty;
        private string _lastEditorName = string.Empty;
        private DateTime _lastUpdateTime = DateTime.Now;
        private int _cursorPosition;
        private int _selectionLength;

        /// <summary>
        /// 编辑会话ID
        /// </summary>
        public string SessionId
        {
            get => _sessionId;
            set { _sessionId = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 纯文本内容
        /// </summary>
        public string Content
        {
            get => _content;
            set { _content = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// RTF 富文本内容
        /// </summary>
        public string RtfContent
        {
            get => _rtfContent;
            set { _rtfContent = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 最后编辑者ID
        /// </summary>
        public string LastEditorId
        {
            get => _lastEditorId;
            set { _lastEditorId = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 最后编辑者名称
        /// </summary>
        public string LastEditorName
        {
            get => _lastEditorName;
            set { _lastEditorName = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdateTime
        {
            get => _lastUpdateTime;
            set { _lastUpdateTime = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 光标位置
        /// </summary>
        public int CursorPosition
        {
            get => _cursorPosition;
            set { _cursorPosition = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 选中长度
        /// </summary>
        public int SelectionLength
        {
            get => _selectionLength;
            set { _selectionLength = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 编辑器用户光标信息（用于显示协作者光标位置）
    /// </summary>
    public class EditorCursor : INotifyPropertyChanged
    {
        private string _peerId = string.Empty;
        private string _peerName = string.Empty;
        private int _position;
        private string _color = "#FF0000";

        /// <summary>
        /// 用户ID
        /// </summary>
        public string PeerId
        {
            get => _peerId;
            set { _peerId = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 用户名称
        /// </summary>
        public string PeerName
        {
            get => _peerName;
            set { _peerName = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 光标位置
        /// </summary>
        public int Position
        {
            get => _position;
            set { _position = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 光标颜色（用于区分不同用户）
        /// </summary>
        public string Color
        {
            get => _color;
            set { _color = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

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

    /// <summary>
    /// 编辑器打开数据（用于通知）
    /// </summary>
    public class EditorOpenedData
    {
        /// <summary>
        /// 会话ID
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// 发起者ID
        /// </summary>
        public string InitiatorId { get; set; } = string.Empty;

        /// <summary>
        /// 发起者名称
        /// </summary>
        public string InitiatorName { get; set; } = string.Empty;
    }
}
