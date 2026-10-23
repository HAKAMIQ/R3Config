using System.Collections.Generic;

#pragma warning disable IDE0130

namespace R3Config.Svc;

// Win32 system error code -> symbol name(s), generated from the Windows SDK
// winerror.h (Win32ErrorSymbols.g.cs; R3Test --gen-win32-symbols). The table
// carries only "#define ERROR_x <number>" facts of the primary source; the
// description text is whatever the log itself printed, never a copied string.
public static partial class Win32ErrorSymbols
{
    public static string? Lookup(int code) =>
        Table.TryGetValue(code, out var symbol) ? symbol : null;

    private static partial IReadOnlyDictionary<int, string> BuildTable();

    private static IReadOnlyDictionary<int, string> Table { get; } = BuildTable();

    public static int Count => Table.Count;
}

#pragma warning restore IDE0130
