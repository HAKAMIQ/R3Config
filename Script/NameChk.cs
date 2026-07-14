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
var bad = new List<string>();
var ext = new HashSet<string>(StringComparer.Ordinal)
{
    "rpcs3.exe",
    "PARAM.SFO",
    "patch.yml",
    "imported_patch.yml",
    "patch_config.yml",
    "config_<SERIAL>.yml",
    "<TITLE_ID>_patch.yml",
    "RPCS3_CONFIG_DIR"
};

void ChkName(string kind, string name, string path)
{
    if (name.Length is >= 2 and <= 8)
    {
        return;
    }

    bad.Add($"{kind} '{name}' at {path}");
}

bool IsExt(string name)
{
    return ext.Contains(name) ||
           name.StartsWith("config_", StringComparison.Ordinal) &&
           name.EndsWith(".yml", StringComparison.Ordinal) ||
           name.EndsWith("_patch.yml", StringComparison.Ordinal);
}

bool IsBuild(string path)
{
    var rel = Path.GetRelativePath(root, path);
    return rel.Split(Path.DirectorySeparatorChar)
        .Any(part => part is "bin" or "obj");
}

string Stem(string name)
{
    while (Path.HasExtension(name))
    {
        name = Path.GetFileNameWithoutExtension(name);
    }

    return name;
}

foreach (var area in new[] { "App", "Core", "Tst", "docs", "Script" })
{
    var areaPath = Path.Combine(root, area);
    if (!Directory.Exists(areaPath))
    {
        continue;
    }

    foreach (var dir in Directory.EnumerateDirectories(
                 areaPath,
                 "*",
                 SearchOption.AllDirectories))
    {
        if (!IsBuild(dir))
        {
            ChkName("Folder", Path.GetFileName(dir), Path.GetRelativePath(root, dir));
        }
    }

    foreach (var file in Directory.EnumerateFiles(
                 areaPath,
                 "*",
                 SearchOption.AllDirectories))
    {
        if (IsBuild(file))
        {
            continue;
        }

        var name = Path.GetFileName(file);
        if (!name.StartsWith('.') && !IsExt(name))
        {
            ChkName("File", Stem(name), Path.GetRelativePath(root, file));
        }
    }
}

foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly))
{
    var name = Path.GetFileName(file);
    if (!name.StartsWith('.') && !IsExt(name))
    {
        ChkName("File", Stem(name), name);
    }
}

foreach (var area in new[] { "App", "Core", "Tst", "Script" })
{
    var areaPath = Path.Combine(root, area);
    foreach (var file in Directory.EnumerateFiles(
                 areaPath,
                 "*.cs",
                 SearchOption.AllDirectories))
    {
        if (IsBuild(file))
        {
            continue;
        }

        var rel = Path.GetRelativePath(root, file);
        var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file));
        foreach (var node in tree.GetRoot().DescendantNodesAndSelf())
        {
            string? name = null;
            string? kind = null;
            switch (node)
            {
                case BaseTypeDeclarationSyntax item:
                    name = item.Identifier.ValueText;
                    kind = "Type";
                    break;
                case DelegateDeclarationSyntax item:
                    name = item.Identifier.ValueText;
                    kind = "Type";
                    break;
                case MethodDeclarationSyntax item:
                    if (item.ExplicitInterfaceSpecifier is not null ||
                        item.Modifiers.Any(token => token.IsKind(SyntaxKind.OverrideKeyword)))
                    {
                        continue;
                    }
                    name = item.Identifier.ValueText;
                    kind = "Method";
                    break;
                case LocalFunctionStatementSyntax item:
                    name = item.Identifier.ValueText;
                    kind = "Method";
                    break;
                case ConstructorDeclarationSyntax item:
                    name = item.Identifier.ValueText;
                    kind = "Method";
                    break;
                case PropertyDeclarationSyntax item:
                    name = item.Identifier.ValueText;
                    kind = "Property";
                    break;
                case EventDeclarationSyntax item:
                    name = item.Identifier.ValueText;
                    kind = "Property";
                    break;
                case EnumMemberDeclarationSyntax item:
                    name = item.Identifier.ValueText;
                    kind = "Property";
                    break;
                case VariableDeclaratorSyntax item
                    when item.Parent?.Parent is FieldDeclarationSyntax:
                    name = item.Identifier.ValueText;
                    kind = "Field";
                    break;
                case ParameterSyntax item
                    when item.Parent?.Parent is RecordDeclarationSyntax:
                    name = item.Identifier.ValueText;
                    kind = "Property";
                    break;
                case BaseNamespaceDeclarationSyntax item:
                    foreach (var part in item.Name.ToString().Split('.'))
                    {
                        if (part != "R3Config")
                        {
                            ChkName("Namespace", part, rel);
                        }
                    }
                    break;
            }

            if (name is not null && !ext.Contains(name))
            {
                ChkName(kind!, name, rel);
            }
        }
    }
}

var xns = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
foreach (var file in Directory.EnumerateFiles(
             Path.Combine(root, "App"),
             "*.xaml",
             SearchOption.AllDirectories))
{
    var rel = Path.GetRelativePath(root, file);
    var doc = XDocument.Load(file, LoadOptions.SetLineInfo);
    foreach (var attr in doc.Descendants().Attributes(xns + "Name"))
    {
        if (!attr.Value.StartsWith("PART_", StringComparison.Ordinal))
        {
            ChkName("XAML", attr.Value, rel);
        }
    }
}

if (bad.Count > 0)
{
    foreach (var item in bad.Order(StringComparer.Ordinal))
    {
        Console.Error.WriteLine(item);
    }

    Console.Error.WriteLine($"Name verification failed with {bad.Count} violation(s).");
    return 1;
}

Console.WriteLine("Name verification passed.");
return 0;
