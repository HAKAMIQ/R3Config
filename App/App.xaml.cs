using System.Windows;
using R3Config.Svc;
using R3Config.Lang;

namespace R3Config;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        UiText.SetLang(UiPrefs.LoadLang());
        UiLaySvc.Apply();
        base.OnStartup(e);

        var window = new MainWin();
        MainWindow = window;
        window.Show();
    }
}