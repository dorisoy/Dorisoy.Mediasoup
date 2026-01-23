using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace Dorisoy.Meeting.Client.Views;

/// <summary>
/// 图片预览窗口
/// </summary>
public partial class ImagePreviewWindow : FluentWindow
{
    private readonly BitmapSource? _imageSource;
    private readonly string? _fileName;
    private readonly string? _filePath;
    private readonly long _fileSize;
    
    private double _zoomLevel = 1.0;
    private const double ZoomStep = 0.1;
    private const double MinZoom = 0.1;
    private const double MaxZoom = 5.0;
    
    // 拖动支持
    private bool _isDragging;
    private Point _dragStartPoint;
    private Point _scrollStartOffset;

    public ImagePreviewWindow(BitmapSource imageSource, string? fileName = null, string? filePath = null, long fileSize = 0)
    {
        _imageSource = imageSource;
        _fileName = fileName;
        _filePath = filePath;
        _fileSize = fileSize;
        
        InitializeComponent();
        
        Loaded += ImagePreviewWindow_Loaded;
    }

    private void ImagePreviewWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_imageSource == null) return;
        
        PreviewImage.Source = _imageSource;
        
        // 设置文件名
        FileNameText.Text = string.IsNullOrEmpty(_fileName) ? "图片预览" : _fileName;
        Title = FileNameText.Text;
        
        // 设置图片尺寸信息
        ImageSizeText.Text = $"尺寸: {_imageSource.PixelWidth} × {_imageSource.PixelHeight}";
        
        // 设置文件大小信息
        if (_fileSize > 0)
        {
            FileSizeText.Text = $"大小: {FormatFileSize(_fileSize)}";
        }
        else
        {
            // 估算图片大小
            var estimatedSize = _imageSource.PixelWidth * _imageSource.PixelHeight * 4; // ARGB
            FileSizeText.Text = $"大小: 约 {FormatFileSize(estimatedSize)}";
        }
        
        // 自动适应窗口大小
        Dispatcher.BeginInvoke(new Action(FitToWindow), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// 格式化文件大小
    /// </summary>
    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// 更新缩放显示
    /// </summary>
    private void UpdateZoom()
    {
        ImageScale.ScaleX = _zoomLevel;
        ImageScale.ScaleY = _zoomLevel;
        ZoomText.Text = $"{(int)(_zoomLevel * 100)}%";
    }

    /// <summary>
    /// 放大
    /// </summary>
    private void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        _zoomLevel = Math.Min(MaxZoom, _zoomLevel + ZoomStep);
        UpdateZoom();
    }

    /// <summary>
    /// 缩小
    /// </summary>
    private void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        _zoomLevel = Math.Max(MinZoom, _zoomLevel - ZoomStep);
        UpdateZoom();
    }

    /// <summary>
    /// 适应窗口
    /// </summary>
    private void FitToWindow_Click(object sender, RoutedEventArgs e)
    {
        FitToWindow();
    }

    private void FitToWindow()
    {
        if (_imageSource == null) return;
        
        var viewportWidth = ImageScrollViewer.ActualWidth - 20;
        var viewportHeight = ImageScrollViewer.ActualHeight - 20;
        
        if (viewportWidth <= 0 || viewportHeight <= 0) return;
        
        var scaleX = viewportWidth / _imageSource.PixelWidth;
        var scaleY = viewportHeight / _imageSource.PixelHeight;
        
        _zoomLevel = Math.Min(scaleX, scaleY);
        _zoomLevel = Math.Max(MinZoom, Math.Min(MaxZoom, _zoomLevel));
        
        UpdateZoom();
    }

    /// <summary>
    /// 原始大小
    /// </summary>
    private void ActualSize_Click(object sender, RoutedEventArgs e)
    {
        _zoomLevel = 1.0;
        UpdateZoom();
    }

    /// <summary>
    /// 保存图片
    /// </summary>
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_imageSource == null) return;
        
        var dialog = new SaveFileDialog
        {
            Title = "保存图片",
            FileName = _fileName ?? "image.png",
            Filter = "PNG 图片|*.png|JPEG 图片|*.jpg;*.jpeg|BMP 图片|*.bmp|所有文件|*.*",
            DefaultExt = ".png"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                BitmapEncoder encoder = Path.GetExtension(dialog.FileName).ToLower() switch
                {
                    ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = 95 },
                    ".bmp" => new BmpBitmapEncoder(),
                    _ => new PngBitmapEncoder()
                };

                encoder.Frames.Add(BitmapFrame.Create(_imageSource));

                using var stream = new FileStream(dialog.FileName, FileMode.Create);
                encoder.Save(stream);

                var successBox = new Wpf.Ui.Controls.MessageBox
                {
                    Title = "保存成功",
                    Content = $"图片已保存到:\n{dialog.FileName}",
                    CloseButtonText = "确定"
                };
                _ = successBox.ShowDialogAsync();
            }
            catch (Exception ex)
            {
                var errorBox = new Wpf.Ui.Controls.MessageBox
                {
                    Title = "保存失败",
                    Content = $"保存图片失败: {ex.Message}",
                    CloseButtonText = "确定"
                };
                _ = errorBox.ShowDialogAsync();
            }
        }
    }

    /// <summary>
    /// 复制到剪贴板
    /// </summary>
    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (_imageSource == null) return;
        
        try
        {
            Clipboard.SetImage(_imageSource);
            
            // 简单提示
            var originalText = ZoomText.Text;
            ZoomText.Text = "已复制!";
            
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            timer.Tick += (s, args) =>
            {
                ZoomText.Text = originalText;
                timer.Stop();
            };
            timer.Start();
        }
        catch (Exception ex)
        {
            var errorBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = "复制失败",
                Content = $"复制图片失败: {ex.Message}",
                CloseButtonText = "确定"
            };
            _ = errorBox.ShowDialogAsync();
        }
    }

    /// <summary>
    /// 鼠标滚轮缩放
    /// </summary>
    private void ImageScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (e.Delta > 0)
                _zoomLevel = Math.Min(MaxZoom, _zoomLevel + ZoomStep);
            else
                _zoomLevel = Math.Max(MinZoom, _zoomLevel - ZoomStep);
            
            UpdateZoom();
            e.Handled = true;
        }
    }

    /// <summary>
    /// 键盘快捷键
    /// </summary>
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                break;
            case Key.OemPlus or Key.Add:
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    _zoomLevel = Math.Min(MaxZoom, _zoomLevel + ZoomStep);
                    UpdateZoom();
                }
                break;
            case Key.OemMinus or Key.Subtract:
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    _zoomLevel = Math.Max(MinZoom, _zoomLevel - ZoomStep);
                    UpdateZoom();
                }
                break;
            case Key.D0 or Key.NumPad0:
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    _zoomLevel = 1.0;
                    UpdateZoom();
                }
                break;
            case Key.S:
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    Save_Click(sender, new RoutedEventArgs());
                }
                break;
            case Key.C:
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    Copy_Click(sender, new RoutedEventArgs());
                }
                break;
        }
    }

    /// <summary>
    /// 拖动开始
    /// </summary>
    private void ImageContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // 双击切换适应/原始大小
            if (Math.Abs(_zoomLevel - 1.0) < 0.01)
                FitToWindow();
            else
            {
                _zoomLevel = 1.0;
                UpdateZoom();
            }
            return;
        }
        
        _isDragging = true;
        _dragStartPoint = e.GetPosition(ImageScrollViewer);
        _scrollStartOffset = new Point(ImageScrollViewer.HorizontalOffset, ImageScrollViewer.VerticalOffset);
        ImageContainer.CaptureMouse();
        ImageContainer.Cursor = Cursors.Hand;
    }

    /// <summary>
    /// 拖动结束
    /// </summary>
    private void ImageContainer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        ImageContainer.ReleaseMouseCapture();
        ImageContainer.Cursor = Cursors.Arrow;
    }

    /// <summary>
    /// 拖动移动
    /// </summary>
    private void ImageContainer_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;
        
        var currentPoint = e.GetPosition(ImageScrollViewer);
        var delta = currentPoint - _dragStartPoint;
        
        ImageScrollViewer.ScrollToHorizontalOffset(_scrollStartOffset.X - delta.X);
        ImageScrollViewer.ScrollToVerticalOffset(_scrollStartOffset.Y - delta.Y);
    }
}
