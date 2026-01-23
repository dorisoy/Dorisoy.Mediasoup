using System;
using System.Drawing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;

namespace Dorisoy.Meeting.Client.Views
{
    /// <summary>
    /// 屏幕截取选择遮罩窗口
    /// </summary>
    public partial class ScreenCaptureOverlay : FluentWindow
    {
        private bool _isSelecting;
        private System.Windows.Point _startPoint;
        private System.Windows.Point _endPoint;
        private Rect _selectionRect;
        
        /// <summary>
        /// 截取完成事件，返回截取的图片
        /// </summary>
        public event Action<BitmapSource>? CaptureCompleted;
        
        /// <summary>
        /// 截取取消事件
        /// </summary>
        public event Action? CaptureCancelled;

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
                CancelCapture();
            }
            else if (e.Key == Key.Enter && Toolbar.Visibility == Visibility.Visible)
            {
                ConfirmCapture();
            }
        }

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isSelecting = true;
            _startPoint = e.GetPosition(OverlayCanvas);
            _endPoint = _startPoint;
            
            HintText.Visibility = Visibility.Collapsed;
            Toolbar.Visibility = Visibility.Collapsed;
            SelectionBorder.Visibility = Visibility.Visible;
            SizeHint.Visibility = Visibility.Visible;
            
            OverlayCanvas.CaptureMouse();
            UpdateSelection();
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isSelecting) return;
            
            _endPoint = e.GetPosition(OverlayCanvas);
            UpdateSelection();
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isSelecting) return;
            
            _isSelecting = false;
            OverlayCanvas.ReleaseMouseCapture();
            
            _endPoint = e.GetPosition(OverlayCanvas);
            UpdateSelection();
            
            // 如果选择区域有效，显示工具栏
            if (_selectionRect.Width > 5 && _selectionRect.Height > 5)
            {
                ShowToolbar();
            }
        }

        private void UpdateSelection()
        {
            var x = Math.Min(_startPoint.X, _endPoint.X);
            var y = Math.Min(_startPoint.Y, _endPoint.Y);
            var width = Math.Abs(_endPoint.X - _startPoint.X);
            var height = Math.Abs(_endPoint.Y - _startPoint.Y);
            
            _selectionRect = new Rect(x, y, width, height);
            
            // 更新选择区域几何
            SelectionGeometry.Rect = _selectionRect;
            
            // 更新边框位置
            SelectionBorder.Margin = new Thickness(x, y, 0, 0);
            SelectionBorder.Width = width;
            SelectionBorder.Height = height;
            SelectionBorder.HorizontalAlignment = HorizontalAlignment.Left;
            SelectionBorder.VerticalAlignment = VerticalAlignment.Top;
            
            // 更新尺寸提示
            SizeText.Text = $"{(int)width} × {(int)height}";
            SizeHint.Margin = new Thickness(x, y - 28, 0, 0);
        }

        private void ShowToolbar()
        {
            // 工具栏显示在选择区域右下角
            var toolbarX = _selectionRect.Right - 80;
            var toolbarY = _selectionRect.Bottom + 8;
            
            // 确保工具栏不超出屏幕
            if (toolbarY + 50 > ActualHeight)
            {
                toolbarY = _selectionRect.Top - 50;
            }
            if (toolbarX < 0) toolbarX = 0;
            
            Toolbar.Margin = new Thickness(toolbarX, toolbarY, 0, 0);
            Toolbar.Visibility = Visibility.Visible;
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            ConfirmCapture();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            CancelCapture();
        }

        private void ConfirmCapture()
        {
            if (_selectionRect.Width < 5 || _selectionRect.Height < 5)
            {
                CancelCapture();
                return;
            }
            
            try
            {
                // 先隐藏窗口，避免截图包含遮罩
                Hide();
                
                // 等待窗口完全隐藏
                System.Threading.Thread.Sleep(100);
                
                // 获取 DPI 缩放
                var dpiScale = VisualTreeHelper.GetDpi(this);
                var scaleX = dpiScale.DpiScaleX;
                var scaleY = dpiScale.DpiScaleY;
                
                // 计算实际屏幕坐标
                int screenX = (int)(_selectionRect.X * scaleX);
                int screenY = (int)(_selectionRect.Y * scaleY);
                int screenWidth = (int)(_selectionRect.Width * scaleX);
                int screenHeight = (int)(_selectionRect.Height * scaleY);
                
                // 使用 GDI+ 截取屏幕区域
                using var bitmap = new Bitmap(screenWidth, screenHeight);
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(screenX, screenY, 0, 0, 
                        new System.Drawing.Size(screenWidth, screenHeight));
                }
                
                // 转换为 BitmapSource
                var bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    bitmap.GetHbitmap(),
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                bitmapSource.Freeze();
                
                CaptureCompleted?.Invoke(bitmapSource);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScreenCapture] 截图失败: {ex.Message}");
                CaptureCancelled?.Invoke();
            }
            finally
            {
                Close();
            }
        }

        private void CancelCapture()
        {
            CaptureCancelled?.Invoke();
            Close();
        }
    }
}
