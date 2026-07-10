using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AppSvc;

public static class WindowThemeService
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_LEGACY = 19;

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

    private static void Window_SourceInitialized(object? sender, EventArgs e)
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
                DWMWA_USE_IMMERSIVE_DARK_MODE,
                ref enabled,
                Marshal.SizeOf<int>());

            if (result != 0)
            {
                _ = DwmSetWindowAttribute(
                    handle,
                    DWMWA_USE_IMMERSIVE_DARK_MODE_LEGACY,
                    ref enabled,
                    Marshal.SizeOf<int>());
            }
        }
        catch (DllNotFoundException)
        {
            // DWM dark mode is cosmetic. Ignore when unavailable.
        }
        catch (EntryPointNotFoundException)
        {
            // DWM dark mode is cosmetic. Ignore when unavailable.
        }
        catch (Win32Exception)
        {
            // DWM dark mode is cosmetic. Ignore when unavailable.
        }
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute);
}