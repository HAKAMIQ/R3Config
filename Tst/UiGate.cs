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
        var appResources = XDocument.Load(Path.Combine(appDir, "App.xaml"))
            .Descendants()
            .Where(node => node.Attribute(xkey) is not null)
            .ToDictionary(
                node => node.Attribute(xkey)!.Value,
                node => node.Value.Trim(),
                StringComparer.Ordinal);
        var defaultBidiResources = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["App.FlowDirection"] = "RightToLeft",
            ["App.ShellFlowDirection"] = "RightToLeft",
            ["App.TextFlowDirection"] = "RightToLeft",
            ["App.TechnicalFlowDirection"] = "LeftToRight",
            ["App.DefaultTextAlignment"] = "Right",
            ["App.TextAlignment"] = "Right",
            ["App.TechnicalTextAlignment"] = "Left",
            ["App.HorizontalAlignment"] = "Right",
            ["App.TrailingHorizontalAlignment"] = "Left",
            ["App.After6Margin"] = "6,0,0,0",
            ["App.After8Margin"] = "8,0,0,0",
            ["App.After10Margin"] = "10,0,0,0",
            ["App.After12Margin"] = "12,0,0,0",
            ["App.Before8Margin"] = "0,0,8,0",
            ["App.Before10Margin"] = "0,0,10,0",
            ["App.Indent24Margin"] = "0,6,24,0",
            ["App.ScrollPadding4"] = "4,0,0,0",
            ["App.XmlLanguage"] = UiText.ArabLang
        };
        foreach (var pair in defaultBidiResources)
        {
            if (!appResources.TryGetValue(pair.Key, out var value) ||
                !string.Equals(value, pair.Value, StringComparison.Ordinal))
            {
                errs.Add($"Default bidi resource '{pair.Key}' must be {pair.Value}.");
            }
        }

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
            "Button.MinWidth.Compact", "Button.MinWidth.Standard",
            "Button.Padding.Compact", "Button.Padding.Standard", "Button.Padding.Large",
            "Motion.Surface", "FluentButton.Primary", "FluentButton.Secondary",
            "FluentButton.Subtle", "FluentButton.Danger", "FluentButton.Icon",
            "FluentButton.Compact", "FluentButton.Toolbar", "FluentDataGrid", "FluentDataGridHeader",
            "FluentDataGridCell", "FluentDataGridRow", "FluentTabControl", "FluentTabItem"
        };
        foreach (var key in required.Where(key => !themeKeys.Contains(key)))
        {
            errs.Add($"Missing Fluent 2 contract '{key}'.");
        }

        var values = ctrl.Root!
            .Elements()
            .Select(node => new
            {
                Key = node.Attribute(xkey)?.Value,
                Value = node.Value.Trim()
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .ToDictionary(item => item.Key!, item => item.Value, StringComparer.Ordinal);
        var expectedValues = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Radius.Small"] = "2",
            ["Radius.Medium"] = "4",
            ["Radius.Large"] = "8",
            ["Radius.XLarge"] = "12",
            ["Control.Height.Compact"] = "24",
            ["Control.Height.Standard"] = "32",
            ["Control.Height.Large"] = "40",
            ["Button.MinWidth.Compact"] = "64",
            ["Button.MinWidth.Standard"] = "96",
            ["Button.Padding.Compact"] = "8,3",
            ["Button.Padding.Standard"] = "12,5",
            ["Button.Padding.Large"] = "16,8",
            ["DataGrid.HeaderHeight"] = "32",
            ["DataGrid.RowHeight"] = "36"
        };
        foreach (var pair in expectedValues)
        {
            if (!values.TryGetValue(pair.Key, out var value) ||
                !string.Equals(value, pair.Value, StringComparison.Ordinal))
            {
                errs.Add($"Fluent 2 token '{pair.Key}' must be {pair.Value}.");
            }
        }

        var type = XDocument.Load(Path.Combine(appDir, "Thm", "Type.xaml"));
        var typeSizes = type.Descendants()
            .Where(node => node.Name.LocalName == "Style" &&
                           node.Attribute(xkey) is not null)
            .Select(node => new
            {
                Key = node.Attribute(xkey)!.Value,
                Size = node.Elements()
                    .FirstOrDefault(child => child.Name.LocalName == "Setter" &&
                                             child.Attribute("Property")?.Value == "FontSize")
                    ?.Attribute("Value")?.Value
            })
            .Where(item => item.Size is not null)
            .ToDictionary(item => item.Key, item => item.Size!, StringComparer.Ordinal);
        var expectedType = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Type.Display"] = "28",
            ["Type.TitleLarge"] = "24",
            ["Type.Title"] = "20",
            ["Type.Subtitle"] = "16",
            ["Type.Body"] = "14",
            ["Type.Caption"] = "12",
            ["Type.Technical"] = "13"
        };
        foreach (var pair in expectedType)
        {
            if (!typeSizes.TryGetValue(pair.Key, out var value) ||
                !string.Equals(value, pair.Value, StringComparison.Ordinal))
            {
                errs.Add($"Fluent 2 typography role '{pair.Key}' must be {pair.Value}.");
            }
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
            var rootNode = doc.Root;

            if (text.Contains("Language.IetfLanguageTag", StringComparison.Ordinal))
            {
                errs.Add($"{rel}: language-tag direction triggers are forbidden; use central dynamic resources.");
            }

            if (rootNode?.Name.LocalName is "Window" or "UserControl")
            {
                var rootFlow = rootNode.Attribute("FlowDirection")?.Value;
                if (!string.Equals(
                        rootFlow,
                        "{DynamicResource App.FlowDirection}",
                        StringComparison.Ordinal))
                {
                    errs.Add($"{rel}: root FlowDirection must use App.FlowDirection.");
                }
            }

            var titleBar = doc.Descendants()
                .FirstOrDefault(node =>
                    node.Name.LocalName == "Border" &&
                    (node.Attribute("Style")?.Value.Contains("R3DialogTopBar", StringComparison.Ordinal) == true ||
                     node.Attribute("Style")?.Value.Contains("R3ShellTopBar", StringComparison.Ordinal) == true));
            var titleGrid = titleBar?.Elements()
                .FirstOrDefault(node => node.Name.LocalName == "Grid");
            if (titleGrid is not null &&
                !string.Equals(
                    titleGrid.Attribute("FlowDirection")?.Value,
                    "LeftToRight",
                    StringComparison.Ordinal))
            {
                errs.Add($"{rel}: Windows caption grid must remain physical LTR.");
            }

            if (Regex.IsMatch(
                    text,
                    "TextAlignment\\s*=\\s*\"(?:Right|Justify)\"|FlowDirection\\s*=\\s*\"RightToLeft\"",
                    RegexOptions.CultureInvariant))
            {
                errs.Add($"{rel}: forbidden hard-coded mirroring/right/justify value.");
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

            var allowedFontSizes = new HashSet<double> { 12, 13, 14, 16, 20, 24, 28 };
            foreach (var node in doc.Descendants())
            {
                var raw = node.Attribute("FontSize")?.Value;
                if (node.Name.LocalName == "Setter" &&
                    node.Attribute("Property")?.Value == "FontSize")
                {
                    raw = node.Attribute("Value")?.Value;
                }

                if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var size) &&
                    !allowedFontSizes.Contains(size))
                {
                    errs.Add($"{rel}: font size {size:0.##} is outside the Fluent 2 type ramp.");
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

            if (!rel.EndsWith(
                    Path.Combine("Thm", "Ctrl.xaml"),
                    StringComparison.OrdinalIgnoreCase))
            {
                foreach (var button in doc.Descendants()
                             .Where(node => node.Name.LocalName == "Button"))
                {
                    foreach (var attrName in new[] { "MinWidth", "MinHeight", "Padding" })
                    {
                        var value = button.Attribute(attrName)?.Value;
                        if (value is not null &&
                            double.TryParse(value.Split(',')[0], NumberStyles.Float,
                                CultureInfo.InvariantCulture, out _))
                        {
                            errs.Add($"{rel}: Button {attrName} must come from a Fluent button style.");
                        }
                    }
                }
            }

            foreach (var tab in doc.Descendants()
                         .Where(node => node.Name.LocalName == "TabControl" &&
                                        node.Ancestors().Any(parent => parent.Name.LocalName == "TabControl")))
            {
                errs.Add($"{rel}: nested TabControl is forbidden; use a separate window or surface.");
            }

            foreach (var grid in doc.Descendants()
                         .Where(node => node.Name.LocalName == "DataGrid" &&
                                        node.Ancestors().Any(parent => parent.Name.LocalName == "DataGrid")))
            {
                errs.Add($"{rel}: nested DataGrid is forbidden.");
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
                                if (win is AdvDlg advanced)
                                {
                                    SetAdvancedSurface(advanced, showHCanary: false);
                                }

                                VerifySurface(win, size, dpis);

                                if (win is AdvDlg hCanaryHost)
                                {
                                    SetAdvancedSurface(hCanaryHost, showHCanary: true);
                                    VerifySurface(hCanaryHost, size, dpis);
                                    SetAdvancedSurface(hCanaryHost, showHCanary: false);
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
            Console.WriteLine("Runtime UI gate PASS (8 windows plus embedded H-Canary workspace, 4 sizes, 3 DPI scales, 2 languages, 2 themes).");
        }
        finally
        {
            source.Listeners.Remove(trace);
            source.Switch.Level = level;
        }
    }

    private static void VerifySurface(Window window, Size size, IReadOnlyList<double> dpis)
    {
        Lay(window, size);
        Tabs(window, size);
        var root = (FrameworkElement)window.Content;
        Align(root);
        Bounds(root, size);
        RawLocalization(root);
        foreach (var dpi in dpis)
        {
            Dpi(window, size, dpi);
        }
    }

    private static void SetAdvancedSurface(AdvDlg window, bool showHCanary)
    {
        var navigation = window.FindName("AdvancedNavigation") as ListBox;
        var workspace = window.FindName("HCanaryWorkspace") as FrameworkElement;
        var recovery = window.FindName("RecoveryWorkspace") as FrameworkElement;
        var diagnostics = window.FindName("DiagnosticsWorkspace") as FrameworkElement;
        Check(navigation is not null && workspace is not null &&
              recovery is not null && diagnostics is not null,
            "Advanced Tools workspace surfaces were not found.");
        navigation!.SelectedIndex = showHCanary ? 0 : 1;
        workspace!.Visibility = showHCanary ? Visibility.Visible : Visibility.Collapsed;
        recovery!.Visibility = showHCanary ? Visibility.Collapsed : Visibility.Visible;
        diagnostics!.Visibility = Visibility.Collapsed;
        if (showHCanary)
        {
            ShowHCanaryWorkflow(workspace);
        }
    }

    private static void ShowHCanaryWorkflow(FrameworkElement workspace)
    {
        var actions = workspace.FindName("WorkflowActions") as FrameworkElement;
        var steps = workspace.FindName("Steps") as TabControl;
        var bottomBar = workspace.FindName("WorkflowBottomBar") as FrameworkElement;
        Check(actions is not null && steps is not null && bottomBar is not null,
            "H-Canary workflow surfaces were not found.");
        actions!.Visibility = Visibility.Collapsed;
        steps!.Visibility = Visibility.Visible;
        bottomBar!.Visibility = Visibility.Visible;
        foreach (var item in steps.Items.OfType<TabItem>())
        {
            item.IsEnabled = true;
        }
    }

    private static Window[] Make()
    {
        var flow = new AppFlow();
        var toolsVm = new MainVm();
        var canVm = new CanImpVm(
            null,
            flow,
            () => false,
            (_, _) => false);
        return
        [
            new MainWin(),
            new AdvDlg { DataContext = toolsVm },
            new HCanDlg { DataContext = toolsVm },
            new CanDlg(null, canVm),
            new AppDlg(
                null,
                UiText.GetText("App.Title"),
                UiText.GetText("Adv.Description"),
                DlgKind.Confirm,
                true),
            new HCanSavedDlg { DataContext = toolsVm },
            new HCanTargetsDlg { DataContext = toolsVm },
            new HCanAnchorsDlg { DataContext = toolsVm }
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
                .Where(tab => tab.IsVisible &&
                              tab.ActualWidth > 0 &&
                              tab.ActualHeight > 0)
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
                    RawLocalization(root);
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
        if (root is FrameworkElement rootElement)
        {
            var expectedRootFlow = ExpectedFlow(rootElement);
            Check(rootElement.FlowDirection == expectedRootFlow,
                $"{rootElement.GetType().Name} root uses {rootElement.FlowDirection}; expected {expectedRootFlow}.");
        }

        foreach (var item in Walk<FrameworkElement>(root))
        {
            if (item is TextBlock text)
            {
                var label = TextBlockLabel(text);
                CheckTextFlow(text, text.Text, label);
                var expected = ExpectedTextAlignment(text, text.Text);
                Check(text.TextAlignment == expected,
                    $"{label} uses {text.TextAlignment}; expected {expected}.");
            }
            else if (item is TextBox box)
            {
                CheckTextFlow(box, box.Text, TextBoxLabel(box));
                var expected = ExpectedTextAlignment(box, box.Text);
                Check(box.TextAlignment == expected,
                    $"{TextBoxLabel(box)} uses {box.TextAlignment}; expected {expected}.");
            }
            else if (item is ComboBox combo)
            {
                var expectedFlow = ExpectedFlow(combo);
                Check(combo.FlowDirection == expectedFlow,
                    $"ComboBox uses {combo.FlowDirection}; expected {expectedFlow}.");
            }
            else if (item is CheckBox checkBox)
            {
                var expectedFlow = ExpectedFlow(checkBox);
                Check(checkBox.FlowDirection == expectedFlow,
                    $"CheckBox layout uses {checkBox.FlowDirection}; expected {expectedFlow}.");
            }
            else if (item is Button button &&
                     button.Content is string buttonText)
            {
                CheckTextFlow(button, buttonText, $"Button '{buttonText}'");
            }
            else if (item is TabItem tabItem &&
                     tabItem.Header is string headerText)
            {
                var expectedFlow = UiLaySvc.Current.TextFlow;
                Check(tabItem.FlowDirection == expectedFlow,
                    $"TabItem layout uses {tabItem.FlowDirection}; expected {expectedFlow}.");
                var presenter = Walk<ContentPresenter>(tabItem)
                    .FirstOrDefault(candidate =>
                        Equals(candidate.Content, tabItem.Header));
                Check(presenter is not null,
                    $"TabItem '{headerText}' header presenter is unavailable.");
                CheckTextFlow(
                    presenter!,
                    headerText,
                    $"TabItem header '{headerText}'");
            }
        }
    }

    private static void CheckTextFlow(
        FrameworkElement element,
        string? text,
        string label)
    {
        var expected = ExpectedFlow(element);
        Check(element.FlowDirection == expected,
            $"{label} uses {element.FlowDirection}; expected {expected}.");
    }

    private static FlowDirection ExpectedFlow(FrameworkElement element)
    {
        return IsIconText(element) || UsesTechnicalStyle(element) || IsTechnical(element)
            ? FlowDirection.LeftToRight
            : UiLaySvc.Current.TextFlow;
    }

    private static TextAlignment ExpectedTextAlignment(
        FrameworkElement element,
        string? text)
    {
        if (IsIconText(element))
        {
            return TextAlignment.Center;
        }

        return UsesTechnicalStyle(element) || IsTechnical(element)
            ? TextAlignment.Left
            : UiLaySvc.Current.TxtAlign;
    }

    private static bool IsIconText(FrameworkElement element)
    {
        if (element is not TextBlock text)
        {
            return false;
        }

        var family = text.FontFamily.Source;
        if (family.Contains("Segoe Fluent Icons", StringComparison.OrdinalIgnoreCase) ||
            family.Contains("Segoe MDL2 Assets", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var value = text.Text;
        return string.Equals(value, "!", StringComparison.Ordinal);
    }

    private static bool UsesTechnicalStyle(FrameworkElement element)
    {
        var app = Application.Current;
        if (app is null)
        {
            return false;
        }

        var technicalStyles = new[]
        {
            app.TryFindResource("Type.Technical") as Style,
            app.TryFindResource("TechnicalTextBox") as Style,
            app.TryFindResource("FluentTextBox.Technical") as Style,
            app.TryFindResource("IconCheckBox") as Style
        };

        for (var style = element.Style; style is not null; style = style.BasedOn)
        {
            if (technicalStyles.Any(technical =>
                    technical is not null && ReferenceEquals(style, technical)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool UsesStyle(FrameworkElement element, string key)
    {
        var target = Application.Current?.TryFindResource(key) as Style;
        if (target is null)
        {
            return false;
        }

        for (var style = element.Style; style is not null; style = style.BasedOn)
        {
            if (ReferenceEquals(style, target))
            {
                return true;
            }
        }

        return false;
    }

    private static string TextBoxLabel(TextBox box)
    {
        var path = box
            .GetBindingExpression(TextBox.TextProperty)
            ?.ParentBinding
            .Path
            ?.Path;
        var value = Regex.Replace(box.Text ?? string.Empty, "\\s+", " ").Trim();
        if (value.Length > 72)
        {
            value = value[..69] + "...";
        }

        var label = string.IsNullOrWhiteSpace(path)
            ? "TextBox"
            : $"TextBox '{path}'";
        return string.IsNullOrEmpty(value)
            ? label
            : $"{label} value '{value}'";
    }

    private static string TextBlockLabel(TextBlock text)
    {
        var path = text
            .GetBindingExpression(TextBlock.TextProperty)
            ?.ParentBinding
            .Path
            ?.Path;
        var value = Regex.Replace(text.Text ?? string.Empty, "\\s+", " ").Trim();
        if (value.Length > 72)
        {
            value = value[..69] + "...";
        }

        var owner = text.TemplatedParent?.GetType().Name ??
                    VisualTreeHelper.GetParent(text)?.GetType().Name ??
                    "none";
        var source = string.IsNullOrWhiteSpace(path) ? "literal" : path;
        return $"TextBlock '{value}' (source: {source}; owner: {owner})";
    }

    private static bool IsTechnical(FrameworkElement element)
    {
        return string.Equals(
            element.Language.IetfLanguageTag,
            UiText.EnLang,
            StringComparison.OrdinalIgnoreCase);
    }

    private static void Bounds(DependencyObject root, Size size)
    {
        foreach (var item in Walk<FrameworkElement>(root))
        {
            if (!item.IsVisible || item.ActualWidth <= 0 || item.ActualHeight <= 0)
            {
                continue;
            }

            Check(!double.IsNaN(item.ActualWidth) && !double.IsInfinity(item.ActualWidth),
                $"{item.GetType().Name} has invalid width.");
            Check(!double.IsNaN(item.ActualHeight) && !double.IsInfinity(item.ActualHeight),
                $"{item.GetType().Name} has invalid height.");

            if (item is Button button && !UsesStyle(button, "FluentButton.Icon"))
            {
                var compact = UsesStyle(button, "FluentButton.Compact");
                var minWidth = compact ? 64d : 96d;
                var minHeight = compact ? 24d : 32d;
                Check(button.ActualWidth >= minWidth - 1,
                    $"Button '{button.Content}' is {button.ActualWidth:0.#} px wide; expected at least {minWidth:0}.");
                Check(button.ActualHeight >= minHeight - 1,
                    $"Button '{button.Content}' is {button.ActualHeight:0.#} px high; expected at least {minHeight:0}.");
            }
            else if (item is TextBox or ComboBox)
            {
                Check(item.ActualHeight >= 31,
                    $"{item.GetType().Name} is {item.ActualHeight:0.#} px high; expected Fluent medium height 32.");
            }

            if (item is TabItem tab && tab.IsSelected)
            {
                Check(tab.ActualWidth >= 72,
                    $"Selected tab header is clipped at {tab.ActualWidth:0.#} px.");
                Check(tab.ActualHeight >= 31,
                    $"Selected tab header is {tab.ActualHeight:0.#} px high; expected Fluent medium height 32.");
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
        Save(wins[3], $"{tag}-CanDlg.png", outDir);
        Save(wins[4], $"{tag}-AppDlg.png", outDir);

        var adv = wins[1];
        var size = new Size(1120, 700);
        LayRoot(adv, size);
        Save(adv, $"{tag}-Adv-Tools.png", outDir);
        var root = (FrameworkElement)adv.Content;
        var top = Walk<TabControl>(root).First();
        var names = new[] { "Tools", "Rec", "Diag" };
        var old = top.SelectedIndex;
        for (var idx = 0; idx < top.Items.Count; idx++)
        {
            top.SelectedIndex = idx;
            LayRoot(adv, size);
            if (idx > 0)
            {
                Save(adv, $"{tag}-Adv-{names[idx]}.png", outDir);
            }
        }

        top.SelectedIndex = old;
        LayRoot(adv, size);

        top.SelectedIndex = 0;
        var hCanaryWorkspace = adv.FindName("HCanaryWorkspace") as FrameworkElement;
        Check(hCanaryWorkspace is not null,
            "Advanced Tools H-Canary workspace was not found.");
        ShowHCanaryWorkflow(hCanaryWorkspace!);
        LayRoot(adv, size);

        var hTabs = Walk<TabControl>(root).Single(tab => tab.Name == "Steps");
        foreach (var item in hTabs.Items.OfType<TabItem>())
        {
            item.IsEnabled = true;
        }

        var hNames = new[]
        {
            "Game",
            "Targets",
            "Fingerprint",
            "Details",
            "Instructions",
            "Review"
        };
        var hOld = hTabs.SelectedIndex;
        for (var idx = 0; idx < hTabs.Items.Count; idx++)
        {
            hTabs.SelectedIndex = idx;
            LayRoot(adv, size);
            Save(adv, $"{tag}-HCan-{hNames[idx]}.png", outDir);
        }

        var savedDrawer = Walk<Border>(root).Single(border => border.Name == "SavedDrawer");
        savedDrawer.Visibility = Visibility.Visible;
        LayRoot(adv, size);
        Save(adv, $"{tag}-HCan-Saved.png", outDir);
        savedDrawer.Visibility = Visibility.Collapsed;

        foreach (var expander in Walk<Expander>(root).Where(expander =>
                     expander.Name is "TargetsPanel" or "AnchorsPanel"))
        {
            expander.IsExpanded = true;
            LayRoot(adv, size);
            Save(adv, $"{tag}-HCan-{expander.Name}.png", outDir);
            expander.IsExpanded = false;
        }

        hTabs.SelectedIndex = hOld;
        LayRoot(adv, size);
    }

    private static void RawLocalization(DependencyObject root)
    {
        foreach (var text in Walk<TextBlock>(root).Select(item => item.Text))
        {
            Check(!IsRawLocalizationKey(text), $"Raw localization key is visible: {text}");
        }

        foreach (var content in Walk<ContentControl>(root)
                     .Select(item => item.Content as string)
                     .Where(value => value is not null))
        {
            Check(!IsRawLocalizationKey(content), $"Raw localization key is visible: {content}");
        }

        foreach (var header in Walk<HeaderedContentControl>(root)
                     .Select(item => item.Header as string)
                     .Where(value => value is not null))
        {
            Check(!IsRawLocalizationKey(header), $"Raw localization key is visible: {header}");
        }
    }

    private static bool IsRawLocalizationKey(string? value)
    {
        return value is not null &&
               Regex.IsMatch(
                   value,
                   @"^(?:Adv|HCanary)\.[A-Za-z0-9_.]+$",
                   RegexOptions.CultureInvariant);
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
