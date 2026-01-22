using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using QRCoder;
using Wpf.Ui.Controls;

namespace Dorisoy.Meeting.Client.Views;

/// <summary>
/// 分享房间二维码窗口
/// </summary>
public partial class ShareRoomWindow : FluentWindow
{
    private readonly string _roomId;
    private readonly string _serverUrl;

    public ShareRoomWindow(string roomId, string serverUrl)
    {
        _roomId = roomId;
        _serverUrl = serverUrl;
        
        InitializeComponent();
        
        GenerateQrCode();
    }

    /// <summary>
    /// 生成二维码
    /// </summary>
    private void GenerateQrCode()
    {
        try
        {
            // 构建房间链接
            var roomLink = $"{_serverUrl}/join?room={_roomId}";
            
            // 显示房间号
            RoomIdText.Text = _roomId;
            
            // 生成二维码
            using var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(roomLink, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            var qrCodeBytes = qrCode.GetGraphic(10);
            
            // 转换为 BitmapImage
            using var ms = new MemoryStream(qrCodeBytes);
            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = ms;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            
            QrCodeImage.Source = bitmapImage;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"生成二维码失败: {ex.Message}", "错误", 
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 复制邀请信息
    /// </summary>
    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var roomLink = $"{_serverUrl}/join?room={_roomId}";
            var inviteText = $"邀请您加入会议\n房间号: {_roomId}\n链接: {roomLink}";
            
            Clipboard.SetText(inviteText);
            
            // 显示复制成功提示
            if (CopyButton is Wpf.Ui.Controls.Button btn)
            {
                var originalContent = btn.Content;
                btn.Content = "已复制 ✓";
                btn.IsEnabled = false;
                
                // 2秒后恢复
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };
                timer.Tick += (s, args) =>
                {
                    btn.Content = originalContent;
                    btn.IsEnabled = true;
                    timer.Stop();
                };
                timer.Start();
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"复制失败: {ex.Message}", "错误", 
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 点击窗口其他区域关闭（用于拖动）
    /// </summary>
    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // 拖动窗口
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    /// <summary>
    /// 窗口失去焦点时关闭
    /// </summary>
    private void Window_Deactivated(object sender, EventArgs e)
    {
        Close();
    }
}
