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
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Tests;

public static partial class OutOfProcessUiT
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
        var official = $"Version: 1.2\r\nPPU-{canary.NormHash}:\r\n  Official UI Fixture:\r\n    Games:\r\n      God of War 3:\r\n        {game.Serial}: [ {game.Version} ]\r\n    Patch Version: 1.0\r\n    Group: UI\r\n    Notes: Requires the external UI fixture setting before boot.\r\n    Patch:\r\n      - [ be32, 0x00000010, 0x60000000 ]\r\n";
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

        // Canary is actionable only after the one-time local PPU capture
        // (docs/Arch/README.md). The fixture records that completed capture
        // for this temporary install; TempAppData restores the real store.
        var logPath = Path.Combine(env.Install.RootPath, "RPCS3.log");
        var logFingerprint = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(logPath)));
        var captured = DateTime.UtcNow;
        var captureStore = new HCanDiscoveryStore();
        foreach (var installPath in new[] { env.GamePath, hddGame })
        {
            captureStore.SaveBinding(
                new HCanHashBinding(
                    env.Install.ExePath, game.Serial, game.Version!, installPath,
                    [canary.NormHash!], [], captured, captured.AddSeconds(-2),
                    File.GetLastWriteTimeUtc(logPath), logFingerprint, logPath),
                replaceExisting: true);
        }
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
            foreach (var setting in settingsBoxes)
            {
                if (!setting.Current.IsEnabled)
                {
                    var windows = string.Join(";", AutomationElement.RootElement.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id)).Cast<AutomationElement>().Select(e => $"{e.Current.Name}[aid={e.Current.AutomationId},enabled={e.Current.IsEnabled}]"));
                    throw new InvalidOperationException($"Setting checkbox '{setting.Current.Name}' became disabled before it was toggled. ProcessWindows={windows}; Dialog={TextOf(AutomationElement.RootElement.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id)))}");
                }

                Toggle(setting);
            }
            // The combined Setup/Prepare tab no longer exists: each domain is
            // applied through its own section action with a real confirmation.
            var settingsResult = ApplySection(process, "Settings.Tab", "Settings.ApplyButton");
            main = AutomationElement.FromHandle(process.MainWindowHandle);

            var officialTab = Find(main, "Patches.Tab", 10);
            Select(officialTab);
            main = AutomationElement.FromHandle(process.MainWindowHandle);
            var officialList = Find(main, "Patches.OfficialScroll", 10);
            Toggle(WaitDescendant(officialList, ControlType.CheckBox, 10));
            var canaryTab = Find(main, "Canary.Tab", 10);
            Select(canaryTab);
            main = AutomationElement.FromHandle(process.MainWindowHandle);
            var canaryList = Find(main, "Patches.CanaryScroll", 10);
            var canaryBox = WaitDescendant(canaryList, ControlType.CheckBox, 10);
            if (!canaryBox.Current.IsEnabled)
            {
                var boxes = string.Join(" | ", canaryList.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.CheckBox)).Cast<AutomationElement>().Select(e => $"{e.Current.Name}[enabled={e.Current.IsEnabled},help={e.Current.HelpText}]"));
                throw new InvalidOperationException($"The Canary checkbox is not actionable. Boxes={boxes}; ListText={TextOf(canaryList)}");
            }

            Toggle(canaryBox);
            var canaryResult = ApplySection(process, "Canary.Tab", "Patches.CanaryApplyButton");
            main = AutomationElement.FromHandle(process.MainWindowHandle);


            Verify(env, game, canary, officialPath, officialBytes, officialSha, importedPath, importedBefore);
            var backupCount = BackupFiles(env.Install).Length;

            // Nothing selected remains unapplied, so neither action is offered again.
            main = AutomationElement.FromHandle(process.MainWindowHandle);
            Select(Find(main, "Settings.Tab", 10));
            if (AutomationElement.FromHandle(process.MainWindowHandle).FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "Settings.ApplyButton")) is { } again &&
                !again.Current.IsOffscreen && again.Current.IsEnabled)
                throw new InvalidOperationException("Settings Apply is still offered after every selected change was applied.");
            Verify(env, game, canary, officialPath, officialBytes, officialSha, importedPath, importedBefore);
            if (BackupFiles(env.Install).Length != backupCount) throw new InvalidOperationException("A no-op revisit created another backup.");
            VerifyPatchPresentation(process, language);
            VerifyKeyboardFocus(process);
            VerifyNclDialogKeyboard(process, language);
            Console.WriteLine($"Interactive UI Automation results ({language}): Settings=[{settingsResult}] Canary=[{canaryResult}]");
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

    // Opens a domain tab, invokes its section Apply action, accepts the real
    // confirmation and returns the text of the resulting information dialog.
    private static string ApplySection(Process process, string tabId, string buttonId)
    {
        var main = AutomationElement.FromHandle(process.MainWindowHandle);
        Select(Find(main, tabId, 10));
        main = AutomationElement.FromHandle(process.MainWindowHandle);
        var button = Find(main, buttonId, 10);
        if (!button.Current.IsEnabled || button.Current.IsOffscreen)
            throw new InvalidOperationException($"{buttonId} is not actionable after the UI selections.");
        Invoke(button);
        var confirm = WaitElement(process.Id, "Dialog.Confirm", 15);
        if (TextOf(confirm).Length < 20)
            throw new InvalidOperationException($"{buttonId} confirmation did not expose its summary. DialogText={TextOf(confirm)}");
        Invoke(Find(confirm, "Dialog.ConfirmYes", 5));
        var info = WaitElement(process.Id, "Dialog.Info", 20);
        var text = TextOf(info);
        Invoke(Find(info, "Dialog.InfoOk", 5));
        return text;
    }

    // Real rendered patch surfaces for the shipped God of War: Ascension
    // fixture. Its exact Artemis "Unlock FPS" has an applicable Canary
    // equivalent: the row stays visible and labelled Blocked, its selection
    // box is disabled, and hovering the row shows the Canary explanation.
    // With R3_UI_SHOTS set, every patch tab is captured in both themes.
    private const string OfficialFixtureNote = "Requires the external UI fixture setting before boot.";

    private static void VerifyPatchPresentation(Process process, string language)
    {
        var previousLanguage = UiText.CurLang;
        string blocked;
        string explanation;
        string officialSource;
        try
        {
            UiText.SetLang(language);
            blocked = UiText.GetText("Patch.State.CanaryOwned");
            explanation = UiText.GetText("Patch.Direct.CanaryEquivalent");
            officialSource = UiText.GetText("Patch.Note.OfficialOriginal");
        }
        finally
        {
            UiText.SetLang(previousLanguage);
        }

        var window = process.MainWindowHandle;
        WindowCapture.Foreground(window);
        var main = AutomationElement.FromHandle(window);
        Select(Find(main, "Artemis.Tab", 10));
        main = AutomationElement.FromHandle(window);
        var box = WaitFor(
            () => main.FindFirst(
                TreeScope.Descendants,
                new AndCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.CheckBox),
                    new PropertyCondition(AutomationElement.NameProperty, "Unlock FPS"))),
            10,
            "Artemis Unlock FPS selection box");
        if (box.Current.IsEnabled)
            throw new InvalidOperationException("The Canary-equivalent Artemis Unlock FPS selection box is enabled.");

        // WPF keeps IsOffscreen false for rows clipped by a ScrollViewer, so
        // the row is scrolled until it lies inside the scroll viewport.
        var scrollOwner = TreeWalker.ControlViewWalker.GetParent(box);
        while (scrollOwner is not null &&
               !scrollOwner.TryGetCurrentPattern(ScrollPattern.Pattern, out _))
        {
            scrollOwner = TreeWalker.ControlViewWalker.GetParent(scrollOwner);
        }

        if (scrollOwner is not null)
        {
            var scroll = (ScrollPattern)scrollOwner.GetCurrentPattern(ScrollPattern.Pattern);
            for (var attempt = 0; attempt < 60; attempt++)
            {
                var viewport = scrollOwner.Current.BoundingRectangle;
                var current = box.Current.BoundingRectangle;
                if (current.Top >= viewport.Top && current.Bottom <= viewport.Bottom)
                {
                    break;
                }

                scroll.ScrollVertical(current.Top < viewport.Top ? ScrollAmount.SmallDecrement : ScrollAmount.SmallIncrement);
                Thread.Sleep(80);
            }
        }

        var visibleBox = box.Current.BoundingRectangle;
        var listViewport = scrollOwner?.Current.BoundingRectangle ?? visibleBox;
        if (visibleBox.Top < listViewport.Top || visibleBox.Bottom > listViewport.Bottom)
            throw new InvalidOperationException("The Canary-equivalent Artemis Unlock FPS row could not be scrolled into view.");

        var boxBounds = box.Current.BoundingRectangle;
        var rowCenter = boxBounds.Top + (boxBounds.Height / 2);
        var rowTexts = main.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text))
            .Cast<AutomationElement>()
            .Where(item =>
            {
                var bounds = item.Current.BoundingRectangle;
                return !bounds.IsEmpty &&
                       bounds.Top <= rowCenter + 30 &&
                       bounds.Bottom >= rowCenter - 30;
            })
            .ToArray();
        if (!rowTexts.Any(item => item.Current.Name == blocked))
            throw new InvalidOperationException(
                $"The Canary-equivalent Artemis row does not render its '{blocked}' state. RowText={string.Join(" | ", rowTexts.Select(item => item.Current.Name))}");

        var title = rowTexts.FirstOrDefault(item => item.Current.Name == "Unlock FPS")
            ?? throw new InvalidOperationException("The Canary-equivalent Artemis row title is not rendered.");
        WindowCapture.Hover(title.Current.BoundingRectangle);
        var tip = WaitForOptional(
            () => AutomationElement.RootElement
                .FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id))
                .Cast<AutomationElement>()
                .FirstOrDefault(item => VanishSafeText(item).Contains(explanation, StringComparison.Ordinal)),
            8);
        var shots = Environment.GetEnvironmentVariable("R3_UI_SHOTS");
        var tag = language.StartsWith("ar", StringComparison.OrdinalIgnoreCase) ? "ar" : "en";
        if (!string.IsNullOrWhiteSpace(shots))
        {
            WindowCapture.SavePng(window, Path.Combine(shots, $"real-{tag}-Artemis-Hover.png"));
        }

        if (tip is null)
            throw new InvalidOperationException("Hovering the Canary-equivalent Artemis row did not show its Canary explanation.");

        WindowCapture.Hover(new System.Windows.Rect(boxBounds.Left, boxBounds.Top - 400, 1, 1));

        // The fixture's Official note has no reviewed Arabic: the real window
        // shows the upstream text verbatim, and in Arabic its provenance caption.
        Select(Find(AutomationElement.FromHandle(window), "Patches.Tab", 10));
        Thread.Sleep(400);
        var officialTexts = AutomationElement.FromHandle(window)
            .FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text))
            .Cast<AutomationElement>()
            .Select(item => item.Current.Name)
            .ToArray();
        if (!officialTexts.Contains(OfficialFixtureNote, StringComparer.Ordinal))
            throw new InvalidOperationException(
                $"The Official note without reviewed Arabic was not rendered verbatim. Texts={string.Join(" | ", officialTexts)}");
        if (language.StartsWith("ar", StringComparison.OrdinalIgnoreCase) &&
            !officialTexts.Contains(officialSource, StringComparer.Ordinal))
            throw new InvalidOperationException("The upstream Official note fallback is missing its provenance caption.");

        // The theme switch must change the rendered theme, not only its own
        // state, for a UI Automation Toggle as well as for a click. Two passes
        // return the switch to its original state.
        var theme = Find(main, "App.ThemeToggle", 10);
        var luminance = new double[2];
        for (var pass = 0; pass < 2; pass++)
        {
            var dark = theme.TryGetCurrentPattern(TogglePattern.Pattern, out var toggle) &&
                       ((TogglePattern)toggle).Current.ToggleState == ToggleState.On;
            foreach (var (tab, name) in new[] { ("Settings.Tab", "Settings"), ("Patches.Tab", "Official"), ("Canary.Tab", "Canary"), ("Artemis.Tab", "Artemis"), ("Runtime.Tab", "Runtime") })
            {
                Select(Find(AutomationElement.FromHandle(window), tab, 10));
                Thread.Sleep(400);
                if (!string.IsNullOrWhiteSpace(shots))
                {
                    WindowCapture.SavePng(window, Path.Combine(shots, $"real-{(dark ? "dark" : "light")}-{tag}-{name}.png"));
                }
            }

            luminance[pass] = WindowCapture.MeanLuminance(window);
            if (dark ? luminance[pass] > 110 : luminance[pass] < 150)
                throw new InvalidOperationException(
                    $"The theme switch reports {(dark ? "dark" : "light")} but the window renders mean luminance {luminance[pass]:0}.");

            Toggle(theme);
            Thread.Sleep(600);
            theme = Find(AutomationElement.FromHandle(window), "App.ThemeToggle", 10);
        }

        if (Math.Abs(luminance[0] - luminance[1]) < 60)
            throw new InvalidOperationException(
                $"Toggling the theme switch did not change the rendered theme ({luminance[0]:0} -> {luminance[1]:0}).");
    }

    // Real keyboard traversal of the main window. Every Tab stop must stay in
    // R3Config, be enabled, on screen and named for assistive technology; the
    // traversal must reach the RPCS3 path and Analyze actions; and Shift+Tab
    // must return to the previous stop.
    private static void VerifyKeyboardFocus(Process process)
    {
        var window = process.MainWindowHandle;
        WindowCapture.Foreground(window);
        Thread.Sleep(300);
        var windowBounds = AutomationElement.FromHandle(window).Current.BoundingRectangle;
        var visited = new List<AutomationElement>();
        for (var press = 0; press < 60; press++)
        {
            WindowCapture.PressTab(shift: false);
            Thread.Sleep(120);
            var focused = AutomationElement.FocusedElement;
            var current = focused.Current;
            if (current.ProcessId != process.Id)
                throw new InvalidOperationException($"Tab moved keyboard focus out of R3Config after {press + 1} presses.");

            var bounds = current.BoundingRectangle;
            if (!current.IsEnabled || current.IsOffscreen || bounds.IsEmpty ||
                !windowBounds.Contains(new System.Windows.Point(bounds.Left + (bounds.Width / 2), bounds.Top + (bounds.Height / 2))))
                throw new InvalidOperationException(
                    $"Tab stop {press + 1} ('{current.Name}', {current.AutomationId}, {current.ControlType.ProgrammaticName}) is disabled or not visible.");

            if (string.IsNullOrWhiteSpace(current.Name) && string.IsNullOrWhiteSpace(current.AutomationId))
                throw new InvalidOperationException(
                    $"Tab stop {press + 1} ({current.ControlType.ProgrammaticName}) has no accessible name.");

            visited.Add(focused);
        }

        var ids = visited.Select(item => item.Current.AutomationId).ToHashSet(StringComparer.Ordinal);
        foreach (var required in new[] { "App.Rpcs3Path", "App.AnalyzeButton" })
        {
            if (!ids.Contains(required))
                throw new InvalidOperationException(
                    $"Keyboard traversal never reached {required}. Stops={string.Join(" | ", visited.Select(item => item.Current.AutomationId + ":" + item.Current.Name).Distinct())}");
        }

        var last = visited[^1];
        var previous = visited[^2];
        WindowCapture.PressTab(shift: true);
        Thread.Sleep(150);
        if (!Automation.Compare(AutomationElement.FocusedElement, previous))
            throw new InvalidOperationException(
                $"Shift+Tab from '{last.Current.Name}' did not return to '{previous.Current.Name}'.");
    }

    // The NCL dialog opened the way a user opens it (Artemis options menu).
    // Keyboard focus must stay inside the modal dialog, and every stop must be
    // enabled, on screen and named. The real rendered dialog is captured with
    // R3_UI_SHOTS, then closed through its window pattern.
    private static void VerifyNclDialogKeyboard(Process process, string language)
    {
        var previousLanguage = UiText.CurLang;
        string optionsName;
        string importName;
        string dialogTitle;
        try
        {
            UiText.SetLang(language);
            optionsName = UiText.GetText("Patch.Artemis.Options");
            importName = UiText.GetText("Patch.Ncl.Import");
            dialogTitle = UiText.GetText("Patch.Ncl.Title");
        }
        finally
        {
            UiText.SetLang(previousLanguage);
        }

        var window = process.MainWindowHandle;
        WindowCapture.Foreground(window);
        var main = AutomationElement.FromHandle(window);
        Select(Find(main, "Artemis.Tab", 10));
        main = AutomationElement.FromHandle(window);
        Invoke(WaitFor(() => main.FindFirst(TreeScope.Descendants, new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                new PropertyCondition(AutomationElement.NameProperty, optionsName))), 10, "Artemis options button"));
        var menuItem = WaitFor(() => AutomationElement.RootElement.FindFirst(TreeScope.Descendants, new AndCondition(
                new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem),
                new PropertyCondition(AutomationElement.NameProperty, importName))), 10, "NCL import menu item");
        Invoke(menuItem);
        // An owned WPF window is exposed under its owner, not on the desktop.
        var dialog = WaitForOptional(() => AutomationElement.FromHandle(window).FindFirst(TreeScope.Descendants, new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window),
                new PropertyCondition(AutomationElement.NameProperty, dialogTitle))), 15)
            ?? throw new TimeoutException(
                $"The NCL dialog '{dialogTitle}' did not open. MenuItemEnabled={menuItem.Current.IsEnabled}; TopLevel=" +
                string.Join(" | ", AutomationElement.RootElement
                    .FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id))
                    .Cast<AutomationElement>()
                    .Select(item => item.Current.ControlType.ProgrammaticName + ":" + item.Current.Name)));
        var dialogHandle = new IntPtr(dialog.Current.NativeWindowHandle);
        WindowCapture.Foreground(dialogHandle);
        Thread.Sleep(1500);

        var shots = Environment.GetEnvironmentVariable("R3_UI_SHOTS");
        if (!string.IsNullOrWhiteSpace(shots))
        {
            var tag = language.StartsWith("ar", StringComparison.OrdinalIgnoreCase) ? "ar" : "en";
            WindowCapture.SavePng(dialogHandle, Path.Combine(shots, $"real-{tag}-NclDialog.png"));
        }

        var dialogBounds = dialog.Current.BoundingRectangle;
        var visited = new List<AutomationElement>();
        try
        {
            for (var press = 0; press < 30; press++)
            {
                WindowCapture.PressTab(shift: false);
                Thread.Sleep(120);
                var focused = AutomationElement.FocusedElement;
                var current = focused.Current;
                var bounds = current.BoundingRectangle;
                if (current.ProcessId != process.Id ||
                    bounds.IsEmpty ||
                    !dialogBounds.Contains(new System.Windows.Point(bounds.Left + (bounds.Width / 2), bounds.Top + (bounds.Height / 2))))
                    throw new InvalidOperationException(
                        $"Tab moved focus out of the modal NCL dialog after {press + 1} presses ('{current.Name}').");

                if (!current.IsEnabled || current.IsOffscreen)
                    throw new InvalidOperationException(
                        $"NCL dialog Tab stop {press + 1} ('{current.Name}', {current.ControlType.ProgrammaticName}) is disabled or not visible.");

                if (string.IsNullOrWhiteSpace(current.Name) && string.IsNullOrWhiteSpace(current.AutomationId))
                    throw new InvalidOperationException(
                        $"NCL dialog Tab stop {press + 1} ({current.ControlType.ProgrammaticName}) has no accessible name.");

                visited.Add(focused);
            }

            var previous = visited[^2];
            WindowCapture.PressTab(shift: true);
            Thread.Sleep(150);
            if (!Automation.Compare(AutomationElement.FocusedElement, previous))
                throw new InvalidOperationException(
                    $"Shift+Tab in the NCL dialog did not return to '{previous.Current.Name}'.");
        }
        finally
        {
            if (dialog.TryGetCurrentPattern(WindowPattern.Pattern, out var closer))
            {
                ((WindowPattern)closer).Close();
            }

            Thread.Sleep(500);
        }
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

    // A transient top-level window (tooltip, popup) can close while a poll
    // enumerates it; it simply does not match this round and polling goes on.
    private static string VanishSafeText(AutomationElement e)
    {
        try
        {
            return e.Current.Name + " " + TextOf(e);
        }
        catch (ElementNotAvailableException)
        {
            return string.Empty;
        }
    }
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

    // The launched app resolves its settings through the Windows known-folder
    // API, which ignores the APPDATA/LOCALAPPDATA environment variables. The
    // real %LOCALAPPDATA%\R3Config and %APPDATA%\R3Config files are therefore
    // captured byte-for-byte before launch and restored afterwards; files the
    // run created are removed, so the user's own settings are never changed.
    private sealed class TempAppData : IDisposable
    {
        private readonly Dictionary<string, byte[]?> _snapshot = new(StringComparer.OrdinalIgnoreCase);
        private readonly string[] _roots =
        [
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "R3Config"),
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "R3Config")
        ];

        public TempAppData(string path)
        {
            Path = path;
            Directory.CreateDirectory(path);
            foreach (var file in _roots.Where(Directory.Exists)
                         .SelectMany(root => Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly)))
            {
                _snapshot[file] = File.ReadAllBytes(file);
            }
        }

        public string Path { get; }

        public void Dispose()
        {
            foreach (var file in _roots.Where(Directory.Exists)
                         .SelectMany(root => Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly))
                         .Where(file => !_snapshot.ContainsKey(file))
                         .ToArray())
            {
                File.Delete(file);
            }

            foreach (var (file, bytes) in _snapshot)
            {
                if (bytes is not null && (!File.Exists(file) || !File.ReadAllBytes(file).AsSpan().SequenceEqual(bytes)))
                {
                    File.WriteAllBytes(file, bytes);
                }
            }
        }
    }
}
