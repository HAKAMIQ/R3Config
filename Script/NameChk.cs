using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: NameChk <repo-root>");
    return 2;
}

var root = Path.GetFullPath(args[0]);
var errors = new List<string>();
string[] sourceAreas = ["App", "Core", "Tst", "Script"];
var sourceFiles = sourceAreas
    .SelectMany(area => Directory.EnumerateFiles(
        Path.Combine(root, area),
        "*.cs",
        SearchOption.AllDirectories))
    .Where(path => !IsBuild(path))
    .Order(StringComparer.OrdinalIgnoreCase)
    .ToArray();

var retiredNames = new HashSet<string>(StringComparer.Ordinal)
{
    "AnlzCase", "AppCases", "BkpCase", "CfgCase", "HCanCase",
    "PatMut", "RecCat", "AppComp", "PatCmp", "PatTxn", "MutGuard",
    "IMutGd", "ImpSvc", "CanBndl", "ChtBndl", "BkpSvc", "GmScan",
    "InstSvc", "HashPrb", "PatMerge", "SelComp", "PatApply", "CfgApply",
    "R3Inst", "R3Game", "CmpDec", "CmpVerd", "CmpCode", "PatEntry",
    "PatPlan", "PrepFile", "ScanDiag", "ScanRes", "BkpMan", "BkpSnap",
    "CatLoad", "PatCatRs", "PatSrcSt", "GameRow", "AnlzRes", "IPatMsg",
    "R3Paths", "AppRun", "PlanSvc", "MatchSvc", "PatAuth", "PatMsgFm"
};

var requiredTypes = new HashSet<string>(StringComparer.Ordinal)
{
    "AnalysisUseCase", "AppUseCases", "BackupUseCase", "CanaryImportUseCase",
    "ConfigUseCase", "HCanaryUseCase", "PatchCatalogUseCase",
    "PatchMutationUseCase", "RecommendationCatalogUseCase", "AppComposition",
    "PatchCompatibilityService", "PatchFileTransactionService",
    "Rpcs3MutationGuard", "IRpcs3MutationGuard", "CanaryPatchImportService",
    "BundledCanaryPatchService", "BundledCheatPatchService", "BackupService",
    "GameScanner", "Rpcs3InstallService", "PatchHashProbeService",
    "PatchCatalogMergeService", "PatchSelectionCompiler", "PatchApplyService",
    "ConfigApplyService", "Rpcs3InstallDescriptor", "Rpcs3GameDescriptor",
    "PatchCompatibilityDecision", "PatchCompatibilityVerdict",
    "PatchCompatibilityReasonCode", "Rpcs3PatchEntry", "PatchApplyPlan",
    "PatchPreparedFile", "GameScanDiagnostic", "GameScanResult",
    "BackupManifest", "BackupSnapshot", "PlanService", "PatchMatchService",
    "HCanaryPatchAuthoringService", "LocalizedPatchCatalogMessageFormatter"
};

var externalContracts = new[]
{
    "rpcs3.exe", "PARAM.SFO", "patch.yml", "imported_patch.yml",
    "patch_config.yml", "config_<SERIAL>.yml", "<TITLE_ID>_patch.yml",
    "RPCS3_CONFIG_DIR"
};

var ownedRuntimeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "r3hcan.json", "r3config_hcanary.json", "r3cht.yml",
    "r3config_cheats_patch.yml"
};

string[] requiredOwnedRuntimeNames =
[
    "R3Bkp", "R3ConfigBackups", "r3hcan.json", "r3config_hcanary.json",
    "r3cht.yml", "r3config_cheats_patch.yml"
];
var requiredRuntimeNames = externalContracts
    .Concat(requiredOwnedRuntimeNames)
    .ToArray();

var typeDecls = new List<(string FullName, string Name, string Path, bool IsPublic, bool IsPartial)>();
var codeTexts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
var resourceKeys = new HashSet<string>(StringComparer.Ordinal);

foreach (var file in sourceFiles)
{
    var relative = Path.GetRelativePath(root, file);
    var text = File.ReadAllText(file);
    codeTexts[file] = text;
    var tree = CSharpSyntaxTree.ParseText(text);
    var syntaxRoot = tree.GetRoot();

    foreach (var node in syntaxRoot.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
    {
        var name = node.Identifier.ValueText;
        var isPublic = node.Modifiers.Any(token => token.IsKind(SyntaxKind.PublicKeyword));
        var isPartial = node.Modifiers.Any(token => token.IsKind(SyntaxKind.PartialKeyword));
        var ns = node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString()
                 ?? string.Empty;
        var parents = node.Ancestors().OfType<BaseTypeDeclarationSyntax>()
            .Reverse()
            .Select(parent => parent.Identifier.ValueText);
        var fullName = string.Join(
            ".",
            new[] { ns }.Concat(parents).Append(name).Where(part => part.Length > 0));

        typeDecls.Add((fullName, name, relative, isPublic, isPartial));

        if (isPublic && name.Length == 1)
        {
            errors.Add($"Public type '{name}' uses a one-character name at {relative}.");
        }

        if (isPublic && retiredNames.Contains(name))
        {
            errors.Add($"Public API still uses retired ambiguous name '{name}' at {relative}.");
        }
    }

    foreach (var node in syntaxRoot.DescendantNodes().OfType<DelegateDeclarationSyntax>())
    {
        var name = node.Identifier.ValueText;
        if (node.Modifiers.Any(token => token.IsKind(SyntaxKind.PublicKeyword)) && name.Length == 1)
        {
            errors.Add($"Public delegate '{name}' uses a one-character name at {relative}.");
        }
    }

    foreach (var ns in syntaxRoot.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>())
    {
        var parts = ns.Name.ToString().Split('.');
        if (parts.Contains("Case", StringComparer.Ordinal) ||
            parts.Contains("Comp", StringComparer.Ordinal))
        {
            errors.Add($"Retired architecture namespace '{ns.Name}' remains at {relative}.");
        }
    }

    foreach (Match match in Regex.Matches(
                 text,
                 "(?:Resources|FindResource)\\s*(?:\\[|\\()\\s*\"(?<key>[^\"]+)\""))
    {
        resourceKeys.Add(match.Groups["key"].Value);
    }

    foreach (var literal in syntaxRoot.DescendantNodes().OfType<LiteralExpressionSyntax>()
                 .Where(node => node.IsKind(SyntaxKind.StringLiteralExpression)))
    {
        var value = literal.Token.ValueText;
        if (Regex.IsMatch(value, "^(?:r3config|r3)[a-z0-9_-]*\\.(?:json|ya?ml)$", RegexOptions.IgnoreCase) &&
            !ownedRuntimeNames.Contains(value))
        {
            errors.Add($"Unapproved R3Config runtime filename '{value}' at {relative}.");
        }
    }
}

foreach (var group in typeDecls.GroupBy(item => item.FullName, StringComparer.Ordinal)
             .Where(group => group.Count() > 1 && !group.All(item => item.IsPartial)))
{
    errors.Add(
        $"Type collision for '{group.Key}': {string.Join(", ", group.Select(item => item.Path).Distinct())}.");
}

var publicTypeNames = typeDecls
    .Where(item => item.IsPublic)
    .Select(item => item.Name)
    .ToHashSet(StringComparer.Ordinal);
foreach (var required in requiredTypes.Where(required => !publicTypeNames.Contains(required)))
{
    errors.Add($"Required architectural or contract type '{required}' is missing.");
}

var xns = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
var xamlFiles = Directory.EnumerateFiles(
        Path.Combine(root, "App"),
        "*.xaml",
        SearchOption.AllDirectories)
    .Where(path => !IsBuild(path))
    .Order(StringComparer.OrdinalIgnoreCase)
    .ToArray();
var xamlDocs = new List<(string Path, XDocument Document)>();

foreach (var file in xamlFiles)
{
    var relative = Path.GetRelativePath(root, file);
    XDocument document;
    try
    {
        document = XDocument.Load(file, LoadOptions.SetLineInfo);
    }
    catch (Exception exception)
    {
        errors.Add($"Invalid XAML at {relative}: {exception.Message}");
        continue;
    }

    xamlDocs.Add((relative, document));
    foreach (var key in document.Descendants().Attributes(xns + "Key"))
    {
        resourceKeys.Add(key.Value);
    }

    foreach (var classAttr in document.Root?.DescendantsAndSelf().Attributes(xns + "Class") ?? [])
    {
        if (!typeDecls.Any(item => string.Equals(item.FullName, classAttr.Value, StringComparison.Ordinal)))
        {
            errors.Add($"x:Class '{classAttr.Value}' has no matching source type at {relative}.");
        }
    }
}

var resourcePattern = new Regex(
    "\\{(?:StaticResource|DynamicResource)\\s+(?<key>[^},\\s]+)",
    RegexOptions.CultureInvariant);
foreach (var (path, document) in xamlDocs)
{
    foreach (var attr in document.Descendants().Attributes())
    {
        foreach (Match match in resourcePattern.Matches(attr.Value))
        {
            var key = match.Groups["key"].Value;
            if (key.StartsWith("{x:Type", StringComparison.Ordinal))
            {
                continue;
            }

            if (!resourceKeys.Contains(key))
            {
                errors.Add($"Missing XAML resource key '{key}' referenced at {path}.");
            }
        }

        if ((attr.Value.Contains("{Binding", StringComparison.Ordinal) ||
             attr.Value.Contains("Resource", StringComparison.Ordinal)) &&
            retiredNames.Any(name => Regex.IsMatch(attr.Value, $@"\b{Regex.Escape(name)}\b")))
        {
            errors.Add($"XAML contract at {path} references a retired identifier: {attr.Value}");
        }
    }
}

var docs = new[]
    {
        Path.Combine(root, "README.md"),
        Path.Combine(root, "NAMES.md"),
        Path.Combine(root, "docs", "ProdAr.md")
    }
    .Concat(Directory.EnumerateFiles(Path.Combine(root, "docs", "Arch"), "*.md"))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .Where(File.Exists)
    .ToArray();
var docTexts = docs.ToDictionary(path => path, File.ReadAllText, StringComparer.OrdinalIgnoreCase);

foreach (var (path, text) in docTexts)
{
    var relative = Path.GetRelativePath(root, path);
    var isNameRegistry = relative.Equals("NAMES.md", StringComparison.OrdinalIgnoreCase);
    foreach (Match match in Regex.Matches(text, "`(?<value>[^`\\r\\n]+)`"))
    {
        CheckDocValue(match.Groups["value"].Value, relative, checkType: true);
    }

    foreach (Match block in Regex.Matches(
                 text,
                 "```[^\\r\\n]*\\r?\\n(?<body>.*?)```",
                 RegexOptions.Singleline | RegexOptions.CultureInvariant))
    {
        foreach (var line in block.Groups["body"].Value.Split(['\r', '\n']))
        {
            var pathMatch = Regex.Match(
                line,
                "^\\s*(?<value>(?:App|Core|Tst|Script|docs)[/\\\\][^\\s]+)",
                RegexOptions.CultureInvariant);
            if (pathMatch.Success)
            {
                CheckDocValue(
                    pathMatch.Groups["value"].Value.TrimEnd(',', ';', ':', '،', '؛'),
                    relative,
                    checkType: false);
            }
        }
    }

    if (!isNameRegistry)
    {
        var identifierText = Regex.Replace(text, @"\]\([^\)\r\n]+\)", "]");
        foreach (var retired in retiredNames.Where(retired =>
                     Regex.IsMatch(identifierText, $@"\b{Regex.Escape(retired)}\b")))
        {
            errors.Add($"Documentation still references retired identifier '{retired}' ({relative}).");
        }
    }

    if (!isNameRegistry && HasRetiredArchitectureName(text))
    {
        errors.Add($"Documentation still references a retired architecture path or namespace ({relative}).");
    }
}

foreach (var requiredPath in new[]
         {
             "Core/Svc/Patch", "Core/Svc/Mutation", "Core/Svc/Input"
         })
{
    if (!Directory.Exists(Path.Combine(root, requiredPath.Replace('/', Path.DirectorySeparatorChar))))
    {
        errors.Add($"Required architecture path '{requiredPath}' is missing.");
    }
}

var testRoot = Path.Combine(root, "Tst");
var testText = string.Join(
    Environment.NewLine,
    Directory.EnumerateFiles(
            testRoot,
            "Program*.cs",
            SearchOption.TopDirectoryOnly)
        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
        .Select(File.ReadAllText));
if (HasRetiredArchitectureName(testText))
{
    errors.Add("Test architecture assertion still references a retired path or namespace.");
}

string[] staleArchitectureNames =
[
    "ImpSvc", "AppComp.cs", "PatMut.cs", "new PatTxn", "new MutGuard"
];
foreach (var stale in staleArchitectureNames
             .Where(stale => Regex.IsMatch(
                 testText,
                 $@"\b{Regex.Escape(stale)}\b")))
{
    errors.Add($"Test architecture assertion still references retired name '{stale}'.");
}

var policyFiles = docs.Concat(new[]
{
    Path.Combine(root, "Script", "NameChk.cs"),
    Path.Combine(root, "Script", "NameChk.ps1"),
    Path.Combine(root, "Script", "SrcChk.ps1"),
    Path.Combine(root, "Script", "SrcExp.ps1")
});
var numericNameRule = new Regex(
    "(?:2\\s*(?:<=|through|to|-)\\s*(?:name\\s*length\\s*)?8|name\\.Length\\s+is\\s+>=\\s*2\\s+and\\s+<=\\s*8|الحد\\s+الأقصى.{0,24}8)",
    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
foreach (var file in policyFiles.Distinct(StringComparer.OrdinalIgnoreCase).Where(File.Exists))
{
    if (numericNameRule.IsMatch(File.ReadAllText(file)))
    {
        errors.Add($"Numeric identifier-length policy remains in {Path.GetRelativePath(root, file)}.");
    }
}

var allContractText = string.Join(
    "\n",
    codeTexts.Values.Concat(docTexts.Values));
foreach (var contract in requiredRuntimeNames.Where(contract =>
             !allContractText.Contains(contract, StringComparison.Ordinal)))
{
    errors.Add($"Required RPCS3 or migration contract name '{contract}' is missing.");
}

if (errors.Count > 0)
{
    foreach (var error in errors.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
    {
        Console.Error.WriteLine(error);
    }

    Console.Error.WriteLine($"Contract verification failed with {errors.Distinct().Count()} violation(s).");
    return 1;
}

Console.WriteLine("Architecture, naming, XAML, runtime, and documentation contracts passed.");
return 0;

bool IsBuild(string path)
{
    var relative = Path.GetRelativePath(root, path);
    return relative.Split(Path.DirectorySeparatorChar)
        .Any(part => part is "bin" or "obj" or "artifacts");
}

bool HasRetiredArchitectureName(string text)
{
    return Regex.IsMatch(text, @"App/(?:Case|Comp)(?:/|\b)") ||
           Regex.IsMatch(text, @"R3Config\.(?:Case|Comp)\b") ||
           Regex.IsMatch(text, @"Core[/\\]Svc[/\\](?:Pat|Mut|Inp)(?:[/\\]|\b)");
}

void CheckDocValue(string rawValue, string relative, bool checkType)
{
    var value = rawValue.Trim().Replace('\\', '/');
    if (value.StartsWith("./", StringComparison.Ordinal))
    {
        value = value[2..];
    }

    if (Regex.IsMatch(value, "^(?:App|Core|Tst|Script|docs)/", RegexOptions.CultureInvariant))
    {
        var wildcard = value.IndexOfAny(['*', '<', '>', '{', '}']);
        if (wildcard >= 0)
        {
            value = value[..wildcard].TrimEnd('/');
        }

        value = value.TrimEnd('/');
        if (value.Length > 0 && !value.Contains(' ') &&
            !File.Exists(Path.Combine(root, value.Replace('/', Path.DirectorySeparatorChar))) &&
            !Directory.Exists(Path.Combine(root, value.Replace('/', Path.DirectorySeparatorChar))))
        {
            errors.Add($"Documented path '{value}' does not exist ({relative}).");
        }
    }

    if (checkType && Regex.IsMatch(
            value,
            "^[A-Z][A-Za-z0-9]*(?:UseCase|Service|Guard|Descriptor|Decision|Verdict|Result|Manifest|Snapshot|Formatter)$") &&
        !publicTypeNames.Contains(value))
    {
        errors.Add($"Documented type '{value}' does not exist ({relative}).");
    }
}
