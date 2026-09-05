using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using R3Config;
using R3Config.Lang;
using R3Config.Mdl;
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

    public static void RunVisualOnly()
    {
        Visual();
    }

    public static void RunBusyOverlay()
    {
        Exception? error = null;
        var work = new Thread(() =>
        {
            try
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherSynchronizationContext(
                        Dispatcher.CurrentDispatcher));

                var app = new App();
                app.InitializeComponent();
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                VerifyBusyOverlay();
            }
            catch (Exception exception)
            {
                error = exception;
            }
            finally
            {
                Application.Current?.Shutdown();
            }
        });

        work.SetApartmentState(ApartmentState.STA);
        work.IsBackground = true;
        work.Start();
        if (!work.Join(TimeSpan.FromSeconds(45)))
        {
            throw new TimeoutException(
                "Busy overlay runtime regression did not finish within forty-five seconds.");
        }

        if (error is not null)
        {
            throw new InvalidOperationException(
                "Busy overlay runtime regression failed.",
                error);
        }
    }

    public static void RunPatchMode()
    {
        Exception? error = null;
        var work = new Thread(() =>
        {
            try
            {
                var app = new App();
                app.InitializeComponent();
                VerifyPatchModeVisibility();
            }
            catch (Exception exception)
            {
                error = exception;
            }
            finally
            {
                Application.Current?.Shutdown();
            }
        });

        work.SetApartmentState(ApartmentState.STA);
        work.IsBackground = true;
        work.Start();
        if (!work.Join(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException(
                "Artemis patch-mode visibility regression did not finish within thirty seconds.");
        }

        if (error is not null)
        {
            throw new InvalidOperationException(
                "Artemis patch-mode visibility regression failed.",
                error);
        }
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
        var themeFiles = Directory.GetFiles(
            Path.Combine(appDir, "Thm"),
            "*.xaml",
            SearchOption.TopDirectoryOnly);

        VerifyFrozenDirectionContract(files, appDir, errs);
        VerifyInteractionStateContract(
            files,
            appDir,
            errs);

        var themeDocs = themeFiles
            .Select(path => XDocument.Load(
                path,
                LoadOptions.SetLineInfo))
            .ToArray();

        var keys = themeDocs
            .SelectMany(document => document.Descendants())
            .Where(node => node.Name.LocalName == "Style")
            .Select(node => node.Attribute(xkey)?.Value)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.Ordinal);

        var themeKeys = themeDocs
            .SelectMany(document => document.Descendants())
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

        var defaultBidiResources = new Dictionary<string, string>(
            StringComparer.Ordinal)
        {
            ["App.FlowDirection"] = "RightToLeft",
            ["App.ShellFlowDirection"] = "RightToLeft",
            ["App.TextFlowDirection"] = "RightToLeft",
            ["App.TechnicalFlowDirection"] = "LeftToRight",
            ["App.DefaultTextAlignment"] = "Left",
            ["App.TextAlignment"] = "Left",
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
                !string.Equals(
                    value,
                    pair.Value,
                    StringComparison.Ordinal))
            {
                errs.Add(
                    $"Default bidi resource '{pair.Key}' must be {pair.Value}.");
            }
        }

        var required = new[]
        {
            "Surface.Background",
            "Surface.Card",
            "Surface.CardHover",
            "Surface.CardPressed",
            "Surface.Subtle",
            "Surface.Overlay",
            "Text.Primary",
            "Text.Secondary",
            "Text.Disabled",
            "Text.OnAccent",
            "Stroke.Default",
            "Stroke.Subtle",
            "Stroke.Strong",
            "Stroke.Focus",
            "Accent.Default",
            "Accent.Hover",
            "Accent.Pressed",
            "Accent.Disabled",
            "Status.Success",
            "Status.Warning",
            "Status.Danger",
            "Status.Info",
            "Type.Display",
            "Type.TitleLarge",
            "Type.Title",
            "Type.Subtitle",
            "Type.Body",
            "Type.BodyStrong",
            "Type.Caption",
            "Type.Technical",
            "Space.4",
            "Space.8",
            "Space.12",
            "Space.16",
            "Space.20",
            "Space.24",
            "Space.32",
            "Radius.Small",
            "Radius.Medium",
            "Radius.Large",
            "Radius.XLarge",
            "Elevation.1",
            "Elevation.2",
            "Elevation.3",
            "Motion.Hover",
            "Motion.Press",
            "Button.MinWidth.Compact",
            "Button.MinWidth.Standard",
            "Button.Padding.Compact",
            "Button.Padding.Standard",
            "Button.Padding.Large",
            "Motion.Surface",
            "FluentButton.Primary",
            "FluentButton.Secondary",
            "FluentButton.Subtle",
            "FluentButton.Danger",
            "FluentButton.Icon",
            "FluentButton.Compact",
            "FluentButton.Toolbar",
            "FluentDataGrid",
            "FluentDataGridHeader",
            "FluentDataGridCell",
            "FluentDataGridRow",
            "FluentTabControl",
            "FluentTabItem"
        };

        foreach (var key in required.Where(
                     key => !themeKeys.Contains(key)))
        {
            errs.Add($"Missing Fluent 2 contract '{key}'.");
        }

        var values = themeDocs
            .SelectMany(document => document.Root!.Elements())
            .Select(node => new
            {
                Key = node.Attribute(xkey)?.Value,
                Value = node.Value.Trim()
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .ToDictionary(
                item => item.Key!,
                item => item.Value,
                StringComparer.Ordinal);

        var expectedValues = new Dictionary<string, string>(
            StringComparer.Ordinal)
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
                !string.Equals(
                    value,
                    pair.Value,
                    StringComparison.Ordinal))
            {
                errs.Add(
                    $"Fluent 2 token '{pair.Key}' must be {pair.Value}.");
            }
        }

        var type = XDocument.Load(
            Path.Combine(appDir, "Thm", "Type.xaml"));

        var typeSizes = type
            .Descendants()
            .Where(node =>
                node.Name.LocalName == "Style" &&
                node.Attribute(xkey) is not null)
            .Select(node => new
            {
                Key = node.Attribute(xkey)!.Value,
                Size = node
                    .Elements()
                    .FirstOrDefault(child =>
                        child.Name.LocalName == "Setter" &&
                        child.Attribute("Property")?.Value == "FontSize")
                    ?.Attribute("Value")
                    ?.Value
            })
            .Where(item => item.Size is not null)
            .ToDictionary(
                item => item.Key,
                item => item.Size!,
                StringComparer.Ordinal);

        var expectedType = new Dictionary<string, string>(
            StringComparer.Ordinal)
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
                !string.Equals(
                    value,
                    pair.Value,
                    StringComparison.Ordinal))
            {
                errs.Add(
                    $"Fluent 2 typography role '{pair.Key}' must be {pair.Value}.");
            }
        }

        foreach (var mode in new[] { "Dark", "Light" })
        {
            foreach (var key in new[]
                     {
                         "Surface.Background",
                         "Surface.Card",
                         "Text.Primary",
                         "Text.Secondary",
                         "Stroke.Default",
                         "Stroke.Focus",
                         "Accent.Default",
                         "Status.Success",
                         "Status.Warning",
                         "Status.Danger",
                         "Status.Info"
                     })
            {
                if (!themeKeys.Contains(
                        $"Fluent.{mode}.{key}.Color"))
                {
                    errs.Add(
                        $"Missing Fluent {mode} palette token '{key}'.");
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
            var style = themeDocs
                .SelectMany(document => document.Descendants())
                .FirstOrDefault(node =>
                    node.Name.LocalName == "Style" &&
                    Target(
                        node.Attribute("TargetType")?.Value,
                        contract.Target) &&
                    string.Equals(
                        node.Attribute(xkey)?.Value,
                        contract.Key,
                        StringComparison.Ordinal));

            var focus = style?
                .Descendants()
                .Any(node =>
                    node.Name.LocalName == "Trigger" &&
                    node.Attribute("Property")?.Value is
                        "IsKeyboardFocused" or
                        "IsKeyboardFocusWithin") == true;

            if (!focus)
            {
                errs.Add(
                    $"Fluent {contract.Target} contract requires a visible keyboard focus state.");
            }
        }

        foreach (var file in files)
        {
            var rel = Path.GetRelativePath(repo, file);
            var text = File.ReadAllText(file);
            var doc = XDocument.Parse(
                text,
                LoadOptions.SetLineInfo);
            var rootNode = doc.Root;

            if (text.Contains(
                    "Language.IetfLanguageTag",
                    StringComparison.Ordinal))
            {
                errs.Add(
                    $"{rel}: language-tag direction triggers are forbidden; use central dynamic resources.");
            }

            if (rootNode?.Name.LocalName is "Window" or "UserControl")
            {
                var rootFlow =
                    rootNode.Attribute("FlowDirection")?.Value;

                if (!string.Equals(
                        rootFlow,
                        "{DynamicResource App.FlowDirection}",
                        StringComparison.Ordinal))
                {
                    errs.Add(
                        $"{rel}: root FlowDirection must use App.FlowDirection.");
                }
            }

            var titleBar = doc
                .Descendants()
                .FirstOrDefault(node =>
                    node.Name.LocalName == "Border" &&
                    (node.Attribute("Style")?.Value.Contains(
                         "R3DialogTopBar",
                         StringComparison.Ordinal) == true ||
                     node.Attribute("Style")?.Value.Contains(
                         "R3ShellTopBar",
                         StringComparison.Ordinal) == true));

            var titleGrid = titleBar?
                .Elements()
                .FirstOrDefault(
                    node => node.Name.LocalName == "Grid");

            var titleGridFlow = titleGrid?
                .Attribute("FlowDirection")
                ?.Value;
            if (titleGrid is not null &&
                !string.Equals(
                    titleGridFlow,
                    "{DynamicResource App.CaptionButtonsFlowDirection}",
                    StringComparison.Ordinal) &&
                !string.Equals(
                    titleGridFlow,
                    "LeftToRight",
                    StringComparison.Ordinal))
            {
                errs.Add(
                    $"{rel}: Windows caption grid must use the centralized language direction contract.");
            }

            if (Regex.IsMatch(
                    text,
                    "TextAlignment\\s*=\\s*\"(?:Right|Justify)\"|FlowDirection\\s*=\\s*\"RightToLeft\"",
                    RegexOptions.CultureInvariant))
            {
                errs.Add(
                    $"{rel}: forbidden hard-coded mirroring/right/justify value.");
            }

            foreach (var attr in doc.Descendants().Attributes())
            {
                var val = attr.Value.Trim();

                if (attr.Name.LocalName.EndsWith(
                        "Margin",
                        StringComparison.Ordinal) &&
                    Regex.IsMatch(
                        val,
                        "(?:^|,)\\s*-\\d",
                        RegexOptions.CultureInvariant))
                {
                    errs.Add(
                        $"{rel}: negative margin '{val}'.");
                }

                if (!rel.EndsWith(
                        Path.Combine("Thm", "Colors.xaml"),
                        StringComparison.OrdinalIgnoreCase) &&
                    IsColor(attr.Name.LocalName, val))
                {
                    errs.Add(
                        $"{rel}: literal color '{val}' outside Colors.xaml.");
                }

                if (!rel.StartsWith(
                        Path.Combine("App", "Thm") + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase) &&
                    attr.Name.LocalName == "CornerRadius" &&
                    attr.Parent?.Name.LocalName != "WindowChrome" &&
                    !val.StartsWith('{'))
                {
                    errs.Add(
                        $"{rel}: literal CornerRadius '{val}' outside Fluent tokens.");
                }
            }

            var allowedFontSizes = new HashSet<double>
            {
                12,
                13,
                14,
                16,
                20,
                24,
                28
            };

            foreach (var node in doc.Descendants())
            {
                var raw = node.Attribute("FontSize")?.Value;

                if (node.Name.LocalName == "Setter" &&
                    node.Attribute("Property")?.Value == "FontSize")
                {
                    raw = node.Attribute("Value")?.Value;
                }

                if (UsesSegoeFluentIcons(node))
                {
                    continue;
                }

                if (double.TryParse(
                        raw,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var size) &&
                    !allowedFontSizes.Contains(size))
                {
                    errs.Add(
                        $"{rel}: font size {size:0.##} is outside the Fluent 2 type ramp.");
                }
            }

            foreach (var button in doc
                         .Descendants()
                         .Where(node =>
                             node.Name.LocalName == "Button" &&
                             (node.Attribute("Style")?.Value.Contains(
                                  "FluentButton.Icon",
                                  StringComparison.Ordinal) == true ||
                              node.Attribute("Style")?.Value.Contains(
                                  "FluentButton.Close",
                                  StringComparison.Ordinal) == true)))
            {
                var raw = button.ToString(
                    SaveOptions.DisableFormatting);

                if (button.Attribute("ToolTip") is null ||
                    !raw.Contains(
                        "AutomationProperties.Name=",
                        StringComparison.Ordinal))
                {
                    errs.Add(
                        $"{rel}: icon button requires ToolTip and AutomationProperties.Name.");
                }
            }

            if (!rel.StartsWith(
                    Path.Combine("App", "Thm") + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                foreach (var button in doc
                             .Descendants()
                             .Where(node =>
                                 node.Name.LocalName == "Button"))
                {
                    foreach (var attrName in new[]
                             {
                                 "MinWidth",
                                 "MinHeight",
                                 "Padding"
                             })
                    {
                        var value =
                            button.Attribute(attrName)?.Value;

                        if (value is not null &&
                            double.TryParse(
                                value.Split(',')[0],
                                NumberStyles.Float,
                                CultureInfo.InvariantCulture,
                                out _))
                        {
                            errs.Add(
                                $"{rel}: Button {attrName} must come from a Fluent button style.");
                        }
                    }
                }
            }

            foreach (var tab in doc
                         .Descendants()
                         .Where(node =>
                             node.Name.LocalName == "TabControl" &&
                             node.Ancestors().Any(
                                 parent =>
                                     parent.Name.LocalName ==
                                     "TabControl")))
            {
                errs.Add(
                    $"{rel}: nested TabControl is forbidden; use a separate window or surface.");
            }

            foreach (var grid in doc
                         .Descendants()
                         .Where(node =>
                             node.Name.LocalName == "DataGrid" &&
                             node.Ancestors().Any(
                                 parent =>
                                     parent.Name.LocalName ==
                                     "DataGrid")))
            {
                errs.Add(
                    $"{rel}: nested DataGrid is forbidden.");
            }

            foreach (var grid in doc
                         .Descendants()
                         .Where(node =>
                             node.Name.LocalName == "DataGrid"))
            {
                var hpol = grid
                    .Attributes()
                    .Any(attr =>
                        attr.Name.LocalName ==
                        "ScrollViewer.HorizontalScrollBarVisibility");

                var vpol = grid
                    .Attributes()
                    .Any(attr =>
                        attr.Name.LocalName ==
                        "ScrollViewer.VerticalScrollBarVisibility");

                if (!hpol || !vpol)
                {
                    errs.Add(
                        $"{rel}: DataGrid must declare horizontal and vertical scroll policy.");
                }

                var direct = grid
                    .Attribute("Style")
                    ?.Value
                    .Contains(
                        "FluentDataGrid",
                        StringComparison.Ordinal) == true;

                var local = grid
                    .Elements()
                    .Where(node =>
                        node.Name.LocalName == "DataGrid.Style")
                    .Descendants()
                    .Any(node =>
                        node.Name.LocalName == "Style" &&
                        node.Attribute("BasedOn")
                            ?.Value
                            .Contains(
                                "FluentDataGrid",
                                StringComparison.Ordinal) == true);

                if (!direct && !local)
                {
                    errs.Add(
                        $"{rel}: DataGrid must use FluentDataGrid.");
                }
            }

            foreach (var col in doc
                         .Descendants()
                         .Where(node =>
                             node.Name.LocalName.StartsWith(
                                 "DataGrid",
                                 StringComparison.Ordinal) &&
                             node.Name.LocalName.EndsWith(
                                 "Column",
                                 StringComparison.Ordinal)))
            {
                var width = col.Attribute("Width")?.Value;

                if (double.TryParse(
                        width,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var size) &&
                    size > 160 &&
                    (col.Attribute("MinWidth") is null ||
                     col.Attribute("MaxWidth") is null))
                {
                    errs.Add(
                        $"{rel}: fixed DataGrid column width {size} lacks MinWidth/MaxWidth.");
                }
            }

            if (!file.StartsWith(
                    Path.Combine(appDir, "Thm") + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                foreach (var style in doc
                             .Descendants()
                             .Where(node =>
                                 node.Name.LocalName == "Style"))
                {
                    var key = style.Attribute(xkey)?.Value;

                    if (key is not null &&
                        keys.Contains(key))
                    {
                        errs.Add(
                            $"{rel}: local style '{key}' duplicates a shared theme dictionary.");
                    }
                }
            }

            foreach (var tab in doc
                         .Descendants()
                         .Where(node =>
                             node.Name.LocalName == "TabItem"))
            {
                foreach (var view in tab
                             .Descendants()
                             .Where(node =>
                                 node.Name.LocalName ==
                                 "ScrollViewer"))
                {
                    var nested = view
                        .Ancestors()
                        .TakeWhile(node => node != tab)
                        .Any(node =>
                            node.Name.LocalName ==
                            "ScrollViewer");

                    if (nested && VScroll(view))
                    {
                        errs.Add(
                            $"{rel}: nested vertical ScrollViewer inside one TabItem.");
                    }
                }
            }

            foreach (var trig in doc
                         .Descendants()
                         .Where(node =>
                             node.Name.LocalName == "Trigger" &&
                             node.Attribute("Property")?.Value ==
                             "IsEnabled" &&
                             node.Attribute("Value")?.Value ==
                             "False"))
            {
                var props = trig
                    .Elements()
                    .Where(node =>
                        node.Name.LocalName == "Setter")
                    .Select(node =>
                        node.Attribute("Property")?.Value)
                    .Where(val => val is not null)
                    .ToArray();

                if (props.Length > 0 &&
                    props.All(val => val == "Opacity"))
                {
                    errs.Add(
                        $"{rel}: disabled state uses opacity alone.");
                }
            }
        }

        Check(
            errs.Count == 0,
            string.Join(Environment.NewLine, errs));

        Console.WriteLine(
            $"UI XAML gate PASS ({files.Length} files).");
    }

    private static void VerifyFrozenDirectionContract(
        IEnumerable<string> files,
        string appDir,
        List<string> errs)
    {
        const int expectedCount = 811;
        const string expectedSha256 =
            "202cfd323807a58f49530b7f322b3002df8b29033104b080100a6b35879383e3";
        var propertyNames = new HashSet<string>(
            [
                "FlowDirection",
                "TextAlignment",
                "TextBlock.TextAlignment",
                "HorizontalContentAlignment"
            ],
            StringComparer.Ordinal);
        var entries = new List<string>();

        foreach (var path in files
                     .OrderBy(
                         item => Path.GetRelativePath(appDir, item),
                         StringComparer.Ordinal))
        {
            var relativePath = Path
                .GetRelativePath(appDir, path)
                .Replace('\\', '/');
            var document = XDocument.Load(path);
            var nodes = document
                .Root!
                .DescendantsAndSelf()
                .ToArray();
            var propertyOrdinal = 0;

            for (var index = 0; index < nodes.Length; index++)
            {
                var node = nodes[index];

                foreach (var attribute in node.Attributes()
                             .Where(attribute =>
                                 propertyNames.Contains(
                                     attribute.Name.LocalName))
                             .OrderBy(
                                 attribute => attribute.Name.LocalName,
                                 StringComparer.Ordinal))
                {
                    entries.Add(
                        $"{relativePath}|{propertyOrdinal++}|{node.Name.LocalName}|@{attribute.Name.LocalName}={attribute.Value.Trim()}");
                }

                if (node.Name.LocalName != "Setter")
                {
                    continue;
                }

                var property = node
                    .Attribute("Property")
                    ?.Value
                    .Trim();
                if (property is null ||
                    !propertyNames.Contains(property))
                {
                    continue;
                }

                var value = node
                    .Attribute("Value")
                    ?.Value
                    .Trim() ?? string.Concat(
                        node.Elements().Select(element =>
                            element.ToString(
                                SaveOptions.DisableFormatting)));
                entries.Add(
                    $"{relativePath}|{propertyOrdinal++}|Setter|{property}={value}");
            }
        }

        var bytes = Encoding.UTF8.GetBytes(
            string.Join("\n", entries));
        var hash = Convert
            .ToHexString(SHA256.HashData(bytes))
            .ToLowerInvariant();

        if (entries.Count != expectedCount ||
            !string.Equals(
                hash,
                expectedSha256,
                StringComparison.Ordinal))
        {
            errs.Add(
                "The frozen XAML direction/alignment contract changed. " +
                $"Expected {expectedCount}/{expectedSha256}; " +
                $"actual {entries.Count}/{hash}.");
        }
    }

    private static void VerifyInteractionStateContract(
        IEnumerable<string> files,
        string appDir,
        List<string> errs)
    {
        var xkey = XName.Get(
            "Key",
            "http://schemas.microsoft.com/winfx/2006/xaml");
        var controls = XDocument.Load(
            Path.Combine(
                appDir,
                "Thm",
                "Ctrl.xaml"));
        var styles = controls
            .Descendants()
            .Where(node =>
                node.Name.LocalName == "Style")
            .ToArray();
        var selectionStyles =
            new (string Name, XElement? Style)[]
            {
                (
                    "GameRailItem",
                    styles.SingleOrDefault(style =>
                        style.Attribute(xkey)?.Value ==
                        "GameRailItem")),
                (
                    "ComboBoxItem",
                    styles.SingleOrDefault(style =>
                        style.Attribute(xkey) is null &&
                        style.Attribute("TargetType")?.Value ==
                        "{x:Type ComboBoxItem}")),
                (
                    "ListBoxItem",
                    styles.SingleOrDefault(style =>
                        style.Attribute(xkey) is null &&
                        style.Attribute("TargetType")?.Value ==
                        "{x:Type ListBoxItem}")),
                (
                    "DataGridRow",
                    styles.SingleOrDefault(style =>
                        style.Attribute(xkey) is null &&
                        style.Attribute("TargetType")?.Value ==
                        "DataGridRow"))
            };

        foreach (var (name, style) in selectionStyles)
        {
            if (style is null)
            {
                errs.Add(
                    $"Missing centralized {name} interaction style.");
                continue;
            }

            var text = style.ToString(
                SaveOptions.DisableFormatting);
            if (!text.Contains(
                    "Surface.CardHover",
                    StringComparison.Ordinal) ||
                !text.Contains(
                    "Selection.Default",
                    StringComparison.Ordinal) ||
                !text.Contains(
                    "Selection.Inactive",
                    StringComparison.Ordinal) ||
                !text.Contains(
                    "Stroke.Focus",
                    StringComparison.Ordinal))
            {
                errs.Add(
                    $"{name} must define distinct hover, selected, inactive-selected, and keyboard-focus states.");
            }

            foreach (var trigger in style
                         .Descendants()
                         .Where(node =>
                             node.Name.LocalName == "Trigger" &&
                             node.Attribute("Property")
                                 ?.Value
                                 .Contains(
                                     "Focus",
                                     StringComparison.Ordinal) ==
                             true))
            {
                if (trigger
                    .Elements()
                    .Any(setter =>
                        setter.Name.LocalName == "Setter" &&
                        setter.Attribute("Property")?.Value ==
                        "Background"))
                {
                    errs.Add(
                        $"{name} keyboard focus must use the focus stroke without impersonating hover or selection.");
                }
            }
        }

        var tabStyle = styles.SingleOrDefault(style =>
            style.Attribute(xkey)?.Value ==
            "FluentTabItem");
        var tabText = tabStyle?.ToString(
            SaveOptions.DisableFormatting) ??
            string.Empty;
        if (!tabText.Contains(
                "Surface.CardHover",
                StringComparison.Ordinal) ||
            !tabText.Contains(
                "SelectionIndicator",
                StringComparison.Ordinal) ||
            !tabText.Contains(
                "Selection.Inactive",
                StringComparison.Ordinal) ||
            !tabText.Contains(
                "Stroke.Focus",
                StringComparison.Ordinal) ||
            !tabText.Contains(
                "SourceName=\"TabSurface\"",
                StringComparison.Ordinal))
        {
            errs.Add(
                "FluentTabItem must scope hover to its header surface and keep hover, selected indicator, inactive selection, and keyboard focus visually distinct.");
        }

        var checkBoxStyles = new (string Name, XElement? Style)[]
        {
            (
                "CheckBox",
                styles.SingleOrDefault(style =>
                    style.Attribute(xkey) is null &&
                    style.Attribute("TargetType")?.Value is
                        "CheckBox" or "{x:Type CheckBox}")),
            (
                "IconCheckBox",
                styles.SingleOrDefault(style =>
                    style.Attribute(xkey)?.Value ==
                    "IconCheckBox"))
        };

        foreach (var (name, style) in checkBoxStyles)
        {
            if (style is null)
            {
                errs.Add(
                    $"Missing centralized {name} interaction style.");
                continue;
            }

            var text = style.ToString(
                SaveOptions.DisableFormatting);
            var requiredStates = new[]
            {
                "HoverSurface",
                "Stroke.Strong",
                "Accent.Disabled",
                "Accent.Hover",
                "Accent.Pressed",
                "Stroke.Focus",
                "Stroke.Thickness.Focus",
                "IsPressed",
                "Property=\"Cursor\" Value=\"Hand\""
            };

            foreach (var requiredState in requiredStates.Where(
                         requiredState => !text.Contains(
                             requiredState,
                             StringComparison.Ordinal)))
            {
                errs.Add(
                    $"{name} must expose a strong idle border, visible hover surface, accent hover/press, and keyboard-focus states. Missing '{requiredState}'.");
            }
        }

        var iconCheckBox = checkBoxStyles
            .Single(item => item.Name == "IconCheckBox")
            .Style;
        var iconCheckBoxWidth = iconCheckBox
            ?.Elements()
            .FirstOrDefault(node =>
                node.Name.LocalName == "Setter" &&
                node.Attribute("Property")?.Value == "Width")
            ?.Attribute("Value")
            ?.Value;
        var iconCheckBoxHeight = iconCheckBox
            ?.Elements()
            .FirstOrDefault(node =>
                node.Name.LocalName == "Setter" &&
                node.Attribute("Property")?.Value == "Height")
            ?.Attribute("Value")
            ?.Value;

        if (!string.Equals(
                iconCheckBoxWidth,
                "28",
                StringComparison.Ordinal) ||
            !string.Equals(
                iconCheckBoxHeight,
                "28",
                StringComparison.Ordinal))
        {
            errs.Add(
                "IconCheckBox must keep a 28x28 interaction target around the 20x20 visual box.");
        }

        var tooltipStyle = styles.SingleOrDefault(style =>
            style.Attribute(xkey)?.Value ==
            "ModernToolTip");
        var staysOpen = tooltipStyle
            ?.Elements()
            .FirstOrDefault(node =>
                node.Name.LocalName == "Setter" &&
                node.Attribute("Property")?.Value ==
                "StaysOpen");
        if (staysOpen is not null)
        {
            errs.Add(
                "ModernToolTip must leave StaysOpen at the framework default.");
        }

        var tooltipPlacement = tooltipStyle
            ?.Elements()
            .FirstOrDefault(node =>
                node.Name.LocalName == "Setter" &&
                node.Attribute("Property")?.Value == "Placement")
            ?.Attribute("Value")
            ?.Value;
        var tooltipVerticalOffset = tooltipStyle
            ?.Elements()
            .FirstOrDefault(node =>
                node.Name.LocalName == "Setter" &&
                node.Attribute("Property")?.Value == "VerticalOffset")
            ?.Attribute("Value")
            ?.Value;
        if (!string.Equals(
                tooltipPlacement,
                "Bottom",
                StringComparison.Ordinal) ||
            !string.Equals(
                tooltipVerticalOffset,
                "8",
                StringComparison.Ordinal))
        {
            errs.Add(
                "ModernToolTip must use stable Bottom placement with an 8px vertical offset.");
        }

        var checklistTooltip = styles.SingleOrDefault(style =>
            style.Attribute(xkey)?.Value ==
            "ChecklistDetailToolTip");
        if (checklistTooltip is null ||
            checklistTooltip.Attribute("BasedOn")
                ?.Value
                .Contains(
                    "ModernToolTip",
                    StringComparison.Ordinal) != true)
        {
            errs.Add(
                "ChecklistDetailToolTip must derive from ModernToolTip.");
        }

        var tooltipPolicyPath = Path.Combine(
            appDir,
            "Svc",
            "ToolTipPolicy.cs");
        var appCodePath = Path.Combine(
            appDir,
            "App.xaml.cs");
        if (!File.Exists(tooltipPolicyPath))
        {
            errs.Add(
                "Missing centralized ToolTip timing policy.");
        }
        else
        {
            var tooltipPolicy = File.ReadAllText(
                tooltipPolicyPath);
            foreach (var contract in new[]
                     {
                         "InitialShowDelayMs = 600",
                         "BetweenShowDelayMs = 150",
                         "ShowDurationMs = 6000",
                         "ToolTipService.SetInitialShowDelay",
                         "ToolTipService.SetBetweenShowDelay",
                         "ToolTipService.SetShowDuration",
                         "ToolTipService.SetShowOnDisabled"
                     })
            {
                if (!tooltipPolicy.Contains(
                        contract,
                        StringComparison.Ordinal))
                {
                    errs.Add(
                        $"ToolTip timing policy is missing '{contract}'.");
                }
            }
        }

        if (!File.ReadAllText(appCodePath).Contains(
                "ToolTipPolicy.Register();",
                StringComparison.Ordinal))
        {
            errs.Add(
                "Application startup must register the centralized ToolTip timing policy before creating windows.");
        }

        foreach (var path in files)
        {
            var document = XDocument.Load(path);
            var relativePath = Path.GetRelativePath(appDir, path);

            foreach (var checkBox in document
                         .Descendants()
                         .Where(node =>
                             node.Name.LocalName == "CheckBox" &&
                             node.Attribute("Style")
                                 ?.Value
                                 .Contains(
                                     "IconCheckBox",
                                     StringComparison.Ordinal) == true))
            {
                foreach (var attributeName in new[]
                         {
                             "Width",
                             "Height",
                             "MinWidth",
                             "MinHeight"
                         })
                {
                    var value = checkBox
                        .Attribute(attributeName)
                        ?.Value;
                    if (value is not null &&
                        double.TryParse(
                            value,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out var size) &&
                        size < 28)
                    {
                        errs.Add(
                            $"{relativePath}: IconCheckBox must not override {attributeName} below the centralized 28px interaction target.");
                    }
                }
            }

            foreach (var node in document.Descendants())
            {
                var directPlacement =
                    node.Attribute("Placement")?.Value;
                var servicePlacement = node
                    .Attributes()
                    .FirstOrDefault(attribute =>
                        attribute.Name.LocalName ==
                        "ToolTipService.Placement")
                    ?.Value;

                if (string.Equals(
                        directPlacement,
                        "Mouse",
                        StringComparison.Ordinal) ||
                    string.Equals(
                        servicePlacement,
                        "Mouse",
                        StringComparison.Ordinal))
                {
                    errs.Add(
                        $"{relativePath}: mouse-position ToolTips are forbidden; use the centralized stable placement.");
                }

                if (node.Attributes().Any(attribute =>
                        attribute.Name.LocalName is
                            "ToolTipService.InitialShowDelay" or
                            "ToolTipService.BetweenShowDelay" or
                            "ToolTipService.ShowDuration" or
                            "ToolTipService.ShowOnDisabled") ||
                    (node.Name.LocalName == "Setter" &&
                     node.Attribute("Property")?.Value is
                         "ToolTipService.InitialShowDelay" or
                         "ToolTipService.BetweenShowDelay" or
                         "ToolTipService.ShowDuration" or
                         "ToolTipService.ShowOnDisabled"))
                {
                    errs.Add(
                        $"{relativePath}: local ToolTip timing overrides are forbidden; ToolTipPolicy is authoritative.");
                }
            }

            foreach (var tooltipProperty in document
                         .Descendants()
                         .Where(node =>
                             node.Name.LocalName.EndsWith(
                                 ".ToolTip",
                                 StringComparison.Ordinal)))
            {
                var owner = tooltipProperty.Parent;
                if (owner is null)
                {
                    continue;
                }

                var competingOwner = owner
                    .Descendants()
                    .FirstOrDefault(node =>
                        !node
                            .AncestorsAndSelf()
                            .Contains(tooltipProperty) &&
                        (node.Attribute("ToolTip") is not null ||
                         (node.Name.LocalName.EndsWith(
                              ".ToolTip",
                              StringComparison.Ordinal) &&
                          node != tooltipProperty)));

                if (competingOwner is not null)
                {
                    errs.Add(
                        $"{relativePath}: '{owner.Name.LocalName}' owns a detail ToolTip and a descendant owns a competing ToolTip. One list row must have one ToolTip owner.");
                }
            }

            foreach (var surface in document
                         .Descendants()
                         .Where(node =>
                             (node.Name.LocalName is
                                 "ListBox" or
                                 "DataGrid") &&
                             node.Attribute("ToolTip") is not null))
            {
                errs.Add(
                    $"{relativePath}: a full {surface.Name.LocalName} surface must not own a ToolTip.");
            }
        }

        var recommendationViewPath = Path.Combine(
            appDir,
            "Ctrl",
            "Rec.xaml");
        var recommendationView = File.ReadAllText(
            recommendationViewPath);
        if (!recommendationView.Contains(
                "Key=Plan.WikiInstructions",
                StringComparison.Ordinal) ||
            !recommendationView.Contains(
                "RecToolTipSectionLabelText",
                StringComparison.Ordinal) ||
            !recommendationView.Contains(
                "RecToolTipTechnicalValueText",
                StringComparison.Ordinal))
        {
            errs.Add(
                "Recommendation notes must use vertical current/proposed sections followed by a dedicated Wiki instructions section.");
        }

        if (!recommendationView.Contains(
                "Visibility=\"{Binding Plan.HasChg,",
                StringComparison.Ordinal))
        {
            errs.Add(
                "The settings Apply action must stay hidden until the user selects a real settings change.");
        }

        var setupView = File.ReadAllText(Path.Combine(
            appDir,
            "Ctrl",
            "Setup.xaml"));
        if (!setupView.Contains(
                "Visibility=\"{Binding SetupCanApply,",
                StringComparison.Ordinal))
        {
            errs.Add(
                "The change-summary Apply action must stay hidden until an applyable delta exists.");
        }

        var patchShell = File.ReadAllText(Path.Combine(
            appDir,
            "Ctrl",
            "Pat.xaml"));
        var officialPatchView = File.ReadAllText(Path.Combine(
            appDir,
            "Ctrl",
            "POff.xaml"));
        var canaryPatchView = File.ReadAllText(Path.Combine(
            appDir,
            "Ctrl",
            "PCan.xaml"));
        var patchTemplates = File.ReadAllText(Path.Combine(
            appDir,
            "Ctrl",
            "PTmp.xaml"));

        if (patchShell.Contains("FingerprintCaptureStatus", StringComparison.Ordinal) ||
            patchShell.Contains("FingerprintCaptureCmd", StringComparison.Ordinal) ||
            patchShell.Contains("Patch.Launch.BootRecommended", StringComparison.Ordinal))
        {
            errs.Add(
                "The shared patch shell must not expose verification controls or persistent Boot Game guidance; both belong to the action that actually needs them.");
        }

        if (!canaryPatchView.Contains("ShowCanaryVerificationPanel", StringComparison.Ordinal) ||
            !canaryPatchView.Contains("PatchReadiness.VerifyAction", StringComparison.Ordinal) ||
            !canaryPatchView.Contains("PatchReadiness.ReverifyAction", StringComparison.Ordinal) ||
            !canaryPatchView.Contains("ShowCanaryReverificationAction", StringComparison.Ordinal) ||
            !officialPatchView.Contains("Preview.OfclEmptyText", StringComparison.Ordinal) ||
            !canaryPatchView.Contains("Preview.CanEmptyText", StringComparison.Ordinal))
        {
            errs.Add(
                "Patch compatibility guidance must be contextual: precise empty states on official/Canary pages, first-time verification, and explicit Canary mismatch reverification.");
        }

        if (!patchTemplates.Contains("Text=\"{Binding ArtemisTitle}\"", StringComparison.Ordinal) ||
            !patchTemplates.Contains("Text=\"{Binding NoteBody}\"", StringComparison.Ordinal) ||
            !patchTemplates.Contains("Visibility=\"{Binding HasDesc,", StringComparison.Ordinal) ||
            !patchTemplates.Contains("ToolTipService.IsEnabled=\"{Binding HasArtemisTip}\"", StringComparison.Ordinal) ||
            !patchTemplates.Contains("<Border.ToolTip>", StringComparison.Ordinal) ||
            !patchTemplates.Contains("Text=\"{Binding ArtemisTip}\"", StringComparison.Ordinal))
        {
            errs.Add(
                "Artemis rows must keep cleaned titles separate from localized Notes, suppress technical prose in the primary list, and preserve details in the row ToolTip.");
        }

        var mainView = File.ReadAllText(Path.Combine(appDir, "Main.xaml"));
        var cheatPatchView = File.ReadAllText(Path.Combine(appDir, "Ctrl", "PCht.xaml"));
        if (mainView.Contains("PatReady", StringComparison.Ordinal) ||
            mainView.Contains("ReadyTip", StringComparison.Ordinal) ||
            mainView.Contains("ReadyTon", StringComparison.Ordinal) ||
            !cheatPatchView.Contains("Preview.ChtEmptyText", StringComparison.Ordinal))
        {
            errs.Add(
                "Compatibility state must stay inside patch surfaces, and Artemis must use one precise dynamic empty-state sentence.");
        }

        if (recommendationView.Contains(
                "Grid.Column=\"4\" Text=\"{lang:TextExt Key=Plan.Proposed}\"",
                StringComparison.Ordinal) ||
            recommendationView.Contains(
                "Text=\"{Binding BeginSum}\" Style=\"{StaticResource RecLocalizedBodyText}\"",
                StringComparison.Ordinal) ||
            recommendationView.Contains(
                "Text=\"{Binding DspPath}\" Style=\"{StaticResource ChecklistToolTipSubtleText}\"",
                StringComparison.Ordinal))
        {
            errs.Add(
                "Recommendation ToolTips must not restore the old horizontal comparison or redundant summary/path lines.");
        }

        var patchTemplate = XDocument.Load(
            Path.Combine(
                appDir,
                "Ctrl",
                "PTmp.xaml"));
        var mergesPatchStyles = patchTemplate
            .Descendants()
            .Any(node =>
                node.Name.LocalName ==
                "ResourceDictionary" &&
                node.Attribute("Source")?.Value ==
                "PSty.xaml");
        if (!mergesPatchStyles)
        {
            errs.Add(
                "PTmp.xaml must merge PSty.xaml before resolving PatStatusBadge and related template resources.");
        }
    }

    private static bool UsesSegoeFluentIcons(XElement node)
    {
        const string expectedFontFamily = "Segoe Fluent Icons";

        var directFontFamily =
            node.Attribute("FontFamily")?.Value;

        if (ContainsFontFamily(
                directFontFamily,
                expectedFontFamily))
        {
            return true;
        }

        if (node.Name.LocalName != "Setter" ||
            node.Attribute("Property")?.Value != "FontSize")
        {
            return false;
        }

        var style = node
            .Ancestors()
            .FirstOrDefault(ancestor =>
                ancestor.Name.LocalName == "Style");

        if (style is null)
        {
            return false;
        }

        var styleFontFamily = style
            .Elements()
            .FirstOrDefault(child =>
                child.Name.LocalName == "Setter" &&
                child.Attribute("Property")?.Value ==
                "FontFamily")
            ?.Attribute("Value")
            ?.Value;

        return ContainsFontFamily(
            styleFontFamily,
            expectedFontFamily);
    }

    private static bool ContainsFontFamily(
        string? value,
        string expectedFontFamily)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value
            .Split(
                ',',
                StringSplitOptions.TrimEntries |
                StringSplitOptions.RemoveEmptyEntries)
            .Any(fontFamily =>
                string.Equals(
                    fontFamily,
                    expectedFontFamily,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static bool Target(
        string? value,
        string name)
    {
        return string.Equals(
                   value,
                   name,
                   StringComparison.Ordinal) ||
               string.Equals(
                   value,
                   $"{{x:Type {name}}}",
                   StringComparison.Ordinal);
    }

    private static bool IsColor(
        string name,
        string val)
    {
        if (val.StartsWith('{') ||
            string.Equals(
                val,
                "Transparent",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!name.Contains(
                "Color",
                StringComparison.Ordinal) &&
            !name.Contains(
                "Brush",
                StringComparison.Ordinal) &&
            name is not
                ("Background" or
                 "Foreground" or
                 "Fill" or
                 "Stroke"))
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
        var val = view
            .Attributes()
            .FirstOrDefault(attr =>
                attr.Name.LocalName is
                    "VerticalScrollBarVisibility" or
                    "ScrollViewer.VerticalScrollBarVisibility")
            ?.Value;

        return !string.Equals(
            val,
            "Disabled",
            StringComparison.Ordinal);
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
        work.IsBackground = true;
        work.Start();
        if (!work.Join(TimeSpan.FromMinutes(2)))
        {
            Application.Current?.Dispatcher.BeginInvokeShutdown(
                DispatcherPriority.Send);
            _ = work.Join(TimeSpan.FromSeconds(5));
            throw new TimeoutException(
                "Runtime UI layout gate did not shut down within two minutes.");
        }

        if (err is not null)
        {
            throw new InvalidOperationException(
                "Runtime UI layout gate failed.",
                err);
        }
    }

    private static void VerifyBusyOverlay()
    {
        var originalLanguage = UiText.CurLang;
        try
        {
            string[] languages =
            [
                UiText.ArabLang,
                UiText.EnLang
            ];

            foreach (var language in languages)
            {
                UiText.SetLang(language);
                UiLaySvc.Apply();

                var viewModel = new MainVm();
                var window = new MainWin(viewModel);
                window.Show();
                DrainDispatcher(window.Dispatcher);

                var content = window.FindName("ApplicationContent") as Grid
                    ?? throw new InvalidDataException(
                        "The main application content surface was not found.");
                var overlay = window.FindName("BusyOverlay") as Grid
                    ?? throw new InvalidDataException(
                        "The main busy overlay surface was not found.");
                var spinner = window.FindName("BusySpinner") as FrameworkElement
                    ?? throw new InvalidDataException(
                        "The main busy spinner was not found.");
                var message = window.FindName("BusyMessage") as TextBlock
                    ?? throw new InvalidDataException(
                        "The main busy message was not found.");

                Check(
                    overlay.Visibility == Visibility.Collapsed &&
                    content.IsEnabled &&
                    content.Effect is null,
                    "The main window must start outside the busy state.");

                SetMainBusy(viewModel, true);
                DrainDispatcher(window.Dispatcher);

                Check(
                    overlay.Visibility == Visibility.Visible &&
                    overlay.IsHitTestVisible &&
                    !content.IsEnabled &&
                    content.Effect is BlurEffect blur &&
                    blur.Radius > 0,
                    "Busy analysis must show the overlay, block the application surface, and apply blur.");

                Check(
                    !viewModel.AnlzCmd.CanRun(null),
                    "Analyze must reject re-entry while the busy overlay is active.");

                Check(
                    string.Equals(
                        viewModel.BusyText,
                        UiText.GetText("Busy.Analyze.ReadRpcs3"),
                        StringComparison.Ordinal) &&
                    string.Equals(
                        message.Text,
                        viewModel.BusyText,
                        StringComparison.Ordinal),
                    "The busy overlay did not use the localized initial analysis stage.");

                SetMainBusyStage(
                    viewModel,
                    AnalyzeStage.LoadPatches);
                DrainDispatcher(window.Dispatcher);

                Check(
                    string.Equals(
                        viewModel.BusyText,
                        UiText.GetText("Busy.Analyze.Patches"),
                        StringComparison.Ordinal) &&
                    string.Equals(
                        message.Text,
                        viewModel.BusyText,
                        StringComparison.Ordinal),
                    "The busy overlay stage text did not update through the analysis progress contract.");

                var rotate = spinner.RenderTransform as RotateTransform;
                Check(
                    rotate is not null && rotate.HasAnimatedProperties,
                    "The busy spinner must have an active indeterminate rotation animation.");

                SetMainBusy(viewModel, false);
                DrainDispatcher(window.Dispatcher);

                Check(
                    overlay.Visibility == Visibility.Collapsed &&
                    content.IsEnabled &&
                    content.Effect is null,
                    "Leaving the busy state must remove the overlay and blur and restore interaction.");

                VerifyAnalyzeFailureClearsBusy(
                    viewModel,
                    window,
                    content,
                    overlay);

                window.Close();
                DrainDispatcher(window.Dispatcher);
            }
        }
        finally
        {
            UiText.SetLang(originalLanguage);
            UiLaySvc.Apply();
        }
    }

    private static void VerifyAnalyzeFailureClearsBusy(
        MainVm viewModel,
        MainWin window,
        Grid content,
        Grid overlay)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "R3ConfigBusyOverlay",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var executablePath = Path.Combine(
            root,
            "rpcs3.exe");
        File.WriteAllBytes(
            executablePath,
            []);

        var catalogField = typeof(MainVm).GetField(
            "_catalog",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "MainVm recommendation catalog field was not found.");
        var analyzeMethod = typeof(MainVm).GetMethod(
            "DoAsync",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "MainVm analysis method was not found.");

        viewModel.ExePath = executablePath;
        catalogField.SetValue(
            viewModel,
            null);

        var clearedBeforeDialog = false;
        var dialogObserved = false;
        var timer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(10),
            DispatcherPriority.Send,
            (_, _) =>
            {
                var dialog = Application.Current?
                    .Windows
                    .OfType<AppDlg>()
                    .FirstOrDefault(item => item.IsVisible);
                if (dialog is null)
                {
                    return;
                }

                dialogObserved = true;
                clearedBeforeDialog =
                    !viewModel.IsBusy &&
                    overlay.Visibility == Visibility.Collapsed &&
                    content.IsEnabled &&
                    content.Effect is null;
                dialog.Close();
            },
            window.Dispatcher);

        try
        {
            timer.Start();
            var task = analyzeMethod.Invoke(
                viewModel,
                null) as Task
                ?? throw new InvalidOperationException(
                    "MainVm analysis did not return a Task.");
            var deadline = DateTime.UtcNow.AddSeconds(10);

            while (!task.IsCompleted &&
                   DateTime.UtcNow < deadline)
            {
                DrainDispatcher(window.Dispatcher);
            }

            Check(
                task.IsCompleted,
                "The analysis failure regression did not complete within ten seconds.");
            task.GetAwaiter().GetResult();

            Check(
                dialogObserved &&
                clearedBeforeDialog &&
                !viewModel.IsBusy &&
                overlay.Visibility == Visibility.Collapsed &&
                content.IsEnabled &&
                content.Effect is null,
                "An analysis exception must clear busy state before the error dialog is shown.");
        }
        finally
        {
            timer.Stop();
            Directory.Delete(
                root,
                recursive: true);
        }
    }

    private static void SetMainBusy(
        MainVm viewModel,
        bool value)
    {
        var setter = typeof(MainVm)
            .GetProperty(nameof(MainVm.IsBusy))
            ?.GetSetMethod(nonPublic: true)
            ?? throw new InvalidOperationException(
                "MainVm.IsBusy private setter was not found.");

        setter.Invoke(
            viewModel,
            [value]);
    }

    private static void SetMainBusyStage(
        MainVm viewModel,
        AnalyzeStage stage)
    {
        var method = typeof(MainVm).GetMethod(
            "SetBusyStage",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "MainVm busy-stage updater was not found.");

        method.Invoke(
            viewModel,
            [stage]);
    }

    private static void VerifyPatchModeVisibility()
    {
        var view = new R3Config.Ctrl.PatView
        {
            Mode = R3Config.Ctrl.PatMode.Cheat
        };
        var root = (FrameworkElement)view.FindName("PatRoot");
        var official = (FrameworkElement)view.FindName("OfclBody");
        var canary = (FrameworkElement)view.FindName("CanBody");
        var artemis = (FrameworkElement)view.FindName("ChtRoot");

        if (root.Visibility != Visibility.Visible ||
            artemis.Visibility != Visibility.Visible ||
            official.Visibility != Visibility.Collapsed ||
            canary.Visibility != Visibility.Collapsed)
        {
            throw new InvalidDataException(
                "Artemis mode must keep its parent and content visible while hiding the other patch surfaces.");
        }

        view.Mode = R3Config.Ctrl.PatMode.Patches;
        if (root.Visibility != Visibility.Visible ||
            official.Visibility != Visibility.Visible ||
            canary.Visibility != Visibility.Collapsed ||
            artemis.Visibility != Visibility.Collapsed)
        {
            throw new InvalidDataException(
                "Official patch mode visibility did not recover after leaving Artemis mode.");
        }
    }

    private static void RunUi()
    {
        var app = new App();
        app.InitializeComponent();
        app.ShutdownMode =
            ShutdownMode.OnExplicitShutdown;
        var dispatcherErrors = new List<Exception>();
        DispatcherUnhandledExceptionEventHandler dispatcherFailure =
            (_, eventArgs) =>
            {
                dispatcherErrors.Add(eventArgs.Exception);
                eventArgs.Handled = true;
            };
        app.DispatcherUnhandledException += dispatcherFailure;

        var buf = new StringBuilder();

        using var write = new StringWriter(
            buf,
            CultureInfo.InvariantCulture);

        using var trace =
            new TextWriterTraceListener(write);

        var source =
            PresentationTraceSources.DataBindingSource;

        var level = source.Switch.Level;

        source.Switch.Level = SourceLevels.Error;
        source.Listeners.Add(trace);
        PresentationTraceSources.Refresh();

        try
        {
            VerifyDispatcherFailureCapture(
                app.Dispatcher,
                dispatcherErrors);
            VerifyPatchModeVisibility();

            var themes = new[] { true, false };
            var langs = new[]
            {
                UiText.ArabLang,
                UiText.EnLang
            };
            var sizes = new[]
            {
                new Size(960, 620),
                new Size(1120, 700),
                new Size(1366, 768),
                new Size(1920, 1080)
            };
            var dpis = new[]
            {
                96d,
                120d,
                144d
            };

            var shotDir =
                Environment.GetEnvironmentVariable(
                    "R3_UI_SHOTS");

            var held = new List<Window>();

            foreach (var isDark in themes)
            {
                WinTheme.ApplyResources(isDark);

                foreach (var lang in langs)
                {
                    UiText.SetLang(lang);
                    UiLaySvc.Apply();
                    VerifyGameDataTextDirections();

                    var wins = Make();

                    try
                    {
                        if (!string.IsNullOrWhiteSpace(
                                shotDir))
                        {
                            Shots(
                                wins,
                                lang,
                                isDark,
                                shotDir);
                        }

                        foreach (var size in sizes)
                        {
                            foreach (var win in wins)
                            {
                                if (win is AdvDlg advanced)
                                {
                                    SetAdvancedSurface(
                                        advanced,
                                        showHCanary: false);
                                }

                                VerifySurface(
                                    win,
                                    size,
                                    dpis);

                                if (win is AdvDlg hCanaryHost)
                                {
                                    SetAdvancedSurface(
                                        hCanaryHost,
                                        showHCanary: true);

                                    VerifySurface(
                                        hCanaryHost,
                                        size,
                                        dpis);

                                    SetAdvancedSurface(
                                        hCanaryHost,
                                        showHCanary: false);
                                }
                            }
                        }

                        SelLang(wins[0], lang);
                    }
                    finally
                    {
                        if (!string.IsNullOrWhiteSpace(
                                shotDir))
                        {
                            held.AddRange(wins);
                        }
                        else
                        {
                            foreach (var win in wins)
                            {
                                win.Close();
                            }

                            DrainDispatcher(app.Dispatcher);
                        }
                    }
                }
            }

            foreach (var win in held)
            {
                win.Close();
            }

            DrainDispatcher(app.Dispatcher);

            if (dispatcherErrors.Count > 0)
            {
                throw new AggregateException(
                    "Runtime UI dispatcher callback failed.",
                    dispatcherErrors);
            }

            trace.Flush();

            var log = buf.ToString();

            Check(
                string.IsNullOrWhiteSpace(log),
                $"WPF binding error:{Environment.NewLine}{log}");

            Console.WriteLine(
                "Runtime UI gate PASS (8 windows plus embedded H-Canary workspace, 4 sizes, 3 DPI scales, 2 languages, 2 themes).");
        }
        finally
        {
            app.DispatcherUnhandledException -= dispatcherFailure;
            source.Listeners.Remove(trace);
            source.Switch.Level = level;
        }
    }

    private static void VerifyDispatcherFailureCapture(
        Dispatcher dispatcher,
        List<Exception> dispatcherErrors)
    {
        const string probeMessage =
            "Intentional runtime UI dispatcher failure probe.";
        dispatcher.BeginInvoke(
            DispatcherPriority.Normal,
            new Action(() => throw new InvalidOperationException(
                probeMessage)));
        DrainDispatcher(dispatcher);

        Check(
            dispatcherErrors.Count == 1 &&
            dispatcherErrors[0] is InvalidOperationException exception &&
            string.Equals(
                exception.Message,
                probeMessage,
                StringComparison.Ordinal),
            "The runtime UI gate did not capture its dispatcher failure probe. " +
            $"Captured {dispatcherErrors.Count}: " +
            string.Join(
                " | ",
                dispatcherErrors.Select(error =>
                    $"{error.GetType().Name}: {error.Message}")));
        dispatcherErrors.Clear();
    }

    private static void VerifyGameDataTextDirections()
    {
        const string latinTitle =
            "Assassin's Creed® Rogue";
        const string arabicTitle =
            "لعبة عربية";
        const string serial =
            "BLUS31465";

        var gameList = new ListVm();
        gameList.Load(
        [
            MakeGameRow(serial, latinTitle),
            MakeGameRow("BLES00000", arabicTitle)
        ]);

        var view = new R3Config.Ctrl.GameList
        {
            DataContext = new GameListDirectionContext(
                gameList)
        };
        var size = new Size(360, 480);
        view.Measure(size);
        view.Arrange(new Rect(size));
        view.UpdateLayout();

        var textBlocks =
            Walk<TextBlock>(view).ToArray();
        var latin = textBlocks.Single(text =>
            string.Equals(
                text.Text,
                latinTitle,
                StringComparison.Ordinal));
        var arabic = textBlocks.Single(text =>
            string.Equals(
                text.Text,
                arabicTitle,
                StringComparison.Ordinal));
        var technical = textBlocks.Single(text =>
            string.Equals(
                text.Text,
                serial,
                StringComparison.Ordinal));

        Check(
            latin.FlowDirection ==
                FlowDirection.LeftToRight &&
            string.Equals(
                latin.Language.IetfLanguageTag,
                UiText.EnLang,
                StringComparison.OrdinalIgnoreCase),
            "A Latin game title must remain LTR with Latin language metadata.");
        Check(
            arabic.FlowDirection ==
                FlowDirection.RightToLeft &&
            string.Equals(
                arabic.Language.IetfLanguageTag,
                UiText.ArabLang,
                StringComparison.OrdinalIgnoreCase),
            "An Arabic game title must remain RTL with Arabic language metadata.");
        Check(
            technical.FlowDirection ==
                FlowDirection.LeftToRight &&
            technical.TextAlignment ==
                TextAlignment.Left,
            "An RPCS3 serial must remain LTR and left-aligned.");

        Align(view);
    }

    private static GameVm MakeGameRow(
        string serial,
        string title)
    {
        var game = new Rpcs3GameDescriptor(
            Serial: serial,
            Title: title,
            InstPath: string.Empty,
            Version: "01.00",
            PpuHash: null,
            SpuHash: null);

        return new GameVm(
            game,
            recommendation: null,
            new RecSum(
                serial,
                0,
                0,
                0,
                0,
                0,
                0,
                []),
            serial,
            title,
            isDetectedInRpcs3: true);
    }

    private static void DrainDispatcher(Dispatcher dispatcher)
    {
        var frame = new DispatcherFrame();
        dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static void VerifySurface(
        Window window,
        Size size,
        IReadOnlyList<double> dpis)
    {
        Lay(window, size);
        Tabs(window, size);

        var root =
            (FrameworkElement)window.Content;

        Align(root);
        Bounds(root, size);
        RawLocalization(root);

        foreach (var dpi in dpis)
        {
            Dpi(window, size, dpi);
        }
    }

    private static void SetAdvancedSurface(
        AdvDlg window,
        bool showHCanary)
    {
        var navigation =
            window.FindName("AdvancedNavigation") as ListBox;

        var workspace =
            window.FindName("HCanaryWorkspace")
                as FrameworkElement;

        var recovery =
            window.FindName("RecoveryWorkspace")
                as FrameworkElement;

        var diagnostics =
            window.FindName("DiagnosticsWorkspace")
                as FrameworkElement;

        Check(
            navigation is not null &&
            workspace is not null &&
            recovery is not null &&
            diagnostics is not null,
            "Advanced Tools workspace surfaces were not found.");

        navigation!.SelectedIndex =
            showHCanary ? 0 : 1;

        workspace!.Visibility =
            showHCanary
                ? Visibility.Visible
                : Visibility.Collapsed;

        recovery!.Visibility =
            showHCanary
                ? Visibility.Collapsed
                : Visibility.Visible;

        diagnostics!.Visibility =
            Visibility.Collapsed;

        if (showHCanary)
        {
            ShowHCanaryWorkflow(workspace);
        }
    }

    private static void ShowHCanaryWorkflow(
        FrameworkElement workspace)
    {
        var actions =
            workspace.FindName("WorkflowActions")
                as FrameworkElement;

        var steps =
            workspace.FindName("Steps")
                as TabControl;

        var bottomBar =
            workspace.FindName("WorkflowBottomBar")
                as FrameworkElement;

        Check(
            actions is not null &&
            steps is not null &&
            bottomBar is not null,
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
            new AdvDlg
            {
                DataContext = toolsVm
            },
            new HCanDlg
            {
                DataContext = toolsVm
            },
            new CanDlg(
                null,
                canVm),
            new AppDlg(
                null,
                UiText.GetText("App.Title"),
                UiText.GetText("Adv.Description"),
                DlgKind.Confirm,
                true),
            new HCanSavedDlg
            {
                DataContext = toolsVm
            },
            new HCanTargetsDlg
            {
                DataContext = toolsVm
            },
            new HCanAnchorsDlg
            {
                DataContext = toolsVm
            }
        ];
    }

    private static void Lay(
        Window win,
        Size size)
    {
        LayRoot(win, size);
    }

    private static void Tabs(
        Window win,
        Size size)
    {
        var root =
            (FrameworkElement)win.Content;

        var seen = new HashSet<TabControl>();

        while (true)
        {
            var tabs = Walk<TabControl>(root)
                .Where(tab =>
                    tab.IsVisible &&
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

                for (var idx = 0;
                     idx < tab.Items.Count;
                     idx++)
                {
                    tab.SelectedIndex = idx;

                    Lay(win, size);
                    RawLocalization(root);
                    Align(tab);

                    Bounds(
                        tab,
                        new Size(
                            tab.ActualWidth,
                            tab.ActualHeight));
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
            var expectedRootFlow =
                ExpectedFlow(rootElement);

            Check(
                rootElement.FlowDirection ==
                expectedRootFlow,
                $"{rootElement.GetType().Name} root uses {rootElement.FlowDirection}; expected {expectedRootFlow}.");
        }

        foreach (var item in Walk<FrameworkElement>(root))
        {
            if (item is TextBlock text)
            {
                var label = TextBlockLabel(text);

                CheckTextFlow(
                    text,
                    text.Text,
                    label);

                var expected =
                    ExpectedTextAlignment(
                        text,
                        text.Text);

                Check(
                    text.TextAlignment == expected,
                    $"{label} uses {text.TextAlignment}; expected {expected}.");
            }
            else if (item is TextBox box)
            {
                CheckTextFlow(
                    box,
                    box.Text,
                    TextBoxLabel(box));

                var expected =
                    ExpectedTextAlignment(
                        box,
                        box.Text);

                Check(
                    box.TextAlignment == expected,
                    $"{TextBoxLabel(box)} uses {box.TextAlignment}; expected {expected}.");
            }
            else if (item is ComboBox combo)
            {
                var expectedFlow =
                    ExpectedFlow(combo);

                Check(
                    combo.FlowDirection ==
                    expectedFlow,
                    $"ComboBox uses {combo.FlowDirection}; expected {expectedFlow}.");
            }
            else if (item is CheckBox checkBox)
            {
                var expectedFlow =
                    ExpectedFlow(checkBox);

                Check(
                    checkBox.FlowDirection ==
                    expectedFlow,
                    $"CheckBox layout uses {checkBox.FlowDirection}; expected {expectedFlow}.");
            }
            else if (item is Button button &&
                     button.Content is string buttonText)
            {
                CheckTextFlow(
                    button,
                    buttonText,
                    $"Button '{buttonText}'");
            }
            else if (item is TabItem tabItem &&
                     tabItem.Header is string headerText)
            {
                var expectedFlow =
                    UiLaySvc.Current.TextFlow;

                Check(
                    tabItem.FlowDirection ==
                    expectedFlow,
                    $"TabItem layout uses {tabItem.FlowDirection}; expected {expectedFlow}.");

                var presenter =
                    Walk<ContentPresenter>(tabItem)
                        .FirstOrDefault(candidate =>
                            Equals(
                                candidate.Content,
                                tabItem.Header));

                Check(
                    presenter is not null,
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
        var expected = ExpectedFlow(
            element,
            text);

        Check(
            element.FlowDirection == expected,
            $"{label} uses {element.FlowDirection}; expected {expected}.");
    }

    private static FlowDirection ExpectedFlow(
        FrameworkElement element,
        string? text = null)
    {
        if (UsesContentFlowConverter(element))
        {
            return UiLaySvc.ResolveContentFlow(text);
        }

        return IsIconText(element) ||
               UsesTechnicalStyle(element) ||
               IsTechnical(element)
            ? FlowDirection.LeftToRight
            : UiLaySvc.Current.TextFlow;
    }

    private static bool UsesContentFlowConverter(
        FrameworkElement element)
    {
        return element
            .GetBindingExpression(
                FrameworkElement.FlowDirectionProperty)
            ?.ParentBinding
            .Converter is ContentTextFlowConverter;
    }

    private static TextAlignment ExpectedTextAlignment(
        FrameworkElement element,
        string? text)
    {
        if (IsIconText(element))
        {
            return TextAlignment.Center;
        }

        return UsesTechnicalStyle(element) ||
               IsTechnical(element)
            ? TextAlignment.Left
            : UiLaySvc.Current.TxtAlign;
    }

    private static bool IsIconText(
        FrameworkElement element)
    {
        if (element is not TextBlock text)
        {
            return false;
        }

        var family = text.FontFamily.Source;

        if (family.Contains(
                "Segoe Fluent Icons",
                StringComparison.OrdinalIgnoreCase) ||
            family.Contains(
                "Segoe MDL2 Assets",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var value = text.Text;

        return string.Equals(
            value,
            "!",
            StringComparison.Ordinal);
    }

    private static bool UsesTechnicalStyle(
        FrameworkElement element)
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

        for (var style = element.Style;
             style is not null;
             style = style.BasedOn)
        {
            if (technicalStyles.Any(technical =>
                    technical is not null &&
                    ReferenceEquals(style, technical)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool UsesStyle(
        FrameworkElement element,
        string key)
    {
        var target =
            Application.Current
                ?.TryFindResource(key) as Style;

        if (target is null)
        {
            return false;
        }

        for (var style = element.Style;
             style is not null;
             style = style.BasedOn)
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

        var value = Regex
            .Replace(
                box.Text ?? string.Empty,
                "\\s+",
                " ")
            .Trim();

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

    private static string TextBlockLabel(
        TextBlock text)
    {
        var path = text
            .GetBindingExpression(TextBlock.TextProperty)
            ?.ParentBinding
            .Path
            ?.Path;

        var value = Regex
            .Replace(
                text.Text ?? string.Empty,
                "\\s+",
                " ")
            .Trim();

        if (value.Length > 72)
        {
            value = value[..69] + "...";
        }

        var owner =
            text.TemplatedParent?.GetType().Name ??
            VisualTreeHelper
                .GetParent(text)
                ?.GetType()
                .Name ??
            "none";

        var source =
            string.IsNullOrWhiteSpace(path)
                ? "literal"
                : path;

        return
            $"TextBlock '{value}' (source: {source}; owner: {owner})";
    }

    private static bool IsTechnical(
        FrameworkElement element)
    {
        return string.Equals(
            element.Language.IetfLanguageTag,
            UiText.EnLang,
            StringComparison.OrdinalIgnoreCase);
    }

    private sealed record GameListDirectionContext(
        ListVm GameList)
    {
        public bool CanSelectGame { get; } = true;
    }

    private static void Bounds(
        DependencyObject root,
        Size size)
    {
        foreach (var item in Walk<FrameworkElement>(root))
        {
            if (!item.IsVisible ||
                item.ActualWidth <= 0 ||
                item.ActualHeight <= 0)
            {
                continue;
            }

            Check(
                !double.IsNaN(item.ActualWidth) &&
                !double.IsInfinity(item.ActualWidth),
                $"{item.GetType().Name} has invalid width.");

            Check(
                !double.IsNaN(item.ActualHeight) &&
                !double.IsInfinity(item.ActualHeight),
                $"{item.GetType().Name} has invalid height.");

            if (item is Button button &&
                !UsesStyle(
                    button,
                    "FluentButton.Icon"))
            {
                var compact =
                    UsesStyle(
                        button,
                        "FluentButton.Compact");

                var minWidth =
                    compact ? 64d : 96d;

                var minHeight =
                    compact ? 24d : 32d;

                Check(
                    button.ActualWidth >= minWidth - 1,
                    $"Button '{button.Content}' is {button.ActualWidth:0.#} px wide; expected at least {minWidth:0}.");

                Check(
                    button.ActualHeight >= minHeight - 1,
                    $"Button '{button.Content}' is {button.ActualHeight:0.#} px high; expected at least {minHeight:0}.");
            }
            else if (item is TextBox or ComboBox)
            {
                Check(
                    item.ActualHeight >= 31,
                    $"{item.GetType().Name} is {item.ActualHeight:0.#} px high; expected Fluent medium height 32.");
            }

            if (item is TabItem tab &&
                tab.IsSelected)
            {
                Check(
                    tab.ActualWidth >= 72,
                    $"Selected tab header is clipped at {tab.ActualWidth:0.#} px.");

                Check(
                    tab.ActualHeight >= 31,
                    $"Selected tab header is {tab.ActualHeight:0.#} px high; expected Fluent medium height 32.");
            }
        }

        foreach (var panel in Walk<Panel>(root)
                     .Where(item =>
                         item.TemplatedParent is null &&
                         item.ActualWidth > 0 &&
                         item.ActualHeight > 0))
        {
            var kids = panel
                .Children
                .OfType<FrameworkElement>()
                .Where(item =>
                    item.IsVisible &&
                    item.TemplatedParent is null &&
                    item.ActualWidth > 0 &&
                    item.ActualHeight > 0)
                .ToArray();

            var rects =
                new Dictionary<FrameworkElement, Rect>();

            foreach (var item in kids)
            {
                var point =
                    item.TranslatePoint(
                        new Point(),
                        panel);

                var rect = new Rect(
                    point,
                    new Size(
                        item.ActualWidth,
                        item.ActualHeight));

                rects[item] = rect;

                if (!HasScrl(item, panel))
                {
                    Check(
                        rect.Left >= -1 &&
                        rect.Top >= -1 &&
                        rect.Right <= panel.ActualWidth + 1 &&
                        rect.Bottom <= panel.ActualHeight + 1,
                        $"{item.GetType().Name} overflows {panel.GetType().Name} bounds.");
                }
            }

            for (var one = 0;
                 one < kids.Length;
                 one++)
            {
                for (var two = one + 1;
                     two < kids.Length;
                     two++)
                {
                    if (!HitPair(
                            kids[one],
                            kids[two]))
                    {
                        continue;
                    }

                    var hit = Rect.Intersect(
                        rects[kids[one]],
                        rects[kids[two]]);

                    Check(
                        hit.IsEmpty ||
                        hit.Width <= 1 ||
                        hit.Height <= 1,
                        $"{kids[one].GetType().Name} overlaps {kids[two].GetType().Name}.");
                }
            }
        }

        Check(
            size.Width > 0 &&
            size.Height > 0,
            "Layout surface must be non-empty.");
    }

    private static bool HasScrl(
        DependencyObject item,
        DependencyObject stop)
    {
        var node =
            VisualTreeHelper.GetParent(item);

        while (node is not null &&
               node != stop)
        {
            if (node is ScrollViewer)
            {
                return true;
            }

            node =
                VisualTreeHelper.GetParent(node);
        }

        return false;
    }

    private static bool HitPair(
        FrameworkElement one,
        FrameworkElement two)
    {
        if (one is Button ||
            two is Button)
        {
            return
                (one is Button or
                    TextBlock or
                    StackPanel) &&
                (two is Button or
                    TextBlock or
                    StackPanel);
        }

        return
            one is DataGrid &&
            (two is Border or TextBlock) ||
            two is DataGrid &&
            (one is Border or TextBlock);
    }

    private static void Dpi(
        Window win,
        Size size,
        double dpi)
    {
        var root =
            (FrameworkElement)win.Content;

        var pxw =
            (int)Math.Ceiling(
                size.Width * dpi / 96d);

        var pxh =
            (int)Math.Ceiling(
                size.Height * dpi / 96d);

        var bmp = new RenderTargetBitmap(
            pxw,
            pxh,
            dpi,
            dpi,
            PixelFormats.Pbgra32);

        bmp.Render(root);

        Check(
            bmp.PixelWidth == pxw &&
            bmp.PixelHeight == pxh,
            $"DPI render failed at {dpi / 96d:P0}.");
    }

    private static void Shots(
        Window[] wins,
        string lang,
        bool isDark,
        string outDir)
    {
        Directory.CreateDirectory(outDir);

        var langTag =
            lang == UiText.ArabLang
                ? "ar"
                : "en";

        var themeTag =
            isDark
                ? "dark"
                : "light";

        var tag =
            $"{themeTag}-{langTag}";

        Save(
            wins[0],
            $"{tag}-Main.png",
            outDir);

        Save(
            wins[3],
            $"{tag}-CanDlg.png",
            outDir);

        Save(
            wins[4],
            $"{tag}-AppDlg.png",
            outDir);

        var adv = wins[1];
        var size = new Size(1120, 700);

        LayRoot(adv, size);

        Save(
            adv,
            $"{tag}-Adv-Tools.png",
            outDir);

        var root =
            (FrameworkElement)adv.Content;

        var top =
            Walk<TabControl>(root).First();

        var names = new[]
        {
            "Tools",
            "Rec",
            "Diag"
        };

        var old = top.SelectedIndex;

        for (var idx = 0;
             idx < top.Items.Count;
             idx++)
        {
            top.SelectedIndex = idx;
            LayRoot(adv, size);

            if (idx > 0)
            {
                Save(
                    adv,
                    $"{tag}-Adv-{names[idx]}.png",
                    outDir);
            }
        }

        top.SelectedIndex = old;
        LayRoot(adv, size);

        top.SelectedIndex = 0;

        var hCanaryWorkspace =
            adv.FindName("HCanaryWorkspace")
                as FrameworkElement;

        Check(
            hCanaryWorkspace is not null,
            "Advanced Tools H-Canary workspace was not found.");

        ShowHCanaryWorkflow(
            hCanaryWorkspace!);

        LayRoot(adv, size);

        var hTabs = Walk<TabControl>(root)
            .Single(tab => tab.Name == "Steps");

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

        for (var idx = 0;
             idx < hTabs.Items.Count;
             idx++)
        {
            hTabs.SelectedIndex = idx;
            LayRoot(adv, size);

            Save(
                adv,
                $"{tag}-HCan-{hNames[idx]}.png",
                outDir);
        }

        var savedDrawer =
            Walk<Border>(root)
                .Single(border =>
                    border.Name == "SavedDrawer");

        savedDrawer.Visibility =
            Visibility.Visible;

        LayRoot(adv, size);

        Save(
            adv,
            $"{tag}-HCan-Saved.png",
            outDir);

        savedDrawer.Visibility =
            Visibility.Collapsed;

        foreach (var expander in Walk<Expander>(root)
                     .Where(expander =>
                         expander.Name is
                             "TargetsPanel" or
                             "AnchorsPanel"))
        {
            expander.IsExpanded = true;
            LayRoot(adv, size);

            Save(
                adv,
                $"{tag}-HCan-{expander.Name}.png",
                outDir);

            expander.IsExpanded = false;
        }

        hTabs.SelectedIndex = hOld;
        LayRoot(adv, size);
    }

    private static void RawLocalization(
        DependencyObject root)
    {
        foreach (var text in Walk<TextBlock>(root)
                     .Select(item => item.Text))
        {
            Check(
                !IsRawLocalizationKey(text),
                $"Raw localization key is visible: {text}");
        }

        foreach (var content in Walk<ContentControl>(root)
                     .Select(item =>
                         item.Content as string)
                     .Where(value =>
                         value is not null))
        {
            Check(
                !IsRawLocalizationKey(content),
                $"Raw localization key is visible: {content}");
        }

        foreach (var header in Walk<HeaderedContentControl>(root)
                     .Select(item =>
                         item.Header as string)
                     .Where(value =>
                         value is not null))
        {
            Check(
                !IsRawLocalizationKey(header),
                $"Raw localization key is visible: {header}");
        }
    }

    private static bool IsRawLocalizationKey(
        string? value)
    {
        return value is not null &&
               Regex.IsMatch(
                   value,
                   @"^(?:Adv|HCanary)\.[A-Za-z0-9_.]+$",
                   RegexOptions.CultureInvariant);
    }

    private static void Save(
        Window win,
        string name,
        string outDir)
    {
        var root =
            (FrameworkElement)win.Content;

        var wide =
            double.IsNaN(win.Width)
                ? Math.Max(win.MinWidth, 440)
                : win.Width;

        var high = win.Height;

        if (double.IsNaN(high))
        {
            root.Measure(
                new Size(
                    wide,
                    double.PositiveInfinity));

            high = Math.Max(
                win.MinHeight,
                Math.Ceiling(
                    root.DesiredSize.Height));
        }

        if (high <= 0)
        {
            high = Math.Max(
                win.MinHeight,
                260);
        }

        var size = new Size(
            wide,
            high);

        LayRoot(win, size);

        var bmp = new RenderTargetBitmap(
            (int)Math.Ceiling(size.Width),
            (int)Math.Ceiling(size.Height),
            96,
            96,
            PixelFormats.Pbgra32);

        bmp.Render(root);

        var png = new PngBitmapEncoder();

        png.Frames.Add(
            BitmapFrame.Create(bmp));

        using var file = File.Create(
            Path.Combine(
                outDir,
                name));

        png.Save(file);
    }

    private static void LayRoot(
        Window win,
        Size size)
    {
        var root =
            (FrameworkElement)win.Content;

        root.Measure(size);
        root.Arrange(new Rect(size));
        root.UpdateLayout();
    }

    private static void SelLang(
        Window win,
        string lang)
    {
        var root =
            (FrameworkElement)win.Content;

        var sels = Walk<Selector>(root)
            .ToDictionary(
                sel => sel,
                sel => sel.SelectedIndex);

        var next =
            lang == UiText.ArabLang
                ? UiText.EnLang
                : UiText.ArabLang;

        UiText.SetLang(next);
        UiLaySvc.Apply();

        Lay(
            win,
            new Size(1120, 700));

        foreach (var pair in sels)
        {
            Check(
                pair.Key.SelectedIndex ==
                pair.Value,
                $"{pair.Key.GetType().Name} selection changed after language switch.");
        }

        UiText.SetLang(lang);
        UiLaySvc.Apply();
    }

    private static IEnumerable<T> Walk<T>(
        DependencyObject root)
        where T : DependencyObject
    {
        if (root is T match)
        {
            yield return match;
        }

        for (var idx = 0;
             idx <
             VisualTreeHelper.GetChildrenCount(root);
             idx++)
        {
            var child =
                VisualTreeHelper.GetChild(
                    root,
                    idx);

            foreach (var item in Walk<T>(child))
            {
                yield return item;
            }
        }
    }

    private static string Repo()
    {
        var dir =
            new DirectoryInfo(
                AppContext.BaseDirectory);

        while (dir is not null &&
               !File.Exists(
                   Path.Combine(
                       dir.FullName,
                       "R3Cfg.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ??
               throw new InvalidOperationException(
                   "Could not locate the repository root.");
    }

    private static void Check(
        bool ok,
        string msg)
    {
        if (!ok)
        {
            throw new InvalidOperationException(msg);
        }
    }
}
