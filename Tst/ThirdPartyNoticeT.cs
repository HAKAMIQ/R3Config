using System.IO;
using System.Text.RegularExpressions;

namespace R3Config.Tests;

// WPF-UI is MIT and its binary incorporates further notices. The release
// ships THIRD-PARTY-NOTICES.md with both texts verbatim; the notice version
// must be the referenced package version, and the shipped DLL must exist.
internal static partial class ThirdPartyNoticeT
{
    public static void Run(string repoRoot)
    {
        var noticePath = Path.Combine(repoRoot, "THIRD-PARTY-NOTICES.md");
        Check(File.Exists(noticePath), "THIRD-PARTY-NOTICES.md is missing from the repository root.");
        var notice = File.ReadAllText(noticePath);

        var project = File.ReadAllText(Path.Combine(repoRoot, "App", "R3App.csproj"));
        var reference = WpfUiReference().Match(project);
        Check(reference.Success, "App/R3App.csproj has no WPF-UI PackageReference.");
        var version = reference.Groups["version"].Value;

        foreach (var line in new[]
                 {
                     $"## WPF-UI {version}",
                     "- License: MIT",
                     "THIRD-PARTY SOFTWARE NOTICES AND INFORMATION"
                 })
        {
            Check(notice.Contains(line, StringComparison.Ordinal),
                $"THIRD-PARTY-NOTICES.md is missing '{line}' for the referenced WPF-UI {version}.");
        }

        Check(project.Contains(
                  "<None Include=\"..\\THIRD-PARTY-NOTICES.md\" Link=\"THIRD-PARTY-NOTICES.md\" CopyToPublishDirectory=\"Always\" />",
                  StringComparison.Ordinal),
            "App/R3App.csproj must publish THIRD-PARTY-NOTICES.md.");

        var packageLicense = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget", "packages", "wpf-ui", version, "LICENSE.md");
        if (File.Exists(packageLicense))
        {
            var license = File.ReadAllText(packageLicense).Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
            Check(notice.Replace("\r\n", "\n", StringComparison.Ordinal).Contains(license, StringComparison.Ordinal),
                "THIRD-PARTY-NOTICES.md must reproduce the WPF-UI package LICENSE verbatim.");
        }

        Check(File.Exists(Path.Combine(AppContext.BaseDirectory, "Wpf.Ui.dll")),
            "Wpf.Ui.dll covered by THIRD-PARTY-NOTICES.md is not in the build output.");

        var core = File.ReadAllText(Path.Combine(repoRoot, "Core", "R3Core.csproj"));
        var yaml = YamlReference().Match(core);
        Check(yaml.Success, "Core/R3Core.csproj has no YamlDotNet PackageReference.");
        var yamlVersion = yaml.Groups["version"].Value;
        foreach (var line in new[]
                 {
                     $"## YamlDotNet {yamlVersion}",
                     "Copyright (c) 2008, 2009, 2010, 2011, 2012, 2013, 2014 Antoine Aubry and contributors",
                     "Copyright (c) 2006 Kirill Simonov",
                     "## RPCS3 Wiki content",
                     "Creative Commons Attribution-ShareAlike 4.0 International (CC BY-SA 4.0)",
                     "`Data/cat.json`",
                     "`Data/wiki-notes.json`",
                     // PortPub.ps1 publishes self-contained, so the .NET 8
                     // runtime and the WPF Windows Desktop runtime ship too.
                     "## .NET 8 runtime and Windows Desktop runtime (bundled)",
                     "- Project: https://github.com/dotnet/runtime",
                     "- Project: https://github.com/dotnet/wpf",
                     "Copyright (c) .NET Foundation and Contributors",
                     "https://github.com/dotnet/runtime/blob/release/8.0/THIRD-PARTY-NOTICES.TXT"
                 })
        {
            Check(notice.Contains(line, StringComparison.Ordinal),
                $"THIRD-PARTY-NOTICES.md is missing '{line}'.");
        }

        Check(!project.Contains("All rights reserved", StringComparison.OrdinalIgnoreCase),
            "App/R3App.csproj copyright must not contradict GPL-3.0-or-later.");

        Console.WriteLine($"Third-party notice (WPF-UI {version}, YamlDotNet {yamlVersion}, RPCS3 Wiki CC BY-SA 4.0, bundled .NET 8 runtime, verbatim licenses, shipped) PASS.");
    }

    private static void Check(bool ok, string message)
    {
        if (!ok)
        {
            throw new InvalidOperationException(message);
        }
    }

    [GeneratedRegex(@"<PackageReference Include=""WPF-UI"" Version=""(?<version>[0-9.]+)"" />", RegexOptions.CultureInvariant)]
    private static partial Regex WpfUiReference();

    [GeneratedRegex(@"<PackageReference Include=""YamlDotNet"" Version=""(?<version>[0-9.]+)"" />", RegexOptions.CultureInvariant)]
    private static partial Regex YamlReference();
}
