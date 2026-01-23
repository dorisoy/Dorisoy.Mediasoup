using Dorisoy.Meeting.Client.ViewModels;
using Microsoft.Win32;

namespace Dorisoy.Meeting.Client.Views;

/// <summary>
/// SettingPage.xaml 的交互逻辑
/// </summary>
public partial class SettingPage
{
    private readonly MainViewModel _viewModel;
    
    public SettingPage(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    /// <summary>
    /// 关闭按钮点击
    /// </summary>
    private void CloseButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        Close();
    }
    
    /// <summary>
    /// 浏览录制存储目录
    /// </summary>
    private void BrowseRecordingPath_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        // 使用 .NET 8 WPF 原生的 OpenFolderDialog
        var dialog = new OpenFolderDialog
        {
            Title = "选择录制视频保存目录",
            Multiselect = false
        };
        
        if (!string.IsNullOrEmpty(_viewModel.RecordingSavePath) && 
            System.IO.Directory.Exists(_viewModel.RecordingSavePath))
        {
            dialog.InitialDirectory = _viewModel.RecordingSavePath;
        }
        
        if (dialog.ShowDialog() == true)
        {
            _viewModel.RecordingSavePath = dialog.FolderName;
        }
    }
}
