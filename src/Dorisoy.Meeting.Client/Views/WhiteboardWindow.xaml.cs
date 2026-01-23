using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Dorisoy.Meeting.Client.Models;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace Dorisoy.Meeting.Client.Views
{
    /// <summary>
    /// 电子白板窗口
    /// </summary>
    public partial class WhiteboardWindow : FluentWindow
    {
        #region 字段

        private readonly string _sessionId;
        private readonly string _hostId;
        private readonly string _hostName;
        private readonly string _currentPeerId;
        private readonly string _currentPeerName;
        private readonly bool _isHost;
        private bool _isForceClosing; // 标记是否是强制关闭（主持人关闭通知）

        // 当前工具和颜色
        private WhiteboardTool _currentTool = WhiteboardTool.Pen;
        private Color _currentColor = Colors.Black;
        private double _strokeWidth = 3.0;

        // 绘制状态
        private bool _isDrawing;
        private Point _startPoint;
        private Point _lastPoint;
        private List<Point> _currentPoints = new();
        private Shape? _previewShape;
        private Polyline? _currentPolyline;

        // 笔触历史（用于撤销和同步）
        private readonly List<WhiteboardStroke> _strokes = new();
        private readonly Dictionary<string, UIElement> _strokeElements = new();

        // 事件
        public event Action<WhiteboardStrokeUpdate>? StrokeUpdated;
        public event Action<CloseWhiteboardRequest>? WhiteboardClosed;

        #endregion

        #region 构造函数

        public WhiteboardWindow(string sessionId, string hostId, string hostName, string currentPeerId, string currentPeerName)
        {
            InitializeComponent();

            _sessionId = sessionId;
            _hostId = hostId;
            _hostName = hostName;
            _currentPeerId = currentPeerId;
            _currentPeerName = currentPeerName;
            _isHost = currentPeerId == hostId;

            TxtHostName.Text = $" - 主持人: {hostName}";
            TxtStatus.Text = _isHost ? "正在演示..." : "观看中...";

            // 设置模式（主持人可操作，观看者只能看）
            SetupMode();

            // 键盘快捷键
            KeyDown += WhiteboardWindow_KeyDown;

            // 窗口关闭控制
            Closing += WhiteboardWindow_Closing;

            Loaded += WhiteboardWindow_Loaded;
        }

        #endregion

        #region 初始化

        private void WhiteboardWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 确保窗口全屏
            WindowState = WindowState.Maximized;
        }

        private void SetupMode()
        {
            if (_isHost)
            {
                // 主持人模式：显示工具栏和操作按钮
                ToolbarPanel.Visibility = Visibility.Visible;
                HostActions.Visibility = Visibility.Visible;
                ViewerHint.Visibility = Visibility.Collapsed;
                DrawingCanvas.Cursor = Cursors.Cross;
            }
            else
            {
                // 观看者模式：隐藏工具栏和操作按钮
                ToolbarPanel.Visibility = Visibility.Collapsed;
                HostActions.Visibility = Visibility.Collapsed;
                ViewerHint.Visibility = Visibility.Visible;
                DrawingCanvas.Cursor = Cursors.Arrow;
                DrawingCanvas.IsHitTestVisible = false; // 禁止交互
            }
        }

        #endregion

        #region 工具选择

        private void Tool_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isHost) return;

            if (sender == BtnPen)
                _currentTool = WhiteboardTool.Pen;
            else if (sender == BtnRectangle)
                _currentTool = WhiteboardTool.Rectangle;
            else if (sender == BtnEllipse)
                _currentTool = WhiteboardTool.Ellipse;
            else if (sender == BtnText)
                _currentTool = WhiteboardTool.Text;
            else if (sender == BtnEraser)
                _currentTool = WhiteboardTool.Eraser;

            // 更新光标
            DrawingCanvas.Cursor = _currentTool == WhiteboardTool.Eraser
                ? Cursors.No
                : (_currentTool == WhiteboardTool.Text ? Cursors.IBeam : Cursors.Cross);
        }

        private void Color_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isHost) return;

            if (sender is System.Windows.Controls.RadioButton rb && rb.Tag is string colorStr)
            {
                _currentColor = (Color)ColorConverter.ConvertFromString(colorStr);
            }
        }

        private void SliderStrokeWidth_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isHost) return;

            _strokeWidth = e.NewValue;
            if (TxtStrokeWidth != null)
            {
                TxtStrokeWidth.Text = ((int)_strokeWidth).ToString();
            }
        }

        #endregion

        #region 绘制逻辑

        private void DrawingCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isHost) return;

            _isDrawing = true;
            _startPoint = e.GetPosition(DrawingCanvas);
            _lastPoint = _startPoint;
            _currentPoints.Clear();
            _currentPoints.Add(_startPoint);

            DrawingCanvas.CaptureMouse();

            switch (_currentTool)
            {
                case WhiteboardTool.Pen:
                    StartPenDrawing();
                    break;
                case WhiteboardTool.Rectangle:
                case WhiteboardTool.Ellipse:
                    StartShapeDrawing();
                    break;
                case WhiteboardTool.Text:
                    ShowTextInput(_startPoint);
                    _isDrawing = false;
                    break;
                case WhiteboardTool.Eraser:
                    EraseAt(_startPoint);
                    break;
            }
        }

        private void DrawingCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isHost || !_isDrawing) return;

            var currentPoint = e.GetPosition(DrawingCanvas);

            switch (_currentTool)
            {
                case WhiteboardTool.Pen:
                    ContinuePenDrawing(currentPoint);
                    break;
                case WhiteboardTool.Rectangle:
                case WhiteboardTool.Ellipse:
                    UpdateShapePreview(currentPoint);
                    break;
                case WhiteboardTool.Eraser:
                    EraseAt(currentPoint);
                    break;
            }

            _lastPoint = currentPoint;
        }

        private void DrawingCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isHost || !_isDrawing) return;

            _isDrawing = false;
            DrawingCanvas.ReleaseMouseCapture();

            var endPoint = e.GetPosition(DrawingCanvas);

            switch (_currentTool)
            {
                case WhiteboardTool.Pen:
                    FinishPenDrawing();
                    break;
                case WhiteboardTool.Rectangle:
                case WhiteboardTool.Ellipse:
                    FinishShapeDrawing(endPoint);
                    break;
            }
        }

        private void DrawingCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_isDrawing && _isHost)
            {
                // 鼠标离开画布时也结束绘制
                DrawingCanvas_MouseLeftButtonUp(sender, new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                {
                    RoutedEvent = MouseLeftButtonUpEvent
                });
            }
        }

        #endregion

        #region 笔触绘制

        private void StartPenDrawing()
        {
            _currentPolyline = new Polyline
            {
                Stroke = new SolidColorBrush(_currentColor),
                StrokeThickness = _strokeWidth,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            _currentPolyline.Points.Add(_startPoint);
            DrawingCanvas.Children.Add(_currentPolyline);
        }

        private void ContinuePenDrawing(Point currentPoint)
        {
            if (_currentPolyline == null) return;

            _currentPolyline.Points.Add(currentPoint);
            _currentPoints.Add(currentPoint);
        }

        private void FinishPenDrawing()
        {
            if (_currentPolyline == null || _currentPoints.Count < 2) return;

            var stroke = new WhiteboardStroke
            {
                Id = Guid.NewGuid().ToString(),
                Tool = WhiteboardTool.Pen,
                Color = _currentColor.ToString(),
                StrokeWidth = _strokeWidth,
                Points = _currentPoints.SelectMany(p => new[] { p.X, p.Y }).ToList(),
                CreatorId = _currentPeerId
            };

            _strokes.Add(stroke);
            _strokeElements[stroke.Id] = _currentPolyline;
            _currentPolyline = null;

            // 发送更新
            NotifyStrokeAdded(stroke);
        }

        #endregion

        #region 形状绘制

        private void StartShapeDrawing()
        {
            if (_currentTool == WhiteboardTool.Rectangle)
            {
                _previewShape = new Rectangle
                {
                    Stroke = new SolidColorBrush(_currentColor),
                    StrokeThickness = _strokeWidth,
                    Fill = Brushes.Transparent
                };
            }
            else if (_currentTool == WhiteboardTool.Ellipse)
            {
                _previewShape = new Ellipse
                {
                    Stroke = new SolidColorBrush(_currentColor),
                    StrokeThickness = _strokeWidth,
                    Fill = Brushes.Transparent
                };
            }

            if (_previewShape != null)
            {
                Canvas.SetLeft(_previewShape, _startPoint.X);
                Canvas.SetTop(_previewShape, _startPoint.Y);
                DrawingCanvas.Children.Add(_previewShape);
            }
        }

        private void UpdateShapePreview(Point currentPoint)
        {
            if (_previewShape == null) return;

            var x = Math.Min(_startPoint.X, currentPoint.X);
            var y = Math.Min(_startPoint.Y, currentPoint.Y);
            var width = Math.Abs(currentPoint.X - _startPoint.X);
            var height = Math.Abs(currentPoint.Y - _startPoint.Y);

            Canvas.SetLeft(_previewShape, x);
            Canvas.SetTop(_previewShape, y);
            _previewShape.Width = width;
            _previewShape.Height = height;
        }

        private void FinishShapeDrawing(Point endPoint)
        {
            if (_previewShape == null) return;

            var stroke = new WhiteboardStroke
            {
                Id = Guid.NewGuid().ToString(),
                Tool = _currentTool,
                Color = _currentColor.ToString(),
                StrokeWidth = _strokeWidth,
                StartX = Math.Min(_startPoint.X, endPoint.X),
                StartY = Math.Min(_startPoint.Y, endPoint.Y),
                EndX = Math.Max(_startPoint.X, endPoint.X),
                EndY = Math.Max(_startPoint.Y, endPoint.Y),
                CreatorId = _currentPeerId
            };

            _strokes.Add(stroke);
            _strokeElements[stroke.Id] = _previewShape;
            _previewShape = null;

            // 发送更新
            NotifyStrokeAdded(stroke);
        }

        #endregion

        #region 文字输入

        private void ShowTextInput(Point position)
        {
            TextInputBox.Text = string.Empty;
            TextInputBox.Foreground = new SolidColorBrush(_currentColor);
            Canvas.SetLeft(TextInputBox, position.X);
            Canvas.SetTop(TextInputBox, position.Y);
            TextInputBox.Visibility = Visibility.Visible;
            TextInputBox.Focus();
        }

        private void TextInputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                FinishTextInput();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                TextInputBox.Visibility = Visibility.Collapsed;
                e.Handled = true;
            }
        }

        private void TextInputBox_LostFocus(object sender, RoutedEventArgs e)
        {
            FinishTextInput();
        }

        private void FinishTextInput()
        {
            var text = TextInputBox.Text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                TextInputBox.Visibility = Visibility.Collapsed;
                return;
            }

            var position = new Point(Canvas.GetLeft(TextInputBox), Canvas.GetTop(TextInputBox));

            var textBlock = new System.Windows.Controls.TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(_currentColor),
                FontSize = 16 + _strokeWidth,
                FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei")
            };

            Canvas.SetLeft(textBlock, position.X);
            Canvas.SetTop(textBlock, position.Y);
            DrawingCanvas.Children.Add(textBlock);

            var stroke = new WhiteboardStroke
            {
                Id = Guid.NewGuid().ToString(),
                Tool = WhiteboardTool.Text,
                Color = _currentColor.ToString(),
                Text = text,
                FontSize = 16 + _strokeWidth,
                StartX = position.X,
                StartY = position.Y,
                CreatorId = _currentPeerId
            };

            _strokes.Add(stroke);
            _strokeElements[stroke.Id] = textBlock;

            TextInputBox.Visibility = Visibility.Collapsed;

            // 发送更新
            NotifyStrokeAdded(stroke);
        }

        #endregion

        #region 橡皮擦

        private void EraseAt(Point point)
        {
            var hitRadius = 10.0;
            var toRemove = new List<string>();

            foreach (var kvp in _strokeElements)
            {
                var element = kvp.Value;
                var bounds = new Rect(Canvas.GetLeft(element), Canvas.GetTop(element),
                    element.RenderSize.Width, element.RenderSize.Height);

                // 扩展检测范围
                bounds.Inflate(hitRadius, hitRadius);

                if (bounds.Contains(point))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var id in toRemove)
            {
                if (_strokeElements.TryGetValue(id, out var element))
                {
                    DrawingCanvas.Children.Remove(element);
                    _strokeElements.Remove(id);
                    _strokes.RemoveAll(s => s.Id == id);
                }
            }

            if (toRemove.Count > 0)
            {
                // 发送删除更新
                NotifyStrokesRemoved(toRemove);
            }
        }

        #endregion

        #region 操作按钮

        private void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            if (!_isHost || _strokes.Count == 0) return;

            var lastStroke = _strokes.Last();
            _strokes.RemoveAt(_strokes.Count - 1);

            if (_strokeElements.TryGetValue(lastStroke.Id, out var element))
            {
                DrawingCanvas.Children.Remove(element);
                _strokeElements.Remove(lastStroke.Id);
            }

            // 发送撤销更新
            NotifyStrokesRemoved(new List<string> { lastStroke.Id });
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            if (!_isHost) return;

            var result = System.Windows.MessageBox.Show(
                "确定要清空所有绘制内容吗？",
                "确认清空",
                System.Windows.MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                ClearCanvas();

                // 发送清空更新
                StrokeUpdated?.Invoke(new WhiteboardStrokeUpdate
                {
                    SessionId = _sessionId,
                    Action = "clear",
                    DrawerId = _currentPeerId,
                    UpdateTime = DateTime.Now
                });
            }
        }

        private void ClearCanvas()
        {
            DrawingCanvas.Children.Clear();
            _strokes.Clear();
            _strokeElements.Clear();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "PNG 图片|*.png|JPEG 图片|*.jpg",
                    DefaultExt = ".png",
                    FileName = $"Whiteboard_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (dialog.ShowDialog() == true)
                {
                    SaveCanvasToImage(dialog.FileName);
                    System.Windows.MessageBox.Show("图片保存成功！", "保存成功",
                        System.Windows.MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"保存失败: {ex.Message}", "错误",
                    System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveCanvasToImage(string filePath)
        {
            // 创建带白色背景的完整画布
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                // 白色背景
                context.DrawRectangle(Brushes.White, null, new Rect(0, 0,
                    DrawingCanvas.ActualWidth, DrawingCanvas.ActualHeight));

                // 绘制所有元素
                var brush = new VisualBrush(DrawingCanvas);
                context.DrawRectangle(brush, null, new Rect(0, 0,
                    DrawingCanvas.ActualWidth, DrawingCanvas.ActualHeight));
            }

            var renderBitmap = new RenderTargetBitmap(
                (int)DrawingCanvas.ActualWidth,
                (int)DrawingCanvas.ActualHeight,
                96, 96, PixelFormats.Pbgra32);

            renderBitmap.Render(visual);

            BitmapEncoder encoder = filePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                ? new JpegBitmapEncoder()
                : new PngBitmapEncoder();

            encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

            using var stream = File.Create(filePath);
            encoder.Save(stream);
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (!_isHost) return;

            var result = System.Windows.MessageBox.Show(
                "确定要取消并关闭白板吗？所有绘制内容将丢失。",
                "确认取消",
                System.Windows.MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                NotifyWhiteboardClosed(false);
                Close();
            }
        }

        private void BtnFinish_Click(object sender, RoutedEventArgs e)
        {
            if (!_isHost) return;

            NotifyWhiteboardClosed(true);
            Close();
        }

        #endregion

        #region 窗口关闭控制

        /// <summary>
        /// 窗口关闭前事件 - 非主持人不能自行关闭
        /// </summary>
        private void WhiteboardWindow_Closing(object? sender, CancelEventArgs e)
        {
            // 如果是强制关闭（主持人通知），直接允许
            if (_isForceClosing)
            {
                return;
            }

            // 非主持人不能自行关闭窗口
            if (!_isHost)
            {
                e.Cancel = true;
                System.Windows.MessageBox.Show(
                    "在主持人结束白板演示前，您不能关闭此窗口。",
                    "提示",
                    System.Windows.MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }
        }

        #endregion

        #region 键盘快捷键

        private void WhiteboardWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (!_isHost) return;

            // Ctrl+Z 撤销
            if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
            {
                BtnUndo_Click(sender, e);
                e.Handled = true;
            }
            // 工具快捷键
            else if (e.Key == Key.P)
            {
                BtnPen.IsChecked = true;
            }
            else if (e.Key == Key.R)
            {
                BtnRectangle.IsChecked = true;
            }
            else if (e.Key == Key.C)
            {
                BtnEllipse.IsChecked = true;
            }
            else if (e.Key == Key.T)
            {
                BtnText.IsChecked = true;
            }
            else if (e.Key == Key.E)
            {
                BtnEraser.IsChecked = true;
            }
            // Escape 关闭
            else if (e.Key == Key.Escape)
            {
                if (_isHost)
                {
                    BtnCancel_Click(sender, e);
                }
            }
        }

        #endregion

        #region 远程同步

        /// <summary>
        /// 应用来自远程的笔触更新
        /// </summary>
        public void ApplyRemoteStroke(WhiteboardStrokeUpdate update)
        {
            if (update == null)
            {
                System.Diagnostics.Debug.WriteLine("[Whiteboard] ApplyRemoteStroke: update is null");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[Whiteboard] ApplyRemoteStroke: Action={update.Action}, DrawerId={update.DrawerId}, CurrentPeerId={_currentPeerId}");

            // 忽略自己的更新 - 使用安全比较
            if (!string.IsNullOrEmpty(update.DrawerId) && 
                !string.IsNullOrEmpty(_currentPeerId) && 
                update.DrawerId == _currentPeerId)
            {
                System.Diagnostics.Debug.WriteLine("[Whiteboard] ApplyRemoteStroke: 忽略自己的更新");
                return;
            }

            Dispatcher.Invoke(() =>
            {
                try
                {
                    switch (update.Action?.ToLower())
                    {
                        case "add":
                            if (update.Stroke != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"[Whiteboard] 添加笔触: Id={update.Stroke.Id}, Tool={update.Stroke.Tool}");
                                AddStrokeToCanvas(update.Stroke);
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("[Whiteboard] ApplyRemoteStroke: Stroke is null for add action");
                            }
                            break;

                        case "remove":
                            if (update.StrokeIds != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"[Whiteboard] 删除笔触: Count={update.StrokeIds.Count}");
                                foreach (var id in update.StrokeIds)
                                {
                                    RemoveStrokeFromCanvas(id);
                                }
                            }
                            break;

                        case "clear":
                            System.Diagnostics.Debug.WriteLine("[Whiteboard] 清空画布");
                            ClearCanvas();
                            break;

                        default:
                            System.Diagnostics.Debug.WriteLine($"[Whiteboard] 未知操作: {update.Action}");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Whiteboard] ApplyRemoteStroke 异常: {ex.Message}");
                }
            });
        }

        private void AddStrokeToCanvas(WhiteboardStroke stroke)
        {
            UIElement? element = null;

            switch (stroke.Tool)
            {
                case WhiteboardTool.Pen:
                    var polyline = new Polyline
                    {
                        Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(stroke.Color)),
                        StrokeThickness = stroke.StrokeWidth,
                        StrokeLineJoin = PenLineJoin.Round,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round
                    };

                    for (int i = 0; i < stroke.Points.Count - 1; i += 2)
                    {
                        polyline.Points.Add(new Point(stroke.Points[i], stroke.Points[i + 1]));
                    }

                    element = polyline;
                    break;

                case WhiteboardTool.Rectangle:
                    var rect = new Rectangle
                    {
                        Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(stroke.Color)),
                        StrokeThickness = stroke.StrokeWidth,
                        Fill = stroke.IsFilled ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(stroke.Color)) : Brushes.Transparent,
                        Width = stroke.EndX - stroke.StartX,
                        Height = stroke.EndY - stroke.StartY
                    };
                    Canvas.SetLeft(rect, stroke.StartX);
                    Canvas.SetTop(rect, stroke.StartY);
                    element = rect;
                    break;

                case WhiteboardTool.Ellipse:
                    var ellipse = new Ellipse
                    {
                        Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(stroke.Color)),
                        StrokeThickness = stroke.StrokeWidth,
                        Fill = stroke.IsFilled ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(stroke.Color)) : Brushes.Transparent,
                        Width = stroke.EndX - stroke.StartX,
                        Height = stroke.EndY - stroke.StartY
                    };
                    Canvas.SetLeft(ellipse, stroke.StartX);
                    Canvas.SetTop(ellipse, stroke.StartY);
                    element = ellipse;
                    break;

                case WhiteboardTool.Text:
                    var textBlock = new System.Windows.Controls.TextBlock
                    {
                        Text = stroke.Text,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(stroke.Color)),
                        FontSize = stroke.FontSize,
                        FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei")
                    };
                    Canvas.SetLeft(textBlock, stroke.StartX);
                    Canvas.SetTop(textBlock, stroke.StartY);
                    element = textBlock;
                    break;
            }

            if (element != null)
            {
                DrawingCanvas.Children.Add(element);
                _strokes.Add(stroke);
                _strokeElements[stroke.Id] = element;
            }
        }

        private void RemoveStrokeFromCanvas(string strokeId)
        {
            if (_strokeElements.TryGetValue(strokeId, out var element))
            {
                DrawingCanvas.Children.Remove(element);
                _strokeElements.Remove(strokeId);
                _strokes.RemoveAll(s => s.Id == strokeId);
            }
        }

        private void NotifyStrokeAdded(WhiteboardStroke stroke)
        {
            StrokeUpdated?.Invoke(new WhiteboardStrokeUpdate
            {
                SessionId = _sessionId,
                Action = "add",
                Stroke = stroke,
                DrawerId = _currentPeerId,
                UpdateTime = DateTime.Now
            });
        }

        private void NotifyStrokesRemoved(List<string> strokeIds)
        {
            StrokeUpdated?.Invoke(new WhiteboardStrokeUpdate
            {
                SessionId = _sessionId,
                Action = "remove",
                StrokeIds = strokeIds,
                DrawerId = _currentPeerId,
                UpdateTime = DateTime.Now
            });
        }

        private void NotifyWhiteboardClosed(bool saveImage)
        {
            string? imageData = null;

            if (saveImage && _strokes.Count > 0)
            {
                try
                {
                    // 生成图片数据
                    var visual = new DrawingVisual();
                    using (var context = visual.RenderOpen())
                    {
                        context.DrawRectangle(Brushes.White, null, new Rect(0, 0,
                            DrawingCanvas.ActualWidth, DrawingCanvas.ActualHeight));
                        var brush = new VisualBrush(DrawingCanvas);
                        context.DrawRectangle(brush, null, new Rect(0, 0,
                            DrawingCanvas.ActualWidth, DrawingCanvas.ActualHeight));
                    }

                    var renderBitmap = new RenderTargetBitmap(
                        (int)DrawingCanvas.ActualWidth,
                        (int)DrawingCanvas.ActualHeight,
                        96, 96, PixelFormats.Pbgra32);
                    renderBitmap.Render(visual);

                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

                    using var stream = new MemoryStream();
                    encoder.Save(stream);
                    imageData = Convert.ToBase64String(stream.ToArray());
                }
                catch
                {
                    // 忽略图片生成错误
                }
            }

            WhiteboardClosed?.Invoke(new CloseWhiteboardRequest
            {
                SessionId = _sessionId,
                CloserId = _currentPeerId,
                CloserName = _currentPeerName,
                SaveImage = saveImage,
                ImageData = imageData
            });
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 同步初始状态（新加入用户使用）
        /// </summary>
        public void SyncState(WhiteboardState state)
        {
            Dispatcher.Invoke(() =>
            {
                ClearCanvas();
                foreach (var stroke in state.Strokes)
                {
                    AddStrokeToCanvas(stroke);
                }
            });
        }

        /// <summary>
        /// 强制关闭窗口（由主持人远程通知触发）
        /// </summary>
        public void ForceClose()
        {
            Dispatcher.Invoke(() =>
            {
                _isForceClosing = true;
                Close();
            });
        }

        #endregion
    }
}
