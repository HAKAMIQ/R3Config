using System.IO;
using R3Config.Lang;

internal static partial class Program
{
    private static void Lang_CompiledPackContract(
        Dictionary<string, string> ar,
        Dictionary<string, string> en)
    {
        var runtimeLangDir = Path.Combine(
            AppContext.BaseDirectory,
            "Lang");
        Assert(
            !File.Exists(Path.Combine(runtimeLangDir, "ar-SA.json")) &&
            !File.Exists(Path.Combine(runtimeLangDir, "en-US.json")),
            "Build output must not expose editable JSON language sources.");

        foreach (var (language, source) in new[]
                 {
                     (UiText.ArabLang, ar),
                     (UiText.EnLang, en)
                 })
        {
            Assert(
                R3LangManifest.TryResolve(
                    language,
                    out var packFileName,
                    out var packSha256),
                $"Expected generated R3 language manifest entry for {language}.");

            var packPath = Path.Combine(
                runtimeLangDir,
                packFileName);
            Assert(
                File.Exists(packPath),
                $"Expected compiled language pack in build output: {packPath}");
            Assert(
                Path.GetExtension(packPath).Equals(
                    ".r3lang",
                    StringComparison.OrdinalIgnoreCase),
                $"Runtime language must use compiled .r3lang format: {packPath}");

            var compiled = R3LangReader.Load(
                packPath,
                language,
                packSha256);
            Assert(
                compiled.Count == source.Count,
                $"Compiled {language} language count differs from its JSON source.");
            foreach (var (key, expectedValue) in source)
            {
                Assert(
                    compiled.TryGetValue(key, out var actualValue) &&
                    string.Equals(
                        actualValue,
                        expectedValue,
                        StringComparison.Ordinal),
                    $"Compiled {language} value differs from source JSON: {key}");
            }

            var tamperedPath = Path.Combine(
                Path.GetTempPath(),
                $"R3Config-{language}-{Guid.NewGuid():N}.r3lang");
            try
            {
                var tampered = File.ReadAllBytes(packPath);
                tampered[^1] ^= 0x5A;
                File.WriteAllBytes(tamperedPath, tampered);

                var rejected = false;
                try
                {
                    _ = R3LangReader.Load(
                        tamperedPath,
                        language,
                        packSha256);
                }
                catch (InvalidDataException)
                {
                    rejected = true;
                }

                Assert(
                    rejected,
                    $"Tampered {language} language pack must fail integrity validation.");
            }
            finally
            {
                File.Delete(tamperedPath);
            }
        }
    }
}
