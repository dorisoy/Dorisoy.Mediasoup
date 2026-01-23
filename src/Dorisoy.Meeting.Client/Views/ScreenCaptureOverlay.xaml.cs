using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace Dorisoy.Meeting.Client.Views
{
    /// <summary>
    /// 屏幕截取选择遮罩窗口 - 支持选区内直接绘制标记
    /// </summary>
    public partial class ScreenCaptureOverlay : Window
    {
        #region 枚举和字段

        private enum CaptureState
        {
            Selecting,  // 正在选择区域
            Editing     // 正在编辑（绘制）
        }

        private enum DrawingTool
        {
            Select,
            Pen,
            Rectangle,
            Ellipse,
            Arrow,
            Text
        }

        private CaptureState _state = CaptureState.Selecting;
        private DrawingTool _currentTool = DrawingTool.Select;
        private System.Windows.Media.Color _currentColor = Colors.Red;
        private double _strokeWidth = 3;

        // 选区相关
        private bool _isSelecting;
        private System.Windows.Point _startPoint;
        private System.Windows.Point _endPoint;
        private Rect _selectionRect;

        // 绘图相关
        private bool _isDrawing;
        private System.Windows.Point _drawStartPoint;
        private UIElement? _currentShape;
        private readonly List<UIElement> _drawingHistory = new();

        // 画笔工具
        private Polyline? _currentPolyline;

        // 箭头工具
        private Line? _arrowLine;
        private Polygon? _arrowHead;

        // 选择工具 - 移动元素
        private UIElement? _selectedElement;
        private Line? _selectedArrowLine;      // 箭头线条部分
        private Polygon? _selectedArrowHead;   // 箭头头部部分
        private bool _isDragging;
        private System.Windows.Point _dragStartPoint;
        private System.Windows.Point _elementStartPosition;

        // 截取的屏幕图像
        private BitmapSource? _capturedImage;

        /// <summary>
        /// 截取完成事件，返回截取的图片
        /// </summary>
        public event Action<BitmapSource>? CaptureCompleted;

        /// <summary>
        /// 截取取消事件
        /// </summary>
        public event Action? CaptureCancelled;

        #endregion

        #region 构造函数

        public ScreenCaptureOverlay()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            KeyDown += OnKeyDown;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 设置全屏几何区域
            FullScreenGeometry.Rect = new Rect(0, 0, ActualWidth, ActualHeight);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (_state == CaptureState.Editing)
                {
                    ResetToSelectingState();
                }
                else
                {
                    CancelCapture();
                }
            }
            else if (e.Key == Key.Enter && _state == CaptureState.Editing)
            {
                ConfirmCapture();
            }
            else if (e.Key == Key.Z && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && _state == CaptureState.Editing)
            {
                Undo();
            }
            else if (e.Key == Key.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && _state == CaptureState.Editing)
            {
                SaveToFile();
            }
            // 快捷键切换工具
            else if (_state == CaptureState.Editing)
            {
                switch (e.Key)
                {
                    case Key.V: BtnSelect.IsChecked = true; break;
                    case Key.P: BtnPen.IsChecked = true; break;
                    case Key.R: BtnRectangle.IsChecked = true; break;
                    case Key.O: BtnEllipse.IsChecked = true; break;
                    case Key.A: BtnArrow.IsChecked = true; break;
                    case Key.T: BtnText.IsChecked = true; break;
                }
            }
        }

        #endregion

        #region 选区相关事件

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_state != CaptureState.Selecting) return;

            _isSelecting = true;
            _startPoint = e.GetPosition(OverlayCanvas);
            _endPoint = _startPoint;

            HintText.Visibility = Visibility.Collapsed;
            SelectionBorder.Visibility = Visibility.Visible;
            SizeHint.Visibility = Visibility.Visible;

            OverlayCanvas.CaptureMouse();
            UpdateSelection();
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isSelecting || _state != CaptureState.Selecting) return;

            _endPoint = e.GetPosition(OverlayCanvas);
            UpdateSelection();
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isSelecting || _state != CaptureState.Selecting) return;

            _isSelecting = false;
            OverlayCanvas.ReleaseMouseCapture();

            _endPoint = e.GetPosition(OverlayCanvas);
            UpdateSelection();

            // 如果选择区域有效，进入编辑状态
            if (_selectionRect.Width > 10 && _selectionRect.Height > 10)
            {
                EnterEditingState();
            }
        }

        private void UpdateSelection()
        {
            var x = Math.Min(_startPoint.X, _endPoint.X);
            var y = Math.Min(_startPoint.Y, _endPoint.Y);
            var width = Math.Abs(_endPoint.X - _startPoint.X);
            var height = Math.Abs(_endPoint.Y - _startPoint.Y);

            _selectionRect = new Rect(x, y, width, height);

            // 更新遮罩镂空区域
            SelectionGeometry.Rect = _selectionRect;

            // 更新选区边框位置和大小
            SelectionBorder.Margin = new Thickness(x, y, 0, 0);
            SelectionBorder.Width = width;
            SelectionBorder.Height = height;

            // 更新尺寸提示
            SizeText.Text = $"{(int)width} × {(int)height}";
            SizeHint.Margin = new Thickness(x, Math.Max(0, y - 24), 0, 0);
        }

        #endregion

        #region 编辑状态

        private void EnterEditingState()
        {
            _state = CaptureState.Editing;

            // 隐藏尺寸提示
            SizeHint.Visibility = Visibility.Collapsed;

            // 截取选区屏幕
            CaptureSelectionToImage();

            // 更新光标
            Cursor = Cursors.Arrow;
            DrawingCanvas.Cursor = Cursors.Arrow;

            // 显示工具栏
            ShowToolbar();
        }

        private void CaptureSelectionToImage()
        {
            try
            {
                // 获取 DPI 缩放
                var dpiScale = VisualTreeHelper.GetDpi(this);
                var scaleX = dpiScale.DpiScaleX;
                var scaleY = dpiScale.DpiScaleY;

                // 计算实际屏幕坐标
                int screenX = (int)(_selectionRect.X * scaleX);
                int screenY = (int)(_selectionRect.Y * scaleY);
                int screenWidth = (int)(_selectionRect.Width * scaleX);
                int screenHeight = (int)(_selectionRect.Height * scaleY);

                // 暂时隐藏窗口截取屏幕
                Opacity = 0;
                System.Threading.Thread.Sleep(50);

                // 使用 GDI+ 截取屏幕区域
                using var bitmap = new Bitmap(screenWidth, screenHeight);
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(screenX, screenY, 0, 0,
                        new System.Drawing.Size(screenWidth, screenHeight));
                }

                // 转换为 BitmapSource
                _capturedImage = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    bitmap.GetHbitmap(),
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                _capturedImage.Freeze();

                // 恢复窗口
                Opacity = 1;

                // 将截取的图像作为绘图画布的背景
                DrawingCanvas.Background = new ImageBrush(_capturedImage)
                {
                    Stretch = Stretch.Fill
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScreenCapture] 截取选区失败: {ex.Message}");
                Opacity = 1;
            }
        }

        private void ShowToolbar()
        {
            // 工具栏固定宽度约520，放在选区下方居中
            var toolbarWidth = 520.0;
            var toolbarHeight = 40.0;
            
            // 计算工具栏X位置（选区居中，但不超出屏幕）
            var toolbarX = _selectionRect.Left + (_selectionRect.Width - toolbarWidth) / 2;
            if (toolbarX < 0) toolbarX = 0;
            if (toolbarX + toolbarWidth > ActualWidth) toolbarX = ActualWidth - toolbarWidth;
            
            // 计算工具栏Y位置（选区下方，如果空间不足则放在选区上方）
            var toolbarY = _selectionRect.Bottom + 8;
            if (toolbarY + toolbarHeight > ActualHeight)
            {
                toolbarY = _selectionRect.Top - toolbarHeight - 8;
                if (toolbarY < 0) toolbarY = _selectionRect.Bottom - toolbarHeight - 8;
            }

            Toolbar.Margin = new Thickness(toolbarX, toolbarY, 0, 0);
            Toolbar.Visibility = Visibility.Visible;
        }

        private void ResetToSelectingState()
        {
            _state = CaptureState.Selecting;

            // 隐藏工具栏和重置绘图层
            Toolbar.Visibility = Visibility.Collapsed;
            SelectionBorder.Visibility = Visibility.Collapsed;

            // 清空绘图历史
            DrawingCanvas.Children.Clear();
            // 重新添加文字输入框
            DrawingCanvas.Children.Add(TextInputContainer);
            _drawingHistory.Clear();
            DrawingCanvas.Background = System.Windows.Media.Brushes.Transparent;

            // 重置选区
            SelectionGeometry.Rect = new Rect(0, 0, 0, 0);
            _selectionRect = Rect.Empty;

            // 显示提示
            HintText.Visibility = Visibility.Visible;
            Cursor = Cursors.Cross;

            // 重置工具
            BtnSelect.IsChecked = true;
            _currentTool = DrawingTool.Select;
        }

        #endregion

        #region 绘图工具事件

        private void Tool_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton radio) return;

            _currentTool = radio.Name switch
            {
                "BtnSelect" => DrawingTool.Select,
                "BtnPen" => DrawingTool.Pen,
                "BtnRectangle" => DrawingTool.Rectangle,
                "BtnEllipse" => DrawingTool.Ellipse,
                "BtnArrow" => DrawingTool.Arrow,
                "BtnText" => DrawingTool.Text,
                _ => DrawingTool.Select
            };

            // 更新光标
            if (DrawingCanvas != null)
            {
                DrawingCanvas.Cursor = _currentTool == DrawingTool.Select ? Cursors.Arrow : Cursors.Cross;
            }
        }

        private void Color_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radio && radio.Tag is string colorHex)
            {
                _currentColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex);
            }
        }

        private void SliderStrokeWidth_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _strokeWidth = e.NewValue;
        }

        #endregion

        #region 绘图画布事件

        private void DrawingCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_state != CaptureState.Editing) return;

            // 文字工具特殊处理
            if (_currentTool == DrawingTool.Text)
            {
                ShowTextInput(e.GetPosition(DrawingCanvas));
                return;
            }

            // 选择工具 - 检测点击的元素
            if (_currentTool == DrawingTool.Select)
            {
                var clickPoint = e.GetPosition(DrawingCanvas);
                var hitElement = FindElementAtPoint(clickPoint);
                
                if (hitElement != null)
                {
                    SelectElement(hitElement);
                    _isDragging = true;
                    _dragStartPoint = clickPoint;
                    _elementStartPosition = GetElementPosition(_selectedElement!);
                    DrawingCanvas.CaptureMouse();
                }
                else
                {
                    ClearSelection();
                }
                return;
            }

            _isDrawing = true;
            _drawStartPoint = e.GetPosition(DrawingCanvas);
            DrawingCanvas.CaptureMouse();

            // 根据工具类型创建形状
            switch (_currentTool)
            {
                case DrawingTool.Pen:
                    _currentPolyline = new Polyline
                    {
                        Stroke = new SolidColorBrush(_currentColor),
                        StrokeThickness = _strokeWidth,
                        StrokeLineJoin = PenLineJoin.Round,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round
                    };
                    _currentPolyline.Points.Add(_drawStartPoint);
                    DrawingCanvas.Children.Add(_currentPolyline);
                    _currentShape = _currentPolyline;
                    break;

                case DrawingTool.Rectangle:
                    _currentShape = new System.Windows.Shapes.Rectangle
                    {
                        Stroke = new SolidColorBrush(_currentColor),
                        StrokeThickness = _strokeWidth,
                        Fill = System.Windows.Media.Brushes.Transparent
                    };
                    DrawingCanvas.Children.Add(_currentShape);
                    Canvas.SetLeft(_currentShape, _drawStartPoint.X);
                    Canvas.SetTop(_currentShape, _drawStartPoint.Y);
                    break;

                case DrawingTool.Ellipse:
                    _currentShape = new System.Windows.Shapes.Ellipse
                    {
                        Stroke = new SolidColorBrush(_currentColor),
                        StrokeThickness = _strokeWidth,
                        Fill = System.Windows.Media.Brushes.Transparent
                    };
                    DrawingCanvas.Children.Add(_currentShape);
                    Canvas.SetLeft(_currentShape, _drawStartPoint.X);
                    Canvas.SetTop(_currentShape, _drawStartPoint.Y);
                    break;

                case DrawingTool.Arrow:
                    _arrowLine = new Line
                    {
                        Stroke = new SolidColorBrush(_currentColor),
                        StrokeThickness = _strokeWidth,
                        X1 = _drawStartPoint.X,
                        Y1 = _drawStartPoint.Y,
                        X2 = _drawStartPoint.X,
                        Y2 = _drawStartPoint.Y
                    };
                    DrawingCanvas.Children.Add(_arrowLine);

                    _arrowHead = new Polygon
                    {
                        Fill = new SolidColorBrush(_currentColor)
                    };
                    DrawingCanvas.Children.Add(_arrowHead);
                    break;
            }
        }

        private void DrawingCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            // 选择工具 - 拖动元素
            if (_isDragging && _selectedElement != null && _currentTool == DrawingTool.Select)
            {
                var dragPoint = e.GetPosition(DrawingCanvas);
                var deltaX = dragPoint.X - _dragStartPoint.X;
                var deltaY = dragPoint.Y - _dragStartPoint.Y;
                
                MoveSelectedElement(deltaX, deltaY);
                return;
            }
            
            if (!_isDrawing || _state != CaptureState.Editing) return;

            var currentPoint = e.GetPosition(DrawingCanvas);

            switch (_currentTool)
            {
                case DrawingTool.Pen:
                    _currentPolyline?.Points.Add(currentPoint);
                    break;

                case DrawingTool.Rectangle:
                case DrawingTool.Ellipse:
                    UpdateShapeSize(currentPoint);
                    break;

                case DrawingTool.Arrow:
                    UpdateArrow(currentPoint);
                    break;
            }
        }

        private void DrawingCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // 选择工具 - 结束拖动
            if (_isDragging && _currentTool == DrawingTool.Select)
            {
                _isDragging = false;
                DrawingCanvas.ReleaseMouseCapture();
                return;
            }
            
            if (!_isDrawing || _state != CaptureState.Editing) return;

            _isDrawing = false;
            DrawingCanvas.ReleaseMouseCapture();

            // 添加到历史记录
            if (_currentTool == DrawingTool.Arrow)
            {
                if (_arrowLine != null) _drawingHistory.Add(_arrowLine);
                if (_arrowHead != null) _drawingHistory.Add(_arrowHead);
                _arrowLine = null;
                _arrowHead = null;
            }
            else if (_currentShape != null)
            {
                _drawingHistory.Add(_currentShape);
                _currentShape = null;
            }
        }

        private void UpdateShapeSize(System.Windows.Point currentPoint)
        {
            if (_currentShape == null) return;

            var x = Math.Min(_drawStartPoint.X, currentPoint.X);
            var y = Math.Min(_drawStartPoint.Y, currentPoint.Y);
            var width = Math.Abs(currentPoint.X - _drawStartPoint.X);
            var height = Math.Abs(currentPoint.Y - _drawStartPoint.Y);

            Canvas.SetLeft(_currentShape, x);
            Canvas.SetTop(_currentShape, y);

            if (_currentShape is System.Windows.Shapes.Rectangle rect)
            {
                rect.Width = width;
                rect.Height = height;
            }
            else if (_currentShape is System.Windows.Shapes.Ellipse ellipse)
            {
                ellipse.Width = width;
                ellipse.Height = height;
            }
        }

        private void UpdateArrow(System.Windows.Point endPoint)
        {
            if (_arrowLine == null || _arrowHead == null) return;

            _arrowLine.X2 = endPoint.X;
            _arrowLine.Y2 = endPoint.Y;

            // 计算箭头头部
            var dx = endPoint.X - _drawStartPoint.X;
            var dy = endPoint.Y - _drawStartPoint.Y;
            var angle = Math.Atan2(dy, dx);

            var headLength = 12 + _strokeWidth * 2;

            var points = new PointCollection
            {
                endPoint,
                new System.Windows.Point(
                    endPoint.X - headLength * Math.Cos(angle - Math.PI / 6),
                    endPoint.Y - headLength * Math.Sin(angle - Math.PI / 6)),
                new System.Windows.Point(
                    endPoint.X - headLength * Math.Cos(angle + Math.PI / 6),
                    endPoint.Y - headLength * Math.Sin(angle + Math.PI / 6))
            };

            _arrowHead.Points = points;
        }

        #endregion

        #region 文字输入

        private void ShowTextInput(System.Windows.Point position)
        {
            TextInputContainer.Visibility = Visibility.Visible;
            Canvas.SetLeft(TextInputContainer, position.X);
            Canvas.SetTop(TextInputContainer, position.Y);

            TextInputBox.Text = string.Empty;
            TextInputBox.Foreground = new SolidColorBrush(_currentColor);
            TextInputBox.FontSize = 12 + _strokeWidth * 2;
            TextInputBox.Focus();
        }

        private void TextInputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                CommitText();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                TextInputContainer.Visibility = Visibility.Collapsed;
                e.Handled = true;
            }
        }

        private void TextInputBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitText();
        }

        private void CommitText()
        {
            if (TextInputContainer.Visibility != Visibility.Visible) return;

            var text = TextInputBox.Text?.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                var textBlock = new System.Windows.Controls.TextBlock
                {
                    Text = text,
                    Foreground = new SolidColorBrush(_currentColor),
                    FontSize = 12 + _strokeWidth * 2,
                    FontWeight = FontWeights.Medium
                };

                Canvas.SetLeft(textBlock, Canvas.GetLeft(TextInputContainer));
                Canvas.SetTop(textBlock, Canvas.GetTop(TextInputContainer));

                DrawingCanvas.Children.Add(textBlock);
                _drawingHistory.Add(textBlock);
            }

            TextInputContainer.Visibility = Visibility.Collapsed;
        }

        #endregion

        #region 操作按钮

        private void BtnUndo_Click(object sender, MouseButtonEventArgs e)
        {
            Undo();
        }

        private void Undo()
        {
            if (_drawingHistory.Count > 0)
            {
                var lastElement = _drawingHistory[^1];
                _drawingHistory.RemoveAt(_drawingHistory.Count - 1);
                DrawingCanvas.Children.Remove(lastElement);

                // 如果是箭头头部，还需要移除线条
                if (lastElement is Polygon && _drawingHistory.Count > 0 && _drawingHistory[^1] is Line)
                {
                    var line = _drawingHistory[^1];
                    _drawingHistory.RemoveAt(_drawingHistory.Count - 1);
                    DrawingCanvas.Children.Remove(line);
                }
            }
        }

        private void BtnSave_Click(object sender, MouseButtonEventArgs e)
        {
            SaveToFile();
        }

        private void SaveToFile()
        {
            var dialog = new SaveFileDialog
            {
                Title = "保存截图",
                Filter = "PNG 图片|*.png|JPEG 图片|*.jpg|BMP 图片|*.bmp",
                DefaultExt = ".png",
                FileName = $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var bitmap = RenderToBitmap();
                    SaveBitmapToFile(bitmap, dialog.FileName);
                    System.Windows.MessageBox.Show($"截图已保存到:\n{dialog.FileName}", "保存成功",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"保存截图失败: {ex.Message}", "保存失败",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnCancel_Click(object sender, MouseButtonEventArgs e)
        {
            CancelCapture();
        }

        private void BtnConfirm_Click(object sender, MouseButtonEventArgs e)
        {
            ConfirmCapture();
        }

        #endregion

        #region 辅助方法

        private void CancelCapture()
        {
            CaptureCancelled?.Invoke();
            Close();
        }

        private void ConfirmCapture()
        {
            try
            {
                var bitmap = RenderToBitmap();
                Clipboard.SetImage(bitmap);
                CaptureCompleted?.Invoke(bitmap);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScreenCapture] 复制到剪贴板失败: {ex.Message}");
            }
            finally
            {
                Close();
            }
        }

        private RenderTargetBitmap RenderToBitmap()
        {
            // 隐藏文字输入框
            TextInputContainer.Visibility = Visibility.Collapsed;

            var width = (int)_selectionRect.Width;
            var height = (int)_selectionRect.Height;

            if (width <= 0 || height <= 0)
            {
                width = 100;
                height = 100;
            }

            var dpi = VisualTreeHelper.GetDpi(this);
            var bitmap = new RenderTargetBitmap(
                (int)(width * dpi.DpiScaleX),
                (int)(height * dpi.DpiScaleY),
                dpi.PixelsPerInchX,
                dpi.PixelsPerInchY,
                PixelFormats.Pbgra32);

            var drawingVisual = new DrawingVisual();
            using (var context = drawingVisual.RenderOpen())
            {
                var brush = new VisualBrush(DrawingCanvas);
                context.DrawRectangle(brush, null, new Rect(0, 0, width * dpi.DpiScaleX, height * dpi.DpiScaleY));
            }

            bitmap.Render(drawingVisual);
            bitmap.Freeze();

            return bitmap;
        }

        private static void SaveBitmapToFile(BitmapSource bitmap, string filePath)
        {
            BitmapEncoder encoder = System.IO.Path.GetExtension(filePath).ToLower() switch
            {
                ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = 95 },
                ".bmp" => new BmpBitmapEncoder(),
                _ => new PngBitmapEncoder()
            };

            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using var stream = File.OpenWrite(filePath);
            encoder.Save(stream);
        }

        #endregion

        #region 选择工具辅助方法

        /// <summary>
        /// 在指定点查找元素
        /// </summary>
        private UIElement? FindElementAtPoint(System.Windows.Point point)
        {
            // 从后往前遍历（后绘制的在上层）
            for (int i = _drawingHistory.Count - 1; i >= 0; i--)
            {
                var element = _drawingHistory[i];
                var bounds = GetElementBounds(element);
                
                if (bounds.Contains(point))
                {
                    return element;
                }
            }
            return null;
        }

        /// <summary>
        /// 获取元素边界
        /// </summary>
        private Rect GetElementBounds(UIElement element)
        {
            if (element is Polyline polyline)
            {
                if (polyline.Points.Count == 0) return Rect.Empty;
                
                double minX = double.MaxValue, minY = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue;
                
                foreach (var pt in polyline.Points)
                {
                    minX = Math.Min(minX, pt.X);
                    minY = Math.Min(minY, pt.Y);
                    maxX = Math.Max(maxX, pt.X);
                    maxY = Math.Max(maxY, pt.Y);
                }
                
                // 扩大点击区域
                var padding = Math.Max(polyline.StrokeThickness, 5);
                return new Rect(minX - padding, minY - padding, 
                    maxX - minX + padding * 2, maxY - minY + padding * 2);
            }
            else if (element is Line line)
            {
                var minX = Math.Min(line.X1, line.X2);
                var minY = Math.Min(line.Y1, line.Y2);
                var maxX = Math.Max(line.X1, line.X2);
                var maxY = Math.Max(line.Y1, line.Y2);
                var padding = Math.Max(line.StrokeThickness, 10);
                return new Rect(minX - padding, minY - padding,
                    maxX - minX + padding * 2, maxY - minY + padding * 2);
            }
            else if (element is Polygon polygon)
            {
                if (polygon.Points.Count == 0) return Rect.Empty;
                
                double minX = double.MaxValue, minY = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue;
                
                foreach (var pt in polygon.Points)
                {
                    minX = Math.Min(minX, pt.X);
                    minY = Math.Min(minY, pt.Y);
                    maxX = Math.Max(maxX, pt.X);
                    maxY = Math.Max(maxY, pt.Y);
                }
                
                return new Rect(minX, minY, maxX - minX, maxY - minY);
            }
            else if (element is FrameworkElement fe)
            {
                var left = Canvas.GetLeft(fe);
                var top = Canvas.GetTop(fe);
                if (double.IsNaN(left)) left = 0;
                if (double.IsNaN(top)) top = 0;
                return new Rect(left, top, fe.ActualWidth, fe.ActualHeight);
            }
            
            return Rect.Empty;
        }

        /// <summary>
        /// 选中元素
        /// </summary>
        private void SelectElement(UIElement element)
        {
            ClearSelection();
            _selectedElement = element;
            
            // 如果是箭头的一部分，找到完整的箭头
            var index = _drawingHistory.IndexOf(element);
            if (element is Line line && index >= 0 && index + 1 < _drawingHistory.Count)
            {
                if (_drawingHistory[index + 1] is Polygon poly)
                {
                    _selectedArrowLine = line;
                    _selectedArrowHead = poly;
                }
            }
            else if (element is Polygon poly && index > 0)
            {
                if (_drawingHistory[index - 1] is Line ln)
                {
                    _selectedArrowLine = ln;
                    _selectedArrowHead = poly;
                    _selectedElement = ln;  // 用线条作为主元素
                }
            }
            
            // 高亮选中元素
            HighlightElement(_selectedElement, true);
            if (_selectedArrowHead != null)
            {
                HighlightElement(_selectedArrowHead, true);
            }
        }

        /// <summary>
        /// 清除选择
        /// </summary>
        private void ClearSelection()
        {
            if (_selectedElement != null)
            {
                HighlightElement(_selectedElement, false);
            }
            if (_selectedArrowHead != null)
            {
                HighlightElement(_selectedArrowHead, false);
            }
            
            _selectedElement = null;
            _selectedArrowLine = null;
            _selectedArrowHead = null;
        }

        /// <summary>
        /// 高亮/取消高亮元素
        /// </summary>
        private static void HighlightElement(UIElement element, bool highlight)
        {
            if (element is Shape shape)
            {
                if (highlight)
                {
                    shape.Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = Colors.Cyan,
                        BlurRadius = 8,
                        ShadowDepth = 0,
                        Opacity = 0.8
                    };
                }
                else
                {
                    shape.Effect = null;
                }
            }
            else if (element is System.Windows.Controls.TextBlock textBlock)
            {
                if (highlight)
                {
                    textBlock.Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = Colors.Cyan,
                        BlurRadius = 8,
                        ShadowDepth = 0,
                        Opacity = 0.8
                    };
                }
                else
                {
                    textBlock.Effect = null;
                }
            }
        }

        /// <summary>
        /// 获取元素位置
        /// </summary>
        private System.Windows.Point GetElementPosition(UIElement element)
        {
            if (element is Polyline polyline && polyline.Points.Count > 0)
            {
                return polyline.Points[0];
            }
            else if (element is Line line)
            {
                return new System.Windows.Point(line.X1, line.Y1);
            }
            else if (element is FrameworkElement fe)
            {
                var left = Canvas.GetLeft(fe);
                var top = Canvas.GetTop(fe);
                if (double.IsNaN(left)) left = 0;
                if (double.IsNaN(top)) top = 0;
                return new System.Windows.Point(left, top);
            }
            return new System.Windows.Point(0, 0);
        }

        /// <summary>
        /// 移动选中的元素
        /// </summary>
        private void MoveSelectedElement(double deltaX, double deltaY)
        {
            if (_selectedElement == null) return;

            if (_selectedElement is Polyline polyline)
            {
                // 移动 Polyline 的所有点
                var newPoints = new PointCollection();
                foreach (var pt in polyline.Points)
                {
                    newPoints.Add(new System.Windows.Point(
                        _elementStartPosition.X + (pt.X - polyline.Points[0].X) + deltaX,
                        _elementStartPosition.Y + (pt.Y - polyline.Points[0].Y) + deltaY));
                }
                polyline.Points = newPoints;
            }
            else if (_selectedArrowLine != null && _selectedArrowHead != null)
            {
                // 移动箭头
                var newX1 = _elementStartPosition.X + deltaX;
                var newY1 = _elementStartPosition.Y + deltaY;
                var dx = _selectedArrowLine.X2 - _selectedArrowLine.X1;
                var dy = _selectedArrowLine.Y2 - _selectedArrowLine.Y1;
                
                _selectedArrowLine.X1 = newX1;
                _selectedArrowLine.Y1 = newY1;
                _selectedArrowLine.X2 = newX1 + dx;
                _selectedArrowLine.Y2 = newY1 + dy;
                
                // 更新箭头头部
                UpdateArrowHeadPosition(_selectedArrowLine, _selectedArrowHead);
            }
            else if (_selectedElement is FrameworkElement fe)
            {
                // 移动矩形、椭圆、文字等
                Canvas.SetLeft(fe, _elementStartPosition.X + deltaX);
                Canvas.SetTop(fe, _elementStartPosition.Y + deltaY);
            }
        }

        /// <summary>
        /// 更新箭头头部位置
        /// </summary>
        private void UpdateArrowHeadPosition(Line line, Polygon arrowHead)
        {
            var endPoint = new System.Windows.Point(line.X2, line.Y2);
            var startPoint = new System.Windows.Point(line.X1, line.Y1);
            
            var dx = endPoint.X - startPoint.X;
            var dy = endPoint.Y - startPoint.Y;
            var angle = Math.Atan2(dy, dx);
            
            var headLength = 12 + _strokeWidth * 2;
            
            var points = new PointCollection
            {
                endPoint,
                new System.Windows.Point(
                    endPoint.X - headLength * Math.Cos(angle - Math.PI / 6),
                    endPoint.Y - headLength * Math.Sin(angle - Math.PI / 6)),
                new System.Windows.Point(
                    endPoint.X - headLength * Math.Cos(angle + Math.PI / 6),
                    endPoint.Y - headLength * Math.Sin(angle + Math.PI / 6))
            };
            
            arrowHead.Points = points;
        }

        #endregion
    }
}
