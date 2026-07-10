using System.Windows;
using AppSvc;
using R3Config.Lang;

namespace R3Config;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        UiText.SetLang(UiPrefs.LoadLang());
        UiLayoutService.ApplyApplicationResources();
        base.OnStartup(e);

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }
}