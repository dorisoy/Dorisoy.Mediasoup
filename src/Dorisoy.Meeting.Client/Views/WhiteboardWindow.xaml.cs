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
        private bool _isPendingCloseConfirm; // 标记是否正在等待关闭确认

        // 当前工具和颜色
        private WhiteboardTool _currentTool = WhiteboardTool.Select;
        private Color _currentColor = Colors.Black;
        private double _strokeWidth = 3.0;

        // 绘制状态
        private bool _isDrawing;
        private bool _isTextInputActive; // 标记是否正在输入文字
        private bool _isDragging; // 标记是否正在拖拽图形
        private Point _startPoint;
        private Point _lastPoint;
        private Point _dragStartPoint; // 拖拽起始点
        private UIElement? _selectedElement; // 当前选中的元素
        private string? _selectedStrokeId; // 当前选中的笔触ID
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
            // 判断是否为主持人：currentPeerId == hostId，且两者都不为空
            _isHost = !string.IsNullOrEmpty(currentPeerId) && !string.IsNullOrEmpty(hostId) && currentPeerId == hostId;
            
            System.Diagnostics.Debug.WriteLine($"[Whiteboard] currentPeerId={currentPeerId}, hostId={hostId}, _isHost={_isHost}");

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
                DrawingCanvas.Cursor = Cursors.Arrow; // 默认选择工具
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

            // 如果正在输入文字，先取消输入
            if (_isTextInputActive)
            {
                CancelTextInput();
            }
            
            // 清除选中状态
            ClearSelection();

            if (sender == BtnSelect)
                _currentTool = WhiteboardTool.Select;
            else if (sender == BtnPen)
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
            UpdateCursor();
        }
        
        /// <summary>
        /// 更新光标样式
        /// </summary>
        private void UpdateCursor()
        {
            DrawingCanvas.Cursor = _currentTool switch
            {
                WhiteboardTool.Select => Cursors.Arrow,
                WhiteboardTool.Eraser => Cursors.No,
                WhiteboardTool.Text => Cursors.IBeam,
                _ => Cursors.Cross
            };
        }
        
        /// <summary>
        /// 切换到选择工具
        /// </summary>
        private void SwitchToSelectTool()
        {
            _currentTool = WhiteboardTool.Select;
            BtnSelect.IsChecked = true;
            UpdateCursor();
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

            _startPoint = e.GetPosition(DrawingCanvas);
            _lastPoint = _startPoint;
            _currentPoints.Clear();
            _currentPoints.Add(_startPoint);

            switch (_currentTool)
            {
                case WhiteboardTool.Select:
                    // 尝试选中图形
                    TrySelectElement(_startPoint);
                    if (_selectedElement != null)
                    {
                        _isDragging = true;
                        _dragStartPoint = _startPoint;
                        DrawingCanvas.CaptureMouse();
                    }
                    break;
                case WhiteboardTool.Pen:
                    _isDrawing = true;
                    DrawingCanvas.CaptureMouse();
                    StartPenDrawing();
                    break;
                case WhiteboardTool.Rectangle:
                case WhiteboardTool.Ellipse:
                    _isDrawing = true;
                    DrawingCanvas.CaptureMouse();
                    StartShapeDrawing();
                    break;
                case WhiteboardTool.Text:
                    ShowTextInput(_startPoint);
                    break;
                case WhiteboardTool.Eraser:
                    _isDrawing = true;
                    DrawingCanvas.CaptureMouse();
                    EraseAt(_startPoint);
                    break;
            }
        }

        private void DrawingCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isHost) return;
            
            var currentPoint = e.GetPosition(DrawingCanvas);

            // 处理拖拽
            if (_isDragging && _selectedElement != null)
            {
                DragSelectedElement(currentPoint);
                _lastPoint = currentPoint;
                return;
            }
            
            if (!_isDrawing) return;

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
            if (!_isHost) return;
            
            DrawingCanvas.ReleaseMouseCapture();
            var endPoint = e.GetPosition(DrawingCanvas);

            // 处理拖拽结束
            if (_isDragging && _selectedElement != null)
            {
                _isDragging = false;
                FinishDragElement(endPoint);
                return;
            }
            
            if (!_isDrawing) return;

            _isDrawing = false;

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

        #region 选择和拖动

        /// <summary>
        /// 尝试选中点击位置的图形
        /// </summary>
        private void TrySelectElement(Point point)
        {
            ClearSelection();
            
            var hitRadius = 5.0;
            
            // 逆序遍历，优先选中上层图形
            foreach (var kvp in _strokeElements.Reverse())
            {
                var element = kvp.Value;
                var bounds = GetElementBounds(element);
                
                // 扩展检测范围
                bounds.Inflate(hitRadius, hitRadius);
                
                if (bounds.Contains(point))
                {
                    _selectedElement = element;
                    _selectedStrokeId = kvp.Key;
                    HighlightElement(element, true);
                    return;
                }
            }
        }
        
        /// <summary>
        /// 获取元素的边界框
        /// </summary>
        private Rect GetElementBounds(UIElement element)
        {
            double left = Canvas.GetLeft(element);
            double top = Canvas.GetTop(element);
            
            // 处理 NaN 情况
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;
            
            return new Rect(left, top, element.RenderSize.Width, element.RenderSize.Height);
        }
        
        /// <summary>
        /// 高亮显示选中元素
        /// </summary>
        private void HighlightElement(UIElement element, bool highlight)
        {
            if (element is Shape shape)
            {
                if (highlight)
                {
                    shape.StrokeDashArray = new DoubleCollection(new[] { 5.0, 3.0 });
                }
                else
                {
                    shape.StrokeDashArray = null;
                }
            }
            else if (element is System.Windows.Controls.TextBlock textBlock)
            {
                if (highlight)
                {
                    textBlock.TextDecorations = TextDecorations.Underline;
                }
                else
                {
                    textBlock.TextDecorations = null;
                }
            }
        }
        
        /// <summary>
        /// 清除选中状态
        /// </summary>
        private void ClearSelection()
        {
            if (_selectedElement != null)
            {
                HighlightElement(_selectedElement, false);
                _selectedElement = null;
                _selectedStrokeId = null;
            }
        }
        
        /// <summary>
        /// 拖动选中的元素
        /// </summary>
        private void DragSelectedElement(Point currentPoint)
        {
            if (_selectedElement == null) return;
            
            var deltaX = currentPoint.X - _dragStartPoint.X;
            var deltaY = currentPoint.Y - _dragStartPoint.Y;
            
            // 对于 Polyline，需要移动所有点
            if (_selectedElement is Polyline polyline)
            {
                var newPoints = new PointCollection();
                foreach (var pt in polyline.Points)
                {
                    newPoints.Add(new Point(pt.X + deltaX, pt.Y + deltaY));
                }
                polyline.Points = newPoints;
            }
            else
            {
                // 其他图形使用 Canvas 定位
                var currentLeft = Canvas.GetLeft(_selectedElement);
                var currentTop = Canvas.GetTop(_selectedElement);
                
                if (double.IsNaN(currentLeft)) currentLeft = 0;
                if (double.IsNaN(currentTop)) currentTop = 0;
                
                Canvas.SetLeft(_selectedElement, currentLeft + deltaX);
                Canvas.SetTop(_selectedElement, currentTop + deltaY);
            }
            
            _dragStartPoint = currentPoint;
        }
        
        /// <summary>
        /// 完成拖动操作
        /// </summary>
        private void FinishDragElement(Point endPoint)
        {
            if (_selectedElement == null || _selectedStrokeId == null) return;
            
            // 更新笔触数据中的位置
            var stroke = _strokes.FirstOrDefault(s => s.Id == _selectedStrokeId);
            if (stroke != null)
            {
                // 更新笔触位置信息
                if (stroke.Tool == WhiteboardTool.Pen && _selectedElement is Polyline polyline)
                {
                    // Polyline 从元素的 Points 获取最新位置
                    stroke.Points = polyline.Points.SelectMany(p => new[] { p.X, p.Y }).ToList();
                }
                else
                {
                    // 其他图形从 Canvas 获取位置
                    var newLeft = Canvas.GetLeft(_selectedElement);
                    var newTop = Canvas.GetTop(_selectedElement);
                    
                    if (double.IsNaN(newLeft)) newLeft = 0;
                    if (double.IsNaN(newTop)) newTop = 0;
                    
                    var width = stroke.EndX - stroke.StartX;
                    var height = stroke.EndY - stroke.StartY;
                    stroke.StartX = newLeft;
                    stroke.StartY = newTop;
                    stroke.EndX = newLeft + width;
                    stroke.EndY = newTop + height;
                }
                
                // 发送移动更新
                NotifyStrokeMoved(stroke);
            }
        }
        
        /// <summary>
        /// 通知笔触移动
        /// </summary>
        private void NotifyStrokeMoved(WhiteboardStroke stroke)
        {
            StrokeUpdated?.Invoke(new WhiteboardStrokeUpdate
            {
                SessionId = _sessionId,
                Action = "move",
                Stroke = stroke,
                DrawerId = _currentPeerId,
                UpdateTime = DateTime.Now
            });
        }

        #endregion

        #region 文字输入

        private void ShowTextInput(Point position)
        {
            // 清空输入框
            TextInputBox.Text = string.Empty;
            TextInputBox.Foreground = new SolidColorBrush(_currentColor);
            TextInputBox.FontSize = 16 + _strokeWidth;
            
            // 将输入框容器定位到鼠标点击位置
            Canvas.SetLeft(TextInputContainer, position.X);
            Canvas.SetTop(TextInputContainer, position.Y);
            
            // 显示输入框并获取焦点
            TextInputContainer.Visibility = Visibility.Visible;
            _isTextInputActive = true;
            
            // 确保焦点在输入框上
            Dispatcher.BeginInvoke(new Action(() =>
            {
                TextInputBox.Focus();
                Keyboard.Focus(TextInputBox);
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        /// <summary>
        /// 取消文字输入（不保存）
        /// </summary>
        private void CancelTextInput()
        {
            if (!_isTextInputActive) return;
            
            _isTextInputActive = false;
            TextInputBox.Text = string.Empty;
            TextInputContainer.Visibility = Visibility.Collapsed;
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
                CancelTextInput();
                e.Handled = true;
            }
        }

        private void TextInputBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // 只有在输入框激活且有内容时才完成输入
            // 如果是切换工具导致的失焦，且没有内容，则取消
            if (_isTextInputActive)
            {
                if (string.IsNullOrWhiteSpace(TextInputBox.Text))
                {
                    CancelTextInput();
                }
                else
                {
                    FinishTextInput();
                }
            }
        }

        private void FinishTextInput()
        {
            if (!_isTextInputActive) return;
            
            var text = TextInputBox.Text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                CancelTextInput();
                return;
            }

            var position = new Point(Canvas.GetLeft(TextInputContainer), Canvas.GetTop(TextInputContainer));

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

            // 隐藏输入框
            _isTextInputActive = false;
            TextInputBox.Text = string.Empty;
            TextInputContainer.Visibility = Visibility.Collapsed;

            // 发送更新
            NotifyStrokeAdded(stroke);
            
            // 文字输入完成后切换到选择工具
            SwitchToSelectTool();
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

        private async void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            if (!_isHost) return;

            var confirmResult = await ShowConfirmDialogAsync("确认清空", "确定要清空所有绘制内容吗？");

            if (confirmResult)
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
                    var successBox = new Wpf.Ui.Controls.MessageBox
                    {
                        Title = "保存成功",
                        Content = "图片保存成功！",
                        CloseButtonText = "确定"
                    };
                    _ = successBox.ShowDialogAsync();
                }
            }
            catch (Exception ex)
            {
                var errorBox = new Wpf.Ui.Controls.MessageBox
                {
                    Title = "错误",
                    Content = $"保存失败: {ex.Message}",
                    CloseButtonText = "确定"
                };
                _ = errorBox.ShowDialogAsync();
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

        private async void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (!_isHost) return;

            var cancelResult = await ShowConfirmDialogAsync("确认取消", "确定要取消并关闭白板吗？所有绘制内容将丢失。");

            if (cancelResult)
            {
                NotifyWhiteboardClosed(false);
                _isForceClosing = true;
                try
                {
                    Close();
                }
                catch (InvalidOperationException)
                {
                    // 窗口已在关闭中，忽略
                }
            }
        }

        private void BtnFinish_Click(object sender, RoutedEventArgs e)
        {
            if (!_isHost) return;

            NotifyWhiteboardClosed(true);
            _isForceClosing = true;
            try
            {
                Close();
            }
            catch (InvalidOperationException)
            {
                // 窗口已在关闭中，忽略
            }
        }

        /// <summary>
        /// 显示确认对话框
        /// </summary>
        private async Task<bool> ShowConfirmDialogAsync(string title, string content)
        {
            var confirmBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = title,
                Content = content,
                PrimaryButtonText = "确定",
                CloseButtonText = "取消"
            };

            var result = await confirmBox.ShowDialogAsync();
            return result == Wpf.Ui.Controls.MessageBoxResult.Primary;
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
                var uiMessageBox = new Wpf.Ui.Controls.MessageBox
                {
                    Title = "提示",
                    Content = "在主持人结束白板演示前，您不能关闭此窗口。",
                    CloseButtonText = "确定"
                };
                _ = uiMessageBox.ShowDialogAsync();
                return;
            }
            
            // 主持人点击 X 按钮关闭窗口时，显示确认对话框
            if (!_isPendingCloseConfirm)
            {
                e.Cancel = true;
                _isPendingCloseConfirm = true;
                ShowHostCloseConfirmDialog();
            }
        }
        
        /// <summary>
        /// 显示主持人关闭确认对话框
        /// </summary>
        private async void ShowHostCloseConfirmDialog()
        {
            var confirmBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = "确认关闭",
                Content = "关闭白板将结束所有用户的白板演示。确定关闭吗？",
                PrimaryButtonText = "确定",
                CloseButtonText = "取消"
            };

            var result = await confirmBox.ShowDialogAsync();
            _isPendingCloseConfirm = false;

            if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
            {
                // 发送关闭通知给所有用户
                NotifyWhiteboardClosed(false);
                _isForceClosing = true;
                
                try
                {
                    Close();
                }
                catch (InvalidOperationException)
                {
                    // 窗口已在关闭中，忽略
                }
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
            else if (e.Key == Key.V)
            {
                BtnSelect.IsChecked = true;
            }
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
                            
                        case "move":
                            if (update.Stroke != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"[Whiteboard] 移动笔触: Id={update.Stroke.Id}");
                                MoveStrokeOnCanvas(update.Stroke);
                            }
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
        
        /// <summary>
        /// 移动画布上的笔触（远程同步）
        /// </summary>
        private void MoveStrokeOnCanvas(WhiteboardStroke stroke)
        {
            if (!_strokeElements.TryGetValue(stroke.Id, out var element))
            {
                System.Diagnostics.Debug.WriteLine($"[Whiteboard] MoveStrokeOnCanvas: 找不到笔触 Id={stroke.Id}");
                return;
            }
            
            // 根据工具类型分别处理
            if (stroke.Tool == WhiteboardTool.Pen && element is Polyline polyline)
            {
                // Polyline 需要更新所有点
                if (stroke.Points != null && stroke.Points.Count >= 2)
                {
                    polyline.Points.Clear();
                    for (int i = 0; i < stroke.Points.Count - 1; i += 2)
                    {
                        polyline.Points.Add(new Point(stroke.Points[i], stroke.Points[i + 1]));
                    }
                }
            }
            else
            {
                // 其他图形使用 Canvas 定位
                Canvas.SetLeft(element, stroke.StartX);
                Canvas.SetTop(element, stroke.StartY);
            }
            
            // 更新本地笔触数据
            var localStroke = _strokes.FirstOrDefault(s => s.Id == stroke.Id);
            if (localStroke != null)
            {
                localStroke.StartX = stroke.StartX;
                localStroke.StartY = stroke.StartY;
                localStroke.EndX = stroke.EndX;
                localStroke.EndY = stroke.EndY;
                if (stroke.Points != null)
                {
                    localStroke.Points = new List<double>(stroke.Points);
                }
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
            System.Diagnostics.Debug.WriteLine($"[WhiteboardWindow] ForceClose 被调用, CurrentThread={System.Threading.Thread.CurrentThread.ManagedThreadId}");
            
            try
            {
                if (Dispatcher.CheckAccess())
                {
                    System.Diagnostics.Debug.WriteLine($"[WhiteboardWindow] 已在 UI 线程，直接关闭");
                    _isForceClosing = true;
                    Close();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[WhiteboardWindow] 不在 UI 线程，调用 Dispatcher.Invoke");
                    Dispatcher.Invoke(() =>
                    {
                        _isForceClosing = true;
                        Close();
                    });
                }
                System.Diagnostics.Debug.WriteLine($"[WhiteboardWindow] ForceClose 完成");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WhiteboardWindow] ForceClose 异常: {ex.Message}");
            }
        }

        #endregion
    }
}
