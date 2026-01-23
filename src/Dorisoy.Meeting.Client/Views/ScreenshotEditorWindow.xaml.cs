using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace Dorisoy.Meeting.Client.Views
{
    /// <summary>
    /// 截图编辑窗口 - 支持绘图标记
    /// </summary>
    public partial class ScreenshotEditorWindow : FluentWindow
    {
        #region 枚举和字段

        /// <summary>
        /// 绘图工具类型
        /// </summary>
        private enum DrawingTool
        {
            Select,
            Pen,
            Rectangle,
            Ellipse,
            Arrow,
            Text
        }

        private DrawingTool _currentTool = DrawingTool.Select;
        private Color _currentColor = Colors.Red;
        private double _strokeWidth = 3;
        
        private bool _isDrawing;
        private Point _startPoint;
        private UIElement? _currentShape;
        private readonly List<UIElement> _drawingHistory = new();
        
        // 用于画笔工具的点集合
        private Polyline? _currentPolyline;
        
        // 用于箭头工具
        private Line? _arrowLine;
        private Polygon? _arrowHead;

        /// <summary>
        /// 截图完成事件，返回最终图片
        /// </summary>
        public event Action<BitmapSource>? ScreenshotCompleted;
        
        /// <summary>
        /// 取消事件
        /// </summary>
        public event Action? ScreenshotCancelled;

        #endregion

        #region 构造函数

        public ScreenshotEditorWindow()
        {
            InitializeComponent();
            KeyDown += OnKeyDown;
        }

        /// <summary>
        /// 设置截图
        /// </summary>
        public void SetScreenshot(BitmapSource screenshot)
        {
            BackgroundImage.Source = screenshot;
            BackgroundImage.Width = screenshot.PixelWidth;
            BackgroundImage.Height = screenshot.PixelHeight;
            DrawingCanvas.Width = screenshot.PixelWidth;
            DrawingCanvas.Height = screenshot.PixelHeight;
        }

        #endregion

        #region 事件处理

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                BtnCancel_Click(sender, e);
            }
            else if (e.Key == Key.Z && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                BtnUndo_Click(sender, e);
            }
            else if (e.Key == Key.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                BtnSave_Click(sender, e);
            }
            else if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                BtnConfirm_Click(sender, e);
            }
        }

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
            DrawingCanvas.Cursor = _currentTool == DrawingTool.Select ? Cursors.Arrow : Cursors.Cross;
        }

        private void Color_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radio && radio.Tag is string colorHex)
            {
                _currentColor = (Color)ColorConverter.ConvertFromString(colorHex);
            }
        }

        private void SliderStrokeWidth_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _strokeWidth = e.NewValue;
        }

        #endregion

        #region 画布事件

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 如果是文字工具，处理文字输入
            if (_currentTool == DrawingTool.Text)
            {
                ShowTextInput(e.GetPosition(DrawingCanvas));
                return;
            }
            
            if (_currentTool == DrawingTool.Select) return;
            
            _isDrawing = true;
            _startPoint = e.GetPosition(DrawingCanvas);
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
                    _currentPolyline.Points.Add(_startPoint);
                    DrawingCanvas.Children.Add(_currentPolyline);
                    _currentShape = _currentPolyline;
                    break;
                    
                case DrawingTool.Rectangle:
                    _currentShape = new Rectangle
                    {
                        Stroke = new SolidColorBrush(_currentColor),
                        StrokeThickness = _strokeWidth,
                        Fill = Brushes.Transparent
                    };
                    DrawingCanvas.Children.Add(_currentShape);
                    Canvas.SetLeft(_currentShape, _startPoint.X);
                    Canvas.SetTop(_currentShape, _startPoint.Y);
                    break;
                    
                case DrawingTool.Ellipse:
                    _currentShape = new Ellipse
                    {
                        Stroke = new SolidColorBrush(_currentColor),
                        StrokeThickness = _strokeWidth,
                        Fill = Brushes.Transparent
                    };
                    DrawingCanvas.Children.Add(_currentShape);
                    Canvas.SetLeft(_currentShape, _startPoint.X);
                    Canvas.SetTop(_currentShape, _startPoint.Y);
                    break;
                    
                case DrawingTool.Arrow:
                    // 创建箭头线
                    _arrowLine = new Line
                    {
                        Stroke = new SolidColorBrush(_currentColor),
                        StrokeThickness = _strokeWidth,
                        X1 = _startPoint.X,
                        Y1 = _startPoint.Y,
                        X2 = _startPoint.X,
                        Y2 = _startPoint.Y
                    };
                    DrawingCanvas.Children.Add(_arrowLine);
                    
                    // 创建箭头头部
                    _arrowHead = new Polygon
                    {
                        Fill = new SolidColorBrush(_currentColor)
                    };
                    DrawingCanvas.Children.Add(_arrowHead);
                    break;
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDrawing) return;
            
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

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDrawing) return;
            
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

        private void UpdateShapeSize(Point currentPoint)
        {
            if (_currentShape == null) return;
            
            var x = Math.Min(_startPoint.X, currentPoint.X);
            var y = Math.Min(_startPoint.Y, currentPoint.Y);
            var width = Math.Abs(currentPoint.X - _startPoint.X);
            var height = Math.Abs(currentPoint.Y - _startPoint.Y);
            
            Canvas.SetLeft(_currentShape, x);
            Canvas.SetTop(_currentShape, y);
            
            if (_currentShape is Rectangle rect)
            {
                rect.Width = width;
                rect.Height = height;
            }
            else if (_currentShape is Ellipse ellipse)
            {
                ellipse.Width = width;
                ellipse.Height = height;
            }
        }

        private void UpdateArrow(Point endPoint)
        {
            if (_arrowLine == null || _arrowHead == null) return;
            
            _arrowLine.X2 = endPoint.X;
            _arrowLine.Y2 = endPoint.Y;
            
            // 计算箭头头部
            var dx = endPoint.X - _startPoint.X;
            var dy = endPoint.Y - _startPoint.Y;
            var angle = Math.Atan2(dy, dx);
            
            var headLength = 12 + _strokeWidth * 2;
            var headWidth = 8 + _strokeWidth;
            
            var points = new PointCollection
            {
                endPoint,
                new Point(
                    endPoint.X - headLength * Math.Cos(angle - Math.PI / 6),
                    endPoint.Y - headLength * Math.Sin(angle - Math.PI / 6)),
                new Point(
                    endPoint.X - headLength * Math.Cos(angle + Math.PI / 6),
                    endPoint.Y - headLength * Math.Sin(angle + Math.PI / 6))
            };
            
            _arrowHead.Points = points;
        }

        #endregion

        #region 文字输入

        private void ShowTextInput(Point position)
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

        #region 工具栏按钮

        private void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            if (_drawingHistory.Count > 0)
            {
                var lastElement = _drawingHistory[^1];
                _drawingHistory.RemoveAt(_drawingHistory.Count - 1);
                DrawingCanvas.Children.Remove(lastElement);
                
                // 如果是箭头，还需要移除箭头头部
                if (lastElement is Polygon && _drawingHistory.Count > 0 && _drawingHistory[^1] is Line)
                {
                    var line = _drawingHistory[^1];
                    _drawingHistory.RemoveAt(_drawingHistory.Count - 1);
                    DrawingCanvas.Children.Remove(line);
                }
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
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
                    
                    var msgBox = new Wpf.Ui.Controls.MessageBox
                    {
                        Title = "保存成功",
                        Content = $"截图已保存到:\n{dialog.FileName}",
                        CloseButtonText = "确定"
                    };
                    _ = msgBox.ShowDialogAsync();
                }
                catch (Exception ex)
                {
                    var msgBox = new Wpf.Ui.Controls.MessageBox
                    {
                        Title = "保存失败",
                        Content = $"保存截图失败: {ex.Message}",
                        CloseButtonText = "确定"
                    };
                    _ = msgBox.ShowDialogAsync();
                }
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            ScreenshotCancelled?.Invoke();
            Close();
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var bitmap = RenderToBitmap();
                Clipboard.SetImage(bitmap);
                ScreenshotCompleted?.Invoke(bitmap);
                
                var msgBox = new Wpf.Ui.Controls.MessageBox
                {
                    Title = "复制成功",
                    Content = "截图已复制到剪贴板，可以在聊天中使用 Ctrl+V 粘贴发送",
                    CloseButtonText = "确定"
                };
                _ = msgBox.ShowDialogAsync();
            }
            catch (Exception ex)
            {
                var msgBox = new Wpf.Ui.Controls.MessageBox
                {
                    Title = "复制失败",
                    Content = $"复制到剪贴板失败: {ex.Message}",
                    CloseButtonText = "确定"
                };
                _ = msgBox.ShowDialogAsync();
            }
            
            Close();
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 将画布渲染为位图
        /// </summary>
        private RenderTargetBitmap RenderToBitmap()
        {
            // 隐藏文字输入框
            TextInputContainer.Visibility = Visibility.Collapsed;
            
            // 渲染整个 CanvasContainer（背景图 + 绘制层）
            var width = (int)CanvasContainer.ActualWidth;
            var height = (int)CanvasContainer.ActualHeight;
            
            if (width == 0 || height == 0)
            {
                width = (int)BackgroundImage.Width;
                height = (int)BackgroundImage.Height;
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
                var brush = new VisualBrush(CanvasContainer);
                context.DrawRectangle(brush, null, new Rect(0, 0, width * dpi.DpiScaleX, height * dpi.DpiScaleY));
            }
            
            bitmap.Render(drawingVisual);
            bitmap.Freeze();
            
            return bitmap;
        }

        /// <summary>
        /// 保存位图到文件
        /// </summary>
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
    }
}
