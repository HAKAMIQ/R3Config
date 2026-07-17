using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Microsoft.Win32;

namespace R3Config.Svc;

public static class WinTheme
{
    private const int DarkMode = 20;
    private const int DarkOld = 19;

    private static readonly string[] SemanticBrushes =
    [
        "Surface.Background",
        "Surface.Card",
        "Surface.CardHover",
        "Surface.CardPressed",
        "Surface.Subtle",
        "Surface.Overlay",
        "Text.Primary",
        "Text.Secondary",
        "Text.Disabled",
        "Text.OnAccent",
        "Stroke.Default",
        "Stroke.Subtle",
        "Stroke.Strong",
        "Stroke.Focus",
        "Accent.Default",
        "Accent.Hover",
        "Accent.Pressed",
        "Accent.Disabled",
        "Status.Success",
        "Status.Warning",
        "Status.Danger",
        "Status.Info",
        "Status.SuccessSurface",
        "Status.WarningSurface",
        "Status.DangerSurface",
        "Status.InfoSurface",
        "Selection.Default",
        "Selection.Inactive",
        "Scroll.Track",
        "Scroll.Thumb",
        "Scroll.ThumbHover"
    ];

    private static bool _isDark = true;

    public static void ApplyResources()
    {
        ApplyResources(!UsesLightApps());
    }

    public static void ApplyResources(bool isDark)
    {
        var app = System.Windows.Application.Current;
        if (app is null)
        {
            return;
        }

        _isDark = isDark;
        var palette = isDark ? "Fluent.Dark" : "Fluent.Light";

        foreach (var key in SemanticBrushes)
        {
            if (app.TryFindResource($"{palette}.{key}.Color") is not Color color ||
                app.TryFindResource(key) is not SolidColorBrush brush)
            {
                continue;
            }

            if (brush.IsFrozen)
            {
                app.Resources[key] = new SolidColorBrush(color);
            }
            else
            {
                brush.Color = color;
            }
        }

        if (app.TryFindResource($"{palette}.Elevation.Shadow.Color") is Color shadow)
        {
            foreach (var key in new[] { "Elevation.1", "Elevation.2", "Elevation.3" })
            {
                if (app.TryFindResource(key) is DropShadowEffect effect && !effect.IsFrozen)
                {
                    effect.Color = shadow;
                }
            }
        }

        app.Resources["Fluent.Theme.Mode"] = isDark ? "Dark" : "Light";
    }

    public static void Apply(Window? window)
    {
        if (window is null)
        {
            return;
        }

        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            window.SourceInitialized -= WinInit;
            window.SourceInitialized += WinInit;
            return;
        }

        TryApply(handle);
    }

    private static bool UsesLightApps()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        }
        catch (Exception ex) when (
            ex is UnauthorizedAccessException or
            System.Security.SecurityException)
        {
            return false;
        }
    }

    private static void WinInit(
        object? sender,
        EventArgs e)
    {
        if (sender is not Window window)
        {
            return;
        }

        window.SourceInitialized -= WinInit;
        Apply(window);
    }

    private static void TryApply(IntPtr handle)
    {
        try
        {
            var enabled = _isDark ? 1 : 0;
            var result = DwmSet(
                handle,
                DarkMode,
                ref enabled,
                sizeof(int));

            if (result != 0)
            {
                _ = DwmSet(
                    handle,
                    DarkOld,
                    ref enabled,
                    sizeof(int));
            }
        }
        catch (Exception ex) when (
            ex is DllNotFoundException or
            EntryPointNotFoundException or
            Win32Exception)
        {
        }
    }

    [DllImport(
        "dwmapi.dll",
        EntryPoint = "DwmSetWindowAttribute",
        ExactSpelling = true,
        PreserveSig = true)]
    private static extern int DwmSet(
        IntPtr hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute);
}
