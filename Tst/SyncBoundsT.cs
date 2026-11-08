using System;
using System.Globalization;
using R3Config.Svc;

namespace R3Config.Tests;

// Non-Enum RPCS3 settings: built-in defaults and numeric limits against a
// frozen snapshot of the RPCS3 source, plus the RecSync value gate
// (WikiSvc.IsSyncValue) at and around every numeric limit.
//
// Snapshot source (read-only reference, not read at test time):
//   rpcs3/Emu/system_config.h of the local RPCS3 checkout RPCS3-Source-Full
//   RPCS3 commit b94078bed10ccdb86c5e4b9f73bff8f479089fa4 (2026-11-06,
//   build v0.0.43-20568-b94078be)
//   SHA-256 d8277cb265ec05ad40303e70ae810ff57aaba1b2b044d2061c3ac2e39fc68a15
//   (CRLF checkout; the git blob is 7a3ab96799089ab316fcaee29c70570bed93fab7a6ebc1a87ed9f4c330ef713d)
// Enum settings are not part of the snapshot. The trailing number is the
// declaration line in system_config.h.
internal static class SyncBoundsT
{
    private static readonly (string Section, string Key, string Kind, string Default, string? Min, string? Max)[] Snapshot =
    [
        ("Core", "PPU Threads", "Int", "2", "1", "8"), // 18
        ("Core", "Preferred SPU Threads", "Int", "0", "0", "6"), // 34
        ("Core", "SPU loop detection", "Bool", "false", null, null), // 36
        ("Core", "Accurate SPU DMA", "Bool", "false", null, null), // 39
        ("Core", "Accurate RSX reservation access", "Bool", "false", null, null), // 44
        ("Core", "Disable SPU GETLLAR Spin Optimization", "Bool", "false", null, null), // 31
        ("Core", "Accurate PPU 128-byte Reservation Op Max Length", "Int", "0", "-1", "14"), // 67
        ("Core", "Max SPURS Threads", "Int", "6", "1", "6"), // 37
        ("Core", "Clocks scale", "Int", "100", "10", "3000"), // 83
        ("Core", "Use Accurate DFMA", "Bool", "true", null, null), // 71
        ("Core", "PPU Accurate Non-Java Mode", "Bool", "false", null, null), // 73
        ("Core", "PPU Accurate Vector NaN Values", "Bool", "false", null, null), // 74
        ("Core", "Debug Console Mode", "Bool", "false", null, null), // 78
        ("Core", "Hook static functions", "Bool", "false", null, null), // 79
        ("Core", "Libraries Control", "List", "", null, null), // 80
        ("Video", "Anisotropic Filter Override", "Int", "0", "0", "16"), // 172
        ("Video", "Resolution Scale", "Int", "100", "25", "800"), // 171
        ("Video", "Minimum Scalable Dimension", "Int", "16", "1", "1024"), // 174
        ("Video", "Write Color Buffers", "Bool", "false", null, null), // 135
        ("Video", "Read Color Buffers", "Bool", "false", null, null), // 137
        ("Video", "Write Depth Buffer", "Bool", "false", null, null), // 136
        ("Video", "Read Depth Buffer", "Bool", "false", null, null), // 138
        ("Video", "Handle RSX Memory Tiling", "Bool", "false", null, null), // 139
        ("Video", "Strict Rendering Mode", "Bool", "false", null, null), // 147
        ("Video", "Stretch To Display Area", "Bool", "false", null, null), // 145
        ("Video", "Multithreaded RSX", "Bool", "false", null, null), // 161
        ("Video", "Vulkan.Asynchronous Texture Streaming", "Bool", "false", null, null), // 197
        ("Video", "Disable On-Disk Shader Cache", "Bool", "false", null, null), // 157
        ("Video", "Disable Vertex Cache", "Bool", "false", null, null), // 151
        ("Video", "Driver Wake-Up Delay", "Int", "0", "0", "16667"), // 177
        ("Video", "Vblank Rate", "Int", "60", "1", "6000"), // 178
        ("Video", "Vblank NTSC Fixup", "Bool", "true", null, null), // 179
        ("Video", "Force CPU Blit", "Bool", "false", null, null), // 156
        ("Video", "Use GPU texture scaling", "Bool", "false", null, null), // 144
        ("Video", "Disable ZCull Occlusion Queries", "Bool", "false", null, null), // 149
        ("Video", "Texture LOD Bias Addend", "Float", "0", "-32", "32"), // 173
        ("Video", "Allow Host GPU Labels", "Bool", "false", null, null), // 181
        ("Video", "Force Hardware MSAA Resolve", "Bool", "false", null, null), // 163
        ("Video", "Emulate Special Depth Comparison", "Bool", "false", null, null), // 153
        ("Video", "Disable Asynchronous Memory Manager", "Bool", "false", null, null), // 183
        ("Video", "Relaxed ZCULL Sync", "Bool", "false", null, null), // 162
        ("Video", "Accurate ZCULL stats", "Bool", "true", null, null), // 168
        ("Audio", "Dump to file", "Bool", "false", null, null), // 268
        ("Audio", "Convert to 16 bit", "Bool", "false", null, null), // 269
        ("Audio", "Master Volume", "Int", "100", "0", "200"), // 274
        ("Audio", "Enable Buffering", "Bool", "true", null, null), // 275
        ("Audio", "Desired Audio Buffer Duration", "Int", "34", "4", "250"), // 276
        ("Audio", "Enable Time Stretching", "Bool", "false", null, null), // 277
        ("Audio", "Time Stretching Threshold", "Int", "75", "0", "100"), // 279
        ("Miscellaneous", "Silence All Logs", "Bool", "false", null, null) // 393
    ];

    public static void Run()
    {
        BuiltInDefaultsMatchSnapshot();
        SyncValueLimits();
        NullHandlerValuesAreYamlValues();
    }

    private static void BuiltInDefaultsMatchSnapshot()
    {
        Assert(Snapshot.Length == 50,
            "The RPCS3 snapshot must cover the 50 non-Enum keys that matched system_config.h.");

        foreach (var row in Snapshot)
        {
            var actual = PlanService.ReadBuiltIn(row.Section, row.Key);
            Assert(string.Equals(actual, row.Default, StringComparison.Ordinal),
                $"Built-in default for {row.Section}/{row.Key} is '{actual}' but system_config.h declares '{row.Default}'.");

            if (row.Kind is "Int" or "Float")
            {
                var min = decimal.Parse(row.Min!, CultureInfo.InvariantCulture);
                var max = decimal.Parse(row.Max!, CultureInfo.InvariantCulture);
                var value = decimal.Parse(row.Default, CultureInfo.InvariantCulture);
                Assert(value >= min && value <= max,
                    $"Built-in default for {row.Section}/{row.Key} lies outside its RPCS3 limits.");
            }
        }
    }

    private static void SyncValueLimits()
    {
        var numeric = 0;
        foreach (var row in Snapshot)
        {
            var path = $"{row.Section}/{row.Key}";
            Assert(WikiSvc.TryGetSyncTarget(row.Section, row.Key, out var wikiKey),
                $"{path} must be a sync target.");

            if (row.Kind is "Int" or "Float")
            {
                Assert(WikiSvc.NumericBounds.TryGetValue(wikiKey, out var bound) &&
                       bound.Min == decimal.Parse(row.Min!, CultureInfo.InvariantCulture) &&
                       bound.Max == decimal.Parse(row.Max!, CultureInfo.InvariantCulture) &&
                       bound.Fractional == (row.Kind == "Float"),
                    $"The shared numeric-limit table must match system_config.h for {path}.");
            }

            switch (row.Kind)
            {
                case "Bool":
                    Assert(WikiSvc.IsSyncValue(row.Section, row.Key, "true") &&
                           WikiSvc.IsSyncValue(row.Section, row.Key, "false") &&
                           !WikiSvc.IsSyncValue(row.Section, row.Key, "maybe") &&
                           !WikiSvc.IsSyncValue(row.Section, row.Key, "1"),
                        $"{path} must accept only true/false.");
                    break;

                case "Int":
                    numeric++;
                    IntegerLimits(row.Section, row.Key, int.Parse(row.Min!, CultureInfo.InvariantCulture), int.Parse(row.Max!, CultureInfo.InvariantCulture));
                    break;

                case "Float":
                    numeric++;
                    FractionalLimits(row.Section, row.Key, decimal.Parse(row.Min!, CultureInfo.InvariantCulture), decimal.Parse(row.Max!, CultureInfo.InvariantCulture));
                    break;
            }
        }

        Assert(numeric == 14 && WikiSvc.NumericBounds.Count == 14,
            "Fourteen numeric settings carry RPCS3 limits, in the snapshot and in the shared table.");
    }

    private static void IntegerLimits(string section, string key, int min, int max)
    {
        var path = $"{section}/{key}";
        Assert(Accepts(section, key, min) && Accepts(section, key, max),
            $"{path} must accept exactly its limits {min} and {max}.");
        Assert(Accepts(section, key, (min + max) / 2),
            $"{path} must accept a value inside its limits.");
        Assert(!Accepts(section, key, (long)min - 1) && !Accepts(section, key, (long)max + 1),
            $"{path} must reject {min - 1L} and {max + 1L}, one step outside its RPCS3 limits.");
        Assert(!Accepts(section, key, 1_000_000) && !Accepts(section, key, -1_000_000) &&
               !Accepts(section, key, int.MaxValue),
            $"{path} must reject far-out-of-range integers.");
        Assert(!WikiSvc.IsSyncValue(section, key, "1.5") &&
               !WikiSvc.IsSyncValue(section, key, "four") &&
               !WikiSvc.IsSyncValue(section, key, "") &&
               !WikiSvc.IsSyncValue(section, key, " 1"),
            $"{path} is an integer setting and must reject fractional or non-numeric text.");
    }

    private static void FractionalLimits(string section, string key, decimal min, decimal max)
    {
        var path = $"{section}/{key}";
        Assert(AcceptsText(section, key, min) && AcceptsText(section, key, max) &&
               AcceptsText(section, key, 0.5m) && AcceptsText(section, key, -31.99m) &&
               AcceptsText(section, key, 31.99m),
            $"{path} is a float setting: its limits and fractional values inside them must be accepted.");
        Assert(!AcceptsText(section, key, max + 0.01m) && !AcceptsText(section, key, min - 0.01m) &&
               !AcceptsText(section, key, max + 0.000001m) && !AcceptsText(section, key, 1_000_000m),
            $"{path} must reject fractional values just outside its limits.");
        Assert(!WikiSvc.IsSyncValue(section, key, "abc") &&
               !WikiSvc.IsSyncValue(section, key, "1e1") &&
               !WikiSvc.IsSyncValue(section, key, "0,5"),
            $"{path} must reject non-decimal text.");
    }

    // The Wiki writes "Disabled" for these three settings and Wiki.Convert maps it
    // to "Null"; the RPCS3 YAML only ever holds the to_string text of the enum
    // (system_config_types.cpp: video_renderer, audio_renderer, microphone_handler).
    // RecSync values are YAML values, so "Null" is valid and "Disabled" is not.
    // The value lists are the to_string cases at RPCS3 b94078b ("Eye Toy" and
    // "PS Eye" were added to microphone_handler in 2d103407, build 20013).
    private static void NullHandlerValuesAreYamlValues()
    {
        (string Section, string Key, string[] Valid)[] rows =
        [
            ("Video", "Renderer", ["Null", "OpenGL", "Vulkan"]),
            ("Audio", "Renderer", ["Null", "XAudio2", "Cubeb", "FAudio"]),
            ("Audio", "Microphone Type", ["Null", "Standard", "SingStar", "Real SingStar", "Rocksmith", "Eye Toy", "PS Eye"])
        ];

        foreach (var row in rows)
        {
            var path = $"{row.Section}/{row.Key}";
            foreach (var valid in row.Valid)
            {
                Assert(WikiSvc.IsSyncValue(row.Section, row.Key, valid),
                    $"{path} must accept the RPCS3 YAML value '{valid}'.");
            }

            Assert(!WikiSvc.IsSyncValue(row.Section, row.Key, "Disabled"),
                $"{path} must reject 'Disabled': it is Wiki wording, RPCS3 writes 'Null'.");
            Assert(!WikiSvc.IsSyncValue(row.Section, row.Key, "Turbo") &&
                   !WikiSvc.IsSyncValue(row.Section, row.Key, ""),
                $"{path} must reject unknown text.");
        }
    }

    private static bool Accepts(string section, string key, long value) =>
        WikiSvc.IsSyncValue(section, key, value.ToString(CultureInfo.InvariantCulture));

    private static bool AcceptsText(string section, string key, decimal value) =>
        WikiSvc.IsSyncValue(section, key, value.ToString(CultureInfo.InvariantCulture));
}
