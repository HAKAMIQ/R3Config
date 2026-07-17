using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using R3Config;
using R3Config.Lang;
using R3Config.Svc;
using R3Config.View;
using R3Config.View.Dlg;
using R3Config.Vm;

internal static class UiGate
{
    public static void Run()
    {
        Xaml();
        Visual();
    }

    private static void Xaml()
    {
        var repo = Repo();
        var appDir = Path.Combine(repo, "App");
        var files = Directory.GetFiles(
            appDir,
            "*.xaml",
            SearchOption.AllDirectories);
        var errs = new List<string>();
        var xkey = XName.Get(
            "Key",
            "http://schemas.microsoft.com/winfx/2006/xaml");
        var ctrl = XDocument.Load(
            Path.Combine(appDir, "Thm", "Ctrl.xaml"),
            LoadOptions.SetLineInfo);
        var keys = ctrl
            .Descendants()
            .Where(node => node.Name.LocalName == "Style")
            .Select(node => node.Attribute(xkey)?.Value)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.Ordinal);

        var themeKeys = new[]
        {
            Path.Combine(appDir, "Thm", "Colors.xaml"),
            Path.Combine(appDir, "Thm", "Type.xaml"),
            Path.Combine(appDir, "Thm", "Ctrl.xaml")
        }
        .SelectMany(path => XDocument.Load(path).Descendants())
        .Select(node => node.Attribute(xkey)?.Value)
        .Where(key => !string.IsNullOrWhiteSpace(key))
        .ToHashSet(StringComparer.Ordinal);
        var required = new[]
        {
            "Surface.Background", "Surface.Card", "Surface.CardHover", "Surface.CardPressed",
            "Surface.Subtle", "Surface.Overlay", "Text.Primary", "Text.Secondary",
            "Text.Disabled", "Text.OnAccent", "Stroke.Default", "Stroke.Subtle",
            "Stroke.Strong", "Stroke.Focus", "Accent.Default", "Accent.Hover",
            "Accent.Pressed", "Accent.Disabled", "Status.Success", "Status.Warning",
            "Status.Danger", "Status.Info", "Type.Display", "Type.TitleLarge",
            "Type.Title", "Type.Subtitle", "Type.Body", "Type.BodyStrong",
            "Type.Caption", "Type.Technical", "Space.4", "Space.8", "Space.12",
            "Space.16", "Space.20", "Space.24", "Space.32", "Radius.Small",
            "Radius.Medium", "Radius.Large", "Radius.XLarge", "Elevation.1",
            "Elevation.2", "Elevation.3", "Motion.Hover", "Motion.Press",
            "Motion.Surface", "FluentButton.Primary", "FluentButton.Secondary",
            "FluentButton.Subtle", "FluentButton.Danger", "FluentButton.Icon",
            "FluentButton.Compact", "FluentDataGrid", "FluentDataGridHeader",
            "FluentDataGridCell", "FluentDataGridRow", "FluentTabControl", "FluentTabItem"
        };
        foreach (var key in required.Where(key => !themeKeys.Contains(key)))
        {
            errs.Add($"Missing Fluent 2 contract '{key}'.");
        }

        foreach (var mode in new[] { "Dark", "Light" })
        {
            foreach (var key in new[]
                     {
                         "Surface.Background", "Surface.Card", "Text.Primary", "Text.Secondary",
                         "Stroke.Default", "Stroke.Focus", "Accent.Default", "Status.Success",
                         "Status.Warning", "Status.Danger", "Status.Info"
                     })
            {
                if (!themeKeys.Contains($"Fluent.{mode}.{key}.Color"))
                {
                    errs.Add($"Missing Fluent {mode} palette token '{key}'.");
                }
            }
        }

        var focusContracts = new (string Target, string? Key)[]
        {
            ("Button", null),
            ("TextBox", null),
            ("ComboBox", null),
            ("CheckBox", null),
            ("TabItem", "FluentTabItem")
        };
        foreach (var contract in focusContracts)
        {
            var style = ctrl.Descendants()
                .FirstOrDefault(node =>
                    node.Name.LocalName == "Style" &&
                    Target(node.Attribute("TargetType")?.Value, contract.Target) &&
                    string.Equals(node.Attribute(xkey)?.Value, contract.Key, StringComparison.Ordinal));
            var focus = style?.Descendants()
                .Any(node =>
                    node.Name.LocalName == "Trigger" &&
                    node.Attribute("Property")?.Value is "IsKeyboardFocused" or "IsKeyboardFocusWithin") == true;
            if (!focus)
            {
                errs.Add($"Fluent {contract.Target} contract requires a visible keyboard focus state.");
            }
        }

        foreach (var file in files)
        {
            var rel = Path.GetRelativePath(repo, file);
            var text = File.ReadAllText(file);
            var doc = XDocument.Parse(text, LoadOptions.SetLineInfo);

            if (Regex.IsMatch(
                    text,
                    "TextAlignment\\s*=\\s*\"(?:Center|Right|Justify)\"|FlowDirection\\s*=\\s*\"RightToLeft\"",
                    RegexOptions.CultureInvariant))
            {
                errs.Add($"{rel}: forbidden RTL/right/justify value.");
            }

            foreach (var attr in doc.Descendants().Attributes())
            {
                var val = attr.Value.Trim();
                if (attr.Name.LocalName.EndsWith("Margin", StringComparison.Ordinal) &&
                    Regex.IsMatch(val, "(?:^|,)\\s*-\\d", RegexOptions.CultureInvariant))
                {
                    errs.Add($"{rel}: negative margin '{val}'.");
                }

                if (!rel.EndsWith(
                        Path.Combine("Thm", "Colors.xaml"),
                        StringComparison.OrdinalIgnoreCase) &&
                    IsColor(attr.Name.LocalName, val))
                {
                    errs.Add($"{rel}: literal color '{val}' outside Colors.xaml.");
                }

                if (!rel.EndsWith(
                        Path.Combine("Thm", "Ctrl.xaml"),
                        StringComparison.OrdinalIgnoreCase) &&
                    attr.Name.LocalName == "CornerRadius" &&
                    attr.Parent?.Name.LocalName != "WindowChrome" &&
                    !val.StartsWith('{'))
                {
                    errs.Add($"{rel}: literal CornerRadius '{val}' outside Fluent tokens.");
                }
            }

            foreach (var button in doc.Descendants()
                         .Where(node => node.Name.LocalName == "Button" &&
                                        (node.Attribute("Style")?.Value.Contains("FluentButton.Icon", StringComparison.Ordinal) == true ||
                                         node.Attribute("Style")?.Value.Contains("FluentButton.Close", StringComparison.Ordinal) == true)))
            {
                var raw = button.ToString(SaveOptions.DisableFormatting);
                if (button.Attribute("ToolTip") is null ||
                    !raw.Contains("AutomationProperties.Name=", StringComparison.Ordinal))
                {
                    errs.Add($"{rel}: icon button requires ToolTip and AutomationProperties.Name.");
                }
            }

            foreach (var grid in doc.Descendants()
                         .Where(node => node.Name.LocalName == "DataGrid"))
            {
                var hpol = grid.Attributes().Any(attr =>
                    attr.Name.LocalName == "ScrollViewer.HorizontalScrollBarVisibility");
                var vpol = grid.Attributes().Any(attr =>
                    attr.Name.LocalName == "ScrollViewer.VerticalScrollBarVisibility");
                if (!hpol || !vpol)
                {
                    errs.Add($"{rel}: DataGrid must declare horizontal and vertical scroll policy.");
                }

                var direct = grid.Attribute("Style")?.Value.Contains(
                    "FluentDataGrid",
                    StringComparison.Ordinal) == true;
                var local = grid.Elements()
                    .Where(node => node.Name.LocalName == "DataGrid.Style")
                    .Descendants()
                    .Any(node => node.Name.LocalName == "Style" &&
                                 node.Attribute("BasedOn")?.Value.Contains(
                                     "FluentDataGrid",
                                     StringComparison.Ordinal) == true);
                if (!direct && !local)
                {
                    errs.Add($"{rel}: DataGrid must use FluentDataGrid.");
                }
            }

            foreach (var col in doc.Descendants()
                         .Where(node => node.Name.LocalName.StartsWith("DataGrid", StringComparison.Ordinal) &&
                                        node.Name.LocalName.EndsWith("Column", StringComparison.Ordinal)))
            {
                var width = col.Attribute("Width")?.Value;
                if (double.TryParse(
                        width,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var size) &&
                    size > 160 &&
                    (col.Attribute("MinWidth") is null || col.Attribute("MaxWidth") is null))
                {
                    errs.Add($"{rel}: fixed DataGrid column width {size} lacks MinWidth/MaxWidth.");
                }
            }

            if (!string.Equals(
                    file,
                    Path.Combine(appDir, "Thm", "Ctrl.xaml"),
                    StringComparison.OrdinalIgnoreCase))
            {
                foreach (var style in doc.Descendants()
                             .Where(node => node.Name.LocalName == "Style"))
                {
                    var key = style.Attribute(xkey)?.Value;
                    if (key is not null && keys.Contains(key))
                    {
                        errs.Add($"{rel}: local style '{key}' duplicates Ctrl.xaml.");
                    }
                }
            }

            foreach (var tab in doc.Descendants()
                         .Where(node => node.Name.LocalName == "TabItem"))
            {
                foreach (var view in tab.Descendants()
                             .Where(node => node.Name.LocalName == "ScrollViewer"))
                {
                    var nested = view.Ancestors()
                        .TakeWhile(node => node != tab)
                        .Any(node => node.Name.LocalName == "ScrollViewer");
                    if (nested && VScroll(view))
                    {
                        errs.Add($"{rel}: nested vertical ScrollViewer inside one TabItem.");
                    }
                }
            }

            foreach (var trig in doc.Descendants()
                         .Where(node => node.Name.LocalName == "Trigger" &&
                                        node.Attribute("Property")?.Value == "IsEnabled" &&
                                        node.Attribute("Value")?.Value == "False"))
            {
                var props = trig.Elements()
                    .Where(node => node.Name.LocalName == "Setter")
                    .Select(node => node.Attribute("Property")?.Value)
                    .Where(val => val is not null)
                    .ToArray();
                if (props.Length > 0 && props.All(val => val == "Opacity"))
                {
                    errs.Add($"{rel}: disabled state uses opacity alone.");
                }
            }
        }

        Check(errs.Count == 0, string.Join(Environment.NewLine, errs));
        Console.WriteLine($"UI XAML gate PASS ({files.Length} files).");
    }

    private static bool Target(string? value, string name)
    {
        return string.Equals(value, name, StringComparison.Ordinal) ||
               string.Equals(value, $"{{x:Type {name}}}", StringComparison.Ordinal);
    }

    private static bool IsColor(string name, string val)
    {
        if (val.StartsWith('{') ||
            string.Equals(val, "Transparent", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!name.Contains("Color", StringComparison.Ordinal) &&
            !name.Contains("Brush", StringComparison.Ordinal) &&
            name is not ("Background" or "Foreground" or "Fill" or "Stroke"))
        {
            return false;
        }

        try
        {
            return ColorConverter.ConvertFromString(val) is Color;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool VScroll(XElement view)
    {
        var val = view.Attributes()
            .FirstOrDefault(attr =>
                attr.Name.LocalName is "VerticalScrollBarVisibility" or
                    "ScrollViewer.VerticalScrollBarVisibility")
            ?.Value;
        return !string.Equals(val, "Disabled", StringComparison.Ordinal);
    }

    private static void Visual()
    {
        Exception? err = null;
        var work = new Thread(() =>
        {
            try
            {
                RunUi();
            }
            catch (Exception ex)
            {
                err = ex;
            }
            finally
            {
                Application.Current?.Shutdown();
            }
        });

        work.SetApartmentState(ApartmentState.STA);
        work.Start();
        work.Join();

        if (err is not null)
        {
            throw new InvalidOperationException("Runtime UI layout gate failed.", err);
        }
    }

    private static void RunUi()
    {
        var app = new App();
        app.InitializeComponent();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var buf = new StringBuilder();
        using var write = new StringWriter(buf, CultureInfo.InvariantCulture);
        using var trace = new TextWriterTraceListener(write);
        var source = PresentationTraceSources.DataBindingSource;
        var level = source.Switch.Level;
        source.Switch.Level = SourceLevels.Error;
        source.Listeners.Add(trace);
        PresentationTraceSources.Refresh();

        try
        {
            var themes = new[] { true, false };
            var langs = new[] { UiText.ArabLang, UiText.EnLang };
            var sizes = new[]
            {
                new Size(960, 620),
                new Size(1120, 700),
                new Size(1366, 768),
                new Size(1920, 1080)
            };
            var dpis = new[] { 96d, 120d, 144d };
            var shotDir = Environment.GetEnvironmentVariable("R3_UI_SHOTS");
            var held = new List<Window>();

            foreach (var isDark in themes)
            {
                WinTheme.ApplyResources(isDark);
                foreach (var lang in langs)
                {
                    UiText.SetLang(lang);
                    UiLaySvc.Apply();
                    var wins = Make();
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(shotDir))
                        {
                            Shots(wins, lang, isDark, shotDir);
                        }

                        foreach (var size in sizes)
                        {
                            foreach (var win in wins)
                            {
                                Lay(win, size);
                                Tabs(win, size);
                                var root = (FrameworkElement)win.Content;
                                Align(root);
                                Bounds(root, size);
                                foreach (var dpi in dpis)
                                {
                                    Dpi(win, size, dpi);
                                }
                            }
                        }

                        SelLang(wins[0], lang);
                    }
                    finally
                    {
                        if (!string.IsNullOrWhiteSpace(shotDir))
                        {
                            held.AddRange(wins);
                        }
                        else
                        {
                            foreach (var win in wins)
                            {
                                win.Close();
                            }
                        }
                    }
                }
            }

            foreach (var win in held)
            {
                win.Close();
            }

            trace.Flush();
            var log = buf.ToString();
            Check(string.IsNullOrWhiteSpace(log), $"WPF binding error:{Environment.NewLine}{log}");
            Console.WriteLine("Runtime UI gate PASS (4 windows, 4 sizes, 3 DPI scales, 2 languages, 2 themes).");
        }
        finally
        {
            source.Listeners.Remove(trace);
            source.Switch.Level = level;
        }
    }

    private static Window[] Make()
    {
        var flow = new AppFlow();
        var canVm = new CanImpVm(
            null,
            flow,
            () => false,
            (_, _) => false);
        return
        [
            new MainWin(),
            new AdvDlg { DataContext = new MainVm() },
            new CanDlg(null, canVm),
            new AppDlg(
                null,
                UiText.GetText("App.Title"),
                UiText.GetText("Adv.Description"),
                DlgKind.Confirm,
                true)
        ];
    }

    private static void Lay(Window win, Size size)
    {
        LayRoot(win, size);
    }

    private static void Tabs(Window win, Size size)
    {
        var root = (FrameworkElement)win.Content;
        var seen = new HashSet<TabControl>();
        while (true)
        {
            var tabs = Walk<TabControl>(root)
                .Where(tab => seen.Add(tab))
                .ToArray();
            if (tabs.Length == 0)
            {
                break;
            }

            foreach (var tab in tabs)
            {
                var old = tab.SelectedIndex;
                for (var idx = 0; idx < tab.Items.Count; idx++)
                {
                    tab.SelectedIndex = idx;
                    Lay(win, size);
                    Align(tab);
                    Bounds(tab, new Size(tab.ActualWidth, tab.ActualHeight));
                }

                tab.SelectedIndex = old;
                Lay(win, size);
            }
        }
    }

    private static void Align(DependencyObject root)
    {
        foreach (var item in Walk<FrameworkElement>(root))
        {
            if (item is TextBlock text)
            {
                Check(text.FlowDirection == FlowDirection.LeftToRight,
                    $"{text.GetType().Name} uses {text.FlowDirection}.");
                Check(text.TextAlignment == TextAlignment.Left,
                    $"TextBlock '{text.Text}' uses {text.TextAlignment}.");
            }
            else if (item is TextBox box)
            {
                Check(box.FlowDirection == FlowDirection.LeftToRight,
                    $"TextBox uses {box.FlowDirection}.");
                Check(box.TextAlignment == TextAlignment.Left,
                    $"TextBox uses {box.TextAlignment}.");
            }
            else if (item is ComboBox combo)
            {
                Check(combo.FlowDirection == FlowDirection.LeftToRight,
                    $"ComboBox uses {combo.FlowDirection}.");
            }
        }
    }

    private static void Bounds(DependencyObject root, Size size)
    {
        foreach (var item in Walk<FrameworkElement>(root))
        {
            if (!item.IsVisible || item.ActualWidth < 0 || item.ActualHeight < 0)
            {
                continue;
            }

            Check(!double.IsNaN(item.ActualWidth) && !double.IsInfinity(item.ActualWidth),
                $"{item.GetType().Name} has invalid width.");
            Check(!double.IsNaN(item.ActualHeight) && !double.IsInfinity(item.ActualHeight),
                $"{item.GetType().Name} has invalid height.");
            if (item is TabItem tab && tab.IsSelected)
            {
                Check(tab.ActualWidth >= 72,
                    $"Selected tab header is clipped at {tab.ActualWidth:0.#} px.");
            }
        }

        foreach (var panel in Walk<Panel>(root)
                     .Where(item => item.TemplatedParent is null &&
                                    item.ActualWidth > 0 &&
                                    item.ActualHeight > 0))
        {
            var kids = panel.Children
                .OfType<FrameworkElement>()
                .Where(item => item.IsVisible &&
                               item.TemplatedParent is null &&
                               item.ActualWidth > 0 &&
                               item.ActualHeight > 0)
                .ToArray();
            var rects = new Dictionary<FrameworkElement, Rect>();
            foreach (var item in kids)
            {
                var point = item.TranslatePoint(new Point(), panel);
                var rect = new Rect(
                    point,
                    new Size(item.ActualWidth, item.ActualHeight));
                rects[item] = rect;
                if (!HasScrl(item, panel))
                {
                    Check(rect.Left >= -1 && rect.Top >= -1 &&
                          rect.Right <= panel.ActualWidth + 1 &&
                          rect.Bottom <= panel.ActualHeight + 1,
                        $"{item.GetType().Name} overflows {panel.GetType().Name} bounds.");
                }
            }

            for (var one = 0; one < kids.Length; one++)
            {
                for (var two = one + 1; two < kids.Length; two++)
                {
                    if (!HitPair(kids[one], kids[two]))
                    {
                        continue;
                    }

                    var hit = Rect.Intersect(rects[kids[one]], rects[kids[two]]);
                    Check(hit.IsEmpty || hit.Width <= 1 || hit.Height <= 1,
                        $"{kids[one].GetType().Name} overlaps {kids[two].GetType().Name}.");
                }
            }
        }

        Check(size.Width > 0 && size.Height > 0, "Layout surface must be non-empty.");
    }

    private static bool HasScrl(DependencyObject item, DependencyObject stop)
    {
        var node = VisualTreeHelper.GetParent(item);
        while (node is not null && node != stop)
        {
            if (node is ScrollViewer)
            {
                return true;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return false;
    }

    private static bool HitPair(FrameworkElement one, FrameworkElement two)
    {
        if (one is Button || two is Button)
        {
            return (one is Button or TextBlock or StackPanel) &&
                   (two is Button or TextBlock or StackPanel);
        }

        return one is DataGrid && (two is Border or TextBlock) ||
               two is DataGrid && (one is Border or TextBlock);
    }

    private static void Dpi(Window win, Size size, double dpi)
    {
        var root = (FrameworkElement)win.Content;
        var pxw = (int)Math.Ceiling(size.Width * dpi / 96d);
        var pxh = (int)Math.Ceiling(size.Height * dpi / 96d);
        var bmp = new RenderTargetBitmap(
            pxw,
            pxh,
            dpi,
            dpi,
            PixelFormats.Pbgra32);
        bmp.Render(root);
        Check(bmp.PixelWidth == pxw && bmp.PixelHeight == pxh,
            $"DPI render failed at {dpi / 96d:P0}.");
    }

    private static void Shots(Window[] wins, string lang, bool isDark, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var langTag = lang == UiText.ArabLang ? "ar" : "en";
        var themeTag = isDark ? "dark" : "light";
        var tag = $"{themeTag}-{langTag}";
        Save(wins[0], $"{tag}-Main.png", outDir);
        Save(wins[2], $"{tag}-CanDlg.png", outDir);
        Save(wins[3], $"{tag}-AppDlg.png", outDir);

        var adv = wins[1];
        var size = new Size(1120, 700);
        LayRoot(adv, size);
        Save(adv, $"{tag}-Adv-Dev.png", outDir);
        var root = (FrameworkElement)adv.Content;
        var top = Walk<TabControl>(root).First();
        var names = new[] { "Dev", "HCan", "Rec", "Diag" };
        var old = top.SelectedIndex;
        for (var idx = 0; idx < top.Items.Count; idx++)
        {
            top.SelectedIndex = idx;
            LayRoot(adv, size);
            if (idx > 0)
            {
                Save(adv, $"{tag}-Adv-{names[idx]}.png", outDir);
            }
            if (idx != 1)
            {
                continue;
            }

            var inner = Walk<TabControl>(root)
                .First(tab => tab != top);
            var inames = new[] { "Info", "Ins", "Anc", "Prev" };
            var iold = inner.SelectedIndex;
            for (var item = 0; item < inner.Items.Count; item++)
            {
                inner.SelectedIndex = item;
                LayRoot(adv, size);
                Save(adv, $"{tag}-HCan-{inames[item]}.png", outDir);
            }

            inner.SelectedIndex = iold;
        }

        top.SelectedIndex = old;
        LayRoot(adv, size);
    }

    private static void Save(Window win, string name, string outDir)
    {
        var root = (FrameworkElement)win.Content;
        var wide = double.IsNaN(win.Width)
            ? Math.Max(win.MinWidth, 440)
            : win.Width;
        var high = win.Height;
        if (double.IsNaN(high))
        {
            root.Measure(new Size(wide, double.PositiveInfinity));
            high = Math.Max(win.MinHeight, Math.Ceiling(root.DesiredSize.Height));
        }

        if (high <= 0)
        {
            high = Math.Max(win.MinHeight, 260);
        }

        var size = new Size(wide, high);
        LayRoot(win, size);
        var bmp = new RenderTargetBitmap(
            (int)Math.Ceiling(size.Width),
            (int)Math.Ceiling(size.Height),
            96,
            96,
            PixelFormats.Pbgra32);
        bmp.Render(root);
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(bmp));
        using var file = File.Create(Path.Combine(outDir, name));
        png.Save(file);
    }

    private static void LayRoot(Window win, Size size)
    {
        var root = (FrameworkElement)win.Content;
        root.Measure(size);
        root.Arrange(new Rect(size));
        root.UpdateLayout();
    }

    private static void SelLang(Window win, string lang)
    {
        var root = (FrameworkElement)win.Content;
        var sels = Walk<Selector>(root)
            .ToDictionary(sel => sel, sel => sel.SelectedIndex);
        var next = lang == UiText.ArabLang ? UiText.EnLang : UiText.ArabLang;
        UiText.SetLang(next);
        UiLaySvc.Apply();
        Lay(win, new Size(1120, 700));
        foreach (var pair in sels)
        {
            Check(pair.Key.SelectedIndex == pair.Value,
                $"{pair.Key.GetType().Name} selection changed after language switch.");
        }

        UiText.SetLang(lang);
        UiLaySvc.Apply();
    }

    private static IEnumerable<T> Walk<T>(DependencyObject root)
        where T : DependencyObject
    {
        if (root is T match)
        {
            yield return match;
        }

        for (var idx = 0; idx < VisualTreeHelper.GetChildrenCount(root); idx++)
        {
            var child = VisualTreeHelper.GetChild(root, idx);
            foreach (var item in Walk<T>(child))
            {
                yield return item;
            }
        }
    }

    private static string Repo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "R3Cfg.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ??
            throw new InvalidOperationException("Could not locate the repository root.");
    }

    private static void Check(bool ok, string msg)
    {
        if (!ok)
        {
            throw new InvalidOperationException(msg);
        }
    }
}
