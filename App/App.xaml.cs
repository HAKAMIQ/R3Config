using System.Windows;
using System.Windows.Threading;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using R3Config.Lang;
using R3Config.Svc;

namespace R3Config;

public partial class App : System.Windows.Application
{
    private AppInstanceGate? _instanceGate;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Registered first, before any other startup work, so that even a
        // failure inside localization/theme setup below is reported to the
        // user instead of silently crashing the process. Without these
        // handlers, an exception escaping an `async void` command (see
        // R3Config.Vm.AsyncCmd.Execute) or any other unhandled exception on
        // the UI thread terminates R3Config with no diagnostic and no
        // chance to save the user's in-progress work.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        if (e.Args.Length >= 3 &&
            string.Equals(e.Args[0], "--localization-smoke", StringComparison.OrdinalIgnoreCase))
        {
            RunLocalizationSmoke(e.Args[1], e.Args[2]);
            Shutdown(0);
            return;
        }

        UiText.SetLang(UiPrefs.LoadLang());
        UiLaySvc.Apply();

        if (!AppInstanceGate.TryAcquire(
                out _instanceGate))
        {
            MessageBox.Show(
                UiText.GetText("Msg.AlreadyRunning"),
                UiText.GetText("App.Title"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
        WinTheme.ApplyResources();
        ToolTipPolicy.Register();

        var window = new MainWin();
        MainWindow = window;
        window.Show();
    }

    private static void RunLocalizationSmoke(string lang, string resultPath)
    {
        UiText.SetLang(lang);
        var keys = new[]
        {
            "App.Title",
            "Games.Title",
            "Games.SearchPlaceholder",
            "Games.FilterDetectedOnly",
            "Games.EmptyInstalled",
            "Shell.NoGameTitle",
            "Shell.NoGameHint",
            "Btn.Analyze",
            "Action.More",
            "Btn.Browse",
        };
        var values = keys.ToDictionary(key => key, UiText.GetText, StringComparer.Ordinal);
        var payload = new
        {
            Lang = lang,
            UiText.CurLang,
            UiPrefsLang = UiPrefs.LoadLang(),
            AppBase = AppContext.BaseDirectory,
            CurrentDirectory = Environment.CurrentDirectory,
            ResolvedLanguagePath = Path.Combine(AppContext.BaseDirectory, "Lang", UiText.CurLang + ".r3lang"),
            Values = values
        };
        File.WriteAllText(resultPath, JsonSerializer.Serialize(payload));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instanceGate?.Dispose();
        _instanceGate = null;
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        // Runs on the UI thread for any exception that escapes an event
        // handler or an `async void` ICommand (RelayCmd/AsyncCmd never
        // catch broadly by design, so this is the last line of defense).
        // Marking it Handled keeps the window and the user's in-progress
        // analysis/plan state alive instead of terminating the process.
        ReportUnexpectedError(e.Exception);
        e.Handled = true;
    }

    private static void OnDomainUnhandledException(
        object sender,
        UnhandledExceptionEventArgs e)
    {
        // Fires for exceptions on non-UI threads (e.g. inside Task.Run
        // work that itself has no try/catch). The CLR is already
        // terminating the process at this point and that cannot be
        // stopped from here, but the user still deserves to see what
        // happened instead of the app silently vanishing.
        if (e.ExceptionObject is Exception exception)
        {
            ReportUnexpectedError(exception);
        }
    }

    private static void OnUnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs e)
    {
        // Prevents an exception from a fire-and-forget Task whose
        // exception was never awaited/observed from reaching the
        // finalizer thread. .NET no longer crashes the process for this
        // by default, but marking it observed avoids it being reported
        // as a spurious crash/telemetry event later.
        e.SetObserved();
    }

    // 1 while an unexpected-error message box is on screen.
    private static int _reportingError;

    private static void ReportUnexpectedError(Exception exception)
    {
        // MessageBox pumps the dispatcher, so a fault that repeats (layout,
        // binding, timer) would otherwise stack a new modal box per
        // occurrence. The first report stays the one the user reads.
        if (Interlocked.Exchange(ref _reportingError, 1) == 1)
        {
            return;
        }

        try
        {
            // UiText.Fmt (App/Lang/UiText.cs) is this project's existing,
            // shared helper for every other localized "{0}"-style message
            // in the app (Msg.ApplyFailed, Msg.RestoreFailed, etc.). It
            // already resolves the template via UiText.GetText — which
            // tracks whichever language (ar-SA/en-US) is active right now
            // and can change at runtime via the language toggle — and
            // already formats with an explicit CultureInfo.CurrentCulture,
            // satisfying CA1305. Because the template is looked up at call
            // time rather than being a compile-time constant, it cannot be
            // pre-parsed into a cached CompositeFormat without freezing the
            // message to whichever language was active on first use, so
            // CA1863 (which only targets constant format strings) does not
            // apply here, exactly as it does not apply to the other
            // Msg.*Failed call sites that already go through Fmt.
            MessageBox.Show(
                UiText.Fmt("Msg.UnexpectedError", exception.Message),
                UiText.GetText("App.Title"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            // Reporting an unhandled exception must never itself throw;
            // swallow here rather than risk masking the original failure
            // with a secondary crash during process teardown.
        }
        finally
        {
            Interlocked.Exchange(ref _reportingError, 0);
        }
    }
}
