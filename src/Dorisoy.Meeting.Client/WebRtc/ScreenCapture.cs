using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Dorisoy.Meeting.Client.WebRtc;

/// <summary>
/// 屏幕捕获服务 - 使用 GDI+ 捕获屏幕内容
/// </summary>
public class ScreenCapture : IDisposable
{
    private readonly ILogger<ScreenCapture>? _logger;
    private Thread? _captureThread;
    private volatile bool _isCapturing;
    private readonly object _captureLock = new();
    
    // 捕获区域
    private Rectangle _captureRect;
    
    // 目标分辨率
    private int _targetWidth = 1280;
    private int _targetHeight = 720;
    
    // 帧率控制
    private int _targetFps = 15;
    private int _frameInterval => 1000 / _targetFps;
    
    // 是否绘制鼠标指针
    private bool _drawCursor = true;
    
    // Windows API 用于获取鼠标信息
    [DllImport("user32.dll")]
    private static extern bool GetCursorInfo(ref CURSORINFO pci);
    
    [DllImport("user32.dll")]
    private static extern bool DrawIcon(IntPtr hdc, int x, int y, IntPtr hIcon);
    
    [DllImport("user32.dll")]
    private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);
    
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);
    
    [DllImport("user32.dll")]
    private static extern IntPtr CopyIcon(IntPtr hIcon);
    
    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
    
    [StructLayout(LayoutKind.Sequential)]
    private struct CURSORINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hCursor;
        public POINTAPI ptScreenPos;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct POINTAPI
    {
        public int x;
        public int y;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }
    
    private const int CURSOR_SHOWING = 0x00000001;
    
    /// <summary>
    /// 捕获的原始图像数据事件 (BGR24 格式)
    /// </summary>
    public event Action<byte[], int, int>? OnFrameCaptured;
    
    /// <summary>
    /// 捕获的 WriteableBitmap 事件 (用于本地预览)
    /// </summary>
    public event Action<WriteableBitmap>? OnBitmapCaptured;
    
    /// <summary>
    /// 是否正在捕获
    /// </summary>
    public bool IsCapturing => _isCapturing;

    public ScreenCapture(ILogger<ScreenCapture>? logger = null)
    {
        _logger = logger;
        
        // 默认捕获主屏幕
        _captureRect = new Rectangle(0, 0, 
            (int)SystemParameters.PrimaryScreenWidth, 
            (int)SystemParameters.PrimaryScreenHeight);
    }
    
    /// <summary>
    /// 设置捕获区域
    /// </summary>
    public void SetCaptureRegion(Rectangle region)
    {
        lock (_captureLock)
        {
            _captureRect = region;
        }
    }
    
    /// <summary>
    /// 设置目标分辨率
    /// </summary>
    public void SetTargetResolution(int width, int height)
    {
        lock (_captureLock)
        {
            _targetWidth = width;
            _targetHeight = height;
        }
    }
    
    /// <summary>
    /// 设置目标帧率
    /// </summary>
    public void SetTargetFps(int fps)
    {
        if (fps < 1) fps = 1;
        if (fps > 60) fps = 60;
        _targetFps = fps;
    }
    
    /// <summary>
    /// 设置是否绘制鼠标指针
    /// </summary>
    public void SetDrawCursor(bool drawCursor)
    {
        _drawCursor = drawCursor;
    }
    
    /// <summary>
    /// 获取当前目标分辨率
    /// </summary>
    public (int Width, int Height) GetTargetResolution() => (_targetWidth, _targetHeight);
    
    /// <summary>
    /// 获取当前帧率
    /// </summary>
    public int GetTargetFps() => _targetFps;
    
    /// <summary>
    /// 获取是否绘制鼠标
    /// </summary>
    public bool GetDrawCursor() => _drawCursor;

    /// <summary>
    /// 开始屏幕捕获
    /// </summary>
    public void Start()
    {
        if (_isCapturing)
        {
            _logger?.LogWarning("屏幕捕获已在运行");
            return;
        }
        
        _isCapturing = true;
        _captureThread = new Thread(CaptureLoop)
        {
            IsBackground = true,
            Name = "ScreenCaptureThread",
            Priority = ThreadPriority.AboveNormal
        };
        _captureThread.Start();
        
        _logger?.LogInformation("屏幕捕获已启动, 区域: {Width}x{Height}, 目标: {TargetWidth}x{TargetHeight}, FPS: {Fps}",
            _captureRect.Width, _captureRect.Height, _targetWidth, _targetHeight, _targetFps);
    }
    
    /// <summary>
    /// 停止屏幕捕获
    /// </summary>
    public void Stop()
    {
        if (!_isCapturing)
        {
            return;
        }
        
        _isCapturing = false;
        
        if (_captureThread != null)
        {
            if (!_captureThread.Join(2000))
            {
                _logger?.LogWarning("屏幕捕获线程未能及时停止");
            }
            _captureThread = null;
        }
        
        _logger?.LogInformation("屏幕捕获已停止");
    }
    
    /// <summary>
    /// 捕获循环
    /// </summary>
    private void CaptureLoop()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        while (_isCapturing)
        {
            var frameStart = stopwatch.ElapsedMilliseconds;
            
            try
            {
                CaptureFrame();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "捕获帧失败");
            }
            
            // 帧率控制
            var elapsed = stopwatch.ElapsedMilliseconds - frameStart;
            var delay = _frameInterval - (int)elapsed;
            if (delay > 0)
            {
                Thread.Sleep(delay);
            }
        }
    }
    
    /// <summary>
    /// 捕获单帧
    /// </summary>
    private void CaptureFrame()
    {
        Rectangle rect;
        int targetW, targetH;
        
        lock (_captureLock)
        {
            rect = _captureRect;
            targetW = _targetWidth;
            targetH = _targetHeight;
        }
        
        // 捕获屏幕
        using var screenBitmap = new Bitmap(rect.Width, rect.Height, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(screenBitmap))
        {
            graphics.CopyFromScreen(rect.X, rect.Y, 0, 0, rect.Size, CopyPixelOperation.SourceCopy);
            
            // 绘制鼠标指针
            if (_drawCursor)
            {
                DrawMouseCursor(graphics, rect.X, rect.Y);
            }
        }
        
        // 缩放到目标分辨率
        Bitmap? resizedBitmap = null;
        Bitmap targetBitmap;
        
        if (rect.Width != targetW || rect.Height != targetH)
        {
            resizedBitmap = new Bitmap(targetW, targetH, PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(resizedBitmap))
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                graphics.DrawImage(screenBitmap, 0, 0, targetW, targetH);
            }
            targetBitmap = resizedBitmap;
        }
        else
        {
            targetBitmap = screenBitmap;
        }
        
        try
        {
            // 提取 BGR 数据
            var bitmapData = targetBitmap.LockBits(
                new Rectangle(0, 0, targetBitmap.Width, targetBitmap.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format24bppRgb);
            
            try
            {
                var stride = bitmapData.Stride;
                var width = bitmapData.Width;
                var height = bitmapData.Height;
                
                // 分配输出缓冲区 (BGR24)
                var dataSize = width * height * 3;
                var imageData = new byte[dataSize];
                
                // 复制数据（需要处理 stride 对齐）
                var srcPtr = bitmapData.Scan0;
                var srcStride = bitmapData.Stride;
                var dstStride = width * 3;
                
                for (int y = 0; y < height; y++)
                {
                    Marshal.Copy(srcPtr + y * srcStride, imageData, y * dstStride, dstStride);
                }
                
                // 触发原始数据事件
                OnFrameCaptured?.Invoke(imageData, width, height);
                
                // 如果有 UI 预览订阅者，在 UI 线程创建 WriteableBitmap
                if (OnBitmapCaptured != null)
                {
                    var capturedData = imageData;
                    var capturedWidth = width;
                    var capturedHeight = height;
                    
                    Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        try
                        {
                            var wpfBitmap = new WriteableBitmap(
                                capturedWidth, capturedHeight,
                                96, 96,
                                System.Windows.Media.PixelFormats.Bgr24,
                                null);
                            
                            wpfBitmap.Lock();
                            try
                            {
                                var backBuffer = wpfBitmap.BackBuffer;
                                var wpfStride = wpfBitmap.BackBufferStride;
                                var sourceStride = capturedWidth * 3;
                                
                                for (int y = 0; y < capturedHeight; y++)
                                {
                                    Marshal.Copy(capturedData, y * sourceStride, 
                                        backBuffer + y * wpfStride, 
                                        Math.Min(sourceStride, wpfStride));
                                }
                                
                                wpfBitmap.AddDirtyRect(new Int32Rect(0, 0, capturedWidth, capturedHeight));
                            }
                            finally
                            {
                                wpfBitmap.Unlock();
                            }
                            
                            OnBitmapCaptured?.Invoke(wpfBitmap);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "创建预览位图失败");
                        }
                    });
                }
            }
            finally
            {
                targetBitmap.UnlockBits(bitmapData);
            }
        }
        finally
        {
            resizedBitmap?.Dispose();
        }
    }
    
    /// <summary>
    /// 捕获单帧并返回（同步方法）
    /// </summary>
    public byte[]? CaptureOneFrame(out int width, out int height)
    {
        width = 0;
        height = 0;
        
        try
        {
            Rectangle rect;
            int targetW, targetH;
            
            lock (_captureLock)
            {
                rect = _captureRect;
                targetW = _targetWidth;
                targetH = _targetHeight;
            }
            
            using var screenBitmap = new Bitmap(rect.Width, rect.Height, PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(screenBitmap))
            {
                graphics.CopyFromScreen(rect.X, rect.Y, 0, 0, rect.Size, CopyPixelOperation.SourceCopy);
                
                // 绘制鼠标指针
                if (_drawCursor)
                {
                    DrawMouseCursor(graphics, rect.X, rect.Y);
                }
            }
            
            Bitmap? resizedBitmap = null;
            Bitmap targetBitmap;
            
            if (rect.Width != targetW || rect.Height != targetH)
            {
                resizedBitmap = new Bitmap(targetW, targetH, PixelFormat.Format24bppRgb);
                using (var graphics = Graphics.FromImage(resizedBitmap))
                {
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                    graphics.DrawImage(screenBitmap, 0, 0, targetW, targetH);
                }
                targetBitmap = resizedBitmap;
            }
            else
            {
                targetBitmap = screenBitmap;
            }
            
            try
            {
                var bitmapData = targetBitmap.LockBits(
                    new Rectangle(0, 0, targetBitmap.Width, targetBitmap.Height),
                    ImageLockMode.ReadOnly,
                    PixelFormat.Format24bppRgb);
                
                try
                {
                    width = bitmapData.Width;
                    height = bitmapData.Height;
                    
                    var dataSize = width * height * 3;
                    var imageData = new byte[dataSize];
                    
                    var srcPtr = bitmapData.Scan0;
                    var srcStride = bitmapData.Stride;
                    var dstStride = width * 3;
                    
                    for (int y = 0; y < height; y++)
                    {
                        Marshal.Copy(srcPtr + y * srcStride, imageData, y * dstStride, dstStride);
                    }
                    
                    return imageData;
                }
                finally
                {
                    targetBitmap.UnlockBits(bitmapData);
                }
            }
            finally
            {
                resizedBitmap?.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "单帧捕获失败");
            return null;
        }
    }
    
    public void Dispose()
    {
        Stop();
    }
    
    /// <summary>
    /// 绘制鼠标指针到图形上
    /// </summary>
    private void DrawMouseCursor(Graphics graphics, int offsetX, int offsetY)
    {
        try
        {
            var cursorInfo = new CURSORINFO();
            cursorInfo.cbSize = Marshal.SizeOf(cursorInfo);
            
            if (GetCursorInfo(ref cursorInfo) && cursorInfo.flags == CURSOR_SHOWING)
            {
                // 复制鼠标图标
                var iconCopy = CopyIcon(cursorInfo.hCursor);
                if (iconCopy != IntPtr.Zero)
                {
                    try
                    {
                        // 获取鼠标热点信息
                        if (GetIconInfo(iconCopy, out ICONINFO iconInfo))
                        {
                            try
                            {
                                // 计算鼠标在截图中的位置
                                int cursorX = cursorInfo.ptScreenPos.x - offsetX - iconInfo.xHotspot;
                                int cursorY = cursorInfo.ptScreenPos.y - offsetY - iconInfo.yHotspot;
                                
                                // 绘制鼠标
                                IntPtr hdc = graphics.GetHdc();
                                try
                                {
                                    DrawIcon(hdc, cursorX, cursorY, iconCopy);
                                }
                                finally
                                {
                                    graphics.ReleaseHdc(hdc);
                                }
                            }
                            finally
                            {
                                // 清理 ICONINFO 中的位图资源
                                if (iconInfo.hbmMask != IntPtr.Zero)
                                    DeleteObject(iconInfo.hbmMask);
                                if (iconInfo.hbmColor != IntPtr.Zero)
                                    DeleteObject(iconInfo.hbmColor);
                            }
                        }
                    }
                    finally
                    {
                        DestroyIcon(iconCopy);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogTrace(ex, "绘制鼠标指针失败");
        }
    }
}
