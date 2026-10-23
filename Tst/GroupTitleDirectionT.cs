using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;

namespace R3Config.Tests;

// Dynamic title direction is decided by the content class, never by a title
// list: a game title takes its direction from its own characters ("God of War
// 3" stays "God of War 3" inside the Arabic interface, never "3 God of War";
// an Arabic title stays right-to-left inside the English interface), canonical
// English patch titles stay left-to-right, and every one of them is anchored to
// the interface start edge of its surface. Verified for the selected-game
// identity header, the Official/Canary/Artemis group headers and the
// Official/Canary/Artemis patch rows, with digits, colons, periods, mixed
// alphanumerics and an Arabic title.
internal static class GroupTitleDirectionT
{
    private const string ArabicTitle = "لعبة عربية 3";

    private static readonly string[] Titles =
    [
        "God of War 3",
        "3D Dot Game Heroes",
        "F1 2014",
        "WWE 2K14",
        "NieR Replicant ver.1.22474487139...",
        "Call of Duty: Black Ops II",
        ArabicTitle
    ];

    public static void Run()
    {
        var preview = new PatPrev();
        preview.Load(Titles
            .SelectMany((title, index) => Matches(
                title,
                $"BLUS3{index:0000}",
                new string((char)('1' + index), 40)))
            .ToArray());

        var templates = (ResourceDictionary)System.Windows.Application.LoadComponent(
            new Uri("/R3Config;component/Ctrl/PatchTemplates.xaml", UriKind.Relative));
        var canary = new R3Config.Ctrl.CanaryPatchView();
        VerifyGroups((DataTemplate)templates["TgtHead"], preview.OfclGrp, "Official");
        VerifyGroups((DataTemplate)templates["ArtemisTgtHead"], preview.ChtGrps, "Artemis");
        VerifyGroups((DataTemplate)canary.Resources["CanHead"], preview.CanGrps, "Canary");
        VerifyRows((DataTemplate)templates["PatItem"], preview.OfclGrp, "Official Fixture", "Official row");
        VerifyRows((DataTemplate)templates["ArtemisPatItem"], preview.ChtGrps, "Artemis Fixture", "Artemis row");
        VerifyRows((DataTemplate)canary.Resources["CanItem"], preview.CanGrps, "Canary Fixture", "Canary row");

        var identity = (ResourceDictionary)System.Windows.Application.LoadComponent(
            new Uri("/R3Config;component/Ctrl/GameIdentityTemplate.xaml", UriKind.Relative));
        VerifyIdentity((DataTemplate)identity["GameIdentity"]);
        Console.WriteLine(
            $"Dynamic title direction ({Titles.Length} titles: identity header, 3 group headers, 3 patch rows; {System.Windows.Application.Current.FindResource("App.FlowDirection")}) PASS.");
    }

    private static void VerifyGroups(
        DataTemplate template,
        IEnumerable<TgtGrpVm> groups,
        string surface)
    {
        foreach (var title in Titles)
        {
            var group = groups.SingleOrDefault(item =>
                item.Header == title || item.ArtemisHeader == title)
                ?? throw new InvalidOperationException($"{surface}: no group for '{title}'.");
            var host = Render(group, template, 120);
            var header = FindText(host, title, surface);
            CheckContentFlow(header, title, surface);

            var line = ((Panel)header.Parent).Children.OfType<TextBlock>().First(text => !ReferenceEquals(text, header));
            var headerBounds = TextBounds(header, host);
            var lineBounds = TextBounds(line, host);
            // Bounds are in the host's own flow coordinates, where the
            // interface start edge is always x = 0 (the right edge in Arabic).
            Check(Math.Abs(headerBounds.Left - lineBounds.Left) <= 1,
                $"{surface}: header '{title}' left the interface start edge ({headerBounds} vs {lineBounds}).");
        }
    }

    private static void VerifyRows(
        DataTemplate template,
        IEnumerable<TgtGrpVm> groups,
        string patchTitle,
        string surface)
    {
        foreach (var item in groups.SelectMany(group => group.Items))
        {
            var host = Render(item, template, 80);
            var title = FindText(host, patchTitle, surface);
            Check(title.FlowDirection == FlowDirection.LeftToRight,
                $"{surface}: canonical English patch title '{patchTitle}' renders {title.FlowDirection}.");
            // Wrapped lines must stay on the start edge too: inside the Arabic
            // interface a left-to-right title aligns to its physical right.
            var shell = (FlowDirection)System.Windows.Application.Current.FindResource("App.FlowDirection");
            var startAlignment = shell == FlowDirection.RightToLeft ? TextAlignment.Right : TextAlignment.Left;
            Check(title.TextAlignment == startAlignment,
                $"{surface}: wrapped lines of '{patchTitle}' align {title.TextAlignment}; expected {startAlignment} in a {shell} interface.");

            // The title starts at the start edge of its grid column, next to
            // the selection box, in both interface directions.
            DependencyObject child = title;
            var parent = VisualTreeHelper.GetParent(child);
            while (parent is not Grid { ColumnDefinitions.Count: >= 3 })
            {
                child = parent ?? throw new InvalidOperationException($"{surface}: title has no row grid.");
                parent = VisualTreeHelper.GetParent(child);
            }

            var grid = (Grid)parent;
            var column = grid.ColumnDefinitions[Grid.GetColumn((UIElement)child)];
            var bounds = TextBounds(title, grid);
            Check(Math.Abs(bounds.Left - column.Offset) <= 1,
                $"{surface}: patch title '{patchTitle}' left its column start edge ({bounds} vs column offset {column.Offset}).");
        }
    }

    private static void VerifyIdentity(DataTemplate template)
    {
        foreach (var (title, index) in Titles.Select((title, index) => (title, index)))
        {
            var serial = $"BLUS3{index:0000}";
            var fixture = new IdentityFixture(title, serial, "v01.12");
            var host = Render(fixture, template, 96);
            var header = FindText(host, title, "Identity header");
            CheckContentFlow(header, title, "Identity header");

            var chip = FindText(host, serial, "Identity header");
            Check(chip.FlowDirection == FlowDirection.LeftToRight,
                $"Identity header: serial chip '{serial}' renders {chip.FlowDirection}.");
            var tile = Walk(host).OfType<Border>().Single(border =>
                border.Style == (Style)System.Windows.Application.Current.FindResource("IdentityTile"));

            var titleBounds = TextBounds(header, host);
            var chipRow = Bounds((FrameworkElement)VisualTreeHelper.GetParent(VisualTreeHelper.GetParent(chip)), host);
            var tileBounds = Bounds(tile, host);
            Check(Math.Abs(titleBounds.Left - chipRow.Left) <= 1,
                $"Identity header: title '{title}' and its chips do not share the start edge ({titleBounds} vs {chipRow}).");
            Check(titleBounds.Left >= tileBounds.Right &&
                  titleBounds.Left - tileBounds.Right <= 13,
                $"Identity header: title '{title}' is not anchored next to the identity tile ({titleBounds} vs {tileBounds}).");
        }
    }

    private static ContentPresenter Render(object content, DataTemplate template, double height)
    {
        var host = new ContentPresenter
        {
            Content = content,
            ContentTemplate = template,
            FlowDirection = (FlowDirection)System.Windows.Application.Current.FindResource("App.FlowDirection")
        };
        var size = new Size(640, height);
        host.Measure(size);
        host.Arrange(new Rect(size));
        host.UpdateLayout();
        return host;
    }

    private static TextBlock FindText(DependencyObject host, string text, string surface)
    {
        return Walk(host).OfType<TextBlock>().SingleOrDefault(block => block.Text == text)
            ?? throw new InvalidOperationException($"{surface}: '{text}' was not rendered.");
    }

    private static void CheckContentFlow(TextBlock block, string title, string surface)
    {
        var expected = title == ArabicTitle ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        Check(UiLaySvc.ResolveContentFlow(title) == expected,
            $"Content direction of '{title}' resolved to {UiLaySvc.ResolveContentFlow(title)}; expected {expected}.");
        Check(block.FlowDirection == expected,
            $"{surface}: '{title}' renders {block.FlowDirection}; expected {expected} from its own content.");
    }

    // The rendered text inside the block, not its layout box: a stretched
    // block can still draw its text at the far edge of its slot.
    private static Rect TextBounds(TextBlock block, Visual ancestor)
    {
        var box = block.RenderSize;
        var natural = Math.Min(
            box.Width,
            Math.Max(0, block.DesiredSize.Width - block.Margin.Left - block.Margin.Right));
        var x = block.TextAlignment switch
        {
            TextAlignment.Right => box.Width - natural,
            TextAlignment.Center => (box.Width - natural) / 2,
            _ => 0
        };
        return block.TransformToAncestor(ancestor).TransformBounds(new Rect(x, 0, natural, box.Height));
    }

    private static Rect Bounds(FrameworkElement element, Visual host)
    {
        return element.TransformToAncestor(host).TransformBounds(new Rect(element.RenderSize));
    }

    private static IEnumerable<PatMatch> Matches(
        string game,
        string serial,
        string hash)
    {
        var entries = new[]
        {
            Entry(PatSrc.Official, "Official Fixture", game, serial, hash),
            Entry(PatSrc.Can, "Canary Fixture", game, serial, hash),
            Entry(PatSrc.Artemis, "Artemis Fixture", game, serial, hash)
        };
        var descriptor = new Rpcs3GameDescriptor(
            serial,
            game,
            @"C:\Games\" + serial,
            "01.00",
            hash,
            null,
            HCanPpu: [hash],
            HCanSpu: []);
        return new PatchMatchService().Match(new PatCat("1.2", entries), descriptor);
    }

    private static Rpcs3PatchEntry Entry(
        PatSrc source,
        string title,
        string game,
        string serial,
        string hash)
    {
        var yaml =
            "Version: 1.2\n" +
            $"PPU-{hash}:\n" +
            $"  \"{title}\":\n" +
            "    Games:\n" +
            $"      \"{game}\":\n" +
            $"        {serial}: [ 01.00 ]\n" +
            "    Patch Version: 1.0\n" +
            "    Patch:\n" +
            "      - [ be32, 0x00001000, 0x60000000 ]\n";
        var read = new ReadSvc().ReadText(yaml, source, source + ".yml");
        Check(read.IsOk && read.Catalog.Entries.Count == 1,
            "Title direction fixture did not parse: " + read.Error);
        return read.Catalog.Entries[0];
    }

    private static IEnumerable<DependencyObject> Walk(DependencyObject root)
    {
        yield return root;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            foreach (var child in Walk(VisualTreeHelper.GetChild(root, index)))
            {
                yield return child;
            }
        }
    }

    private static void Check(bool ok, string message)
    {
        if (!ok)
        {
            throw new InvalidOperationException(message);
        }
    }

    // The identity template binds these MainVm presentation properties.
    private sealed record IdentityFixture(string SelTitle, string SelSerial, string SelVersion)
    {
        public string SelGame => $"{SelTitle} | {SelSerial} | {SelVersion}";

        public bool HasSelVersion => SelVersion.Length > 0;
    }
}
