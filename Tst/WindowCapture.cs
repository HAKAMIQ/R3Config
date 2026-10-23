using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace R3Config.Tests;

// Captures what Windows actually composes for another process's top-level
// window (PrintWindow with PW_RENDERFULLCONTENT), so the evidence is the real
// rendered R3Config window rather than an in-process re-layout.
internal static class WindowCapture
{
    private const uint PwRenderFullContent = 2;
    private const uint DibRgbColors = 0;
    private const int GwlExStyle = -20;
    private const int WsExLayoutRtl = 0x00400000;
    private const int WsExLayered = 0x00080000;
    private const ushort VkTab = 0x09;
    private const ushort VkShift = 0x10;

    public static void SavePng(
        IntPtr window,
        string path)
    {
        var (pixels, width, height) = Capture(window);
        var source = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            width * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    // Mean Rec. 709 luminance (0-255) of the composed window.
    public static double MeanLuminance(
        IntPtr window)
    {
        var (pixels, _, _) = Capture(window);
        double total = 0;
        for (var index = 0; index < pixels.Length; index += 4)
        {
            total += (0.0722 * pixels[index]) + (0.7152 * pixels[index + 1]) + (0.2126 * pixels[index + 2]);
        }

        return total / (pixels.Length / 4);
    }

    private static (byte[] Pixels, int Width, int Height) Capture(
        IntPtr window)
    {
        if (!GetWindowRect(window, out var rect))
        {
            throw new InvalidOperationException("GetWindowRect failed for the captured window.");
        }

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("The captured window has no area.");
        }

        var screen = GetDC(IntPtr.Zero);
        var memory = CreateCompatibleDC(screen);
        var bitmap = CreateCompatibleBitmap(screen, width, height);
        var previous = SelectObject(memory, bitmap);
        try
        {
            if (!PrintWindow(window, memory, PwRenderFullContent))
            {
                throw new InvalidOperationException("PrintWindow failed for the captured window.");
            }

            var header = new BitmapInfoHeader
            {
                Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                Width = width,
                Height = -height,
                Planes = 1,
                BitCount = 32
            };
            var stride = width * 4;
            var pixels = new byte[stride * height];
            SelectObject(memory, previous);
            if (GetDIBits(memory, bitmap, 0, (uint)height, pixels, ref header, DibRgbColors) != height)
            {
                throw new InvalidOperationException("GetDIBits did not return every captured row.");
            }

            for (var alpha = 3; alpha < pixels.Length; alpha += 4)
            {
                pixels[alpha] = 255;
            }

            // A non-layered RTL window (WS_EX_LAYOUTRTL) is copied through a
            // mirrored DC; flip each row back so the image matches the screen.
            // A layered window (WS_EX_LAYERED) is already captured unmirrored.
            var exStyle = GetWindowLong(window, GwlExStyle);
            if ((exStyle & WsExLayoutRtl) != 0 && (exStyle & WsExLayered) == 0)
            {
                for (var row = 0; row < height; row++)
                {
                    Array.Reverse(pixels, row * stride, stride);
                    for (var pixel = row * stride; pixel < (row + 1) * stride; pixel += 4)
                    {
                        Array.Reverse(pixels, pixel, 4);
                    }
                }
            }

            return (pixels, width, height);
        }
        finally
        {
            DeleteObject(bitmap);
            DeleteDC(memory);
            _ = ReleaseDC(IntPtr.Zero, screen);
        }
    }

    public static void Hover(
        Rect screenBounds)
    {
        var x = (int)Math.Round(screenBounds.Left + (screenBounds.Width / 2));
        var y = (int)Math.Round(screenBounds.Top + (screenBounds.Height / 2));
        if (!SetCursorPos(x, y))
        {
            throw new InvalidOperationException("SetCursorPos failed while hovering the target row.");
        }
    }

    // Real keyboard input to the foreground window: Tab, or Shift+Tab.
    public static void PressTab(
        bool shift)
    {
        var inputs = new List<KeyInput>();
        if (shift)
        {
            inputs.Add(Key(VkShift, up: false));
        }

        inputs.Add(Key(VkTab, up: false));
        inputs.Add(Key(VkTab, up: true));
        if (shift)
        {
            inputs.Add(Key(VkShift, up: true));
        }

        var array = inputs.ToArray();
        if (SendInput((uint)array.Length, array, Marshal.SizeOf<KeyInput>()) != array.Length)
        {
            throw new InvalidOperationException("SendInput did not deliver the Tab keystroke.");
        }
    }

    private static KeyInput Key(
        ushort key,
        bool up)
    {
        return new KeyInput
        {
            Type = 1,
            Keyboard = new KeyboardInput
            {
                VirtualKey = key,
                Flags = up ? 2u : 0u
            }
        };
    }

    public static void Foreground(
        IntPtr window)
    {
        _ = SetForegroundWindow(window);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    // INPUT is a union sized by its largest member (MOUSEINPUT); the padding
    // keeps Marshal.SizeOf equal to the native sizeof(INPUT).
    [StructLayout(LayoutKind.Explicit)]
    private struct KeyInput
    {
        [FieldOffset(0)]
        public uint Type;

        [FieldOffset(8)]
        public KeyboardInput Keyboard;

        [FieldOffset(8)]
        private readonly MousePadding _padding;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MousePadding
    {
        public int X;
        public int Y;
        public uint Data;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr window, int index);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, KeyInput[] inputs, int size);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr window, IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int width, int height);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr dc, IntPtr gdiObject);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr gdiObject);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(
        IntPtr dc,
        IntPtr bitmap,
        uint start,
        uint lines,
        [Out] byte[] bits,
        ref BitmapInfoHeader header,
        uint usage);
}
