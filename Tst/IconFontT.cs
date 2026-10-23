using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace R3Config.Tests;

// R3Config supports Windows 10 and Windows 11. "Segoe Fluent Icons" ships with
// Windows 11 only; Windows 10 carries the same codepoints in "Segoe MDL2
// Assets". Every icon font declaration must name both, and every glyph the
// application uses must exist in both fonts, so no icon renders as a missing
// glyph on a supported system.
internal static partial class IconFontT
{
    private const string Fluent = "Segoe Fluent Icons";
    private const string Mdl2 = "Segoe MDL2 Assets";

    public static void Run(string appDir)
    {
        var xaml = Directory
            .EnumerateFiles(appDir, "*.xaml", SearchOption.AllDirectories)
            .Where(path => !path.Contains(
                               Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar,
                               StringComparison.OrdinalIgnoreCase) &&
                           !path.Contains(
                               Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar,
                               StringComparison.OrdinalIgnoreCase))
            .ToDictionary(path => path, File.ReadAllText);

        var declarations = xaml
            .SelectMany(file => FontDeclaration().Matches(file.Value)
                .Select(match => (File: Path.GetFileName(file.Key), Value: match.Groups["value"].Value)))
            .Where(item => item.Value.Contains(Fluent, StringComparison.OrdinalIgnoreCase) ||
                           item.Value.Contains(Mdl2, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Check(declarations.Length > 0, "No icon font declarations were found.");
        foreach (var (file, value) in declarations)
        {
            Check(value.Contains(Fluent, StringComparison.Ordinal) &&
                  value.Contains(Mdl2, StringComparison.Ordinal),
                $"{file}: icon font '{value}' must declare '{Fluent}, {Mdl2}' for Windows 10 support.");
        }

        var glyphs = xaml.Values
            .SelectMany(text => GlyphReference().Matches(text)
                .Select(match => (char)Convert.ToInt32(match.Groups["hex"].Value, 16)))
            .Distinct()
            .OrderBy(glyph => glyph)
            .ToArray();
        Check(glyphs.Length > 0, "No icon glyph references were found.");

        foreach (var family in new[] { Fluent, Mdl2 })
        {
            var typeface = new Typeface(new FontFamily(family), default, default, default);
            Check(typeface.TryGetGlyphTypeface(out var glyphTypeface) &&
                  glyphTypeface.FamilyNames.Values.Any(name => name.Equals(family, StringComparison.OrdinalIgnoreCase)),
                $"The '{family}' font is not installed on this verification machine.");
            var missing = glyphs
                .Where(glyph => !glyphTypeface!.CharacterToGlyphMap.ContainsKey(glyph))
                .Select(glyph => ((int)glyph).ToString("X4", System.Globalization.CultureInfo.InvariantCulture))
                .ToArray();
            Check(missing.Length == 0,
                $"'{family}' lacks icon glyph(s) used by R3Config: {string.Join(", ", missing)}.");
        }

        Console.WriteLine(
            $"Icon font fallback ({declarations.Length} declarations, {glyphs.Length} glyphs in {Fluent} and {Mdl2}) PASS.");
    }

    private static void Check(bool ok, string message)
    {
        if (!ok)
        {
            throw new InvalidOperationException(message);
        }
    }

    [GeneratedRegex(@"(?:FontFamily|Value)=""(?<value>[^""]*)""", RegexOptions.CultureInvariant)]
    private static partial Regex FontDeclaration();

    [GeneratedRegex(@"&#x(?<hex>[EF][0-9A-Fa-f]{3});", RegexOptions.CultureInvariant)]
    private static partial Regex GlyphReference();
}
