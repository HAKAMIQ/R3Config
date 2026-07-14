using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using R3Config.Lang;
using R3Config.View.Dlg;

namespace R3Config.Svc;

public enum UiDialogKind
{
    Info,
    Warning,
    Error,
    Confirm
}

public static class UiDlg
{
    public static string? PickRpcs3Exe(string currentPath)
    {
        var dialog = new OpenFileDialog
        {
            Filter = UiText.T("Picker.Rpcs3Filter"),
            Title = UiText.T("Picker.Rpcs3Title"),
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false,
            DefaultExt = ".exe",
            FileName = "rpcs3.exe"
        };

        ApplyInitialDirectory(dialog, currentPath);

        return dialog.ShowDialog(ActiveOwner()) == true
            ? dialog.FileName
            : null;
    }

    public static string? PickWikiXml()
    {
        var dialog = new OpenFileDialog
        {
            Filter = UiText.T("Picker.WikiFilter"),
            Title = UiText.T("Picker.WikiTitle"),
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false,
            DefaultExt = ".xml"
        };

        return dialog.ShowDialog(ActiveOwner()) == true
            ? dialog.FileName
            : null;
    }

    public static string? PickCatExportPath()
    {
        var dialog = new SaveFileDialog
        {
            Filter = UiText.T("Picker.ExportCatFilter"),
            Title = UiText.T("Picker.ExportCatTitle"),
            FileName = UiText.T("Picker.ExportCatFileName"),
            AddExtension = true,
            CheckPathExists = true,
            OverwritePrompt = true,
            DefaultExt = ".json"
        };

        return dialog.ShowDialog(ActiveOwner()) == true
            ? dialog.FileName
            : null;
    }

    public static void Info(string message, string title)
    {
        Show(
            message,
            title,
            UiDialogKind.Info,
            isConfirm: false);
    }

    public static void Warn(string message, string title)
    {
        Show(
            message,
            title,
            UiDialogKind.Warning,
            isConfirm: false);
    }

    public static void Error(string message, string title)
    {
        Show(
            message,
            title,
            UiDialogKind.Error,
            isConfirm: false);
    }

    public static bool Confirm(string message, string title)
    {
        return Show(
            message,
            title,
            UiDialogKind.Confirm,
            isConfirm: true);
    }

    public static void CopyText(string text)
    {
        Clipboard.SetText(text);
    }

    private static bool Show(
        string message,
        string title,
        UiDialogKind kind,
        bool isConfirm)
    {
        var dialog = new AppDialog(
            ActiveOwner(),
            title,
            message,
            kind,
            isConfirm);

        return dialog.ShowDialog() == true;
    }

    private static void ApplyInitialDirectory(
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

    private static Window? ActiveOwner()
    {
        var current = Application.Current;
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