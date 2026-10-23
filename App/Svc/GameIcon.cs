using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using R3Config.Mdl;

namespace R3Config.Svc;

// Lazy, virtualized presentation loading. At most one decode and 32 cached
// previews (at most 512 x 512 each); offscreen Images release their references.
public static class GameIcon
{
    public const int MaxFileBytes = 2 * 1024 * 1024;
    public const int MaxDimension = 2048;
    public const int PreviewDimension = 512;
    public const int CacheCapacity = 32;
    private static readonly SemaphoreSlim Workers = new(1);
    private static readonly Dictionary<string, CacheEntry> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConditionalWeakTable<Image, Request> Requests = new();
    private sealed record CacheEntry(BitmapSource? Source, long Created);
    private sealed class Request
    {
        public CancellationTokenSource? Cancellation { get; set; }
    }

    public static readonly DependencyProperty GameProperty = DependencyProperty.RegisterAttached(
        "Game", typeof(Rpcs3GameDescriptor), typeof(GameIcon), new PropertyMetadata(null, GameChanged));

    public static Rpcs3GameDescriptor? GetGame(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (Rpcs3GameDescriptor?)element.GetValue(GameProperty);
    }

    public static void SetGame(DependencyObject element, Rpcs3GameDescriptor? value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(GameProperty, value);
    }

    public static async Task<BitmapSource?> LoadAsync(
        Rpcs3GameDescriptor game, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        await Workers.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => Load(game), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Workers.Release();
        }
    }

    private static BitmapSource? Load(Rpcs3GameDescriptor game)
    {
        try
        {
            var root = RuntimePathPolicy.RequireLocal(game.InstPath, "Game icon");
            var key = root + "|" + game.Serial;
            lock (Cache)
            {
                if (Cache.TryGetValue(key, out var cached) && Environment.TickCount64 - cached.Created < 30_000)
                {
                    return cached.Source;
                }
            }

            var path = Rpcs3GameIconLocator.Find(game);
            var bitmap = path is null ? null : Decode(path);
            lock (Cache)
            {
                if (!Cache.ContainsKey(key) && Cache.Count >= CacheCapacity)
                {
                    Cache.Remove(Cache.MinBy(pair => pair.Value.Created).Key);
                }

                Cache[key] = new CacheEntry(bitmap, Environment.TickCount64);
            }

            return bitmap;
        }
        catch (Exception exception) when (exception is
                   IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or
                   NotSupportedException or System.Security.SecurityException or
                   System.Runtime.InteropServices.COMException or FileFormatException)
        {
            return null;
        }
    }

    private static BitmapImage? Decode(string path)
    {
        try
        {
            return DecodePng(path);
        }
        catch (Exception exception) when (exception is
                   IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or
                   NotSupportedException or System.Security.SecurityException or
                   System.Runtime.InteropServices.COMException or FileFormatException)
        {
            return null;
        }
    }

    private static BitmapImage? DecodePng(string path)
    {
        byte[] bytes;
        using (var file = new FileStream(path, FileMode.Open, FileAccess.Read,
                   FileShare.ReadWrite | FileShare.Delete))
        {
            if (file.Length is < 33 or > MaxFileBytes)
            {
                return null;
            }

            bytes = new byte[(int)file.Length];
            file.ReadExactly(bytes);
            if (file.ReadByte() != -1)
            {
                return null;
            }
        }

        // Validate PNG/IHDR dimensions before invoking any native image codec.
        ReadOnlySpan<byte> header = bytes;
        if (!header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
            BinaryPrimitives.ReadUInt32BigEndian(header[8..12]) != 13 ||
            !header[12..16].SequenceEqual("IHDR"u8))
        {
            return null;
        }

        var width = BinaryPrimitives.ReadUInt32BigEndian(header[16..20]);
        var height = BinaryPrimitives.ReadUInt32BigEndian(header[20..24]);
        if (width is 0 or > MaxDimension || height is 0 or > MaxDimension)
        {
            return null;
        }

        using var memory = new MemoryStream(bytes, writable: false);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = memory;
        if (width >= height && width > PreviewDimension)
        {
            bitmap.DecodePixelWidth = PreviewDimension;
        }
        else if (height > PreviewDimension)
        {
            bitmap.DecodePixelHeight = PreviewDimension;
        }

        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static void GameChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not Image image)
        {
            throw new InvalidOperationException("GameIcon.Game requires an Image.");
        }

        image.Loaded -= Loaded;
        image.Unloaded -= Unloaded;
        image.Loaded += Loaded;
        image.Unloaded += Unloaded;
        Stop(image);
        if (image.IsLoaded)
        {
            Loaded(image, new RoutedEventArgs());
        }
    }

    private static async void Loaded(object sender, RoutedEventArgs args)
    {
        var image = (Image)sender;
        Stop(image);
        if (GetGame(image) is not { } game)
        {
            return;
        }

        var request = Requests.GetOrCreateValue(image);
        var cancellation = new CancellationTokenSource();
        request.Cancellation = cancellation;
        try
        {
            var source = await LoadAsync(game, cancellation.Token);
            if (image.IsLoaded && ReferenceEquals(request.Cancellation, cancellation))
            {
                image.Source = source;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // A recycled/unloaded row no longer owns this result.
        }
        finally
        {
            if (ReferenceEquals(request.Cancellation, cancellation))
            {
                request.Cancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private static void Unloaded(object sender, RoutedEventArgs args) => Stop((Image)sender);

    private static void Stop(Image image)
    {
        if (Requests.TryGetValue(image, out var request))
        {
            request.Cancellation?.Cancel();
            request.Cancellation = null;
        }

        image.Source = null;
    }
}
