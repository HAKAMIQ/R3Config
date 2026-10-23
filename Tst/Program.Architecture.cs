using System.IO;
using System.IO.Compression;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;

using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading;
using System.Xml;
using System.Windows;
using R3Config;
using R3Config.Svc;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Vm;
using R3Config.View;
using R3Config.Application;
using R3Config.ArtImport;
using R3Config.Composition;
using R3Config.Tests;

internal static partial class Program
{
    private static readonly string[] ExpectedTestProjectReferences =
    {
        "../App/R3App.csproj",
        "../Core/R3Core.csproj",
        "../Script/ArtImport/R3ArtImport.csproj"
    };

    private static readonly string[] ExpectedAppProjectReferences =
    {
        "../Core/R3Core.csproj",
        "../Script/LangPack/R3LangPack.csproj"
    };

    static void Arch_Svc()
    {
        var repoRoot = RepoRoot();
        var coreRoot = Path.Combine(repoRoot, "Core");
        var applicationRoot = Path.Combine(repoRoot, "App", "Application");
        var viewModelRoot = Path.Combine(repoRoot, "App", "Vm");

        Assert(Directory.Exists(applicationRoot),
            "Expected an explicit App/Application use-case layer.");

        var coreSource = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));
        Assert(!coreSource.Contains("System.Windows", StringComparison.Ordinal) &&
               !coreSource.Contains("R3Config.Lang", StringComparison.Ordinal) &&
               !coreSource.Contains("R3Config.Vm", StringComparison.Ordinal) &&
               !coreSource.Contains("R3Config.View", StringComparison.Ordinal),
            "Core must remain independent of WPF, localization, and presentation namespaces.");

        var logLocatorPath = Path.Combine(
            coreRoot,
            "Svc",
            "Scan",
            "Rpcs3LogLocator.cs");
        var hCanDiscoveryPath = Path.Combine(
            coreRoot,
            "Svc",
            "Patch",
            "HCan.cs");
        var patchHashProbePath = Path.Combine(
            coreRoot,
            "Svc",
            "Patch",
            "PatchHashProbeService.cs");
        var hashEvidencePath = Path.Combine(
            applicationRoot,
            "HashEvidenceUseCase.cs");
        var hCanViewModelPath = Path.Combine(
            viewModelRoot,
            "HCan.cs");
        var logLocatorSource = File.ReadAllText(logLocatorPath);
        var hCanDiscoverySource = File.ReadAllText(hCanDiscoveryPath);
        var patchHashProbeSource = File.ReadAllText(patchHashProbePath);
        var hashEvidenceSource = File.ReadAllText(hashEvidencePath);
        var hCanViewModelSource = File.ReadAllText(hCanViewModelPath);
        Assert(logLocatorSource.Contains(
                   "install.RootPath",
                   StringComparison.Ordinal) &&
               logLocatorSource.Contains(
                   "\"log\",",
                   StringComparison.Ordinal) &&
               logLocatorSource.Contains(
                   "\"RPCS3.log\"",
                   StringComparison.Ordinal) &&
               hCanDiscoverySource.Contains(
                   "Rpcs3LogLocator.PlainLogPaths",
                   StringComparison.Ordinal) &&
               patchHashProbeSource.Contains(
                   "Rpcs3LogLocator",
                   StringComparison.Ordinal) &&
               hashEvidenceSource.Contains(
                   "discovery.GetLogPaths(install)",
                   StringComparison.Ordinal) &&
               hCanViewModelSource.Contains(
                   "Rpcs3LogLocator.PreferredPlainLogPath",
                   StringComparison.Ordinal),
            "RPCS3 plain-log discovery must remain centralized and cover both root/log/RPCS3.log and legacy root/RPCS3.log from the emulator root.");

        var applicationSource = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(applicationRoot, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));
        Assert(!applicationSource.Contains("System.Windows", StringComparison.Ordinal) &&
               !applicationSource.Contains("R3Config.Vm", StringComparison.Ordinal) &&
               !applicationSource.Contains("R3Config.View", StringComparison.Ordinal) &&
               !applicationSource.Contains("UiDlg", StringComparison.Ordinal) &&
               !applicationSource.Contains("UiText", StringComparison.Ordinal),
            "Application use cases must not depend directly on WPF, dialogs, or localized presentation text.");

        var appFlowPath = Path.Combine(repoRoot, "App", "Flow.cs");
        var appFlowSource = File.ReadAllText(appFlowPath);
        var ca1822DisableCount = appFlowSource
            .Split(
                "#pragma warning disable CA1822",
                StringSplitOptions.None)
            .Length - 1;
        var ca1822RestoreCount = appFlowSource
            .Split(
                "#pragma warning restore CA1822",
                StringSplitOptions.None)
            .Length - 1;
        Assert(!appFlowSource.Contains("System.IO", StringComparison.Ordinal) &&
               !appFlowSource.Contains("File.", StringComparison.Ordinal) &&
               !appFlowSource.Contains("Directory.", StringComparison.Ordinal) &&
               !appFlowSource.Contains("Path.", StringComparison.Ordinal) &&
               !appFlowSource.Contains("Process.", StringComparison.Ordinal) &&
               !appFlowSource.Contains("FileStream", StringComparison.Ordinal) &&
               !appFlowSource.Contains("JsonSerializer", StringComparison.Ordinal) &&
               !appFlowSource.Contains("JsonDocument", StringComparison.Ordinal) &&
               ca1822DisableCount == 2 &&
               ca1822RestoreCount == 2,
            "AppFlow must remain a delegation-only compatibility facade without filesystem, process, or JSON logic, and CA1822 suppression must stay limited to the two legacy static-delegate compatibility methods.");

        var viewModelSource = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(viewModelRoot, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));
        Assert(!viewModelSource.Contains("File.WriteAllText", StringComparison.Ordinal) &&
               !viewModelSource.Contains("File.Move", StringComparison.Ordinal) &&
               !viewModelSource.Contains("File.Replace", StringComparison.Ordinal) &&
               !viewModelSource.Contains("File.Delete", StringComparison.Ordinal),
            "ViewModels must route file mutations through use cases instead of writing directly.");

        var mainViewModelSource = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(
                    viewModelRoot,
                    "Main*.cs",
                    SearchOption.TopDirectoryOnly)
                .Select(File.ReadAllText));
        Assert(!mainViewModelSource.Contains(
                   "CanaryPatchImportService",
                   StringComparison.Ordinal) &&
               !mainViewModelSource.Contains(
                   "_impSvc",
                   StringComparison.Ordinal),
            "MainVm must query Canary state through AppFlow instead of owning a domain writer service.");

        var mainParts = Directory.EnumerateFiles(
                viewModelRoot,
                "Main*.cs",
                SearchOption.TopDirectoryOnly)
            .ToArray();
        Assert(mainParts.Length >= 6 &&
               mainParts.All(path => File.ReadLines(path).Count() < 500),
            "Expected MainVm to remain split into bounded responsibility-based partial files.");

        var testRoot = Path.Combine(repoRoot, "Tst");
        var testProgramFiles = Directory.EnumerateFiles(
                testRoot,
                "Program*.cs",
                SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var testEntryPoint = Path.Combine(
            testRoot,
            "Program.cs");
        var testRunnerPath = Path.Combine(testRoot, "TestRunner.cs");
        var testContextPath = Path.Combine(testRoot, "TestContext.cs");
        var testAssertPath = Path.Combine(testRoot, "TestAssert.cs");
        var testFixturesPath = Path.Combine(testRoot, "TestFixtures.cs");
        var testEntrySource = File.ReadAllText(testEntryPoint);
        Assert(testProgramFiles.Length >= 15 &&
               File.ReadLines(testEntryPoint).Count() < 400 &&
               testProgramFiles
                   .Where(path => !path.Equals(
                       testEntryPoint,
                       StringComparison.OrdinalIgnoreCase))
                   .All(path => File.ReadLines(path).Count() < 1200) &&
               File.Exists(testRunnerPath) &&
               File.ReadLines(testRunnerPath).Count() < 200 &&
               File.Exists(testContextPath) &&
               File.ReadLines(testContextPath).Count() < 150 &&
               File.Exists(testAssertPath) &&
               File.ReadLines(testAssertPath).Count() < 150 &&
               File.Exists(testFixturesPath) &&
               File.ReadLines(testFixturesPath).Count() < 700 &&
               !testEntrySource.Contains(
                   "void RunCase(",
                   StringComparison.Ordinal) &&
               !testEntrySource.Contains(
                   "RunValue<T>(",
                   StringComparison.Ordinal) &&
               !testEntrySource.Contains(
                   "void ReportFailure(",
                   StringComparison.Ordinal) &&
               !testEntrySource.Contains(
                   "void Assert(",
                   StringComparison.Ordinal) &&
               !testEntrySource.Contains(
                   "void MustFail<",
                   StringComparison.Ordinal),
            "Expected R3Test Program.cs to remain a thin CLI router, with runner/context/assert/fixture infrastructure extracted and responsibility-based partial suites bounded.");

        Console.WriteLine("Architecture dependency boundaries PASS.");
    }

    static void Security_Hardening(string tempRoot)
    {
        var repoRoot = RepoRoot();
        var hwProbeSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "Core",
            "Svc",
            "Hw",
            "HwProbe.cs"));
        Assert(!hwProbeSource.Contains(
                   "powershell",
                   StringComparison.OrdinalIgnoreCase) &&
               !hwProbeSource.Contains(
                   "Get-CimInstance",
                   StringComparison.OrdinalIgnoreCase) &&
               hwProbeSource.Contains(
                   "EnumDisplayDevicesW",
                   StringComparison.Ordinal),
            "Expected the GPU probe to remain an in-process read-only Win32 probe without PowerShell.");

        var uiTextSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "App",
            "Lang",
            "UiText.cs"));
        var languageReaderSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "App",
            "Lang",
            "R3LangReader.cs"));
        Assert(!uiTextSource.Contains(
                   "Environment.CurrentDirectory",
                   StringComparison.Ordinal) &&
               uiTextSource.Contains(
                   "AppContext.BaseDirectory",
                   StringComparison.Ordinal) &&
               uiTextSource.Contains(
                   "R3LangManifest.TryResolve",
                   StringComparison.Ordinal) &&
               languageReaderSource.Contains(
                   "MaxPackBytes",
                   StringComparison.Ordinal) &&
               languageReaderSource.Contains(
                   "MaxPayloadBytes",
                   StringComparison.Ordinal) &&
               languageReaderSource.Contains(
                   "SHA256.HashData",
                   StringComparison.Ordinal) &&
               languageReaderSource.Contains(
                   "duplicate key",
                   StringComparison.OrdinalIgnoreCase),
            "Expected UI languages to load only compiled, integrity-checked, bounded packages from the application directory.");

        var appSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "App",
            "App.xaml.cs"));
        var instanceGateSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "App",
            "Svc",
            "AppInstanceGate.cs"));
        Assert(appSource.Contains(
                   "AppInstanceGate.TryAcquire",
                   StringComparison.Ordinal) &&
               instanceGateSource.Contains(
                   @"Local\HAKAMIQ.R3Config.SingleInstance",
                   StringComparison.Ordinal),
            "Expected a named cross-process single-instance gate to protect RPCS3 mutation workflows.");

        var transactionSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "Core",
            "Svc",
            "Patch",
            "PatchFileTransactionService.cs"));
        var stagedFileGuardSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "Core",
            "Svc",
            "Mutation",
            "StagedFileGuard.cs"));
        Assert(transactionSource.Contains(
                   "FileMode.CreateNew",
                   StringComparison.Ordinal) &&
               transactionSource.Contains(
                   "ValidateStages",
                   StringComparison.Ordinal) &&
               transactionSource.Contains(
                   "StagedFileGuard.Validate",
                   StringComparison.Ordinal) &&
               stagedFileGuardSource.Contains(
                   "FileAttributes.ReparsePoint",
                   StringComparison.Ordinal) &&
               stagedFileGuardSource.Contains(
                   "CanPath.GetPath",
                   StringComparison.Ordinal),
            "Expected patch staging files to use exclusive creation and repeated canonical reparse-point validation.");

        var mutationGuardSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "Core",
            "Svc",
            "Mutation",
            "Rpcs3MutationGuard.cs"));
        Assert(mutationGuardSource.Contains(
                   @"Global\HAKAMIQ.R3Config.Writer.",
                   StringComparison.Ordinal) &&
               mutationGuardSource.Contains(
                   "SHA256.HashData",
                   StringComparison.Ordinal) &&
               mutationGuardSource.Contains(
                   "WaitOne",
                   StringComparison.Ordinal) &&
               mutationGuardSource.Contains(
                   "AbandonedMutexException",
                   StringComparison.Ordinal) &&
               mutationGuardSource.Contains(
                   "MutBlock.Locked",
                   StringComparison.Ordinal),
            "Expected a path-derived cross-process writer mutex for each canonical RPCS3 data root.");

        var buildProps = File.ReadAllText(Path.Combine(
            repoRoot,
            "Directory.Build.props"));
        Assert(buildProps.Contains(
                   "EnableNETAnalyzers",
                   StringComparison.Ordinal) &&
               buildProps.Contains(
                   "TreatWarningsAsErrors",
                   StringComparison.Ordinal) &&
               buildProps.Contains(
                   "NuGetAuditMode",
                   StringComparison.Ordinal),
            "Expected repository-wide compiler and NuGet audit hardening.");

        var globalJson = File.ReadAllText(Path.Combine(
            repoRoot,
            "global.json"));
        var toolchainSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "Script",
            "R3Toolchain.ps1"));
        var nameCheckScript = File.ReadAllText(Path.Combine(
            repoRoot,
            "Script",
            "NameChk.ps1"));
        var releaseScript = File.ReadAllText(Path.Combine(
            repoRoot,
            "Script",
            "RelTest.ps1"));
        var sourceExportScript = File.ReadAllText(Path.Combine(
            repoRoot,
            "Script",
            "SrcExp.ps1"));
        Assert(globalJson.Contains(
                   "\"version\":",
                   StringComparison.Ordinal) &&
               globalJson.Contains(
                   "\"rollForward\": \"disable\"",
                   StringComparison.Ordinal) &&
               toolchainSource.Contains(
                   "Assert-R3DotNetSdk",
                   StringComparison.Ordinal) &&
               toolchainSource.Contains(
                   "Assert-R3WorkloadFreeProjectGraph",
                   StringComparison.Ordinal) &&
               toolchainSource.Contains(
                   "MSBuildEnableWorkloadResolver",
                   StringComparison.Ordinal) &&
               toolchainSource.Contains(
                   "Remove-Item Env:MSBuildEnableWorkloadResolver",
                   StringComparison.Ordinal) &&
               toolchainSource.Contains(
                   "DOTNET_WORKLOAD_RESOLVER=DISABLED_BY_REPOSITORY_CONTRACT",
                   StringComparison.Ordinal) &&
               toolchainSource.Contains(
                   "powershell.exe",
                   StringComparison.OrdinalIgnoreCase) &&
               toolchainSource.Contains(
                   "Get-R3MsBuildDirectoryPropertyValue",
                   StringComparison.Ordinal) &&
               nameCheckScript.Contains(
                   "Get-R3MsBuildDirectoryPropertyValue -Path",
                   StringComparison.Ordinal) &&
               nameCheckScript.Contains(
                   "'exec'",
                   StringComparison.Ordinal) &&
               nameCheckScript.Contains(
                   "BaseOutputPath",
                   StringComparison.Ordinal) &&
               nameCheckScript.Contains(
                   "BaseIntermediateOutputPath",
                   StringComparison.Ordinal) &&
               !nameCheckScript.Contains(
                   "--artifacts-path",
                   StringComparison.Ordinal) &&
               releaseScript.Contains(
                   "R3Test.exe",
                   StringComparison.Ordinal) &&
               releaseScript.Contains(
                   ".\\Tst\\R3Test.csproj",
                   StringComparison.Ordinal) &&
               !releaseScript.Contains(
                   "R3Cfg.slnx",
                   StringComparison.Ordinal) &&
               !releaseScript.Contains(
                   "'run', '--project', '.\\Tst\\R3Test.csproj'",
                   StringComparison.Ordinal) &&
               sourceExportScript.Contains(
                   "App/Data/ncl.zip",
                   StringComparison.Ordinal),
            "Expected one pinned repository toolchain with a scoped workload-resolver boundary, canonical MSBuild directory-property normalization, a workload-free project graph, a CLI root compatible with the pinned SDK, direct R3Test execution, deterministic PowerShell gates, and preservation of the pinned NCL corpus in source export.");

        var sourceCheckScript = File.ReadAllText(Path.Combine(
            repoRoot,
            "Script",
            "SrcChk.ps1"));
        Assert(sourceCheckScript.Contains(
                   "ls-files --cached --others --exclude-standard",
                   StringComparison.Ordinal),
            "Expected source verification at repository root to include tracked and untracked nonignored working-tree files.");

        var solution = System.Xml.Linq.XDocument.Load(Path.Combine(
            repoRoot,
            "R3Cfg.slnx"));
        var solutionProjects = solution.Root?
            .Elements("Project")
            .Select(element => ((string?)element.Attribute("Path") ?? string.Empty)
                .Replace('\\', '/'))
            .Where(path => path.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ??
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var expectedSolutionProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Tst/R3Test.csproj",
            "App/R3App.csproj",
            "Core/R3Core.csproj",
            "Script/ArtImport/R3ArtImport.csproj",
            "Script/LangPack/R3LangPack.csproj"
        };
        var testProject = System.Xml.Linq.XDocument.Load(Path.Combine(
            repoRoot,
            "Tst",
            "R3Test.csproj"));
        var testProjectReferences = testProject
            .Descendants("ProjectReference")
            .Select(element => ((string?)element.Attribute("Include") ?? string.Empty)
                .Replace('\\', '/'))
            .Where(path => path.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var appProject = System.Xml.Linq.XDocument.Load(Path.Combine(
            repoRoot,
            "App",
            "R3App.csproj"));
        var appProjectReferences = appProject
            .Descendants("ProjectReference")
            .Select(element => ((string?)element.Attribute("Include") ?? string.Empty)
                .Replace('\\', '/'))
            .Where(path => path.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert(solutionProjects.SetEquals(expectedSolutionProjects) &&
               testProjectReferences.SetEquals(ExpectedTestProjectReferences) &&
               appProjectReferences.SetEquals(ExpectedAppProjectReferences),
            "Expected Tst/R3Test.csproj and its App dependency to cover every project declared by R3Cfg.slnx.");

        var repositoryProjects = Directory
            .EnumerateFiles(repoRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Select(path => (Path: path, Text: File.ReadAllText(path)))
            .ToArray();
        var workloadProject = repositoryProjects.FirstOrDefault(project =>
            Regex.IsMatch(
                project.Text,
                @"Microsoft\.NET\.Sdk\.(?:Maui|BlazorWebAssembly)|<UseMaui>\s*true\s*</UseMaui>|net\d+(?:\.\d+)?-(?:android|ios|maccatalyst|tvos|browser|wasm)(?:[-.]|<)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
        Assert(workloadProject.Path is null,
            $"Repository verification disables the MSBuild workload resolver, so workload-dependent projects are not allowed: {workloadProject.Path}");

        var portPublish = File.ReadAllText(Path.Combine(
            repoRoot,
            "Script",
            "PortPub.ps1"));
        var portCheck = File.ReadAllText(Path.Combine(
            repoRoot,
            "Script",
            "PortChk.ps1"));
        Assert(portPublish.Contains(
                   "$signingEnabled",
                   StringComparison.Ordinal) &&
               portPublish.Contains(
                   "RelTest.ps1",
                   StringComparison.Ordinal) &&
               portPublish.Contains(
                   "Signing skipped: unsigned package (AUTHENTICODE NOT PROVISIONED).",
                   StringComparison.Ordinal) &&
               portPublish.Contains(
                   "SHA256SUMS.txt",
                   StringComparison.Ordinal) &&
               portCheck.Contains(
                   "if (-not [string]::IsNullOrWhiteSpace($ExpectedSignerThumbprint))",
                   StringComparison.Ordinal) &&
               portCheck.Contains(
                   "$forbiddenExtensions",
                   StringComparison.Ordinal),
            "Expected portable Preview publication to allow unsigned binaries while preserving SHA-256 and source-leak verification.");

        var uncBlocked = false;
        try
        {
            _ = RuntimePathPolicy.RequireLocal(
                @"\\server\share\rpcs3.exe",
                "RPCS3 executable");
        }
        catch (InvalidDataException)
        {
            uncBlocked = true;
        }

        Assert(uncBlocked,
            "Expected UNC/SMB runtime paths to be rejected before filesystem access.");

        var deviceBlocked = false;
        try
        {
            _ = RuntimePathPolicy.RequireLocal(
                @"\\?\C:\R3Config\rpcs3.exe",
                "RPCS3 executable");
        }
        catch (InvalidDataException)
        {
            deviceBlocked = true;
        }

        Assert(deviceBlocked,
            "Expected Windows device paths to be rejected before filesystem access.");

        var relativeBlocked = false;
        try
        {
            _ = RuntimePathPolicy.RequireLocal(
                Path.Combine("RPCS3", "rpcs3.exe"),
                "RPCS3 executable");
        }
        catch (InvalidDataException)
        {
            relativeBlocked = true;
        }

        Assert(relativeBlocked,
            "Expected current-directory-relative runtime paths to be rejected.");

        var xmlPath = Path.Combine(
            tempRoot,
            "security-hardening-dtd.xml");
        File.WriteAllText(
            xmlPath,
            "<!DOCTYPE wiki [<!ENTITY xxe SYSTEM 'file:///C:/Windows/win.ini'>]>" +
            "<mediawiki><page><title>&xxe;</title><revision><text>{{gameid|BLUS30000}}</text></revision></page></mediawiki>");

        var dtdBlocked = false;
        try
        {
            _ = WikiSvc.Load(xmlPath);
        }
        catch (XmlException)
        {
            dtdBlocked = true;
        }

        Assert(dtdBlocked,
            "Expected RPCS3 wiki XML DTD and entity expansion to be prohibited.");

        Console.WriteLine("Security hardening gates PASS.");
    }

    static void Can_Comp(string tempRoot)
    {
        var repoRoot = RepoRoot();
        var compositionPath = Path.Combine(
            repoRoot,
            "App",
            "Composition",
            "AppComposition.cs");
        var compositionSource = File.ReadAllText(compositionPath);
        Assert(CountOf(
                   compositionSource,
                   "new Svc.CanaryPatchImportService(") == 1,
            "Expected composition to construct exactly one CanaryPatchImportService instance.");
        Assert(!compositionSource.Contains(
                   "CanaryPatchImportService CanaryImport",
                   StringComparison.Ordinal),
            "Expected AppRuntimeServices not to expose a domain writer service to ViewModels.");

        var readOnlyRoot = Path.Combine(
            tempRoot,
            "canary-composition-read-only");
        Directory.CreateDirectory(readOnlyRoot);
        var patchesPath = Path.Combine(readOnlyRoot, "patches");
        var configPath = Path.Combine(readOnlyRoot, "config");
        var install = new Rpcs3InstallDescriptor(
            readOnlyRoot,
            Path.Combine(readOnlyRoot, "rpcs3.exe"),
            configPath,
            Path.Combine(configPath, "custom_configs"),
            patchesPath,
            Path.Combine(configPath, "patch_config.yml"),
            readOnlyRoot);
        var runtime = AppComposition.BuildRun();

        Assert(!runtime.Flow.HasImp(install) &&
               !Directory.Exists(patchesPath),
            "Expected the Canary state query to remain read-only when patches does not exist.");

        Directory.CreateDirectory(patchesPath);
        File.WriteAllText(
            Path.Combine(
                patchesPath,
                R3Config.Svc.CanaryPatchImportService.ImpName),
            "Version: 1.2\r\n" +
            "PPU-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa:\r\n" +
            "  # R3Config managed Canary patches - begin\r\n" +
            "  # R3Config managed Canary patches - end\r\n");
        Assert(runtime.Flow.HasImp(install),
            "Expected AppFlow to delegate the managed Canary state query to its use case.");

        var main = new MainVm(
            runtime,
            () => false,
            (_, _) => true);
        SetField(main, "_install", install);
        var refreshCanaryState = typeof(MainVm).GetMethod(
            "LoadCan",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "Expected MainVm Canary state refresh method was not found.");
        refreshCanaryState.Invoke(main, null);
        Assert(main.PrepCan,
            "Expected MainVm to refresh managed Canary state through AppFlow.");

        Console.WriteLine(
            "Canary composition and read-only state query tests PASS.");
    }

    static Dictionary<string, string> LangRead(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert(document.RootElement.ValueKind == JsonValueKind.Object, Path.GetFileName(path) + " must be a JSON object.");

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            Assert(values.TryAdd(property.Name, property.Value.GetString() ?? string.Empty), Path.GetFileName(path) + " has a duplicate key: " + property.Name);
        }

        return values;
    }

    static string RepoRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "R3Cfg.slnx")) &&
                Directory.Exists(Path.Combine(current.FullName, "App", "Lang")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate R3Config repo root for language smoke tests.");
    }

    static string ReadSources(params string[] paths)
    {
        return string.Join(
            Environment.NewLine,
            paths.Select(File.ReadAllText));
    }

}