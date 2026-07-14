using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace R3Config.Svc;

public static class WindowThemeService
{
    private const int UseImmersiveDarkMode = 20;
    private const int UseImmersiveDarkModeLegacy = 19;

    public static void ApplyDwmSetWindowAttributeDarkMode(Window? window)
    {
        if (window is null)
        {
            return;
        }

        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            window.SourceInitialized -= Window_SourceInitialized;
            window.SourceInitialized += Window_SourceInitialized;
            return;
        }

        TryApplyDarkMode(handle);
    }

    private static void Window_SourceInitialized(
        object? sender,
        EventArgs e)
    {
        if (sender is not Window window)
        {
            return;
        }

        window.SourceInitialized -= Window_SourceInitialized;
        ApplyDwmSetWindowAttributeDarkMode(window);
    }

    private static void TryApplyDarkMode(IntPtr handle)
    {
        try
        {
            var enabled = 1;
            var result = DwmSetWindowAttribute(
                handle,
                UseImmersiveDarkMode,
                ref enabled,
                sizeof(int));

            if (result != 0)
            {
                _ = DwmSetWindowAttribute(
                    handle,
                    UseImmersiveDarkModeLegacy,
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
        ExactSpelling = true,
        PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute);
}