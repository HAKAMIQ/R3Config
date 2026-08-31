using System.IO;
using System.IO.Compression;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;

using System.Text;
using System.Text.Json;
using System.Threading;
using System.Xml;
using System.Windows;
using R3Config;
using R3Config.Svc;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Vm;
using R3Config.View;
using R3Config.Application;
using R3Config.ArtImport;
using R3Config.Composition;
using R3Config.Tests;

namespace R3Config.Tests;

internal static class TestFixtures
{
    internal static string FileHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
    }

    internal static WikiRec RecBuild()
    {
        return new WikiRec(
            Serial: "BLUS31465",
            WikiName: "Assassin's Creed Rogue",
            Changes:
            [
                new CfgChg(
                    Section: "Video",
                    Key: "Write Color Buffers",
                    Value: "true",
                    WikiKey: "write color buffers",
                    Notes: "Fixes ocean colors",
                    Risk: R3Config.Mdl.RecRisk.Rec),
                new CfgChg(
                    Section: "Video",
                    Key: "Read Color Buffers",
                    Value: "true",
                    WikiKey: "read color buffers",
                    Notes: "Fixes readback issue",
                    Risk: R3Config.Mdl.RecRisk.Rec),
                new CfgChg(
                    Section: "Video",
                    Key: "Resolution Scale",
                    Value: "100",
                    WikiKey: "resolution scale",
                    Notes: "Safe baseline",
                    Risk: R3Config.Mdl.RecRisk.Rec)
            ]);
    }

    internal static WikiRec ManlRec()
    {
        return new WikiRec(
            Serial: "BLUS31465",
            WikiName: "Assassin's Creed Rogue",
            Changes:
            [
                new CfgChg(
                    Section: "Video",
                    Key: "Vblank Rate",
                    Value: "120",
                    WikiKey: "vblank rate",
                    Notes: "60 FPS timing change",
                    Risk: R3Config.Mdl.RecRisk.Timing)
            ]);
    }

    internal static WikiRec LowRec()
    {
        return new WikiRec(
            Serial: "BLUS31465",
            WikiName: "Assassin's Creed Rogue",
            Changes:
            [
                new CfgChg(
                    Section: "Video",
                    Key: "Resolution Scale",
                    Value: "150",
                    WikiKey: "resolution scale",
                    Notes: "Hardware-dependent visual setting",
                    Risk: R3Config.Mdl.RecRisk.Dep)
            ]);
    }

    internal static string WikiXml()
    {
        return """
    <?xml version="1.0" encoding="utf-8"?>
    <mediawiki>
      <page>
        <title>Assassin's Creed Rogue</title>
        <revision>
          <text xml:space="preserve">{{vginfobox
    |gameid = {{gameid|id=BLUS31465, BLES02061|oid=BLJM61208}}
    |demoids = {{gameid|id=NPUB31547}}
    }}
    ==Configuration==
    {{config
    |ppu decoder = Interpreter (static) <!-- Interpreter (static), Interpreter (dynamic), Recompiler (LLVM) -->
    |ppu decoder notes = Fixes freezes
    |spu xfloat accuracy = Accurate
    |resolution scale threshold = 256 x 256
    |resolution scale threshold notes = Fixes bloom&lt;br&gt;See [[Help:Game Patches|game patches]] and {{issue|13546}}.
    |anisotropic filter = Auto
    |framelimit = Off
    |vsync = Full
    |asynchronous texture streaming = On
    |preferred spu threads = 1 - 3
    |disable spu getllar spin optimization = On
    |zcull accuracy = Relaxed
    |silence all logs = On
    |handle rsx memory tiling = On
    |rsx fifo accuracy = Ordered &amp; Atomic
    |accurate ppu 128 reservations = Always Enabled
    |lod bias offset = -1.5
    |write color buffers = On <!-- On, Off -->
    |write color buffers notes = Fixes ocean colors
    |read color buffers = On <!-- On, Off -->
    |read color buffers notes = May cause flickering on some systems
    |write depth buffers = On <!-- On, Off -->
    |write depth buffers notes = Required unless using FPS Patch
    |strict rendering mode = On <!-- On, Off -->
    |strict rendering mode notes = Reduces performance but fixes geometry
    |resolution scale = 150% <!-- Value between 50% to 800% -->
    |resolution scale notes = Optional quality setting
    |vblank rate = 120 <!-- Value between 1 to 500 -->
    |vblank rate notes = 60 FPS timing change
    }}</text>
        </revision>
      </page>
    </mediawiki>
    """;
    }

    internal static string RiskJson()
    {
        return """
    {
      "DataSource": "Built-in",
      "DataVersion": "2099-01-03",
      "RecommendationCount": 999,
      "Recommendations": [
        {
          "Serial": "TEST00001",
          "WikiTitle": "Risk Contract Test",
          "Changes": [
            {
              "Section": "Core",
              "Key": "PPU Decoder",
              "Value": "Interpreter (static)",
              "WikiKey": "ppu decoder",
              "Notes": "Stored risk is deliberately wrong.",
              "Risk": "Recommended"
            }
          ]
        }
      ]
    }
    """;
    }

    internal static string DupKeyJs()
    {
        return """
    {
      "DataSource": "Built-in",
      "DataVersion": "2099-01-04",
      "dataversion": "duplicate",
      "Recommendations": []
    }
    """;
    }

    internal static string DupSerJs()
    {
        return """
    {
      "DataSource": "Built-in",
      "DataVersion": "2099-01-04",
      "Recommendations": [
        {
          "Serial": "TEST00002",
          "WikiTitle": "First duplicate serial",
          "Changes": []
        },
        {
          "Serial": "test00002",
          "WikiTitle": "Second duplicate serial",
          "Changes": []
        }
      ]
    }
    """;
    }

    internal static string DupSetJs()
    {
        return """
    {
      "DataSource": "Built-in",
      "DataVersion": "2099-01-05",
      "Recommendations": [
        {
          "Serial": "TEST00003",
          "WikiTitle": "Duplicate setting",
          "Changes": [
            {
              "Section": "Video",
              "Key": "Write Color Buffers",
              "Value": "true",
              "WikiKey": "write color buffers",
              "Notes": "First value.",
              "Risk": "Recommended"
            },
            {
              "Section": "video",
              "Key": "write color buffers",
              "Value": "false",
              "WikiKey": "write color buffers",
              "Notes": "Conflicting duplicate.",
              "Risk": "Recommended"
            }
          ]
        }
      ]
    }
    """;
    }

    internal static string DupHash()
    {
        return """
    Version: 1.2

    Anchors:
      first_games: &first_games
        "First Game":
          BLUS00001: [ 01.00 ]

    PPU-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa:
      "First Patch":
        Games: *first_games
        Patch Version: 1.0
        Patch:
          - [ be32, 0x00000010, 0x60000000 ]

    Anchors:
      second_games: &second_games
        "Second Game":
          BLUS00002: [ 01.01 ]

    PPU-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa:
      "Second Patch":
        Games: *second_games
        Patch Version: 1.0
        Patch:
          - [ be32, 0x00000020, 0x60000000 ]
    """;
    }

    internal static string PatYaml()
    {
        return """
    Version: 1.2

    Anchors:
      sample_games: &sample_games
        "Assassin's Creed Rogue":
          BLUS31465: [ 01.02 ]

    PPU-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa:
      "Skip Intro Videos":
        Games: *sample_games
        Author: "Example"
        Notes: "Fixes intro hang."
        Patch Version: 1.0
        Patch:
          - [ be32, 0x00000010, 0x60000000 ]
      "60 FPS":
        Games: *sample_games
        Notes: "Forces 60 FPS and may require Vblank."
        Patch Version: 1.0
        Patch:
          - [ be32, 0x00000020, 0x60000000 ]
      "Infinite Health Cheat":
        Games: *sample_games
        Notes: "Cheat for testing."
        Patch Version: 1.0
        Patch:
          - [ be32, 0x00000030, 0x60000000 ]
      "Disable Motion Blur":
        Games: *sample_games
        Notes: "Disables motion blur."
        Patch Version: 1.0
        Patch:
          - [ byte, 0x00000040, 0x00 ]
      "Performance Shadows":
        Games: *sample_games
        Notes: "Improves performance by disabling expensive shadows."
        Patch Version: 1.0
        Patch:
          - [ byte, 0x00000050, 0x00 ]
      "Experimental Test Patch":
        Games: *sample_games
        Notes: "Experimental unstable test patch."
        Patch Version: 0.1
        Patch:
          - [ byte, 0x00000058, 0x01 ]
      "Unknown Memory Patch":
        Games: *sample_games
        Notes:
        Patch Version: 1.0
        Patch:
          - [ byte, 0x00000060, 0x01 ]

    Anchors:
      canary_games: &canary_games
        "Canary Game":
          BLES00001: [ 01.00 ]

    SPU-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb:
      "SPU Timing Patch":
        Games: *canary_games
        Author: "Example"
        Notes: "SPU timing test patch."
        Patch Version: 0.1
        Patch:
          - [ be32, 0x00000070, 0x60000000 ]
    """;
    }

    internal static string ImpYaml()
    {
        return """
    Version: 1.2

    PPU-dddddddddddddddddddddddddddddddddddddddd:
      "Imported Canary Display Patch":
        Games:
          "Assassin's Creed Rogue":
            BLUS31465: [ 01.02 ]
        Author: "Canary Import"
        Notes: "Imported Canary test patch."
        Patch Version: 0.1
        Patch:
          - [ be32, 0x00000080, 0x60000000 ]
    """;
    }

    internal static string ImpDup()
    {
        return """
    Version: 1.2

    PPU-dddddddddddddddddddddddddddddddddddddddd:
      "Imported Canary Display Patch":
        Games:
          "Assassin's Creed Rogue":
            BLUS31465: [ 01.02 ]
        Patch:
          - [ be32, 0x00000080, 0x60000000 ]
      "Imported Canary Display Patch":
        Games:
          "Assassin's Creed Rogue":
            BLUS31465: [ 01.02 ]
        Patch:
          - [ be32, 0x00000084, 0x60000000 ]
    """;
    }

    internal static string CfgYaml()
    {
        return """
    PPU-existingexistingexistingexistingexistingexisting:
      Existing Patch:
        Existing Game:
          BLUS00000:
            01.00:
              Enabled: true
    """;
    }

    internal static void BadSfo(string path)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false);

        writer.Write(0x46535000u);
        writer.Write(0x00000101u);
        writer.Write(1024u);
        writer.Write(2048u);
        writer.Write(1u);
    }

    internal static void SfoWrite(string path, IReadOnlyDictionary<string, string> values)
    {
        var keys = values.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray();
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false);

        var keyTable = new MemoryStream();
        var dataTable = new MemoryStream();
        var entries = new List<(ushort KeyOffset, ushort Format, uint Length, uint MaxLength, uint DataOffset)>();

        foreach (var key in keys)
        {
            var keyOffset = (ushort)keyTable.Position;
            keyTable.Write(Encoding.UTF8.GetBytes(key));
            keyTable.WriteByte(0);

            var dataOffset = (uint)dataTable.Position;
            var bytes = Encoding.UTF8.GetBytes(values[key] + "\0");
            dataTable.Write(bytes);

            entries.Add((keyOffset, 0x0204, (uint)bytes.Length, (uint)bytes.Length, dataOffset));
        }

        var keyTableOffset = 20u + (uint)(entries.Count * 16);
        var dataTableOffset = keyTableOffset + (uint)keyTable.Length;

        writer.Write(0x46535000u);
        writer.Write(0x00000101u);
        writer.Write(keyTableOffset);
        writer.Write(dataTableOffset);
        writer.Write((uint)entries.Count);

        foreach (var entry in entries)
        {
            writer.Write(entry.KeyOffset);
            writer.Write(entry.Format);
            writer.Write(entry.Length);
            writer.Write(entry.MaxLength);
            writer.Write(entry.DataOffset);
        }

        keyTable.Position = 0;
        keyTable.CopyTo(stream);
        dataTable.Position = 0;
        dataTable.CopyTo(stream);
    }

    internal static string EscPath(string path)
    {
        return path.Replace("\\", "\\\\");
    }

    internal static HCanHashOps EmptyHashOps()
    {
        static HashRead EmptyRead(
            Rpcs3InstallDescriptor install,
            Rpcs3GameDescriptor game)
        {
            return new HashRead(
                true,
                string.Empty,
                [],
                Code: HashReadCode.NoMatch);
        }

        return new HCanHashOps(
            (install, game) => new HCanDiscoverySession(
                install.ExePath,
                game.Serial,
                game.Version,
                game.InstPath,
                0,
                DateTime.UtcNow,
                Path.Combine(install.DataRoot ?? install.RootPath, "log", "RPCS3.log"),
                new HCanLogMark(false, 0, null, string.Empty)),
            (_, _) => { },
            _ => { },
            (_, _) => HashReadCode.NoMatch,
            EmptyRead,
            EmptyRead,
            (_, _) => false);
    }

}

internal sealed record SmokeEnv(
    string TempRoot,
    string FakeRoot,
    string CfgPath,
    string GameDir,
    string WikiPath,
    Rpcs3InstallDescriptor Install);

internal sealed record HwState(HwInfo LowEnd);

internal sealed record ScanStat(Rpcs3GameDescriptor Rogue);

internal sealed record CatState(
    WikiRec Rec,
    R3Config.Svc.RecSvc Catalog);

internal sealed record PlanStat(CfgPlan Plan, IReadOnlyList<CfgChg> SelChg);

internal sealed class CaptureProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value)
    {
        report(value);
    }
}

internal sealed class SyncCtx : SynchronizationContext
{
    private int _posts;

    public int Posts => Volatile.Read(ref _posts);

    public override void Post(
        SendOrPostCallback callback,
        object? state)
    {
        Interlocked.Increment(ref _posts);
        ThreadPool.QueueUserWorkItem(_ => callback(state));
    }
}
