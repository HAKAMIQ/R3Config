using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using R3Config.Lang;
using R3Config.View.Dlg;

namespace R3Config.Svc;

public enum DlgKind
{
    Info,
    Warning,
    Error,
    Confirm
}

public static class UiDlg
{
    public static string? PickExe(string currentPath)
    {
        var dialog = new OpenFileDialog
        {
            Filter = UiText.GetText("Picker.Rpcs3Filter"),
            Title = UiText.GetText("Picker.Rpcs3Title"),
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false,
            DefaultExt = ".exe",
            FileName = "rpcs3.exe"
        };

        SetDir(dialog, currentPath);

        return dialog.ShowDialog(Owner()) == true
            ? dialog.FileName
            : null;
    }

    public static string? PickWiki()
    {
        var dialog = new OpenFileDialog
        {
            Filter = UiText.GetText("Picker.WikiFilter"),
            Title = UiText.GetText("Picker.WikiTitle"),
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false,
            DefaultExt = ".xml"
        };

        return dialog.ShowDialog(Owner()) == true
            ? dialog.FileName
            : null;
    }

    // Only the external NCL source ever browses. The bundled corpus lives inside
    // the application and is never a browsing target, so this opens where the
    // user last took an NCL from and otherwise in their Documents folder.
    public static string? PickNcl(string currentPath)
    {
        var dialog = new OpenFileDialog
        {
            Filter = UiText.GetText("Patch.Ncl.FileFilter"),
            Title = UiText.GetText("Patch.Ncl.ChooseFile"),
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false,
            DefaultExt = ".ncl"
        };

        SetDir(
            dialog,
            string.IsNullOrWhiteSpace(currentPath)
                ? Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments)
                : currentPath);

        return dialog.ShowDialog(Owner()) == true
            ? dialog.FileName
            : null;
    }
    public static string? PickExp()
    {
        var dialog = new SaveFileDialog
        {
            Filter = UiText.GetText("Picker.ExportCatFilter"),
            Title = UiText.GetText("Picker.ExportCatTitle"),
            FileName = UiText.GetText("Picker.ExportCatFileName"),
            AddExtension = true,
            CheckPathExists = true,
            OverwritePrompt = true,
            DefaultExt = ".json"
        };

        return dialog.ShowDialog(Owner()) == true
            ? dialog.FileName
            : null;
    }

    public static void Info(string message, string title)
    {
        Show(
            message,
            title,
            DlgKind.Info,
            isConfirm: false);
    }

    public static void Warn(string message, string title)
    {
        Show(
            message,
            title,
            DlgKind.Warning,
            isConfirm: false);
    }

    public static void Error(string message, string title)
    {
        Show(
            message,
            title,
            DlgKind.Error,
            isConfirm: false);
    }

    public static bool Confirm(string message, string title)
    {
        return Show(
            message,
            title,
            DlgKind.Confirm,
            isConfirm: true);
    }

    public static void CopyText(string text)
    {
        Clipboard.SetText(text);
    }

    private static bool Show(
        string message,
        string title,
        DlgKind kind,
        bool isConfirm)
    {
        var dialog = new AppDlg(
            Owner(),
            title,
            message,
            kind,
            isConfirm);

        return dialog.ShowDialog() == true;
    }

    private static void SetDir(
        FileDialog dialog,
        string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        string? directory = null;

        if (File.Exists(path))
        {
            directory = Path.GetDirectoryName(path);
        }
        else if (Directory.Exists(path))
        {
            directory = path;
        }

        if (!string.IsNullOrWhiteSpace(directory))
        {
            dialog.InitialDirectory = directory;
        }
    }

    private static Window? Owner()
    {
        var current = System.Windows.Application.Current;

        if (current is null)
        {
            return null;
        }

        return current.Windows
                   .OfType<Window>()
                   .FirstOrDefault(window => window.IsActive)
               ?? current.MainWindow;
    }
}