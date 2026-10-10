using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Dorisoy.Meeting.Client.Models;

namespace Dorisoy.Meeting.Client.Views;

/// <summary>
/// EmojiPickerWindow.xaml 的交互逻辑
/// </summary>
public partial class EmojiPickerWindow
{
    private static readonly string DefaultSettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Dorisoy.Meeting", "emoji_settings.json");
    private readonly string _settingsPath;
    private readonly Dictionary<string, EmojiOption> _catalog = CommonEmojis.Catalog.ToDictionary(e => e.Text);
    private EmojiPickerSettings _settings = new();
    private string _category = "全部";
    private bool _isReady;
    private Window? _owner;

    /// <summary>
    /// 选中的表情
    /// </summary>
    public string? SelectedEmoji { get; private set; }

    public event Action<string>? EmojiSelected;

    public EmojiPickerWindow() : this(DefaultSettingsPath)
    {
    }

    internal EmojiPickerWindow(string settingsPath)
    {
        _settingsPath = settingsPath;
        InitializeComponent();
        _isReady = true;
        RefreshEmojis();
    }

    /// <summary>
    /// 类别按钮切换
    /// </summary>
    private void CategoryButton_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isReady || sender is not RadioButton { Tag: string category }) return;
        _category = category;
        EmojiSearchBox.Clear();
        SearchArea.Visibility = Visibility.Collapsed;
        RefreshEmojis();
    }

    /// <summary>
    /// 表情按钮点击
    /// </summary>
    private void EmojiButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: EmojiOption emoji }) return;
        SelectedEmoji = emoji.Text;
        _settings.Recent.Remove(emoji.Text);
        _settings.Recent.Insert(0, emoji.Text);
        _settings.Recent = _settings.Recent.Take(10).ToList();
        SaveSettings();
        IsOpen = false;
        EmojiSelected?.Invoke(emoji.Text);
    }

    public void ShowFor(FrameworkElement anchor, PlacementMode placement = PlacementMode.Top)
    {
        PlacementTarget = anchor;
        Placement = placement;
        IsOpen = true;
    }

    private void Picker_Opened(object? sender, EventArgs e)
    {
        _owner = Window.GetWindow(PlacementTarget);
        if (_owner != null)
        {
            _owner.Deactivated += Owner_Dismiss;
            _owner.LocationChanged += Owner_Dismiss;
            _owner.SizeChanged += Owner_SizeChanged;
            _owner.Closed += Owner_Dismiss;
        }

        LoadSettings();
        SelectedEmoji = null;
        _category = "全部";
        AllCategoryButton.IsChecked = true;
        EmojiSearchBox.Clear();
        SearchArea.Visibility = Visibility.Collapsed;
        HintText.Text = "点击选择表情 · 右键收藏 / 取消收藏 · Esc 关闭";
        RefreshEmojis();
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (IsOpen) PickerSurface.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        }));
    }

    private void Picker_Closed(object? sender, EventArgs e)
    {
        if (_owner == null) return;
        _owner.Deactivated -= Owner_Dismiss;
        _owner.LocationChanged -= Owner_Dismiss;
        _owner.SizeChanged -= Owner_SizeChanged;
        _owner.Closed -= Owner_Dismiss;
        _owner = null;
    }

    private void Owner_Dismiss(object? sender, EventArgs e) => IsOpen = false;
    private void Owner_SizeChanged(object sender, SizeChangedEventArgs e) => IsOpen = false;

    private void RefreshEmojis()
    {
        if (!_isReady) return;
        var query = EmojiSearchBox.Text.Trim();
        IEnumerable<EmojiOption> items = CommonEmojis.Catalog;
        if (query.Length > 0)
        {
            items = items.Where(e => e.Text.Contains(query, StringComparison.OrdinalIgnoreCase)
                || e.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || e.Category.Contains(query, StringComparison.OrdinalIgnoreCase)
                || e.Keywords.Contains(query, StringComparison.OrdinalIgnoreCase));
        }
        else if (_category == "收藏")
        {
            items = _settings.Favorites.Where(_catalog.ContainsKey).Select(e => _catalog[e]);
        }
        else if (_category != "全部")
        {
            items = items.Where(e => e.Category == _category);
        }

        var result = items.ToList();
        EmojisControl.ItemsSource = result;
        SectionTitle.Text = query.Length > 0 ? $"搜索结果 · {result.Count}" :
            _category == "全部" ? "所有表情" : _category == "收藏" ? "我的收藏" : _category;
        RecentSection.Visibility = query.Length == 0 && _category == "全部" ? Visibility.Visible : Visibility.Collapsed;
        RecentEmojisControl.ItemsSource = _settings.Recent.Where(_catalog.ContainsKey).Select(e => _catalog[e]).ToList();
        RecentEmptyText.Visibility = _settings.Recent.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyMessage.Text = query.Length > 0 ? "没有找到相关表情，试试其他关键词" : "暂无收藏，右键点击表情即可添加";
        EmptyMessage.Visibility = result.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmojiScrollViewer.ScrollToTop();
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        SearchArea.Visibility = Visibility.Visible;
        EmojiSearchBox.Focus();
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e) => RefreshEmojis();

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        EmojiSearchBox.Clear();
        EmojiSearchBox.Focus();
    }

    private void EmojiButton_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Button { DataContext: EmojiOption emoji }) return;
        ToggleFavorite(emoji);
        e.Handled = true;
    }

    private void ToggleFavorite(EmojiOption emoji)
    {
        var offset = EmojiScrollViewer.VerticalOffset;
        var removed = _settings.Favorites.Remove(emoji.Text);
        if (!removed) _settings.Favorites.Insert(0, emoji.Text);
        var saved = SaveSettings();
        RefreshEmojis();
        if (_category != "收藏") EmojiScrollViewer.ScrollToVerticalOffset(offset);
        HintText.Text = saved ? $"{emoji.Name} · {(removed ? "已取消收藏" : "已加入收藏")}" : "收藏仅在本次生效，本地保存失败";
    }

    private void Picker_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            IsOpen = false;
            e.Handled = true;
        }
        else if ((e.Key == Key.Apps || (e.Key == Key.F10 && Keyboard.Modifiers == ModifierKeys.Shift))
                 && Keyboard.FocusedElement is Button { DataContext: EmojiOption emoji })
        {
            ToggleFavorite(emoji);
            e.Handled = true;
        }
    }

    private void LoadSettings()
    {
        try
        {
            _settings = File.Exists(_settingsPath)
                ? JsonSerializer.Deserialize<EmojiPickerSettings>(File.ReadAllText(_settingsPath)) ?? new()
                : new();
            _settings.Recent = (_settings.Recent ?? []).Where(_catalog.ContainsKey).Distinct().Take(10).ToList();
            _settings.Favorites = (_settings.Favorites ?? []).Where(_catalog.ContainsKey).Distinct().ToList();
        }
        catch (Exception ex)
        {
            _settings = new();
            Serilog.Log.Warning(ex, "读取表情偏好失败");
        }
    }

    private bool SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            var temporaryPath = _settingsPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(_settings));
            File.Move(temporaryPath, _settingsPath, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "保存表情偏好失败");
            return false;
        }
    }

    public sealed class EmojiPickerSettings
    {
        public List<string> Recent { get; set; } = [];
        public List<string> Favorites { get; set; } = [];
    }
}
