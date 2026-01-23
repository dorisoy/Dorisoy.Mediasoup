using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Dorisoy.Meeting.Client.Models
{
    /// <summary>
    /// 白板绘制工具类型
    /// 注意：全局使用 JsonStringEnumMemberConverter，无需单独标记
    /// </summary>
    public enum WhiteboardTool
    {
        /// <summary>
        /// 笔触/自由绘制
        /// </summary>
        Pen,

        /// <summary>
        /// 矩形
        /// </summary>
        Rectangle,

        /// <summary>
        /// 圆形/椭圆
        /// </summary>
        Ellipse,

        /// <summary>
        /// 文字
        /// </summary>
        Text,

        /// <summary>
        /// 橡皮擦
        /// </summary>
        Eraser,

        /// <summary>
        /// 选择工具
        /// </summary>
        Select
    }

    /// <summary>
    /// 白板笔触数据 - 用于网络传输
    /// </summary>
    public class WhiteboardStroke
    {
        /// <summary>
        /// 笔触唯一ID
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 工具类型
        /// </summary>
        public WhiteboardTool Tool { get; set; } = WhiteboardTool.Pen;

        /// <summary>
        /// 笔触颜色 (ARGB)
        /// </summary>
        public string Color { get; set; } = "#FF000000";

        /// <summary>
        /// 笔触宽度
        /// </summary>
        public double StrokeWidth { get; set; } = 2.0;

        /// <summary>
        /// 点序列 (x1,y1,x2,y2,...)
        /// </summary>
        public List<double> Points { get; set; } = new();

        /// <summary>
        /// 起始X坐标（用于矩形、椭圆）
        /// </summary>
        public double StartX { get; set; }

        /// <summary>
        /// 起始Y坐标（用于矩形、椭圆）
        /// </summary>
        public double StartY { get; set; }

        /// <summary>
        /// 结束X坐标（用于矩形、椭圆）
        /// </summary>
        public double EndX { get; set; }

        /// <summary>
        /// 结束Y坐标（用于矩形、椭圆）
        /// </summary>
        public double EndY { get; set; }

        /// <summary>
        /// 文字内容（用于文字工具）
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// 字体大小（用于文字工具）
        /// </summary>
        public double FontSize { get; set; } = 16.0;

        /// <summary>
        /// 是否填充（用于矩形、椭圆）
        /// </summary>
        public bool IsFilled { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 创建者ID
        /// </summary>
        public string CreatorId { get; set; } = string.Empty;
    }

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
    /// 白板打开数据（用于通知）
    /// </summary>
    public class WhiteboardOpenedData
    {
        /// <summary>
        /// 会话ID
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// 主持人ID
        /// </summary>
        public string HostId { get; set; } = string.Empty;

        /// <summary>
        /// 主持人名称
        /// </summary>
        public string HostName { get; set; } = string.Empty;

        /// <summary>
        /// 画布宽度
        /// </summary>
        public double CanvasWidth { get; set; }

        /// <summary>
        /// 画布高度
        /// </summary>
        public double CanvasHeight { get; set; }
    }

    /// <summary>
    /// 白板笔触更新（增量）
    /// </summary>
    public class WhiteboardStrokeUpdate
    {
        /// <summary>
        /// 会话ID
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// 操作类型：add, remove, clear
        /// </summary>
        public string Action { get; set; } = "add";

        /// <summary>
        /// 笔触数据（add 时使用）
        /// </summary>
        public WhiteboardStroke? Stroke { get; set; }

        /// <summary>
        /// 要删除的笔触ID列表（remove 时使用）
        /// </summary>
        public List<string>? StrokeIds { get; set; }

        /// <summary>
        /// 绘制者ID
        /// </summary>
        public string DrawerId { get; set; } = string.Empty;

        /// <summary>
        /// 绘制者名称
        /// </summary>
        public string DrawerName { get; set; } = string.Empty;

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdateTime { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 撤销请求
    /// </summary>
    public class WhiteboardUndoRequest
    {
        /// <summary>
        /// 会话ID
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// 操作者ID
        /// </summary>
        public string OperatorId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 清空白板请求
    /// </summary>
    public class WhiteboardClearRequest
    {
        /// <summary>
        /// 会话ID
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// 操作者ID
        /// </summary>
        public string OperatorId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 关闭白板请求
    /// </summary>
    public class CloseWhiteboardRequest
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

        /// <summary>
        /// 是否保存图片
        /// </summary>
        public bool SaveImage { get; set; }

        /// <summary>
        /// 图片数据（Base64，如果 SaveImage 为 true）
        /// </summary>
        public string? ImageData { get; set; }
    }

    /// <summary>
    /// 白板状态 - 用于新加入用户同步
    /// </summary>
    public class WhiteboardState
    {
        /// <summary>
        /// 会话ID
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// 主持人ID
        /// </summary>
        public string HostId { get; set; } = string.Empty;

        /// <summary>
        /// 所有笔触数据
        /// </summary>
        public List<WhiteboardStroke> Strokes { get; set; } = new();

        /// <summary>
        /// 画布宽度
        /// </summary>
        public double CanvasWidth { get; set; }

        /// <summary>
        /// 画布高度
        /// </summary>
        public double CanvasHeight { get; set; }
    }

    /// <summary>
    /// 白板关闭数据
    /// </summary>
    public class WhiteboardClosedData
    {
        /// <summary>
        /// 会话ID
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// 是否已保存图片
        /// </summary>
        public bool ImageSaved { get; set; }
    }
}
