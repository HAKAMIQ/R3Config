using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace R3Config.Svc;

public static class WinTheme
{
    private const int DarkMode = 20;
    private const int DarkOld = 19;

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
            var enabled = 1;
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
