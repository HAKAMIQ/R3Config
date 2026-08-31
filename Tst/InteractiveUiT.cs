using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using R3Config.Composition;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;

namespace R3Config.Tests;

public static class InteractiveUiT
{
    public static void Run(string tempRoot)
    {
        Exception? failure = null;
        using var done = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            try { RunSta(Path.Combine(tempRoot, "interactive-ui")); }
            catch (Exception ex) { failure = ex; }
            finally { done.Set(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!done.Wait(TimeSpan.FromSeconds(45)))
        {
            throw new TimeoutException("Interactive WPF automation exceeded its 45-second bound.");
        }
        thread.Join(TimeSpan.FromSeconds(5));
        if (failure is not null) { throw new InvalidOperationException("Interactive UI automation failed.", failure); }
        Console.WriteLine("Interactive WPF UI automation PASS (English and Arabic setup surfaces).");
    }

    private static void RunSta(string root)
    {
        Directory.CreateDirectory(root);
        UiText.SetLang("en-US");
        var env = GameSetupT.CreateEnvironment(root);
        var canary = LoadGodOfWarCanary();
        var game = GameSetupT.GameFor(canary, env.GamePath);
        GameSetupT.WriteInstalledGame(env, game);
        File.WriteAllText(
            Path.Combine(env.Install.RootPath, "RPCS3.log"),
            $"SYS: Serial: {game.Serial}{Environment.NewLine}" +
            $"ppu_loader: PPU executable hash: PPU-{canary.NormHash}{Environment.NewLine}",
            new System.Text.UTF8Encoding(false));
        File.WriteAllText(
            Path.Combine(env.Install.PatPath, "patch.yml"),
            $"Version: 1.2\r\nPPU-{canary.NormHash}:\r\n  Official UI Fixture:\r\n    Games:\r\n      God of War 3 (God of War III):\r\n        {game.Serial}: [ {game.Version} ]\r\n    Patch Version: 1.0\r\n    Patch:\r\n      - [ be32, 0x00000010, 0x60000000 ]\r\n");

        var services = AppComposition.BuildRun(new Rpcs3MutationGuard(() => false));
        var confirmed = false;
        var vm = new MainVm(
            services,
            () => false,
            (_, _) => { confirmed = true; return true; },
            info: (_, _) => { });
        vm.ExePath = env.Install.ExePath;
        var analyze = typeof(MainVm).GetMethod("DoAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("MainVm analysis entry point was not found.");
        ((Task)analyze.Invoke(vm, null)!).GetAwaiter().GetResult();
        Assert(vm.GameList.SelGame is not null, "UI automation fixture did not detect a game.");

        vm.Plan.Select();
        var patchItem = vm.Preview.CanItems.FirstOrDefault(item => item.CanOn)
            ?? throw new InvalidOperationException("No compatible Canary UI patch candidate was loaded.");
        patchItem.IsSel = true;

        var app = new R3Config.App();
        app.InitializeComponent();
        var window = new R3Config.MainWin(vm);
        app.MainWindow = window;
        window.Show();
        window.UpdateLayout();
        var rootElement = AutomationElement.FromHandle(new System.Windows.Interop.WindowInteropHelper(window).Handle);
        var setupTab = Find(rootElement, "Setup.Tab");
        var selection = (SelectionItemPattern)setupTab.GetCurrentPattern(SelectionItemPattern.Pattern);
        selection.Select();
        if (setupTab.TryGetCurrentPattern(InvokePattern.Pattern, out var invoke))
        {
            ((InvokePattern)invoke).Invoke();
        }
        if (!setupTab.Current.IsEnabled)
        {
            throw new InvalidOperationException("Setup.Tab was not enabled through Windows UI Automation.");
        }
        rootElement = AutomationElement.FromHandle(new System.Windows.Interop.WindowInteropHelper(window).Handle);
        var prepare = FindEventually(rootElement, "Setup.PrepareButton", window, 5);
        Assert(prepare.Current.IsEnabled, "Prepare Game UI action was not enabled after selection/preview.");
        ((InvokePattern)prepare.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        Pump(window, () => confirmed && vm.SetupStatus.Contains("Game Ready", StringComparison.OrdinalIgnoreCase), 15);

        Assert(File.Exists(Path.Combine(env.Install.CustPath, $"config_{game.Serial}.yml")) &&
               File.Exists(env.Install.PatCfg) &&
               File.Exists(Path.Combine(env.Install.PatPath, "imported_patch.yml")) &&
               File.Exists(Path.Combine(env.Install.RootPath, "config", "R3Bkp")) &&
               File.Exists(Path.Combine(env.Install.PatPath, "patch.yml")),
            "Interactive Prepare did not produce the expected isolated outputs.");
        var officialHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(env.Install.PatPath, "patch.yml"))));
        Assert(officialHash.Length == 64, "Official patch fixture was not readable after UI apply.");
        window.Close();
        UiText.SetLang("ar-SA");
        var arabicWindow = new R3Config.MainWin(vm);
        arabicWindow.Show();
        arabicWindow.UpdateLayout();
        Assert(Find(AutomationElement.FromHandle(new System.Windows.Interop.WindowInteropHelper(arabicWindow).Handle), "Setup.Root") is not null,
            "Arabic UI setup surface could not be found through UI Automation.");
        arabicWindow.Close();
        app.Shutdown();
    }

    private static AutomationElement Find(AutomationElement root, string id)
    {
        var condition = new PropertyCondition(AutomationElement.AutomationIdProperty, id);
        return root.FindFirst(TreeScope.Descendants, condition)
            ?? throw new InvalidOperationException($"Automation element '{id}' was not found.");
    }

    private static AutomationElement FindEventually(
        AutomationElement root,
        string id,
        Window window,
        int seconds)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < until)
        {
            try { return Find(root, id); }
            catch (InvalidOperationException) { }
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
            Thread.Sleep(50);
        }
        var all = root.FindAll(TreeScope.Descendants, System.Windows.Automation.Condition.TrueCondition);
        var observed = string.Join(";", all.Cast<AutomationElement>()
            .Select(element => $"{element.Current.AutomationId}:{element.Current.ControlType.ProgrammaticName}:{element.Current.Name}"));
        throw new InvalidOperationException($"Automation element '{id}' was not found. Observed={observed}");
    }

    private static void Pump(Window window, Func<bool> done, int seconds)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (!done() && DateTime.UtcNow < until)
        {
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
            Thread.Sleep(50);
        }
        Assert(done(), "Interactive UI workflow did not reach Game Ready before timeout.");
    }

    private static Rpcs3PatchEntry LoadGodOfWarCanary()
    {
        var path = BundledCanaryPatchService.GetPath(AppContext.BaseDirectory);
        var text = R3Bound.ReadUtf8(path, R3Limits.PatCatBy, "can.yml");
        var normalized = new CanaryPatchImportService().Norm(text);
        Assert(normalized.IsOk, "Canary fixture normalization failed.");
        var read = new ReadSvc().ReadText(normalized.NormText, PatSrc.Can, path);
        return read.Catalog.Entries.First(entry =>
            entry.Title == "Unlock FPS" && entry.Serials.Contains("BCUS98111") &&
            entry.GameVer.Contains("01.03"));
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
