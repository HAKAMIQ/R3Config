using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Automation;
using R3Config.Application;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Tests;

/// <summary>Click-driven Windows UI Automation against a separately launched R3Config process.</summary>
public static class OutOfProcessUiT
{
    public static void Run(string root, string language)
    {
        var env = GameSetupT.CreateEnvironment(Path.Combine(root, "rpc").Replace('/', '\\'));
        var portable = Path.Combine(env.Install.RootPath, "portable");
        var portableCfg = Path.Combine(portable, "config");
        var portableCust = Path.Combine(portableCfg, "custom_configs");
        var portablePatches = Path.Combine(portable, "patches");
        Directory.CreateDirectory(portableCust);
        Directory.CreateDirectory(portablePatches);
        env = env with { Install = new Rpcs3InstallDescriptor(portable, env.Install.ExePath, portableCfg, portableCust, portablePatches, Path.Combine(portableCfg, "patch_config.yml"), portable) };
        var canary = LoadGodOfWarCanary();
        var game = GameSetupT.GameFor(canary, env.GamePath) with { Serial = "BCUS98232", Version = "01.12", Title = "God of War: Ascension" };
        GameSetupT.WriteInstalledGame(env, game);
        var hddGame = Path.Combine(env.Install.RootPath, "dev_hdd0", "game", game.Serial);
        Directory.CreateDirectory(Path.Combine(hddGame, "PS3_GAME"));
        File.Copy(Path.Combine(env.GamePath, "PS3_GAME", "PARAM.SFO"), Path.Combine(hddGame, "PS3_GAME", "PARAM.SFO"), true);
        File.WriteAllText(Path.Combine(env.Install.RootPath, "RPCS3.log"),
            $"SYS: Serial: {game.Serial}{Environment.NewLine}ppu_loader: PPU executable hash: PPU-{canary.NormHash}{Environment.NewLine}",
            new UTF8Encoding(false));
        var official = $"Version: 1.2\r\nPPU-{canary.NormHash}:\r\n  Official UI Fixture:\r\n    Games:\r\n      God of War 3:\r\n        {game.Serial}: [ {game.Version} ]\r\n    Patch Version: 1.0\r\n    Group: UI\r\n    Notes: External UI fixture\r\n    Patch:\r\n      - [ be32, 0x00000010, 0x60000000 ]\r\n";
        var officialPath = Path.Combine(env.Install.PatPath, "patch.yml");
        File.WriteAllText(officialPath, official, new UTF8Encoding(false));
        var officialBytes = File.ReadAllBytes(officialPath);
        var officialSha = Convert.ToHexString(SHA256.HashData(officialBytes));
        var importedPath = Path.Combine(env.Install.PatPath, "imported_patch.yml");
        File.WriteAllText(importedPath, "Version: 1.2\r\n# USER PATCH KEEP\r\nPPU-1111111111111111111111111111111111111111:\r\n  User Owned Patch:\r\n    Games:\r\n      User Game:\r\n        BLUS99999: [ 01.00 ]\r\n    Author: User\r\n    Patch Version: 1.0\r\n    Patch:\r\n      - [ be32, 0x00000020, 0x60000000 ]\r\n", new UTF8Encoding(false));
        if (!new ReadSvc().Read(importedPath, PatSrc.Imported).IsOk)
            throw new InvalidOperationException("TEST_FIXTURE: imported_patch.yml did not parse as a valid RPCS3 patch document.");
        var customPath = Path.Combine(env.Install.CustPath, $"config_{game.Serial}.yml");
        Directory.CreateDirectory(env.Install.CustPath);
        File.WriteAllText(customPath, "Video:\r\n  Write Color Buffers: false\r\nUser Section:\r\n  Keep Me: yes\r\n", new UTF8Encoding(false));
        var importedBefore = File.ReadAllText(importedPath, Encoding.UTF8);

        var exe = LocateApp();
        using var appData = new TempAppData(Path.Combine(root, "appdata"));
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(exe)!
        };
        psi.Environment["APPDATA"] = appData.Path;
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("R3Config.exe did not start.");
        var started = DateTimeOffset.Now;
        Exception? failure = null;
        try
        {
            var main = WaitWindow(process, 20);
            var languageButton = Find(main, "App.LanguageToggle", 10);
            var languageName = languageButton.Current.Name;
            var wantsArabic = language.Equals("ar-SA", StringComparison.OrdinalIgnoreCase);
            var buttonOffersArabic = languageName.Contains("Arabic", StringComparison.OrdinalIgnoreCase) || languageName.Contains("العربية", StringComparison.OrdinalIgnoreCase);
            var currentArabic = !buttonOffersArabic;
            if (wantsArabic != currentArabic)
            {
                Invoke(languageButton);
                Thread.Sleep(500);
                main = AutomationElement.FromHandle(process.MainWindowHandle);
            }

            var selectedPath = Find(main, "App.Rpcs3Path", 10);
            ((ValuePattern)selectedPath.GetCurrentPattern(ValuePattern.Pattern)).SetValue(env.Install.ExePath);
            selectedPath = Find(AutomationElement.FromHandle(process.MainWindowHandle), "App.Rpcs3Path", 5);
            var pathValue = ((ValuePattern)selectedPath.GetCurrentPattern(ValuePattern.Pattern)).Current.Value;
            if (!pathValue.Contains(env.Install.ExePath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"TEST_FIXTURE: UI did not set the requested RPCS3 path. UI value={pathValue}");
            var analyze = Find(main, "App.AnalyzeButton", 10);
            Invoke(analyze);

            var list = Find(main, "Game.List", 20);
            var item = WaitGameItem(list, game.Serial, 20);
            Select(item);
            if (!TextOf(list).Contains(game.Serial, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Detected game serial was not exposed by UIA. ListText={TextOf(list)}");

            var settingsTab = Find(main, "Settings.Tab", 10);
            Select(settingsTab);
            main = AutomationElement.FromHandle(process.MainWindowHandle);
            var settings = Find(main, "Settings.Scroll", 10);
            var settingsBoxes = settings.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.CheckBox)).Cast<AutomationElement>().Where(item => item.Current.IsEnabled).ToArray();
            if (settingsBoxes.Length == 0) throw new InvalidOperationException("No actionable setting checkbox was exposed by UIA.");
            foreach (var setting in settingsBoxes) Toggle(setting);

            var officialTab = Find(main, "Patches.Tab", 10);
            Select(officialTab);
            main = AutomationElement.FromHandle(process.MainWindowHandle);
            var officialList = Find(main, "Patches.OfficialScroll", 10);
            Toggle(WaitDescendant(officialList, ControlType.CheckBox, 10));
            var canaryTab = Find(main, "Canary.Tab", 10);
            Select(canaryTab);
            main = AutomationElement.FromHandle(process.MainWindowHandle);
            var canaryList = Find(main, "Patches.CanaryScroll", 10);
            Toggle(WaitDescendant(canaryList, ControlType.CheckBox, 10));

            var setupTab = Find(main, "Setup.Tab", 10);
            Select(setupTab);
            main = AutomationElement.FromHandle(process.MainWindowHandle);
            var setup = Find(main, "Setup.Scroll", 15);
            var prepare = Find(setup, "Setup.PrepareButton", 15);
            if (!prepare.Current.IsEnabled) throw new InvalidOperationException("Prepare button is disabled after UI selections.");
            Invoke(prepare);
            var confirm = WaitElement(process.Id, "Dialog.Confirm", 15);
            var message = TextOf(confirm);
            if (message.Length < 20)
                throw new InvalidOperationException($"Confirmation dialog did not expose its plan summary. DialogText={message}");
            Invoke(Find(confirm, "Dialog.ConfirmYes", 5));
            var info = WaitElement(process.Id, "Dialog.Info", 20);
            if (!TextOf(info).Contains("Game Ready", StringComparison.OrdinalIgnoreCase) &&
                !TextOf(info).Contains("جاه", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Game Ready result was not exposed by the real dialog. DialogText={TextOf(info)}");
            Invoke(Find(info, "Dialog.InfoOk", 5));

            Verify(env, game, canary, officialPath, officialBytes, officialSha, importedPath, importedBefore);
            var backupCount = BackupFiles(env.Install).Length;

            main = AutomationElement.FromHandle(process.MainWindowHandle);
            setup = Find(main, "Setup.Scroll", 10);
            prepare = Find(setup, "Setup.PrepareButton", 10);
            if (prepare.Current.IsEnabled)
            {
                Invoke(prepare);
                var second = WaitElement(process.Id, "Dialog.Info", 15);
                var secondText = TextOf(second);
                if (!secondText.Contains("Already", StringComparison.OrdinalIgnoreCase) &&
                    !secondText.Contains("prepared", StringComparison.OrdinalIgnoreCase) &&
                    !secondText.Contains("جاه", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Second Prepare did not report an idempotent result.");
                Invoke(Find(second, "Dialog.InfoOk", 5));
            }
            Verify(env, game, canary, officialPath, officialBytes, officialSha, importedPath, importedBefore);
            if (BackupFiles(env.Install).Length != backupCount) throw new InvalidOperationException("Idempotent Prepare created another backup.");
            Console.WriteLine($"Interactive UI Automation PASS ({language}); pid={process.Id}; started={started:O}");
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            try { if (!process.HasExited) process.CloseMainWindow(); } catch { }
            if (!process.WaitForExit(10_000))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                failure ??= new TimeoutException("R3Config.exe did not exit after UI automation.");
            }
            if (!process.HasExited) failure ??= new TimeoutException("R3Config.exe remained alive after forced test-owned shutdown.");
            else if (process.ExitCode != 0) failure ??= new InvalidOperationException($"R3Config.exe exited with {process.ExitCode}.");
        }
        if (failure is not null) throw new InvalidOperationException($"Interactive {language} workflow failed.", failure);
    }

    private static void Verify(GameSetupT.SetupEnvironment env, Rpcs3GameDescriptor game, Rpcs3PatchEntry canary, string officialPath, byte[] officialBytes, string officialSha, string importedPath, string importedBefore)
    {
        var custom = Path.Combine(env.Install.CustPath, $"config_{game.Serial}.yml");
        var wcb = File.Exists(custom) ? R3Yaml.ReadVal(custom, "Video", "Write Color Buffers") : null;
        var keep = File.Exists(custom) ? R3Yaml.ReadVal(custom, "User Section", "Keep Me") : null;
        if (wcb != "true" || keep != "yes") throw new InvalidOperationException($"Custom configuration values or unrelated user content were not verified. path={custom};wcb={wcb};keep={keep}");
        if (!File.Exists(env.Install.PatCfg)) throw new InvalidOperationException("patch_config.yml was not written.");
        var imported = new ReadSvc().Read(importedPath, PatSrc.Imported);
        if (!imported.IsOk || !File.ReadAllText(importedPath).Contains("USER PATCH KEEP", StringComparison.Ordinal)) throw new InvalidOperationException("User imported patch content was lost or imported_patch.yml is invalid.");
        if (CountOccurrences(File.ReadAllText(importedPath), canary.Title) != 1) throw new InvalidOperationException("Canary definition was duplicated.");
        var patchSnapshot = new CfgRead().ReadSnap(env.Install.PatCfg);
        var match = new PatMatch(canary, MatStat.Match, game.Serial, game.Version, true, "interactive", TgtName: game.Title, TgtSer: game.Serial, TgtVer: game.Version);
        var change = new PatPlnr().Build3(match) ?? throw new InvalidOperationException("Selected Canary patch could not build an enablement change.");
        if (!patchSnapshot.IsOn(change)) throw new InvalidOperationException("Selected Canary patch was not enabled in patch_config.yml.");
        var after = File.ReadAllBytes(officialPath);
        if (!after.SequenceEqual(officialBytes) || !Convert.ToHexString(SHA256.HashData(after)).Equals(officialSha, StringComparison.Ordinal)) throw new InvalidOperationException("Official patch.yml changed during interactive Apply.");
        var snapshots = new BackupService().ListBkp(Rpcs3AppPaths.BkpRoot(env.Install));
        var manifest = snapshots.OrderByDescending(item => item.MadeAt).FirstOrDefault() ?? throw new InvalidOperationException("Backup manifest was not created.");
        if (manifest.Files.Any(entry => entry.TgtPath.Equals(officialPath, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(entry.TgtPath).Equals("patch.yml", StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Backup manifest included RPCS3-owned patch.yml.");
    }

    private static string[] BackupFiles(Rpcs3InstallDescriptor install)
    {
        var root = Path.Combine(install.CfgPath, "R3Bkp");
        return Directory.Exists(root) ? Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).ToArray() : [];
    }

    private static string LocateApp()
    {
        var candidates = new[] { Path.Combine(Directory.GetCurrentDirectory(), "App", "bin", "Release", "net8.0-windows", "R3Config.exe"), Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "App", "bin", "Release", "net8.0-windows", "R3Config.exe") };
        return candidates.Select(Path.GetFullPath).FirstOrDefault(File.Exists) ?? throw new FileNotFoundException("Release R3Config.exe was not found; build App first.");
    }

    private static Rpcs3PatchEntry LoadGodOfWarCanary()
    {
        var path = BundledCanaryPatchService.GetPath(AppContext.BaseDirectory);
        var text = R3Bound.ReadUtf8(path, R3Limits.PatCatBy, "can.yml");
        var normalized = new CanaryPatchImportService().Norm(text);
        if (!normalized.IsOk) throw new InvalidDataException("Bundled Canary catalog normalization failed.");
        var read = new ReadSvc().ReadText(normalized.NormText, PatSrc.Can, path);
        return read.Catalog.Entries.First(entry => entry.Title == "Unlock FPS" && entry.Serials.Contains("BCUS98232") && entry.GameVer.Contains("01.12"));
    }

    private static AutomationElement WaitWindow(Process p, int seconds)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < until)
        {
            if (p.HasExited) throw new InvalidOperationException($"R3Config.exe exited with {p.ExitCode} before showing its window.");
            if (p.MainWindowHandle != IntPtr.Zero) return AutomationElement.FromHandle(p.MainWindowHandle);
            Thread.Sleep(100);
        }
        throw new TimeoutException("R3Config main window was not ready.");
    }

    private static AutomationElement WaitDialog(int processId, int seconds) => WaitElement(processId, null, seconds);
    private static AutomationElement WaitElement(int processId, string? id, int seconds)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < until)
        {
            var root = AutomationElement.RootElement;
            var cond = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window);
            foreach (AutomationElement e in root.FindAll(TreeScope.Subtree, cond))
            {
                if (id is null)
                {
                    if (e.Current.NativeWindowHandle == Process.GetProcessById(processId).MainWindowHandle.ToInt32()) continue;
                    if (e.Current.Name.Contains("Open", StringComparison.OrdinalIgnoreCase) || e.Current.Name.Contains("RPCS3", StringComparison.OrdinalIgnoreCase)) return e;
                    continue;
                }
                if (e.Current.ProcessId == processId && e.Current.AutomationId == id) return e;
            }
            Thread.Sleep(100);
        }
        var snapshot = string.Join(";", AutomationElement.RootElement.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window)).Cast<AutomationElement>().Select(e => $"{e.Current.Name}[pid={e.Current.ProcessId},aid={e.Current.AutomationId}]"));
        throw new TimeoutException($"UIA dialog/window '{id ?? "file picker"}' was not found. TopLevel={snapshot}");
    }

    private static AutomationElement Find(AutomationElement root, string id, int seconds) => WaitFor(() => root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, id)), seconds, id);
    private static AutomationElement? FindText(AutomationElement root, string text, int seconds) => WaitForOptional(() => root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, text)), seconds);
    private static AutomationElement FindByNameOrId(AutomationElement root, params string[] names) => names.Select(n => root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, n)) ?? root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, n))).FirstOrDefault(e => e is not null) ?? throw new InvalidOperationException("Language toggle was not found.");
    private static AutomationElement WaitDescendant(AutomationElement root, ControlType type, int seconds) => WaitFor(() => root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, type)), seconds, type.ProgrammaticName);
    private static AutomationElement WaitGameItem(AutomationElement root, string serial, int seconds) => WaitFor(() => root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem)).Cast<AutomationElement>().FirstOrDefault(e => TextOf(e).Contains(serial, StringComparison.OrdinalIgnoreCase)), seconds, serial);
    private static AutomationElement WaitFor(Func<AutomationElement?> f, int seconds, string label) { var until = DateTime.UtcNow.AddSeconds(seconds); while (DateTime.UtcNow < until) { var e = f(); if (e is not null) return e; Thread.Sleep(100); } throw new TimeoutException($"UIA element '{label}' was not found."); }
    private static AutomationElement? WaitForOptional(Func<AutomationElement?> f, int seconds) { var until = DateTime.UtcNow.AddSeconds(seconds); while (DateTime.UtcNow < until) { var e = f(); if (e is not null) return e; Thread.Sleep(100); } return null; }
    private static void Invoke(AutomationElement e) => ((InvokePattern)e.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
    private static void Select(AutomationElement e) => ((SelectionItemPattern)e.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
    private static void Toggle(AutomationElement e) { if (e.TryGetCurrentPattern(TogglePattern.Pattern, out var p)) ((TogglePattern)p).Toggle(); else Invoke(e); }
    private static string TextOf(AutomationElement e) => string.Join(" ", e.FindAll(TreeScope.Descendants, Condition.TrueCondition).Cast<AutomationElement>().Select(x => x.Current.Name));
    private static int CountOccurrences(string text, string value) { var count = 0; var at = 0; while ((at = text.IndexOf(value, at, StringComparison.Ordinal)) >= 0) { count++; at += value.Length; } return count; }

    private static void SetFilePicker(AutomationElement picker, string path)
    {
        var edits = picker.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)).Cast<AutomationElement>().ToArray();
        if (edits.Length == 0) throw new InvalidOperationException("File picker filename edit was not exposed by UIA.");
        var edit = edits.FirstOrDefault(item => item.Current.Name.Contains("File name", StringComparison.OrdinalIgnoreCase) || item.Current.Name.Contains("اسم الملف", StringComparison.OrdinalIgnoreCase)) ?? edits.First();
        ((ValuePattern)edit.GetCurrentPattern(ValuePattern.Pattern)).SetValue(path);
        Thread.Sleep(100);
        if (picker.Current.NativeWindowHandle != 0)
            picker = AutomationElement.FromHandle(picker.Current.NativeWindowHandle);
        var buttons = picker.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)).Cast<AutomationElement>().ToArray();
        var open = buttons.FirstOrDefault(b => b.Current.Name.Equals("Open", StringComparison.OrdinalIgnoreCase) || b.Current.Name.Equals("Select", StringComparison.OrdinalIgnoreCase)) ?? buttons.LastOrDefault();
        if (open is null) throw new InvalidOperationException("File picker Open button was not exposed by UIA.");
        try { Invoke(open); }
        catch (Exception ex) when (ex is COMException or InvalidOperationException) { /* common-dialog provider may close itself after accepting the filename */ }
    }

    private sealed class TempAppData : IDisposable
    {
        public TempAppData(string path) { Path = path; Directory.CreateDirectory(path); }
        public string Path { get; }
        public void Dispose() { }
    }
}
