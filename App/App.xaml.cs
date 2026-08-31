using System.Windows;
using System.IO;
using System.Linq;
using System.Text.Json;
using R3Config.Lang;
using R3Config.Svc;

namespace R3Config;

public partial class App : System.Windows.Application
{
    private AppInstanceGate? _instanceGate;

    protected override void OnStartup(StartupEventArgs e)
    {
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
            ResolvedLanguagePath = Path.Combine(AppContext.BaseDirectory, "Lang", UiText.CurLang + ".json"),
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
}
