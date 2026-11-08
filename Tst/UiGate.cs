using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
        R3Config.Tests.IconFontT.Run(Path.Combine(Repo(), "App"));
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
        R3Config.Tests.IconFontT.Run(appDir);
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
        VerifyTransientSurfaceOwnership(
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

        var keyboardFocusTriggers = themeDocs
            .SelectMany(document => document.Descendants())
            .Where(node =>
                node.Name.LocalName == "Trigger" &&
                node.Attribute("Property")?.Value is
                    "IsKeyboardFocused" or
                    "IsKeyboardFocusWithin")
            .ToArray();

        if (keyboardFocusTriggers.Length > 0)
        {
            errs.Add(
                "Theme controls must not render keyboard-focus outlines.");
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
        // Historical NCL 14-entry baseline reproduced from the original input;
        // the original non-dialog XAML was the committed 811-entry contract.
        // NCL now has 50 reviewed entries, including explicit physical caption
        // placement and content-derived game-title flow. See NclUiIntegration.md.
        // The NCL option selector added NclOptionRowGrid's Setter
        // FlowDirection=LeftToRight, which keeps the option rows in the same
        // physical column order as the sibling cheat/run-key rows.
        // The NCL source-ordering pass touched View/NclImportDlg.xaml only, by
        // three entries: the removed generic "import" Button dropped one
        // @FlowDirection={DynamicResource App.TextFlowDirection}, and the
        // read-only built-in dialect summary added @FlowDirection=LeftToRight
        // plus @TextAlignment=Left so the technical dialect name renders
        // physically LTR in both locales.
        // The NCL technical-direction pass adds five more entries, again in
        // View/NclImportDlg.xaml only and removing none: the dialect and option
        // item templates each add @FlowDirection=LeftToRight and
        // @TextAlignment=Left, because "Artemis PS3", "NetCheat 4.53" and
        // "10x / 100x / 1,000x" are technical fragments that must stay
        // physically LTR inside an RTL dialog; and the cheat item template adds
        // @FlowDirection={Binding Display, Converter=ContentTextFlowConverter},
        // deriving the direction from the cheat name itself exactly as the
        // game title in this same dialog already does. No other file and no
        // existing entry value changed.
        // The technical-fragment pass moves the contract from the last gate-
        // verified 855 to 852, touching two files and no others:
        //   + Thm/Type.xaml Setter
        //       FlowDirection={DynamicResource TechnicalText.FlowDirection}
        //     - the inline BiDi scope TextFragments applies to a canonical
        //       value; TechnicalText.FlowDirection is hard LeftToRight in every
        //       language (UiLaySt.TechFlow), so a version, address or hash keeps
        //       its physical order inside an RTL paragraph.
        //   + View/NclImportDlg.xaml TextBlock @FlowDirection={Binding Display,
        //       Converter=ContentTextFlowConverter} - the cheat item template,
        //       taking direction from the cheat name itself.
        //   - View/NclImportDlg.xaml Button
        //       @FlowDirection={DynamicResource App.TextFlowDirection}
        //     - the removed generic "import" action.
        //   - 2x View/NclImportDlg.xaml TextBlock @FlowDirection=LeftToRight and
        //   - 2x View/NclImportDlg.xaml TextBlock @TextAlignment=Left
        //     - the dialog's technical summary/item TextBlocks now derive from
        //       TechnicalTextBlock, so their direction comes from that one style
        //       instead of four repeated inline attributes. Centralizing them
        //       removes the entries without changing what renders.
        // TechnicalTextBox's NumberSubstitution.Substitution setter is
        // deliberately invisible here: this contract covers direction and
        // alignment only, and the recomputed entry list is byte-identical with
        // and without it.
        // The game icon preview moves the contract from 852 to 861. All nine
        // added entries live in Ctrl/Games.xaml and none is removed; every one
        // pins a surface that must not mirror when the shell is RTL:
        //   + 1 Grid @FlowDirection=LeftToRight
        //     - the fixed 36x36 preview-button host in the row template.
        //   + 2 Image @FlowDirection=LeftToRight
        //     - the row's lazy icon source and the popup's preview image. A
        //       bitmap inherits the mirror transform of an RTL subtree, so cover
        //       art would render flipped under ar-SA without these.
        //   + 2 TextBlock @FlowDirection=LeftToRight and
        //   + 2 TextBlock @TextAlignment=Center
        //     - the Segoe Fluent Icons glyphs on the preview and close buttons.
        //       Icon glyphs are not prose: they stay physically centered and
        //       left-to-right in every UI language.
        //   + 1 Border @FlowDirection={DynamicResource App.FlowDirection}
        //     - the popup surface itself, which follows the shell so the close
        //       button lands on the shell's trailing edge.
        //   + 1 Border @FlowDirection=LeftToRight
        //     - the inner frame that restores the canonical direction for the
        //       preview image inside that RTL surface.
        // The previous 853 constant was computed against an earlier revision of
        // the row template, before the preview popup existed, and never matched
        // the tree.
        // Wiring Wiki notes, online/RPCN evidence, and per-section settings
        // into the Game Settings view moves the contract from 901 to 917.
        // Ctrl/Rec.xaml nets +12 (15 added, 3 removed) and Ctrl/RecommendationStyles.xaml adds 4:
        //   - Ctrl/Rec.xaml ItemsControl @FlowDirection, @HorizontalContentAlignment
        //     and its ItemsPanel StackPanel @FlowDirection
        //     - the single flat settings list, replaced by the sectioned list
        //       below (the row template itself moved into a resource unchanged).
        //   + Ctrl/Rec.xaml outer StackPanel @FlowDirection={DynamicResource App.FlowDirection}
        //     - the scroll content that stacks online card, notes card, sections.
        //   + Ctrl/Rec.xaml Online card: 1 TextBlock @TextAlignment (title),
        //     2 ItemsControl @HorizontalContentAlignment=Stretch, 1 row-label
        //     TextBlock @TextAlignment, and 1 notes TextBlock
        //     @FlowDirection={Binding Notes, Converter=ContentTextFlowConverter}
        //     - English Wiki prose takes direction from its own content.
        //   + Ctrl/Rec.xaml Notes card: 2 TextBlock @TextAlignment (title and
        //     header), 1 ItemsControl @HorizontalContentAlignment=Stretch, and 1
        //     TextBlock @FlowDirection={Binding Text, Converter=ContentTextFlowConverter}.
        //   + Ctrl/Rec.xaml sectioned list: ItemsControl @FlowDirection and
        //     @HorizontalContentAlignment, its ItemsPanel StackPanel
        //     @FlowDirection, and the per-section rows ItemsControl
        //     @FlowDirection and @HorizontalContentAlignment - the same three
        //     attributes the removed flat list carried plus two for the
        //     nested per-section list.
        //   + Ctrl/RecommendationStyles.xaml RecSectionHeaderText Setters @FlowDirection and
        //     @TextAlignment (technical LTR tab names such as "I/O") and
        //     RecRoleText Setters @FlowDirection and @TextAlignment (localized
        //     role label that follows the shell).
        // Keeping notes that are scoped to another platform or serial apart
        // from the applicable ones moves the contract from 917 to 919. Both
        // entries live in Ctrl/Rec.xaml: the "not applicable to this game"
        // card adds 1 TextBlock @TextAlignment (its heading) and 1
        // ItemsControl @HorizontalContentAlignment=Stretch. The note row
        // template moved into a shared resource unchanged.
        // The Game Settings presentation rework (actions first, one flat settings
        // list with no RPCS3 tab-name headers, compact RPCN with a details
        // toggle, no "not applicable" card) moves the contract from 919 to 917.
        // Ctrl/Rec.xaml now holds 41 direction/alignment attributes and
        // Ctrl/RecommendationStyles.xaml 29 setters. Removed: the per-tab sectioned list, its
        // RecSectionHeaderText style, and the other-scope notes card. Added: the
        // Setting/Value/Role/Enable column-header row, the flat Plan.Rows list,
        // and the RecColumnHeaderText / RecDetailsHeaderText styles.
        // The compact Game Settings correction moves the contract from 917 to 913:
        // the Setting/Value/Role/Enable column-header row and the visible Role
        // column are gone (with the RecColumnHeaderText and RecRoleText styles),
        // and Restore Defaults moved from the top action bar to one bottom row.
        // The RPCN row template gained a download-action link (no direction or
        // alignment attributes of its own).
        // The Runtime Environment & Compatibility domain moves the contract from
        // 913 to 926: thirteen added entries and none removed or changed.
        //   + 12 in Ctrl/Rtm.xaml: the view root and outer Grid, the scroll host
        //     and groups list @FlowDirection={DynamicResource App.FlowDirection},
        //     the fact row Grid @FlowDirection, the label and group-title
        //     TextBlock @TextAlignment={DynamicResource App.TextAlignment}, the
        //     three ItemsControl @HorizontalContentAlignment=Stretch/@FlowDirection
        //     pairs, and the empty-state Border @FlowDirection plus its TextBlock
        //     @TextAlignment. Every one follows the shell direction exactly like
        //     the equivalent Ctrl/Rec.xaml entries; technical values inherit
        //     their LTR direction from the shared Rec technical-value styles.
        //   + 1 in Main.xaml: the new TabItem @TextBlock.TextAlignment=Left, the
        //     same value the four existing domain tabs carry.
        // Log Diagnostics inside the Runtime domain moves the contract from 926
        // to 966: forty added entries, all in Ctrl/Rtm.xaml, none removed or
        // changed. The view is now an inner four-tab TabControl (Overview,
        // Hardware & environment, Log diagnostics, Evidence & solutions); each
        // tab owns one ScrollViewer whose @FlowDirection follows the shell, the
        // stack/grid/list panels of the new cards repeat the same
        // @FlowDirection={DynamicResource App.FlowDirection} and
        // @HorizontalContentAlignment=Stretch pairs the Ctrl/Rec.xaml cards
        // use, and every localized heading/label adds @TextAlignment={DynamicResource
        // App.TextAlignment}. Raw log text, signatures and builds are technical
        // values that take their left-to-right direction from the shared Rec
        // technical-value styles, not from inline attributes.
        // The RPCS3 history / community CPU evidence card ("3b",
        // AutomationId Settings.Knowledge) moves the contract from 966 to 968,
        // both entries in Ctrl/Rec.xaml and none removed or changed: its heading
        // TextBlock @TextAlignment={DynamicResource App.TextAlignment} and its
        // lines ItemsControl @HorizontalContentAlignment=Stretch, the same pair
        // the sibling Notes and Online cards carry. Removing only that Border
        // from the tree reproduces the previous 966/fe0934d0... hash exactly.
        // The rendered premium-UI pass moves the contract from 968 to 975:
        //   + 3 in Ctrl/HSav.xaml: the saved-drawer close glyph moved from the
        //     Button's string Content (rendered in the body font as a missing-
        //     glyph box) into an explicit icon TextBlock with @TextAlignment=
        //     Center and @FlowDirection=LeftToRight, the Games.xaml icon
        //     pattern; and the drawer empty state gains @TextAlignment=Center,
        //     the value HCanSaved.xaml and the stored-empty rule already use.
        //   + 5 in Ctrl/HIns.xaml: both warning glyphs gain @TextAlignment=
        //     Center and @FlowDirection={DynamicResource
        //     TechnicalText.FlowDirection} (icon convention, no literal
        //     direction), and the example row StackPanel gains @FlowDirection=
        //     {DynamicResource App.FlowDirection} so its localized label leads
        //     the technical example in both languages.
        //   - 1 in Ctrl/CheatPatchView.xaml: the Artemis empty-state title drops its
        //     @TextAlignment=Center override and follows PatEmptyStateTitleText
        //     exactly like the Official and Canary empty states.
        // The upstream Official note fallback moves the contract from 975 to
        // 977, all in Ctrl/PatchTemplates.xaml's PatItem template: the new Official
        // provenance caption TextBlock adds @FlowDirection={Binding NoteFlow}
        // and @TextAlignment={DynamicResource App.TextAlignment}, the same pair
        // its sibling note TextBlocks carry; and the InsText TextBlock's
        // @FlowDirection binding changes from NoteFlow to InsFlow so verbatim
        // upstream English keeps its left-to-right order (count unchanged).
        // Upstream game titles in the patch group headers move the contract
        // from 977 to 980: the Official (TgtHead) and Artemis (ArtemisTgtHead)
        // headers in Ctrl/PatchTemplates.xaml and the Canary header (CanHead) in
        // Ctrl/CanaryPatchView.xaml each gain @FlowDirection={Binding Header/ArtemisHeader,
        // Converter=ContentTextFlowConverter}, so "God of War 3" keeps its
        // order inside the Arabic interface (GroupTitleDirectionT).
        // The Fluent 2 visual redesign, stage 2 (shell identity + navigation),
        // moves the contract from 980 to 982:
        //   + 2 in Thm/Ctrl.xaml: the new shared IconGlyphText style (tab
        //     glyphs, identity tile, empty-state glyphs) sets FlowDirection=
        //     LeftToRight and TextAlignment=Center, the icon convention the
        //     inline glyph TextBlocks in Games.xaml/HSav.xaml already carry.
        //   ~ 1 in Main.xaml: the game identity header splits the combined
        //     "Title | Serial | vVersion" line into a content-directed title
        //     plus technical serial/version chips, so the title TextBlock's
        //     @FlowDirection binding moves from SelGame to SelTitle (same
        //     ContentTextFlowConverter; the combined SelGame string remains
        //     the header's accessible name and tooltip). Nothing removed.
        // Stage 4 moves the contract from 982 to 983: the identity header
        // leaves Main.xaml for the shared Ctrl/GameIdentityTemplate.xaml "GameIdentity"
        // DataTemplate (so GroupTitleDirectionT can render it with every title
        // fixture). Its two title entries (@FlowDirection content binding on
        // SelTitle, @TextAlignment={DynamicResource App.TextAlignment}) move
        // unchanged to Ctrl/GameIdentityTemplate.xaml, and the Main.xaml ContentControl host
        // adds @HorizontalContentAlignment=Stretch so the card keeps full width.
        // Canonical English patch titles keep their explicit left-to-right
        // flow but now keep every wrapped line on the interface start edge:
        // the Official/Artemis title TextBlocks in Ctrl/PatchTemplates.xaml change
        // @TextAlignment=Left to {DynamicResource TechnicalText.StartTextAlignment}
        // (Right in Arabic, Left in English, set live by UiLaySvc), and the
        // shared PatPatchTitleText setter (Canary rows) moves from
        // App.TextAlignment to the same resource. The Artemis dependency line
        // (technical dependency titles beside the localized "Requires:" label)
        // takes the same resource instead of @TextAlignment=Left, so the titles
        // sit next to their label in Arabic. Count unchanged (983).
        // Dialog direction pass: 13 existing entries change value, none added
        // or removed (count unchanged, 983). The caption Grid of Adv, CImp,
        // HCan, HCanAnchors, HCanSaved, HCanTargets and NclImportDlg changes
        // @FlowDirection=LeftToRight to
        // {DynamicResource App.CaptionButtonsFlowDirection} (as Main.xaml), so
        // Close is physically left in Arabic and right in English. In
        // NclImportDlg the five label/field row Grids and the NclOptionRowGrid
        // Setter change LeftToRight to {DynamicResource App.FlowDirection}
        // with the label moved to the first column, so English reads
        // "Label [field]" and Arabic keeps its label on the physical right.
        // The Danger button label fix moves the contract from 983 to 984, one
        // added entry in Thm/Ctrl.xaml and none removed or changed:
        // FluentButton.Danger gains its own template (the shared Button
        // template plus the FluentButton.Primary label style), whose
        // ContentPresenter adds @FlowDirection={TemplateBinding FlowDirection},
        // the entry the shared and Primary templates already carry. Removing
        // only that template reproduces 983/48f3cd9e... exactly.
        // Naming cleanup Wave A renames nine XAML resource files (PSty, PTmp,
        // RSty, HSty, HASty, HSSty, HTSty, ISty and Ident become PatchStyles,
        // PatchTemplates, RecommendationStyles, HCanStyles, HCanAnchorsStyles,
        // HCanSavedStyles, HCanTargetsStyles, ImportDlgStyles and
        // GameIdentityTemplate). Only entry paths and file order change: the
        // count stays 984 and every payload is identical; remapping the paths
        // of the 0e869fb1... entries and re-sorting reproduces 72787ed6...
        // (artifacts/naming-audit/wave-a-map.txt).
        // Naming cleanup Wave B1 renames the five patch views and their
        // classes (Pat/PatView, POff/POffView, PCan/PCanView, PCht/PChtView and
        // PSrc/PSrcView become PatchView, OfficialPatchView, CanaryPatchView,
        // CheatPatchView and PatchSourceView). The count stays 984; entry
        // paths change, and the one entry on a renamed host element (the
        // source area's @FlowDirection in Ctrl/PatchView.xaml) records
        // PatchSourceView instead of PSrcView with the same ordinal, attribute
        // and value. Remapping those paths and element types in the 72787ed6...
        // entries and re-sorting reproduces this hash
        // (artifacts/naming-audit/wave-b1-map.txt).
        // Offline licensing moves the contract from 984 to 1002, adding 18
        // entries and removing none; deleting View/ALic.xaml and the License
        // ListBoxItem/ALicView host from View/Adv.xaml reproduces
        // 984/771bd01b... exactly:
        //   + 16 View/ALic.xaml entries: 12 @FlowDirection (9 follow
        //     {DynamicResource App.FlowDirection}; the activation code,
        //     license ID and Trial game-ID value pills are LeftToRight because
        //     they are technical LTR values) and 4 action Button
        //     @TextBlock.TextAlignment=Center, as in ARec/ADiag.
        //   + 2 View/Adv.xaml entries for the fourth navigation ListBoxItem
        //     (@FlowDirection={DynamicResource App.FlowDirection},
        //     @HorizontalContentAlignment=Stretch), identical to the three
        //     existing items. The following Grid/HCanView entries keep their
        //     values and only move two ordinals.
        //
        // Online activation moves the contract from 1002 to 1008, changing
        // only View/ALic.xaml (16 -> 22 entries, all other files identical):
        // the primary activation section (Border/StackPanel following
        // App.FlowDirection, the activation-code TextBox and its format hint
        // LeftToRight, Activate @TextBlock.TextAlignment=Center), the License
        // ID pill (LeftToRight) moved into the header StackPanel, and the
        // support path wrapped in an Expander + StackPanel following
        // App.FlowDirection, with its device-request and Trial game-ID pills
        // LeftToRight and three centered action Buttons.
        //
        // The Fluent License correction moves the contract from 1008 to 1017,
        // changing only View/ALic.xaml (22 -> 28) and View/ASty.xaml (+3):
        //   ALic: the root Grid becomes a StackPanel and the License ID row a
        //   Grid (same App.FlowDirection values); + the Trial-limit notice
        //   Grid, the activation field column Grid, the inline activation
        //   error Grid, and the support notice Border + Grid, all following
        //   App.FlowDirection; + @TextAlignment=Left on the LTR in-field
        //   format hint (technical text, already LeftToRight). The activation
        //   TextBox and technical value pills stay LeftToRight.
        //   ASty: AdvPrimaryActionButton, the Primary sibling of
        //   AdvStandardActionButton with the same three setters
        //   (FlowDirection App.TextFlowDirection, centered content/text).
        //
        // Removing licensing (R3Config is GPL-3.0 with no Trial/Premium)
        // returns the contract from 1017 to the pre-licensing 984/771bd01b...
        // (artifacts/naming-audit/wave-b1-map.txt): View/ALic.xaml (28), its
        // View/Adv.xaml ListBoxItem (2) and ASty AdvPrimaryActionButton (3)
        // are gone, and every remaining entry is identical.
        //
        // Recommendation updates add the fourth navigation surface and its
        // localized update workspace. The frozen direction/alignment contract
        // below was recomputed from the current XAML after that surface was
        // added; serials, settings and values remain LTR through the shared
        // DataGridCellTextLeft style.
        //
        // The row ToolTip reason (Ctrl/Rec.xaml, PlanRowTemplate) adds exactly
        // two entries, 1002 -> 1004: TextAlignment={DynamicResource
        // App.TextAlignment} on the reason title and on the reason body, the
        // same value the neighbouring Wiki instruction heading already uses.
        // No existing entry changed or was removed.
        //
        // The G1 database-config warning card (Ctrl/Rec.xaml,
        // Settings.DbConfigWarning) adds exactly two entries, 1004 -> 1006:
        // HorizontalContentAlignment="Stretch" on its lines ItemsControl (as
        // the Knowledge card) and FlowDirection="LeftToRight" on each line,
        // because every line is an RPCS3 section/key and value. Removing only
        // that card reproduces 1004/f4c09c13...; no existing entry changed.
        //
        // Round 12 splits that card's lines into (a) keepable and (b+d) lost
        // groups, 1006 -> 1007: the one lines ItemsControl becomes two, each
        // with HorizontalContentAlignment="Stretch" (+1), and the line
        // FlowDirection="LeftToRight" moves unchanged into the shared
        // DbWarnLineTemplate (same entry, later Rec.xaml ordinals shift). The
        // LinkText.Source bindings carry no direction. Restoring only the
        // pre-round-12 card and bindings reproduces 1006/c279789b...; an
        // ordinal-free diff shows that single added entry.
        const int expectedCount = 1007;
        const string expectedSha256 =
            "f960e24ce9257f893f0456536ee19e3d8ecef1ee4743306cac5cccc66a34ae82";
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

    // Transient surfaces (Popup, ContextMenu, ToolTip) open only from a
    // deliberate user interaction: never from a literal IsOpen, a view-model
    // binding (PropertyChanged, refresh, navigation, Loaded), or code outside
    // an input event handler. Control templates that bind their own dropdown
    // state (ComboBox PART_Popup) are the only binding owners.
    private static void VerifyTransientSurfaceOwnership(
        IEnumerable<string> files,
        string appDir,
        List<string> errs)
    {
        var surfaces = new HashSet<string>(["Popup", "ContextMenu", "ToolTip"], StringComparer.Ordinal);
        foreach (var path in files)
        {
            var relativePath = Path.GetRelativePath(appDir, path);
            foreach (var node in XDocument.Load(path).Descendants()
                         .Where(node => surfaces.Contains(node.Name.LocalName)))
            {
                var isOpen = node.Attribute("IsOpen")?.Value.Trim();
                if (isOpen is null ||
                    isOpen.StartsWith("{TemplateBinding ", StringComparison.Ordinal))
                {
                    continue;
                }

                errs.Add(
                    $"{relativePath}: {node.Name.LocalName} IsOpen='{isOpen}' opens a transient surface without user interaction.");
            }
        }

        var openCall = new System.Text.RegularExpressions.Regex(@"\.IsOpen\s*=\s*true\b");
        var member = new System.Text.RegularExpressions.Regex(@"^\s*(private|public|internal|protected)\b[^=;]*\(");
        foreach (var path in Directory.GetFiles(appDir, "*.cs", SearchOption.AllDirectories)
                     .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                                    !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
        {
            var lines = File.ReadAllLines(path);
            string? owner = null;
            for (var index = 0; index < lines.Length; index++)
            {
                if (member.IsMatch(lines[index]))
                {
                    owner = string.Concat(lines.Skip(index).Take(4));
                }

                if (openCall.IsMatch(lines[index]) &&
                    owner?.Contains("RoutedEventArgs", StringComparison.Ordinal) != true)
                {
                    errs.Add(
                        $"{Path.GetRelativePath(appDir, path)}:{index + 1}: a transient surface is opened outside a user input handler.");
                }
            }
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
            "PatchView.xaml"));
        var officialPatchView = File.ReadAllText(Path.Combine(
            appDir,
            "Ctrl",
            "OfficialPatchView.xaml"));
        var canaryPatchView = File.ReadAllText(Path.Combine(
            appDir,
            "Ctrl",
            "CanaryPatchView.xaml"));
        var patchTemplates = File.ReadAllText(Path.Combine(
            appDir,
            "Ctrl",
            "PatchTemplates.xaml"));

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
        var cheatPatchView = File.ReadAllText(Path.Combine(appDir, "Ctrl", "CheatPatchView.xaml"));
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
                "PatchTemplates.xaml"));
        var mergesPatchStyles = patchTemplate
            .Descendants()
            .Any(node =>
                node.Name.LocalName ==
                "ResourceDictionary" &&
                node.Attribute("Source")?.Value ==
                "PatchStyles.xaml");
        if (!mergesPatchStyles)
        {
            errs.Add(
                "PatchTemplates.xaml must merge PatchStyles.xaml before resolving PatStatusBadge and related template resources.");
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
        var view = new R3Config.Ctrl.PatchView
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
        SynchronizationContext.SetSynchronizationContext(
            new DispatcherSynchronizationContext(
                Dispatcher.CurrentDispatcher));

        var app = new App();
        app.InitializeComponent();
        app.ShutdownMode =
            ShutdownMode.OnExplicitShutdown;
        IsolateQueuedStartup(app);
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
            GameSettingsUiT.RenderSettingsView();
            RuntimeViewUiT.Render();
            LogDiagViewUiT.Render();

            var themes = new[] { true, false };
            var langs = new[]
            {
                UiText.ArabLang,
                UiText.EnLang
            };
            var sizes = new[]
            {
                new Size(960, 620),
                new Size(960, 640),
                new Size(1120, 700),
                new Size(1280, 800),
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
            AccentLabelThemes.Clear();
            DangerLabelThemes.Clear();
            CaptionCloseLanguages.Clear();

            foreach (var isDark in themes)
            {
                WinTheme.ApplyResources(isDark);
                // One theme state: R3Config's switch also selects WPF UI's theme.
                var libraryTheme = System.Windows.Application.Current.Resources
                    .MergedDictionaries
                    .OfType<Wpf.Ui.Markup.ThemesDictionary>()
                    .Single();
                Check(
                    libraryTheme.Source?.OriginalString.Contains(
                        isDark ? "/Dark." : "/Light.",
                        StringComparison.OrdinalIgnoreCase) == true,
                    $"WPF UI theme {libraryTheme.Source} does not follow the R3Config {(isDark ? "dark" : "light")} theme.");

                foreach (var lang in langs)
                {
                    UiText.SetLang(lang);
                    UiLaySvc.Apply();
                    VerifyGameDataTextDirections();
                    R3Config.Tests.GroupTitleDirectionT.Run();
                    VerifyGameIconPresentation();
                    R3Config.Tests.GameCompatLineUiT.VerifyRuntime(lang, isDark, shotDir);
                    R3Config.Tests.DbWarnUiT.VerifyRuntime(lang, isDark, shotDir);
                    R3Config.Tests.NoteRevealUiT.VerifyRuntime(lang, isDark, shotDir);
                    R3Config.Tests.TechnicalTextT.VerifyRuntime(lang, isDark, shotDir);
                    DangerProbe();

                    var wins = Make();

                    try
                    {
                        var nclWindow = wins
                            .OfType<NclImportDlg>()
                            .Single();
                        InitializeNclWindow(nclWindow);

                        VerifySurface(
                            nclWindow,
                            new Size(660, 560),
                            dpis);
                        VerifySurface(
                            nclWindow,
                            new Size(760, 650),
                            dpis);
                        var nclDetails = (Expander)nclWindow.FindName(
                            "NclDetailsExpander");
                        nclDetails.IsExpanded = true;
                        VerifySurface(nclWindow, new Size(660, 560), dpis);
                        VerifySurface(nclWindow, new Size(760, 650), dpis);
                        nclDetails.IsExpanded = false;

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

                                    SetAdvancedDiagnostics(hCanaryHost);

                                    VerifySurface(
                                        hCanaryHost,
                                        size,
                                        dpis);

                                    SetAdvancedRecSync(hCanaryHost);

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

            Check(
                AccentLabelThemes.Count == themes.Length,
                $"An enabled primary button label was rendered and checked in {AccentLabelThemes.Count} of {themes.Length} themes.");
            Check(
                DangerLabelThemes.Count == themes.Length,
                $"An enabled danger button label was rendered and checked in {DangerLabelThemes.Count} of {themes.Length} themes.");
            Check(
                CaptionCloseLanguages.Count == langs.Length,
                $"A custom caption Close button was rendered and checked in {CaptionCloseLanguages.Count} of {langs.Length} languages.");

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
                "Runtime UI gate PASS (9 windows including NCL import plus embedded H-Canary workspace, NCL min/default sizes, 4 shared sizes, 3 DPI scales, 2 languages, 2 themes).");
        }
        finally
        {
            app.DispatcherUnhandledException -= dispatcherFailure;
            source.Listeners.Remove(trace);
            source.Switch.Level = level;
        }
    }

    // Constructing App queues Application.OnStartup at Send priority, so the
    // first dispatcher pump runs it: it opens the real main window and
    // registers the production crash reporter, which shows a modal MessageBox.
    // Left in place, the intentional dispatcher-failure probe below raised that
    // MessageBox and the rest of the gate ran inside its nested message loop;
    // the thread could only finish if the box's owner window happened to be
    // closed, otherwise the two-minute join expired. Run the queued startup
    // here, deterministically, then detach the reporter and close the window
    // it opened, so gate failures are captured only by the gate's own handler.
    private static void IsolateQueuedStartup(App app)
    {
        DrainDispatcher(app.Dispatcher);

        var reporter = typeof(App).GetMethod(
            "OnDispatcherUnhandledException",
            System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "The production dispatcher crash reporter was not found.");
        app.DispatcherUnhandledException -=
            (DispatcherUnhandledExceptionEventHandler)Delegate.CreateDelegate(
                typeof(DispatcherUnhandledExceptionEventHandler),
                reporter);

        app.MainWindow?.Close();
        app.MainWindow = null;
        DrainDispatcher(app.Dispatcher);
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

    private static void VerifyGameIconPresentation()
    {
        var context = new R3Config.Tests.TestContext();
        var root = context.CreateTempRoot("R3ConfigUiIcons");
        Window? window = null;
        try
        {
            var iconRoot = Path.Combine(root, "with-icon");
            var replacementRoot = Path.Combine(root, "replacement");
            WriteGameIconFixture(iconRoot, reversed: false);
            WriteGameIconFixture(replacementRoot, reversed: true);
            var row = MakeGameRow("BLUS90091", "Icon fixture", iconRoot);
            var missing = MakeGameRow("BLUS90092", "Missing icon", Path.Combine(root, "missing"));
            var gameList = new ListVm();
            gameList.Load([row, missing]);
            var view = new R3Config.Ctrl.GameList
            {
                DataContext = new GameListDirectionContext(gameList)
            };
            window = new Window
            {
                Content = view,
                Width = 360,
                Height = 480,
                ShowActivated = false,
                ShowInTaskbar = false
            };
            window.Show();
            DrainDispatcher(window.Dispatcher);

            var images = Walk<Image>(view)
                .Where(image => image.Name == "GameIconImage")
                .ToArray();
            var buttons = Walk<Button>(view)
                .Where(button => button.Name == "GameImagePreviewButton")
                .ToArray();
            Check(images.Length == 2 && buttons.Length == 2,
                "Both realized game rows must own one lazy image source and one preview button.");

            var icon = images.Single(image => ReferenceEquals(GameIcon.GetGame(image), row.Game));
            var empty = images.Single(image => ReferenceEquals(GameIcon.GetGame(image), missing.Game));
            WaitForIconPresentation(window.Dispatcher, () => icon.Source is not null,
                "The loaded game row did not receive its asynchronous icon.");
            DrainDispatcher(window.Dispatcher);

            var original = (BitmapSource)icon.Source;
            Check(original.IsFrozen, "Published game icons must be frozen.");

            var iconHost = icon.Parent as Grid
                ?? throw new InvalidOperationException(
                    "The game icon loader must remain inside the fixed preview-button host.");
            var emptyHost = empty.Parent as Grid
                ?? throw new InvalidOperationException(
                    "The missing game icon loader must remain inside the fixed preview-button host.");
            var iconButton = buttons.Single(button => ReferenceEquals(button.Tag, icon));
            var emptyButton = buttons.Single(button => ReferenceEquals(button.Tag, empty));

            Check(iconButton.IsEnabled && !emptyButton.IsEnabled,
                "Only rows with a loaded game image may enable the image preview button.");
            Check(!ToolTipService.GetIsEnabled(emptyButton),
                "A missing image must suppress the disabled preview-button tooltip.");

            foreach (var image in images)
            {
                var host = image.Parent as Grid
                    ?? throw new InvalidOperationException(
                        "The game icon loader must remain inside the preview-button host.");
                var button = host.Children
                    .OfType<Button>()
                    .Single(item => item.Name == "GameImagePreviewButton");
                var rowGrid = host.Parent as Grid
                    ?? throw new InvalidOperationException(
                        "The preview-button host must remain inside the game-row grid.");

                Check(host.ActualWidth == 36 && host.ActualHeight == 36 &&
                      button.ActualWidth == 36 && button.ActualHeight == 36 &&
                      button.MinWidth == 36 && button.MinHeight == 36,
                    "The image action must preserve the existing 36x36 row footprint.");
                Check(image.Width == 0 && image.Height == 0 && image.Opacity == 0 &&
                      !image.IsHitTestVisible && !image.Focusable,
                    "The lazy game image must load without rendering a thumbnail in the list.");
                Check(ReferenceEquals(button.Tag, image) &&
                      button.FlowDirection == FlowDirection.LeftToRight,
                    "The preview button must target its row image source without changing physical layout direction.");

                var glyph = Walk<TextBlock>(button).Single();
                Check(glyph.Text == "\uE91B" &&
                      glyph.FontFamily.Source.Contains("Segoe Fluent Icons", StringComparison.Ordinal) &&
                      glyph.FlowDirection == FlowDirection.LeftToRight,
                    "The row action must use the Fluent Photo glyph instead of a scaled game thumbnail.");

                var game = GameIcon.GetGame(image)!;
                var title = Walk<TextBlock>(view).Single(text => text.Text == game.Title);
                var serial = Walk<TextBlock>(view).Single(text => text.Text == game.Serial);
                var hostRight = host.TranslatePoint(
                    new Point(host.ActualWidth, 0),
                    rowGrid).X;
                Check(rowGrid.FlowDirection == FlowDirection.LeftToRight &&
                      title.ActualWidth > 0 && serial.ActualWidth > 0 &&
                      title.TranslatePoint(new Point(), rowGrid).X >= hostRight &&
                      serial.TranslatePoint(new Point(), rowGrid).X >= hostRight,
                    "Game title and serial must keep their current alignment beside the fixed image action.");

                if (ReferenceEquals(button, iconButton))
                {
                    var initialDelay = ToolTipService.GetInitialShowDelay(button);
                    var betweenDelay = ToolTipService.GetBetweenShowDelay(button);
                    var showDuration = ToolTipService.GetShowDuration(button);
                    var showOnDisabled = ToolTipService.GetShowOnDisabled(button);
                    var tooltipValue = ToolTipService.GetToolTip(button);
                    var initialLocal = button.ReadLocalValue(
                        ToolTipService.InitialShowDelayProperty);
                    var betweenLocal = button.ReadLocalValue(
                        ToolTipService.BetweenShowDelayProperty);
                    var durationLocal = button.ReadLocalValue(
                        ToolTipService.ShowDurationProperty);
                    var disabledLocal = button.ReadLocalValue(
                        ToolTipService.ShowOnDisabledProperty);
                    var initialSource = DependencyPropertyHelper.GetValueSource(
                        button,
                        ToolTipService.InitialShowDelayProperty);
                    var betweenSource = DependencyPropertyHelper.GetValueSource(
                        button,
                        ToolTipService.BetweenShowDelayProperty);
                    var durationSource = DependencyPropertyHelper.GetValueSource(
                        button,
                        ToolTipService.ShowDurationProperty);
                    var disabledSource = DependencyPropertyHelper.GetValueSource(
                        button,
                        ToolTipService.ShowOnDisabledProperty);

                    Check(
                        initialDelay == ToolTipPolicy.InitialShowDelayMs &&
                        betweenDelay == ToolTipPolicy.BetweenShowDelayMs &&
                        showDuration == ToolTipPolicy.ShowDurationMs &&
                        showOnDisabled,
                        $"The game image action must use the centralized ToolTip timing policy. " +
                        $"Actual=Initial:{initialDelay},Between:{betweenDelay},Duration:{showDuration},ShowOnDisabled:{showOnDisabled}; " +
                        $"Expected=Initial:{ToolTipPolicy.InitialShowDelayMs},Between:{ToolTipPolicy.BetweenShowDelayMs},Duration:{ToolTipPolicy.ShowDurationMs},ShowOnDisabled:true; " +
                        $"IsLoaded={button.IsLoaded}; IsEnabled={button.IsEnabled}; ToolTipNull={tooltipValue is null}; ToolTipType={tooltipValue?.GetType().FullName ?? "<null>"}; " +
                        $"Local=Initial:{FormatLocalValue(initialLocal)},Between:{FormatLocalValue(betweenLocal)},Duration:{FormatLocalValue(durationLocal)},ShowOnDisabled:{FormatLocalValue(disabledLocal)}; " +
                        $"Source=Initial:{FormatValueSource(initialSource)},Between:{FormatValueSource(betweenSource)},Duration:{FormatValueSource(durationSource)},ShowOnDisabled:{FormatValueSource(disabledSource)}.");
                    Check(!string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)),
                        "The game image action must expose a localized automation name.");
                }
            }

            var popup = view.FindName("GameImagePreviewPopup") as Popup
                ?? throw new InvalidOperationException("The shared game image preview popup was not found.");
            var preview = view.FindName("GameImagePreviewImage") as Image
                ?? throw new InvalidOperationException("The shared game image preview image was not found.");
            var surface = view.FindName("GameImagePreviewSurface") as Border
                ?? throw new InvalidOperationException("The shared game image preview surface was not found.");
            var previewScale = view.FindName("GameImagePreviewScale") as ScaleTransform
                ?? throw new InvalidOperationException("The shared game image preview scale was not found.");
            var closeButton = view.FindName("GameImagePreviewCloseButton") as Button
                ?? throw new InvalidOperationException("The shared game image preview close button was not found.");

            Check(!popup.IsOpen && preview.Source is null,
                "The shared game image preview must start closed without retaining a bitmap.");

            iconButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            DrainDispatcher(window.Dispatcher);
            Check(popup.IsOpen && ReferenceEquals(popup.PlacementTarget, iconButton) &&
                  ReferenceEquals(preview.Source, original),
                "Clicking the image action must open the shared preview for exactly that row bitmap.");
            Check(preview.Width == 480 && preview.Height == 270 &&
                  preview.Stretch == Stretch.Uniform &&
                  preview.FlowDirection == FlowDirection.LeftToRight &&
                  RenderOptions.GetBitmapScalingMode(preview) == BitmapScalingMode.HighQuality,
                "The shared preview must present the bitmap as a bounded, unmirrored high-quality image.");

            WaitForIconPresentation(
                window.Dispatcher,
                () => surface.Opacity > 0.99 &&
                      previewScale.ScaleX > 0.99 &&
                      previewScale.ScaleY > 0.99,
                "The shared preview did not complete its surface motion.");
            Check(surface.Effect is not null,
                "The shared preview must keep the existing dialog elevation treatment.");
            Check(!string.IsNullOrWhiteSpace(AutomationProperties.GetName(closeButton)),
                "The image preview close action must expose a localized automation name.");
            Check(
                ToolTipService.GetInitialShowDelay(closeButton) == ToolTipPolicy.InitialShowDelayMs &&
                ToolTipService.GetBetweenShowDelay(closeButton) == ToolTipPolicy.BetweenShowDelayMs &&
                ToolTipService.GetShowDuration(closeButton) == ToolTipPolicy.ShowDurationMs &&
                ToolTipService.GetShowOnDisabled(closeButton),
                "The image preview close action must use the centralized ToolTip timing policy.");

            closeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            DrainDispatcher(window.Dispatcher);
            Check(!popup.IsOpen && preview.Source is null && popup.PlacementTarget is null,
                "Closing the shared image preview must release the displayed bitmap and placement target.");

            // Replace the descriptor before pumping its pending asynchronous completion.
            var replacement = row.Game with { InstPath = replacementRoot };
            GameIcon.SetGame(icon, replacement);
            GameIcon.SetGame(icon, missing.Game);
            Check(icon.Source is null,
                "Descriptor replacement must immediately clear the previous game's icon.");
            var queuedLoads = GameIcon.LoadAsync(missing.Game);
            WaitForIconPresentation(window.Dispatcher, () => queuedLoads.IsCompleted,
                "Pending descriptor replacement loads did not finish.");
            queuedLoads.GetAwaiter().GetResult();
            DrainDispatcher(window.Dispatcher);
            Check(icon.Source is null,
                "A replaced descriptor must reject a stale icon completion.");
            GameIcon.SetGame(icon, replacement);
            WaitForIconPresentation(window.Dispatcher, () => icon.Source is not null,
                "The replacement game did not receive its own icon.");
            Check(!ReferenceEquals(icon.Source, original),
                "Different game paths must not reuse the previous game's bitmap.");
            var replacementPixels = new byte[4 * 2 * 4];
            var replacementSource = icon.Source as BitmapSource
                ?? throw new InvalidOperationException(
                    "The replacement game did not produce a bitmap source.");
            replacementSource.CopyPixels(replacementPixels, 4 * 4, 0);
            Check(replacementPixels[0] > replacementPixels[2],
                "The replacement descriptor must display its blue-left fixture.");

            window.Content = null;
            DrainDispatcher(window.Dispatcher);
            Check(icon.Source is null && empty.Source is null,
                "Unloaded rows must release their displayed bitmap references.");
            window.Content = view;
            DrainDispatcher(window.Dispatcher);
            GameIcon.SetGame(icon, row.Game);
            WaitForIconPresentation(window.Dispatcher, () => icon.Source is not null,
                "A reloaded row did not recover its icon.");
            Check(ReferenceEquals(icon.Source, original),
                "Reloading the same game must reuse its cached bitmap.");
        }
        finally
        {
            window?.Close();
            DrainDispatcher(Dispatcher.CurrentDispatcher);
            context.Cleanup();
        }
    }

    private static string FormatLocalValue(object value)
    {
        return ReferenceEquals(value, DependencyProperty.UnsetValue)
            ? "<unset>"
            : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "<null>";
    }

    private static string FormatValueSource(ValueSource source)
    {
        return $"{source.BaseValueSource}/Expr:{source.IsExpression}/Current:{source.IsCurrent}/Animated:{source.IsAnimated}/Coerced:{source.IsCoerced}";
    }

    private static void WriteGameIconFixture(string root, bool reversed)
    {
        Directory.CreateDirectory(root);
        byte[] pixels =
        [
            0, 0, 255, 255, 0, 0, 255, 255, 255, 0, 0, 255, 255, 0, 0, 255,
            0, 0, 255, 255, 0, 0, 255, 255, 255, 0, 0, 255, 255, 0, 0, 255
        ];
        if (reversed)
        {
            for (var offset = 0; offset < pixels.Length; offset += 4)
            {
                (pixels[offset], pixels[offset + 2]) = (pixels[offset + 2], pixels[offset]);
            }
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(
            4, 2, 96, 96, PixelFormats.Bgra32, null, pixels, 16)));
        using var stream = File.Create(Path.Combine(root, "ICON0.PNG"));
        encoder.Save(stream);
    }

    private static void WaitForIconPresentation(Dispatcher dispatcher, Func<bool> ready, string failure)
    {
        var elapsed = Stopwatch.StartNew();
        while (!ready() && elapsed.Elapsed < TimeSpan.FromSeconds(5))
        {
            DrainDispatcher(dispatcher);
        }

        Check(ready(), failure);
    }

    private static GameVm MakeGameRow(
        string serial,
        string title,
        string instPath = "")
    {
        var game = new Rpcs3GameDescriptor(
            Serial: serial,
            Title: title,
            InstPath: instPath,
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
        DisabledText(root);
        AccentText(root);
        DangerText(root);
        CaptionSide(root);
        WesternDigits(root);
        ButtonLabels(root);
        HeaderText(root);
        AccessibleNames(root);
        RawLocalization(root);

        if (window is NclImportDlg nclWindow)
        {
            VerifyNclPresentation(nclWindow);
        }

        foreach (var dpi in dpis)
        {
            Dpi(window, size, dpi);
        }
    }

    private static void InitializeNclWindow(NclImportDlg window)
    {
        var viewModel = (NclImportVm)window.DataContext;
        var operation = viewModel.InitializeAsync();
        if (!operation.IsCompleted)
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.Send)
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            timer.Tick += (_, _) => frame.Continue = false;
            _ = operation.ContinueWith(
                _ => window.Dispatcher.BeginInvoke(
                    DispatcherPriority.Send,
                    new Action(() => frame.Continue = false)),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            timer.Start();
            try
            {
                Dispatcher.PushFrame(frame);
            }
            finally
            {
                timer.Stop();
            }
        }

        Check(operation.IsCompleted,
            "NCL discovery did not finish within thirty seconds.");
        // Completed-task observation at the synchronous STA harness boundary.
        operation.GetAwaiter().GetResult();
        DrainDispatcher(window.Dispatcher);
    }

    private static void VerifyNclPresentation(NclImportDlg window)
    {
        var caption = (Grid)window.FindName("NclCaptionGrid");
        var close = (Button)window.FindName("NclCaptionClose");
        var closeX = PhysicalCenterX(close, caption);
        Check(UiLaySvc.Current.IsRtl
                ? closeX < caption.ActualWidth / 2
                : closeX > caption.ActualWidth / 2,
            "NCL close control must follow the caption direction: physically left in Arabic, right in English.");

        // Form rows read label first, then field: label on the physical
        // right in Arabic and on the physical left in English.
        var labelStyle = (Style)window.FindResource("NclLocalizedLabelText");
        var formRoot = (FrameworkElement)window.Content;
        var formRows = 0;
        foreach (var label in Walk<TextBlock>(formRoot)
                     .Where(item => Shown(item) &&
                                    ReferenceEquals(item.Style, labelStyle)))
        {
            var row = VisualTreeHelper.GetParent(label) as Grid;
            var field = row?.Children
                .OfType<FrameworkElement>()
                .FirstOrDefault(item => !ReferenceEquals(item, label) &&
                                        Grid.GetColumn(item) == 2);
            Check(field is not null,
                $"NCL label '{label.Text}' has no field in its row.");
            var labelX = PhysicalCenterX(label, formRoot);
            var fieldX = PhysicalCenterX(field!, formRoot);
            Check(UiLaySvc.Current.IsRtl ? labelX > fieldX : labelX < fieldX,
                $"NCL label '{label.Text}' must precede its field in {UiLaySvc.Current.Language} reading order (label x={labelX:0}, field x={fieldX:0}).");
            formRows++;
        }

        Check(formRows > 0,
            "NCL form rows were not found for the label/field order check.");
        var title = (TextBlock)window.FindName("NclGameTitle");
        var identity = (TextBlock)window.FindName("NclGameIdentity");
        var details = (TextBox)window.FindName("NclDetailsText");
        var notice = (TextBlock)window.FindName("NclSourceNotice");
        var nclViewModel = (NclImportVm)window.DataContext;
        R3Config.Tests.TechnicalTextT.VerifyTextBlock(
            notice, nclViewModel.SourceNoticeFragments, UiLaySvc.Current.TextFlow);
        Check(!string.IsNullOrWhiteSpace(title.Text) &&
              title.FlowDirection == UiLaySvc.ResolveContentFlow(title.Text) &&
              title.TextAlignment == TextAlignment.Left,
            "NCL game title must render with content-derived direction and left alignment.");
        Check(identity.Text.Contains("BLES01128", StringComparison.Ordinal) &&
              identity.Text.Contains("01.02", StringComparison.Ordinal) &&
              identity.FlowDirection == FlowDirection.LeftToRight &&
              identity.TextAlignment == TextAlignment.Left,
            "NCL selected serial/version must render in an independent LTR technical surface.");
        Check(details.IsReadOnly &&
              details.FlowDirection == FlowDirection.LeftToRight &&
              details.TextAlignment == TextAlignment.Left,
            "NCL conversion details must remain read-only, LTR and left aligned.");
    }

    // The third Advanced Tools section. SetAdvancedSurface covers H-Canary
    // and Recovery; Diagnostics must be laid out and verified as well.
    private static void SetAdvancedDiagnostics(
        AdvDlg window)
    {
        SetAdvancedSurface(
            window,
            showHCanary: false);

        var navigation =
            (ListBox)window.FindName("AdvancedNavigation");
        navigation.SelectedIndex = 2;
        ((FrameworkElement)window.FindName("RecoveryWorkspace")).Visibility =
            Visibility.Collapsed;
        ((FrameworkElement)window.FindName("DiagnosticsWorkspace")).Visibility =
            Visibility.Visible;
    }

    // The fourth Advanced Tools section: recommendation database updates,
    // rendered with deterministic sample rows (long titles, quarantine
    // reasons, a store note) instead of the user's local sync store.
    private static void SetAdvancedRecSync(
        AdvDlg window)
    {
        SetAdvancedSurface(
            window,
            showHCanary: false);

        var navigation =
            (ListBox)window.FindName("AdvancedNavigation");
        navigation.SelectedIndex = 3;
        ((FrameworkElement)window.FindName("RecoveryWorkspace")).Visibility =
            Visibility.Collapsed;
        var sync = (FrameworkElement)window.FindName("RecSyncWorkspace");
        sync.Visibility = Visibility.Visible;
        LayRoot(window, new Size(1100, 760));
        var details = Walk<Expander>(sync).Single(item =>
            AutomationProperties.GetAutomationId(item) == "Sync.AdvancedDetails");
        details.IsExpanded = true;
        LayRoot(window, new Size(1100, 760));
        Check(
            Walk<DataGrid>(sync).All(grid => grid.FlowDirection == UiLaySvc.Current.ShFlow),
            "Recommendation update grids must follow the current interface direction.");
        Check(
            Walk<TextBlock>(sync).Any(text => text.Text == "BLUS31465" && text.FlowDirection == FlowDirection.LeftToRight),
            "Recommendation update serials must render left-to-right.");

        foreach (var size in new[] { new Size(960, 640), new Size(1280, 800) })
        {
            details.IsExpanded = false;
            VerifySurface(window, size, [96d]);
            details.IsExpanded = true;
            VerifySurface(window, size, [96d]);
        }
        details.IsExpanded = false;
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

        if (window.FindName("RecSyncWorkspace") is FrameworkElement recSync)
        {
            recSync.Visibility = Visibility.Collapsed;
        }

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
        toolsVm.Sync.Load(
            R3Config.Tests.RecSyncT.UiSample(),
            RecSyncStoreHealth.RecoveredFromBackup,
            8542,
            7794,
            null);
        var githubState = toolsVm.Sync.Sources.Single(source => source.Name == UiText.GetText("Sync.Source.GitHub"));
        Check(githubState.Status == UiText.GetText("Sync.Status.NotPublished") && githubState.Detail.Length == 0,
            "An unpublished GitHub database must have a short localized status and hide technical details.");

        var canVm = new CanImpVm(
            null,
            flow,
            () => false,
            (_, _) => false);

        var nclRoot = Path.Combine(
            Path.GetTempPath(),
            "R3ConfigUiGateNcl");
        var nclInstall = new Rpcs3InstallDescriptor(
            nclRoot,
            Path.Combine(nclRoot, "rpcs3.exe"),
            Path.Combine(nclRoot, "config.yml"),
            Path.Combine(nclRoot, "custom_configs"),
            Path.Combine(nclRoot, "patches"),
            Path.Combine(nclRoot, "patch_config.yml"));
        var nclGame = new Rpcs3GameDescriptor(
            Serial: "BLES01128",
            Title: "Grand Theft Auto IV: Complete Edition",
            InstPath: string.Empty,
            Version: "01.02",
            PpuHash: "a2c90aafce24744d5ac6da8ad009ce301458c8a7",
            SpuHash: null);
        var nclVm = new NclImportVm(
            flow,
            nclInstall,
            nclGame,
            UiLaySvc.Current.ShFlow == FlowDirection.RightToLeft
                ? "جراند ثفت اوتو IV"
                : nclGame.Title,
            [],
            () => false,
            (_, _) => false,
            () => null);

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
            },
            new NclImportDlg(
                null,
                nclVm)
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
                    Shown(tab) &&
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
                    // A workflow may disable later steps until earlier ones are
                    // complete; every step is still verified, then restored.
                    var step = tab.Items[idx] as TabItem;
                    var original = step?.ReadLocalValue(UIElement.IsEnabledProperty);
                    if (step is not null)
                    {
                        step.IsEnabled = true;
                    }

                    tab.SelectedIndex = idx;

                    Lay(win, size);
                    RawLocalization(root);
                    Align(tab);
                    DisabledText(tab);
                    WesternDigits(tab);
                    ButtonLabels(tab);
                    HeaderText(tab);
                    AccessibleNames(tab);

                    Bounds(
                        tab,
                        new Size(
                            tab.ActualWidth,
                            tab.ActualHeight));

                    if (step is not null)
                    {
                        if (original is BindingExpressionBase expression)
                        {
                            BindingOperations.SetBinding(step, UIElement.IsEnabledProperty, expression.ParentBindingBase);
                        }
                        else if (original == DependencyProperty.UnsetValue)
                        {
                            step.ClearValue(UIElement.IsEnabledProperty);
                        }
                        else
                        {
                            step.SetValue(UIElement.IsEnabledProperty, original);
                        }
                    }
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

        if (IsHCanaryStoredEmptyCaption(element))
        {
            return TextAlignment.Center;
        }

        if (IsHCanaryAnchorsCenteredHeader(element))
        {
            return TextAlignment.Center;
        }

        return UsesTechnicalStyle(element) ||
               IsTechnical(element)
            ? TextAlignment.Left
            : UiLaySvc.Current.TxtAlign;
    }

    // HCanAnchors' short DataGrid column headers
    // (App/View/HCanAnchors.xaml, {lang:TextExt Key=HCanary.Instructions.Address}
    // and {lang:TextExt Key=HCanary.Instructions.Value}) are intentionally
    // centered via HCanAnchorsShortHeaderText. The same two localization
    // keys are also used elsewhere with other styles, so this is scoped to
    // both the exact binding path AND the exact HCanAnchorsShortHeaderText
    // style instance -- not a global exemption for either key.
    private static bool IsHCanaryAnchorsCenteredHeader(
        FrameworkElement element)
    {
        if (element is not TextBlock text)
        {
            return false;
        }

        var path = text
            .GetBindingExpression(TextBlock.TextProperty)
            ?.ParentBinding
            .Path
            ?.Path;

        if (path is not (
                "[HCanary.Instructions.Address]" or
                "[HCanary.Instructions.Value]"))
        {
            return false;
        }

        var expectedStyle = text.TryFindResource(
            "HCanAnchorsShortHeaderText") as Style;

        return expectedStyle is not null &&
            ReferenceEquals(text.Style, expectedStyle);
    }

    // Canary's "no saved patches yet" empty-state caption
    // (App/View/HCanSaved.xaml and App/Ctrl/HSav.xaml, both
    // {lang:TextExt Key=HCanary.Stored.Empty}) is an intentional
    // centered empty-state message inside a centered empty-state panel,
    // not left-aligned prose. Scoped to this exact localization key's
    // binding path only -- it does not exempt centered TextBlocks or
    // empty states in general.
    private static bool IsHCanaryStoredEmptyCaption(
        FrameworkElement element)
    {
        if (element is not TextBlock text)
        {
            return false;
        }

        var path = text
            .GetBindingExpression(TextBlock.TextProperty)
            ?.ParentBinding
            .Path
            ?.Path;

        return string.Equals(
            path,
            "[HCanary.Stored.Empty]",
            StringComparison.Ordinal);
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
                StringComparison.OrdinalIgnoreCase) ||
            family.Contains(
                "#FluentSystemIcons",
                StringComparison.OrdinalIgnoreCase))
        {
            // WPF UI's bundled Fluent System Icons are icon fonts too.
            return true;
        }

        var value = text.Text;

        if (string.Equals(
                value,
                "!",
                StringComparison.Ordinal))
        {
            return true;
        }

        // AppDlg's severity badge (App/View/Dlg/App.xaml, x:Name="IconText")
        // shows one decorative single-glyph icon per DlgKind -- "!" above,
        // "×" for Error, "i" for Info, and "?" for Confirm -- centered
        // in a circular Border. It is not prose, so Center is correct there.
        // Scoped to that exact named element so an actual "?" used as real
        // UI text elsewhere still follows the Left alignment policy.
        return string.Equals(
                value,
                "?",
                StringComparison.Ordinal) &&
            string.Equals(
                text.Name,
                "IconText",
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

    // Runtime surfaces are laid out off-screen without Show(), so IsVisible
    // stays false everywhere. Effective visibility is the element's own and
    // every visual ancestor's Visibility, which layout does honour.
    private static bool Shown(
        UIElement element)
    {
        for (DependencyObject? current = element;
             current is not null;
             current = VisualTreeHelper.GetParent(current))
        {
            if (current is UIElement item &&
                item.Visibility != Visibility.Visible)
            {
                return false;
            }
        }

        return true;
    }

    // Digits stay Western (European) in both languages. An Arabic-language
    // TextBlock otherwise substitutes Arabic-Indic digits at render time.
    private static void WesternDigits(
        DependencyObject root)
    {
        foreach (var text in Walk<TextBlock>(root)
                     .Where(item =>
                         Shown(item) &&
                         item.Text.Any(char.IsAsciiDigit) &&
                         item.Language.IetfLanguageTag.StartsWith(
                             "ar",
                             StringComparison.OrdinalIgnoreCase)))
        {
            Check(
                NumberSubstitution.GetSubstitution(text) ==
                NumberSubstitutionMethod.European,
                $"{TextBlockLabel(text)} renders digits with {NumberSubstitution.GetSubstitution(text)} substitution; expected European.");
        }

        foreach (var box in Walk<TextBox>(root)
                     .Where(item =>
                         Shown(item) &&
                         item.Text.Any(char.IsAsciiDigit) &&
                         item.Language.IetfLanguageTag.StartsWith(
                             "ar",
                             StringComparison.OrdinalIgnoreCase)))
        {
            Check(
                NumberSubstitution.GetSubstitution(box) ==
                NumberSubstitutionMethod.European,
                $"{TextBoxLabel(box)} renders digits with {NumberSubstitution.GetSubstitution(box)} substitution; expected European.");
        }
    }

    // Every input a keyboard user can reach is announced by name: an
    // automation name or a LabeledBy label, not only a visual placeholder
    // or tooltip.
    private static void AccessibleNames(
        DependencyObject root)
    {
        foreach (var input in Walk<Control>(root)
                     .Where(item =>
                         (item is TextBox or ComboBox) &&
                         item.TemplatedParent is null &&
                         Shown(item) &&
                         item.Focusable &&
                         item.IsTabStop))
        {
            Check(
                !string.IsNullOrWhiteSpace(AutomationProperties.GetName(input)) ||
                AutomationProperties.GetLabeledBy(input) is not null,
                $"{input.GetType().Name} '{input.Name}' has no automation name or LabeledBy label.");
        }
    }

    // A column header shows its whole caption: wrapped header text must fit
    // the header height, or the second line is cut off.
    private static void HeaderText(
        DependencyObject root)
    {
        foreach (var header in Walk<DataGridColumnHeader>(root)
                     .Where(item => Shown(item) && item.ActualHeight > 0 && item.Column is not null))
        {
            foreach (var text in Walk<TextBlock>(header)
                         .Where(item => Shown(item) && item.ActualWidth > 0 && item.Text.Length > 0))
            {
                // Arranged height, not a re-measure: a header wider than its
                // presenter that stays on one line is not clipped.
                var presenter = VisualTreeHelper.GetParent(text) as FrameworkElement ?? header;
                var needed = text.ActualHeight;
                Check(
                    needed <= presenter.ActualHeight + 0.5,
                    $"Column header '{text.Text}' needs {needed:0.#} px but is {presenter.ActualHeight:0.#} px high.");
            }
        }
    }

    // A button shows its whole label. With RecognizesAccessKey an underscore
    // is consumed as an access-key marker, so "imported_patch.yml" would
    // render as "importedpatch.yml".
    private static void ButtonLabels(
        DependencyObject root)
    {
        foreach (var button in Walk<ButtonBase>(root)
                     .Where(item => Shown(item) && item.Content is string))
        {
            var label = (string)button.Content;
            Check(
                !Walk<AccessText>(button).Any(item =>
                    item.Text.Replace("__", string.Empty, StringComparison.Ordinal).Contains('_', StringComparison.Ordinal)),
                $"{button.GetType().Name} label '{label}' loses an underscore to access-key processing.");

            // The label line must fit vertically; a fixed-height button whose
            // padding leaves less than one line box clips marks below the
            // baseline, such as the hamza of an Arabic alef.
            foreach (var presenter in Walk<ContentPresenter>(button)
                         .Where(item => item.ActualHeight > 0))
            {
                foreach (var text in Walk<TextBlock>(presenter)
                             .Where(item => Shown(item) && item.ActualHeight > 0))
                {
                    text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    Check(
                        text.DesiredSize.Height <= presenter.ActualHeight + 0.5,
                        $"{button.GetType().Name} label '{label}' needs {text.DesiredSize.Height:0.#} px but its content area is {presenter.ActualHeight:0.#} px high.");
                    text.InvalidateMeasure();
                }
            }
        }
    }

    // A disabled button's text must render the button's own disabled
    // foreground. The application-wide implicit TextBlock style must not
    // restore the enabled text colour inside a disabled control.
    private static void DisabledText(
        DependencyObject root)
    {
        foreach (var button in Walk<ButtonBase>(root)
                     .Where(item => Shown(item) && !item.IsEnabled))
        {
            var expected =
                (button.Foreground as SolidColorBrush)?.Color;

            foreach (var text in Walk<TextBlock>(button)
                         .Where(item => Shown(item)))
            {
                var actual =
                    (text.Foreground as SolidColorBrush)?.Color;

                Check(
                    expected is not null &&
                    actual == expected,
                    $"Disabled {button.GetType().Name} text '{text.Text}' renders {actual} instead of its disabled foreground {expected}.");
            }
        }
    }

    // Themes (keyed by their Text.OnAccent colour) in which an enabled
    // FluentButton.Primary label was actually rendered and checked.
    private static readonly HashSet<Color> AccentLabelThemes = [];

    // Themes (keyed by their Status.Danger colour) in which an enabled
    // FluentButton.Danger label was actually rendered and checked.
    private static readonly HashSet<Color> DangerLabelThemes = [];

    // Languages in which a custom caption Close button was actually checked.
    private static readonly HashSet<bool> CaptionCloseLanguages = [];

    // Accent-filled buttons must render their label in Text.OnAccent, and that
    // colour must reach WCAG AA normal-text contrast on every accent state.
    // The application-wide implicit TextBlock style once replaced it with
    // Text.Primary (about 2.8:1 in Dark and 2.9:1 in Light).
    private static void AccentText(
        DependencyObject root)
    {
        var onAccent = ((SolidColorBrush)Application.Current.FindResource(
            "Text.OnAccent")).Color;

        foreach (var key in new[] { "Accent.Default", "Accent.Hover", "Accent.Pressed" })
        {
            var fill = ((SolidColorBrush)Application.Current.FindResource(key)).Color;
            var ratio = ContrastRatio(onAccent, fill);
            Check(
                ratio >= 4.5,
                $"Text.OnAccent {onAccent} on {key} {fill} is {ratio:0.00}:1; accent button labels need at least 4.5:1.");
        }

        foreach (var button in Walk<Button>(root)
                     .Where(item => Shown(item) &&
                                    item.IsEnabled &&
                                    UsesStyle(item, "FluentButton.Primary")))
        {
            var labels = Walk<TextBlock>(button)
                .Where(item => Shown(item) && item.Text.Length > 0)
                .ToArray();
            Check(
                labels.Length > 0,
                $"Primary button '{button.Content}' renders no label text.");

            foreach (var label in labels)
            {
                var actual = (label.Foreground as SolidColorBrush)?.Color;
                Check(
                    actual == onAccent,
                    $"Primary button label '{label.Text}' renders {actual} instead of Text.OnAccent {onAccent}.");
            }

            AccentLabelThemes.Add(onAccent);
        }
    }

    // Danger buttons render their label in Status.Danger, at WCAG AA
    // normal-text contrast on the fill actually drawn behind it. The
    // application-wide implicit TextBlock style once replaced it with
    // Text.Primary, dropping the danger colour from the label.
    private static void DangerText(
        DependencyObject root)
    {
        foreach (var button in Walk<Button>(root)
                     .Where(item => Shown(item) &&
                                    item.IsEnabled &&
                                    UsesStyle(item, "FluentButton.Danger")))
        {
            DangerLabel(button, "rest");
        }
    }

    private static void DangerLabel(
        Button button,
        string state)
    {
        var danger = ((SolidColorBrush)Application.Current.FindResource(
            "Status.Danger")).Color;
        var chrome = button.Template?.FindName("Chrome", button) as Border;
        var fill = (chrome?.Background as SolidColorBrush)?.Color;
        Check(
            fill is not null,
            $"Danger button '{button.Content}' ({state}) has no solid Chrome fill.");

        var labels = Walk<TextBlock>(button)
            .Where(item => Shown(item) && item.Text.Length > 0)
            .ToArray();
        Check(
            labels.Length > 0,
            $"Danger button '{button.Content}' ({state}) renders no label text.");

        foreach (var label in labels)
        {
            var actual = (label.Foreground as SolidColorBrush)?.Color;
            Check(
                actual == danger,
                $"Danger button label '{label.Text}' ({state}) renders {actual} instead of Status.Danger {danger}.");
            var ratio = ContrastRatio(actual!.Value, fill!.Value);
            Check(
                ratio >= 4.5,
                $"Danger button label '{label.Text}' ({state}) {actual} on {fill} is {ratio:0.00}:1; button labels need at least 4.5:1.");
        }

        DangerLabelThemes.Add(danger);
    }

    // The gate's surfaces show an enabled Danger button only when a backup or
    // a stored H-Canary patch exists, so one real FluentButton.Danger is laid
    // out in every theme and language, at rest and pressed. Hover cannot be
    // forced, so its label (not changed by the hover trigger) is checked
    // against the fill the template's own IsMouseOver trigger draws.
    private static void DangerProbe()
    {
        var button = new DangerProbeButton
        {
            Content = UiText.GetText("Adv.DeleteSelectedBackup"),
            Style = (Style)Application.Current.FindResource("FluentButton.Danger")
        };
        var host = new Border
        {
            Background = (Brush)Application.Current.FindResource("Surface.Card"),
            Child = button
        };
        var size = new Size(320, 80);
        host.Measure(size);
        host.Arrange(new Rect(size));
        host.UpdateLayout();

        DangerLabel(button, "rest");

        button.Press(true);
        host.UpdateLayout();
        DangerLabel(button, "pressed");
        button.Press(false);
        host.UpdateLayout();

        var label = Walk<TextBlock>(button).First(item => item.Text.Length > 0);
        var labelColor = ((SolidColorBrush)label.Foreground).Color;
        var hoverFills = button.Template.Triggers
            .OfType<Trigger>()
            .Where(trigger => trigger.Property == UIElement.IsMouseOverProperty)
            .SelectMany(trigger => trigger.Setters.OfType<Setter>())
            .Where(setter => setter.TargetName == "Chrome" &&
                             setter.Property == Border.BackgroundProperty)
            .Select(setter => setter.Value is DynamicResourceExtension resource
                ? button.FindResource(resource.ResourceKey)
                : setter.Value)
            .OfType<SolidColorBrush>()
            .ToArray();
        Check(
            hoverFills.Length == 1,
            "FluentButton.Danger template must draw exactly one solid hover fill.");
        var hoverRatio = ContrastRatio(labelColor, hoverFills[0].Color);
        Check(
            hoverRatio >= 4.5,
            $"Danger button label {labelColor} on hover fill {hoverFills[0].Color} is {hoverRatio:0.00}:1; button labels need at least 4.5:1.");
    }

    private sealed class DangerProbeButton : Button
    {
        public void Press(bool pressed) => IsPressed = pressed;
    }

    // Custom caption Close buttons follow the interface direction exactly as
    // the main window's caption buttons do: physically left in Arabic and
    // physically right in English.
    private static void CaptionSide(
        FrameworkElement root)
    {
        var rtl = UiLaySvc.Current.IsRtl;
        foreach (var close in Walk<Button>(root)
                     .Where(item => Shown(item) &&
                                    item.ActualWidth > 0 &&
                                    UsesStyle(item, "FluentButton.Close.Caption")))
        {
            var closeX = PhysicalCenterX(close, root);
            Check(
                rtl ? closeX < root.ActualWidth / 2 : closeX > root.ActualWidth / 2,
                $"{Window.GetWindow(root)?.GetType().Name} caption Close is at physical x={closeX:0} of {root.ActualWidth:0}; expected the {(rtl ? "left" : "right")} side in {UiLaySvc.Current.Language}.");
            CaptionCloseLanguages.Add(rtl);
        }
    }

    // Screen-physical X of an element's centre inside frame. A frame laid out
    // RightToLeft mirrors its coordinate space, so X is measured from its
    // physical left edge.
    private static double PhysicalCenterX(
        FrameworkElement element,
        FrameworkElement frame)
    {
        var center = element.TranslatePoint(
            new Point(element.ActualWidth / 2, element.ActualHeight / 2),
            frame);
        return frame.FlowDirection == FlowDirection.RightToLeft
            ? frame.ActualWidth - center.X
            : center.X;
    }

    private static double ContrastRatio(
        Color first,
        Color second)
    {
        var a = RelativeLuminance(first);
        var b = RelativeLuminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    // WCAG 2.x relative luminance of an sRGB colour.
    private static double RelativeLuminance(
        Color color)
    {
        static double Channel(byte value)
        {
            var channel = value / 255d;
            return channel <= 0.03928
                ? channel / 12.92
                : Math.Pow((channel + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(color.R) +
               0.7152 * Channel(color.G) +
               0.0722 * Channel(color.B);
    }

    private static void Bounds(
        DependencyObject root,
        Size size)
    {
        foreach (var item in Walk<FrameworkElement>(root))
        {
            if (!Shown(item) ||
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

            // An icon-only button shows one icon-font glyph; like
            // FluentButton.Icon it is sized as a square, not as a text button.
            if (item is Button button &&
                !UsesStyle(
                    button,
                    "FluentButton.Icon") &&
                !(button.Content is TextBlock glyph &&
                  IsIconText(glyph)))
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
                    Shown(item) &&
                    item.TemplatedParent is null &&
                    item.ActualWidth > 0 &&
                    item.ActualHeight > 0)
                .ToArray();

            var rects =
                new Dictionary<FrameworkElement, Rect>();

            foreach (var item in kids)
            {
                // TransformBounds, unlike TranslatePoint of the origin, stays
                // correct when a FlowDirection boundary mirrors the child.
                var rect = item
                    .TransformToVisual(panel)
                    .TransformBounds(
                        new Rect(
                            new Size(
                                item.ActualWidth,
                                item.ActualHeight)));

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

        Save(
            wins.OfType<NclImportDlg>().Single(),
            $"{tag}-NclImportDlg.png",
            outDir);

        Save(wins.OfType<HCanSavedDlg>().Single(), $"{tag}-HCanSavedDlg.png", outDir);
        Save(wins.OfType<HCanTargetsDlg>().Single(), $"{tag}-HCanTargetsDlg.png", outDir);
        Save(wins.OfType<HCanAnchorsDlg>().Single(), $"{tag}-HCanAnchorsDlg.png", outDir);

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
                    $"{tag}-Adv-{(idx < names.Length ? names[idx] : "Tab" + idx)}.png",
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
                $"{tag}-HCan-{(idx < hNames.Length ? hNames[idx] : "Tab" + idx)}.png",
                outDir);
        }

        var savedDrawer =
            Walk<FrameworkElement>(root)
                .Single(element =>
                    element.Name == "SavedDrawer");

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

        SetAdvancedSurface((AdvDlg)adv, showHCanary: false);
        LayRoot(adv, size);
        Save(adv, $"{tag}-Adv-Recovery.png", outDir);

        SetAdvancedDiagnostics((AdvDlg)adv);
        LayRoot(adv, size);
        Save(adv, $"{tag}-Adv-Diagnostics.png", outDir);

        SetAdvancedRecSync((AdvDlg)adv);
        LayRoot(adv, size);
        Save(adv, $"{tag}-Adv-RecSync.png", outDir);
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
