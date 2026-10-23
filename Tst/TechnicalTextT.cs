using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using R3Config.Lang;
using R3Config.Svc;

namespace R3Config.Tests;

internal static class TechnicalTextT
{
    private static readonly string[] Values =
    [
        "01.00", "01.12", "1.2.3", "10", "100", "1,000", "10x", "100x",
        "1,000x", "1920x1080", "60.00", "0x0067ED08", "3C A0 42 C8",
        "BCUS98232", "PPU-3a0b43e4a5f4bfea64f53612ee7c5d990f88129c"
    ];

    public static void Run()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var previousLanguage = UiText.CurLang;
            try
            {
                foreach (var language in new[] { UiText.ArabLang, UiText.EnLang })
                {
                    UiText.SetLang(language);
                    BuildAndVerify(language);
                }
            }
            catch (Exception error)
            {
                failure = error;
            }
            finally
            {
                UiText.SetLang(previousLanguage);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Check(thread.Join(TimeSpan.FromSeconds(30)), "Technical-fragment test timed out.");
        if (failure is not null)
        {
            throw new InvalidOperationException("Technical-fragment presentation failed.", failure);
        }
    }

    public static void VerifyRuntime(string language, bool isDark, string? shotDirectory)
    {
        var surface = BuildAndVerify(language);
        if (string.IsNullOrWhiteSpace(shotDirectory))
        {
            return;
        }

        Directory.CreateDirectory(shotDirectory);
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(surface.ActualWidth),
            (int)Math.Ceiling(surface.ActualHeight),
            96, 96, PixelFormats.Pbgra32);
        bitmap.Render(surface);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(
            shotDirectory, $"{(isDark ? "dark" : "light")}-{language}-TechnicalFragments.png"));
        encoder.Save(stream);
    }

    private static Border BuildAndVerify(string language)
    {
        var layout = UiLaySt.FromLang(language);
        var panel = new StackPanel();
        var surface = new Border
        {
            Width = 960,
            Padding = new Thickness(24),
            FlowDirection = FlowDirection.LeftToRight,
            Background = System.Windows.Application.Current?.TryFindResource("Surface.Background") as Brush ?? Brushes.White,
            Child = panel
        };
        if (System.Windows.Application.Current is null)
        {
            surface.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("/R3Config;component/Thm/Type.xaml", UriKind.Relative)
            });
            surface.Resources["TechnicalText.FlowDirection"] = layout.TechFlow;
            surface.Resources["TechnicalText.Language"] = XmlLanguage.GetLanguage(UiText.EnLang);
        }

        foreach (var value in Values)
        {
            // The existing content converters already cover ASCII numeric-only
            // fragments. Do not introduce another classifier for mixed prose.
            Check(Equals(new ContentTextFlowConverter().Convert(value, typeof(FlowDirection), null,
                    CultureInfo.InvariantCulture), FlowDirection.LeftToRight), value + " content flow");
            Check(((XmlLanguage)new ContentTextLanguageConverter().Convert(value, typeof(XmlLanguage), null,
                    CultureInfo.InvariantCulture)).IetfLanguageTag.Equals(UiText.EnLang,
                    StringComparison.OrdinalIgnoreCase), value + " content language");
            Add(TextFragments.TechnicalList(
                UiText.GetText("Patch.Ncl.Corpus.OtherVersions"), [value]));
        }

        var separator = UiText.GetText("Ui.ListSeparator");
        var versionFragments = TextFragments.TechnicalList(
            UiText.GetText("Patch.Ncl.Corpus.OtherVersions"), ["01.00", "01.01", "02.00"]);
        var versions = Add(versionFragments);
        var prose = Add([new TextFragment(language == UiText.ArabLang ? "عدد العناصر 10" : "Item count 10")]);
        Check(!prose.Inlines.OfType<Span>().Any(), "Ordinary prose numbers must not be classified as technical.");

        Layout();
        foreach (var block in panel.Children.OfType<TextBlock>())
        {
            VerifyTextBlock(block, TextFragments.GetContent(block)!, layout.TextFlow);
        }

        // The list is asserted from the source fragments and from the Inline
        // tree they produced. TextBlock.Text is a coerced projection of complex
        // content and is never the authority for what a list actually carries.
        (string Text, bool IsTechnical)[] expectedList =
        [
            (UiText.GetText("Patch.Ncl.Corpus.OtherVersions"), false),
            (" ", false),
            ("01.00", true),
            (separator, false),
            ("01.01", true),
            (separator, false),
            ("02.00", true)
        ];

        Check(versionFragments.Count == expectedList.Length,
            $"The technical list must produce {expectedList.Length} fragments, not {versionFragments.Count}.");

        for (var index = 0; index < expectedList.Length; index++)
        {
            var (text, isTechnical) = expectedList[index];
            Check(versionFragments[index].Text == text &&
                  versionFragments[index].IsTechnical == isTechnical,
                $"Fragment {index} must be '{text}' (technical={isTechnical}), not " +
                $"'{versionFragments[index].Text}' (technical={versionFragments[index].IsTechnical}).");
        }

        var versionInlines = versions.Inlines.ToArray();
        Check(versionInlines.Length == expectedList.Length,
            $"Every list fragment must render as its own inline: expected {expectedList.Length}, rendered {versionInlines.Length}.");

        for (var index = 0; index < expectedList.Length; index++)
        {
            var (text, isTechnical) = expectedList[index];
            if (isTechnical)
            {
                Check(versionInlines[index] is Span technical &&
                      technical.Inlines.Count == 1 &&
                      technical.Inlines.FirstInline is Run technicalRun &&
                      technicalRun.Text == text,
                    $"Technical fragment {index} must render as a Span wrapping exactly one Run '{text}'.");
                continue;
            }

            Check(versionInlines[index] is Run prosePart && prosePart.Text == text,
                $"Prose fragment {index} must render as a plain Run '{text}', outside every technical scope.");
        }

        Check(versionInlines.OfType<Span>().Count() == 3, "Every version needs its own BiDi scope.");
        Check(versionInlines.OfType<Run>().Count(run =>
                run.Text.Equals(separator, StringComparison.Ordinal)) == 2,
            $"The list must carry exactly two localized separators as prose runs: '{separator}'.");
        Check(!versionInlines.OfType<Span>().Any(span =>
                InlineText(span.Inlines).Contains(separator, StringComparison.Ordinal)),
            "List punctuation must remain localized and outside the technical scopes.");

        // Replacement and clearing must never leave stale inlines or text.
        var transient = Add([new TextFragment("01.00", IsTechnical: true)]);
        TextFragments.SetContent(transient, [new TextFragment("01.12", IsTechnical: true)]);
        Layout();
        VerifyTextBlock(transient, TextFragments.GetContent(transient)!, layout.TextFlow);
        TextFragments.SetContent(transient, null);
        Check(transient.Inlines.Count == 0 && transient.Text.Length == 0, "Clearing fragments must clear rendered text.");
        panel.Children.Remove(transient);
        Layout();
        return surface;

        TextBlock Add(IReadOnlyList<TextFragment> fragments)
        {
            var block = new TextBlock
            {
                FlowDirection = layout.TextFlow,
                Language = layout.XmlLang,
                TextAlignment = TextAlignment.Left,
                FontSize = 18,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8),
                Foreground = System.Windows.Application.Current?.TryFindResource("Text.Primary") as Brush ?? Brushes.Black
            };
            panel.Children.Add(block);
            TextFragments.SetContent(block, fragments);
            return block;
        }

        void Layout()
        {
            surface.Measure(new Size(960, double.PositiveInfinity));
            surface.Arrange(new Rect(new Point(), surface.DesiredSize));
            surface.UpdateLayout();
        }
    }

    public static void VerifyTextBlock(
        TextBlock block,
        IReadOnlyList<TextFragment> fragments,
        FlowDirection proseFlow)
    {
        Check(block.FlowDirection == proseFlow, "The surrounding paragraph must retain its localized direction.");
        var expected = TextFragments.PlainText(fragments);
        var rendered = InlineText(block.Inlines);
        Check(rendered == expected,
            "Text must remain unchanged, without digit conversion or injected BiDi controls. " +
            $"expected='{expected}' rendered='{rendered}' TextBlock.Text='{block.Text}'");
        var inlines = block.Inlines.ToArray();
        Check(inlines.Length == fragments.Count, "Every semantic fragment must remain distinct.");
        for (var index = 0; index < fragments.Count; index++)
        {
            var fragment = fragments[index];
            if (!fragment.IsTechnical)
            {
                Check(inlines[index] is Run prose && prose.FlowDirection == proseFlow && prose.Text == fragment.Text,
                    "Localized prose/separators must retain their text and inherited direction.");
                continue;
            }

            Check(inlines[index] is Span, "Canonical values require a distinct Span BiDi scope.");
            var span = (Span)inlines[index];
            var run = (Run)span.Inlines.Single();
            Check(span.FlowDirection == FlowDirection.LeftToRight &&
                  span.Language.IetfLanguageTag.Equals(UiText.EnLang, StringComparison.OrdinalIgnoreCase) &&
                  NumberSubstitution.GetSubstitution(run) == NumberSubstitutionMethod.European &&
                  run.Text == fragment.Text,
                "Technical fragments must use LTR/en-US/European digits without changing canonical text.");
            // Glyph rects come back in the block's own space, so they need a
            // frame that is a real ancestor. The caller's surface is not one in
            // every host - the runtime gate hands us a dialog whose named
            // TextBlock is not reachable from the Window visual - so the frame
            // is derived here: the block's own visual root always is an
            // ancestor. A root laid out RightToLeft mirrors its coordinate
            // space, so X is normalised to screen-physical before comparing;
            // for an LeftToRight root this is exactly the previous arithmetic.
            var root = VisualRoot(block);
            var toRoot = ReferenceEquals(root, block)
                ? Transform.Identity
                : (GeneralTransform)block.TransformToAncestor(root);
            var mirroredRoot = root is FrameworkElement rootElement &&
                               rootElement.FlowDirection == FlowDirection.RightToLeft;
            var previousX = double.NegativeInfinity;
            var previousY = double.NaN;
            for (var offset = 0; offset < run.Text.Length; offset++)
            {
                var rectangle = run.ContentStart.GetPositionAtOffset(offset)!
                    .GetCharacterRect(LogicalDirection.Forward);
                var mapped = toRoot.TransformBounds(rectangle);
                var physicalX = mirroredRoot ? -mapped.X : mapped.X;
                Check(mapped.Y != previousY || physicalX >= previousX,
                    $"Technical glyphs were visually reordered: {fragment.Text}, character {offset}.");
                previousX = physicalX;
                previousY = mapped.Y;
            }
        }
    }

    // The topmost visual above this one, which is an ancestor by construction.
    // PresentationSource.FromVisual is not usable here: the Application-less
    // harness measures a detached Border that belongs to no source, and would
    // get back null.
    private static Visual VisualRoot(Visual visual)
    {
        var current = visual;
        while (VisualTreeHelper.GetParent(current) is Visual parent)
        {
            current = parent;
        }

        return current;
    }

    // The rendered text is what the Run tree carries. TextBlock.Text is a
    // coerced projection WPF synthesizes from the text container whenever a
    // TextBlock holds Inlines instead of a plain string, so it is not a sound
    // witness for "the canonical text survived" - Text and Inlines are
    // alternative content models, not two views of one value.
    //
    // Reading the tree is also the stricter check: a substituted digit or an
    // injected BiDi control would have to appear inside a Run to reach the
    // screen, and any inline other than Run/Span - a LineBreak or an
    // InlineUIContainer smuggled into the content - fails outright here.
    private static string InlineText(InlineCollection inlines)
    {
        var builder = new System.Text.StringBuilder();
        Append(inlines);
        return builder.ToString();

        void Append(InlineCollection items)
        {
            foreach (var inline in items)
            {
                switch (inline)
                {
                    case Run run:
                        builder.Append(run.Text);
                        break;

                    case Span span:
                        Append(span.Inlines);
                        break;

                    default:
                        throw new InvalidOperationException(
                            $"Unexpected inline {inline.GetType().Name} in a technical fragment tree.");
                }
            }
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
