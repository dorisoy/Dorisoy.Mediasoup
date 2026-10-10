#:property TargetFramework=net10.0-windows10.0.17763.0
#:property UseWPF=true
#:property PublishAot=false
#:property PublishTrimmed=false
#:project ../src/Dorisoy.Meeting.Client/Dorisoy.Meeting.Client.csproj

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Markup;
using System.Xml.Linq;
using Dorisoy.Meeting.Client.Models;
using Dorisoy.Meeting.Client.Views;

internal static class EmojiSmoke
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        Console.WriteLine("PASS: " + description);
    }
    private static object? Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, Private)!.Invoke(target, args);
    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
    private static EmojiPickerWindow CreatePicker(string path) => (EmojiPickerWindow)Activator.CreateInstance(
        typeof(EmojiPickerWindow), Private, null, new object[] { path }, null)!;
    private static List<EmojiOption> Items(EmojiPickerWindow picker, string name) =>
        ((ItemsControl)picker.FindName(name)).Items.Cast<EmojiOption>().ToList();

    [STAThread]
    public static void Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary
            { Theme = Wpf.Ui.Appearance.ApplicationTheme.Dark });
        app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
        var source = XDocument.Load("src/Dorisoy.Meeting.Client/App.xaml");
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        foreach (var element in source.Descendants().Where(e => e.Name.NamespaceName.EndsWith(".Converters")))
        {
            var type = typeof(ChatPanel).Assembly.GetType("Dorisoy.Meeting.Client.Converters." + element.Name.LocalName)!;
            app.Resources[(string)element.Attribute(x + "Key")!] = Activator.CreateInstance(type)!;
        }

        Check(CommonEmojis.Catalog.Count >= 150, "表情目录不少于150项");
        Check(CommonEmojis.Catalog.Select(e => e.Text).Distinct().Count() == CommonEmojis.Catalog.Count, "目录无重复表情");
        var path = Path.GetFullPath(".qoder/emoji-smoke-settings.json");
        File.WriteAllText(path, "{\"Recent\":[],\"Favorites\":[]}");
        var picker = CreatePicker(path);
        var search = (TextBox)picker.FindName("EmojiSearchBox");
        search.Text = "握手";
        Check(Items(picker, "EmojisControl").Single().Text == "🤝", "中文搜索");
        search.Text = "coffee";
        Check(Items(picker, "EmojisControl").Single().Text == "☕", "英文搜索");
        search.Text = "不存在的表情关键词";
        Check(Items(picker, "EmojisControl").Count == 0, "无搜索结果状态");
        search.Clear();
        var favorite = CommonEmojis.Catalog.First(e => e.Text == "🤝");
        Call(picker, "ToggleFavorite", favorite);
        Call(picker, "CategoryButton_Checked", new RadioButton { Tag = "收藏" }, new RoutedEventArgs());
        Check(Items(picker, "EmojisControl").Single().Text == favorite.Text, "收藏分类");
        var emitted = "";
        picker.EmojiSelected += value => emitted = value;
        for (var index = 0; index < 12; index++)
            Call(picker, "EmojiButton_Click", new Button { DataContext = CommonEmojis.Catalog[index] }, new RoutedEventArgs());
        Check(emitted == CommonEmojis.Catalog[11].Text, "选择事件携带Unicode文本");
        Call(picker, "EmojiButton_Click", new Button { DataContext = CommonEmojis.Catalog[11] }, new RoutedEventArgs());
        var restored = CreatePicker(path);
        Call(restored, "LoadSettings");
        Call(restored, "RefreshEmojis");
        var recent = Items(restored, "RecentEmojisControl");
        Check(recent.Count == 10 && recent[0].Text == emitted, "最近使用持久化、去重排序及10项上限");
        Call(restored, "CategoryButton_Checked", new RadioButton { Tag = "收藏" }, new RoutedEventArgs());
        Check(Items(restored, "EmojisControl").Single().Text == favorite.Text, "收藏持久化恢复");
        Call(restored, "ToggleFavorite", favorite);
        Check(Items(restored, "EmojisControl").Count == 0, "取消收藏");
        File.WriteAllText(path, "无效的JSON");
        Call(restored, "LoadSettings");
        Call(restored, "RefreshEmojis");
        Check(Items(restored, "RecentEmojisControl").Count == 0, "损坏配置安全回退");

        var panel = new ChatPanel();
        var input = (TextBox)panel.FindName("MessageInput");
        input.Text = "你好世界";
        input.Select(2, 0);
        Call(panel, "InsertEmoji", "😊");
        Check(input.Text == "你好😊世界", "按光标位置插入且保留草稿");
        input.Select(2, 2);
        Call(panel, "InsertEmoji", "❤️");
        Check(input.Text == "你好❤️世界", "替换选中内容且不破坏Unicode");

        var host = new Window { Width = 1100, Height = 800, Left = -10000, Top = -10000,
            ShowInTaskbar = false, Content = panel };
        try
        {
            host.Show();
            Pump();
            var toggle = (ToggleButton)panel.FindName("EmojiToggle");
            var chatPicker = (EmojiPickerWindow)panel.FindName("ChatEmojiPicker");
            toggle.IsChecked = true;
            Pump();
            Check(chatPicker.IsOpen && chatPicker.PlacementTarget == toggle, "聊天按钮打开锚定浮层");
            chatPicker.IsOpen = false;
            Pump();
            Check(toggle.IsChecked == false, "浮层关闭同步按钮状态");
            toggle.IsChecked = true;
            Pump();
            var keyArgs = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(host)!, 0, System.Windows.Input.Key.Escape);
            Call(chatPicker, "Picker_KeyDown", chatPicker, keyArgs);
            Pump();
            Check(!chatPicker.IsOpen && keyArgs.Handled, "Esc关闭浮层");
            for (var index = 0; index < 10; index++)
                Call(picker, "EmojiButton_Click", new Button { DataContext = CommonEmojis.Catalog[index] }, new RoutedEventArgs());
            picker.ShowFor(toggle);
            Pump();
            var surface = (FrameworkElement)picker.FindName("PickerSurface");
            surface.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(surface.ActualWidth + surface.Margin.Left + surface.Margin.Right),
                (int)Math.Ceiling(surface.ActualHeight + surface.Margin.Top + surface.Margin.Bottom), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(surface);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(".qoder/emoji-picker-smoke.png")) encoder.Save(stream);
            var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
            var colored = 0;
            for (var i = 0; i < pixels.Length; i += 4)
                if (pixels[i + 2] > 180 && pixels[i + 1] > 100 && pixels[i] < 100) colored++;
            Check(colored > 500, "彩色Emoji渲染而非单色字形");
            host.Close();
            Pump();
            Check(!picker.IsOpen, "宿主关闭时浮层关闭");
        }
        finally
        {
            picker.IsOpen = false;
            host.Close();
            app.Shutdown();
        }
        Console.WriteLine("ALL EMOJI SMOKE TESTS PASSED");
    }
}
