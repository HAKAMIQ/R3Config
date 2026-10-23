using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using R3Config.Mdl;
using R3Config.Svc;

// Offline disc/identity knowledge adapted from the discord-bot audit: IRD import
// and the observed-anomaly vs IRD-verified distinction, the local game-update
// snapshot contract, exact-identifier and Unicode safety, hardware normalization
// with raw evidence preserved. Synthetic IRDs are built by the writer below from
// the public IRD layout; validation against a REAL .ird file is still pending
// (none exists locally), and the report says so.
internal static class DiscKnowledgeT
{
    public static void Run(string root)
    {
        AssertIrdParsingAndComparison();
        AssertDiscFailuresAreObservationsOnly();
        AssertUpdateSnapshotContract(root);
        AssertExactIdentityAndUnicode();
        AssertHardwareNormalization();
        AssertInstallPathTraits();

        Console.WriteLine("Disc/identity knowledge (IRD import + observed-vs-verified, update snapshot contract, exact identity vs Unicode, hardware raw/normalized) PASS.");
    }

    // ------------------------------------------------------------------ IRD

    private static byte[] IsoHeader(params (string Path, long Lba, long Size)[] files)
    {
        const int sector = 2048;
        var header = new byte[40 * sector];
        var pvd = 16 * sector;
        header[pvd] = 1;
        "CD001"u8.CopyTo(header.AsSpan(pvd + 1));
        var root = pvd + 156;
        header[root] = 34;
        BitConverter.GetBytes(20u).CopyTo(header, root + 2);
        BitConverter.GetBytes(2048u).CopyTo(header, root + 10);
        header[root + 25] = 2;
        header[root + 32] = 1;

        // Directory tree: 20 root, 21 PS3_GAME, 22 USRDIR.
        var directories = new Dictionary<string, (int Lba, List<(string Name, long Lba, long Size, bool Dir)> Entries)>
        {
            [string.Empty] = (20, []),
            ["PS3_GAME"] = (21, []),
            ["PS3_GAME/USRDIR"] = (22, [])
        };
        directories[string.Empty].Entries.Add(("PS3_GAME", 21, 2048, true));
        directories["PS3_GAME"].Entries.Add(("USRDIR", 22, 2048, true));
        foreach (var (path, lba, size) in files)
        {
            var slash = path.LastIndexOf('/');
            var directory = slash < 0 ? string.Empty : path[..slash];
            directories[directory].Entries.Add((path[(slash + 1)..], lba, size, false));
        }

        foreach (var (_, (dirLba, entries)) in directories)
        {
            var position = dirLba * sector;
            void Write(string name, long lba, long size, bool dir)
            {
                var nameBytes = Encoding.ASCII.GetBytes(name);
                var length = 33 + nameBytes.Length;
                length += length % 2;
                header[position] = (byte)length;
                BitConverter.GetBytes((uint)lba).CopyTo(header, position + 2);
                BitConverter.GetBytes((uint)size).CopyTo(header, position + 10);
                header[position + 25] = dir ? (byte)2 : (byte)0;
                header[position + 32] = (byte)nameBytes.Length;
                nameBytes.CopyTo(header, position + 33);
                position += length;
            }

            Write("\0", dirLba, 2048, true);
            Write("\u0001", dirLba, 2048, true);
            foreach (var (name, lba, size, dir) in entries)
            {
                Write(dir ? name : name + ";1", lba, size, dir);
            }
        }

        return header;
    }

    private static byte[] Gzip(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(data);
        }

        return output.ToArray();
    }

    private static byte[] BuildIrd(byte version, string productCode, string title, byte[] isoHeader, (long Offset, byte Fill)[] hashes, bool corruptCrc = false, bool compress = true)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("3IRD"u8.ToArray());
        writer.Write(version);
        writer.Write(Encoding.ASCII.GetBytes(productCode.PadRight(9)[..9]));
        var titleBytes = Encoding.UTF8.GetBytes(title);
        writer.Write((byte)titleBytes.Length);
        writer.Write(titleBytes);
        writer.Write(Encoding.ASCII.GetBytes("0000"));
        writer.Write(Encoding.ASCII.GetBytes("01.00"));
        writer.Write(Encoding.ASCII.GetBytes("01.00"));
        if (version == 7)
        {
            writer.Write(1234);
        }

        var header = Gzip(isoHeader);
        writer.Write(header.Length);
        writer.Write(header);
        var footer = Gzip([1, 2, 3]);
        writer.Write(footer.Length);
        writer.Write(footer);
        writer.Write((byte)0);
        writer.Write(hashes.Length);
        foreach (var (offset, fill) in hashes)
        {
            writer.Write(offset);
            writer.Write(Enumerable.Repeat(fill, 16).ToArray());
        }

        writer.Write(0);
        if (version == 9)
        {
            writer.Write(new byte[115]);
        }

        writer.Write(new byte[32]);
        if (version < 9)
        {
            writer.Write(new byte[115]);
        }

        writer.Write(77);
        writer.Flush();
        var body = stream.ToArray();
        var crc = IrdParser.Crc32(body);
        var result = body.Concat(BitConverter.GetBytes(corruptCrc ? crc ^ 1u : crc)).ToArray();
        return compress ? Gzip(result) : result;
    }

    private static void AssertIrdParsingAndComparison()
    {
        var header = IsoHeader(("PS3_GAME/USRDIR/EBOOT.BIN", 100, 4096), ("PS3_GAME/USRDIR/data.bin", 200, 8192), ("PS3_GAME/PARAM.SFO", 50, 512));
        foreach (var version in new byte[] { 6, 7, 8, 9 })
        {
            var ird = IrdParser.Parse(BuildIrd(version, "TEST00002", "Synthetic Disc", header, [(100, 0xAA), (200, 0xBB), (50, 0xCC)]));
            Assert(ird.Version == version && ird.ProductCode == "TEST00002" && ird.Title == "Synthetic Disc" && ird.FileHashes.Count == 3 &&
                   ird.DiscListingComplete &&
                   ird.DiscFiles.Select(file => file.Path).SequenceEqual(["PS3_GAME/PARAM.SFO", "PS3_GAME/USRDIR/EBOOT.BIN", "PS3_GAME/USRDIR/data.bin"]) &&
                   ird.DiscFiles.Single(file => file.Path.EndsWith("data.bin", StringComparison.Ordinal)).Lba == 200 &&
                   ird.SourceSha256.Length == 64,
                $"IRD version {version} (gzip): identity, hash entries and the ISO 9660 file list parse.");
        }

        var raw = IrdParser.Parse(BuildIrd(9, "TEST00002", "Raw", header, [(100, 1)], compress: false));
        Assert(raw.Title == "Raw", "An uncompressed IRD parses too.");

        void Rejected(byte[] data, string because)
        {
            var thrown = false;
            try
            {
                IrdParser.Parse(data);
            }
            catch (IrdFormatException)
            {
                thrown = true;
            }

            Assert(thrown, because);
        }

        Rejected(BuildIrd(9, "TEST00002", "X", header, [(100, 1)], corruptCrc: true), "A checksum mismatch is rejected.");
        Rejected(BuildIrd(5, "TEST00002", "X", header, [(100, 1)]), "An unsupported IRD version is rejected.");
        Rejected(new byte[300], "Data without the 3IRD signature is rejected.");
        Rejected(new byte[10], "Data that is too small is rejected.");
        var truncated = BuildIrd(9, "TEST00002", "X", header, [(100, 1)], compress: false);
        Rejected(truncated[..(truncated.Length / 2)], "A truncated IRD is rejected.");

        // Observed anomaly vs IRD-verified mismatch.
        var info = IrdParser.Parse(BuildIrd(9, "TEST00002", "Synthetic Disc", header, [(100, 0xAA)]));
        var observed = new[] { "PS3_GAME/USRDIR/EBOOT.BIN", "PS3_GAME/USRDIR/filemap.txt", "PS3_GAME/USRDIR/EBOOT.BIN" };
        var withIrd = IrdComparer.Compare(info, observed);
        Assert(withIrd.Count == 2 &&
               withIrd.Single(item => item.Path.EndsWith("EBOOT.BIN", StringComparison.Ordinal)).State == IrdCheckState.ListedAndFailed &&
               withIrd.Single(item => item.Path.EndsWith("filemap.txt", StringComparison.Ordinal)).State == IrdCheckState.NotListed,
            "With an IRD, a failed file the IRD lists is a verified mismatch; a path the IRD does not list is not an anomaly.");
        Assert(IrdComparer.Compare(null, observed).All(item => item.State == IrdCheckState.NotChecked) &&
               IrdComparer.Normalize("/dev_bdvd/PS3_GAME/USRDIR/x") == "PS3_GAME/USRDIR/x",
            "Without an IRD every path is NotChecked: the absence of an IRD never claims corruption.");
    }

    private static void AssertDiscFailuresAreObservationsOnly()
    {
        const string Dot = "·";
        var lines = new[]
        {
            "RPCS3 v0.0.30-15910-2369266b Alpha | master",
            "12th Gen Test CPU | 16 Threads | 31.78 GiB RAM",
            $"{Dot}! 0:00:02.000000 SYS: Emulator::BootGame: path='X', title",
            $"{Dot}! 0:00:02.100000 SYS: Serial: TEST00002",
            // Verbatim real-log form.
            $"{Dot}E 0:00:16.077992 {{PPU[0x1000006] Thread (IOReq_t) [libfs: 0x01512090]}} sys_fs: 'sys_fs_open' failed with 0x80010006 : CELL_ENOENT, “/dev_bdvd/PS3_GAME/USRDIR/filemap.txt” [1]",
            $"{Dot}E 0:00:16.077999 {{PPU[0x1000006] Thread (IOReq_t) [libfs: 0x01512090]}} sys_fs: 'sys_fs_open' failed with 0x80010006 : CELL_ENOENT, “/dev_bdvd/PS3_GAME/USRDIR/filemap.txt” [1]",
            $"{Dot}E 0:00:16.078000 {{PPU[0x1000006]}} sys_fs: 'sys_fs_stat' failed with 0x80010006 : CELL_ENOENT, “/dev_hdd0/game/TEST00002/x” [1]"
        };
        var run = Rpcs3LogParser.Parse(lines).RunContext;
        Assert(run.FailedDiscPathList.SequenceEqual(["PS3_GAME/USRDIR/filemap.txt"]),
            "Only distinct /dev_bdvd failures are recorded, relative to the disc root; other volumes are not disc paths.");
        Assert(IrdComparer.Compare(null, run.FailedDiscPathList).All(item => item.State == IrdCheckState.NotChecked),
            "Observed filesystem failures alone are NotChecked, never a corruption verdict.");
    }

    // ------------------------------------------------------------- update snapshot

    private static void AssertUpdateSnapshotContract(string root)
    {
        var document = new GameUpdateSnapshotDocument(
            1, "synthetic test snapshot", "2026-09-01",
            [new GameUpdateEntry("TEST00002", "UP0001-TEST00002_00-0123456789ABCDEF", "Synthetic", [new GameUpdateVersion("01.02"), new GameUpdateVersion("01.10")])]);
        var directory = Path.Combine(root, "UpdateSnapshot");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "updates.json");
        File.WriteAllBytes(path, GameUpdateSnapshot.ToBytes(document));
        var loaded = GameUpdateSnapshot.Load(path);
        Assert(loaded.Entries.Single().Versions.Count == 2, "A valid local snapshot loads.");

        var asOf = new DateOnly(2026, 9, 20);
        var newer = GameUpdateSnapshot.Evaluate(loaded, "TEST00002", "01.06", asOf);
        Assert(newer is { State: GameUpdateState.NewerInSnapshot, NewestInSnapshot: "01.10", SnapshotDate: "2026-09-01", Stale: false },
            "A newer version listed in the dated snapshot is reported as 'newer in snapshot', with the snapshot date and source.");
        Assert(GameUpdateSnapshot.Evaluate(loaded, "TEST00002", "01.10", asOf).State == GameUpdateState.NotOlderThanSnapshot &&
               GameUpdateSnapshot.Evaluate(loaded, "TEST00002", null, asOf).State == GameUpdateState.InstalledVersionUnknown &&
               GameUpdateSnapshot.Evaluate(loaded, "TEST00002", "not-a-version", asOf).State == GameUpdateState.InstalledVersionUnknown,
            "Up to date means not older than the snapshot's newest; an unknown installed version is unknown, not assumed 01.00.");
        Assert(GameUpdateSnapshot.Evaluate(loaded, "OTHER00001", "01.00", asOf).State == GameUpdateState.NotChecked &&
               GameUpdateSnapshot.Evaluate(null, "TEST00002", "01.00", asOf).State == GameUpdateState.NotChecked,
            "A product absent from the snapshot, or no snapshot at all, is NotChecked: nothing is claimed.");
        Assert(GameUpdateSnapshot.Evaluate(loaded, "TEST00002", "01.06", new DateOnly(2027, 1, 1)).Stale &&
               !GameUpdateSnapshot.Evaluate(loaded, "TEST00002", "01.06", new DateOnly(2026, 9, 30)).Stale,
            "A snapshot older than 90 days is marked stale, relative to the runtime date that is never stored in the file.");

        void Invalid(GameUpdateSnapshotDocument bad, string because)
        {
            var thrown = false;
            try
            {
                GameUpdateSnapshot.Validate(bad);
            }
            catch (InvalidDataException)
            {
                thrown = true;
            }

            Assert(thrown, because);
        }

        Invalid(document with { SnapshotDate = "yesterday" }, "A snapshot needs a real date.");
        Invalid(document with { Source = " " }, "A snapshot needs a source.");
        Invalid(document with { Entries = [new GameUpdateEntry("test00002", null, null, [])] }, "A lower-case product code is not exact.");
        Invalid(document with { Entries = [new GameUpdateEntry("TEST00002", "UP0001-TEST00002_00", null, [])] }, "A malformed content id is rejected.");
        Invalid(document with { Entries = [new GameUpdateEntry("TEST00002", null, null, [new GameUpdateVersion("latest")])] }, "A non-numeric version is rejected.");
        Invalid(document with { Entries = [document.Entries[0], document.Entries[0]] }, "A duplicate product code is rejected.");
        Assert(GameUpdateSnapshot.ContentIdPattern().IsMatch("UP0001-BLUS30000_00-0123456789ABCDEF") &&
               !GameUpdateSnapshot.ContentIdPattern().IsMatch("BLUS30000") &&
               !GameUpdateSnapshot.ProductCodePattern().IsMatch("UP0001-BLUS30000_00-0123456789ABCDEF"),
            "A ContentId and a ProductCode are different identifiers and never interchangeable.");
    }

    // ---------------------------------------------------------- identity / Unicode

    private static void AssertExactIdentityAndUnicode()
    {
        Assert(TextSafety.ExactProductCode("BLUS31465") == "BLUS31465" &&
               TextSafety.ExactProductCode("BLUS31465\n") is null &&
               TextSafety.ExactProductCode("blus31465") is null &&
               TextSafety.ExactProductCode("BLUS3１465") is null &&      // full-width digit
               TextSafety.ExactProductCode("BLUS31465​") is null &&     // zero-width space
               TextSafety.ExactProductCode("ΒLUS31465") is null &&      // Greek capital beta
               TextSafety.ExactProductCode(" BLUS31465") is null,
            "Normalization or look-alike characters never turn an inexact ProductCode into an exact identity.");

        var safe = TextSafety.ForDisplay("Game​‮Title‬\u0000 ﻿X");
        Assert(safe.Raw == "Game​‮Title‬\u0000 ﻿X" && safe.Display == "GameTitle X" && safe.Altered,
            "Display text drops zero-width, bidi-control, BOM and control characters while the raw text is preserved.");
        var stacked = TextSafety.ForDisplay("a" + new string('́', 50));
        Assert(stacked.Display.Length <= 3 && stacked.Raw.Length == 51 && stacked.Altered,
            "A run of combining marks is capped for display; the raw text is kept.");
        Assert(TextSafety.ForDisplay("\uD800broken").Display == "broken" || TextSafety.ForDisplay("\uD800broken").Display == "�broken".Replace("�", string.Empty),
            "A malformed (lone surrogate) string does not throw and yields safe display text.");
        Assert(TextSafety.ForDisplay(null).Display.Length == 0 && TextSafety.ForDisplay(new string('x', 1000), 50).Display.Length == 50,
            "Null and over-long text are handled and bounded.");
        Assert(TextSafety.ForDisplay("مرحبا ABC-123").Display == "مرحبا ABC-123",
            "Arabic text and ASCII technical text pass through display sanitation unchanged.");
        Assert(TextSafety.SearchKey("BLÜS3１465") == "blus31465" &&
               TextSafety.ExactProductCode("BLÜS3１465") is null,
            "A loose search key exists for search boxes only and never confers identity.");
    }

    private static void AssertInstallPathTraits()
    {
        const string Dot = "·";
        var real = Rpcs3LogParser.Parse(
        [
            "RPCS3 v0.0.30-15910-2369266b Alpha | master",
            "12th Gen Test CPU | 16 Threads | 31.78 GiB RAM",
            // Verbatim real-log form.
            $"{Dot}! 0:00:01.170874 Compat: Finished reading database from file: D:/emulation/emulators/rpcs3-v0.0.26-14743-6b30cdac_win64/GuiConfigs/compat_database.dat"
        ]).RunContext;
        Assert(real.DataPath == "D:/emulation/emulators/rpcs3-v0.0.26-14743-6b30cdac_win64" && real.DataPathTraitList.Count == 0,
            "The data folder is what the log printed; an ordinary folder has no location trait.");

        Assert(InstallPathTraits.Of("C:/Program Files/RPCS3").SequenceEqual([InstallPathTraits.ProgramFiles]) &&
               InstallPathTraits.Of(@"D:\Sync\OneDrive\Games\rpcs3").SequenceEqual([InstallPathTraits.CloudSync]) &&
               InstallPathTraits.Of("C:/Users/x/OneDrive - Corp/rpcs3").SequenceEqual([InstallPathTraits.CloudSync]) &&
               InstallPathTraits.Of("C:/Users/x/AppData/Local/Temp/Rar$EXa1234/rpcs3").SequenceEqual([InstallPathTraits.ArchiveTemp]) &&
               InstallPathTraits.Of("D:").SequenceEqual([InstallPathTraits.DriveRoot]) &&
               InstallPathTraits.Of("C:/Programs/RPCS3").Count == 0 && InstallPathTraits.Of("C:/OneDriveClone/rpcs3").Count == 0,
            "Only exact named locations are traits; a similar folder name is not, and no consequence is stated.");
    }

    // ---------------------------------------------------------------- hardware

    private static void AssertHardwareNormalization()
    {
        var session = LogSession.Empty with
        {
            CpuLine = "12th Gen Intel(R) Core(TM) i9-12900K | 24 Threads | 31.78 GiB RAM",
            GpuName = "NVIDIA GeForce RTX 4090",
            Run = LogRunContext.Empty with { GpuDriver = "551.61.0.0" }
        };
        var view = LogHardware.Read(session);
        Assert(view.Source == HardwareSource.ImportedLog &&
               view.CpuVendor is { Value: "Intel", Method: "NAME_TOKEN" } && view.CpuVendor.Raw == session.CpuLine &&
               view.GpuVendor is { Value: "NVIDIA", Raw: "NVIDIA GeForce RTX 4090" } &&
               view.GpuDriver is { Raw: "551.61.0.0", Value: "551.61", Method: "NVIDIA_VULKAN_QUAD_TRIM" },
            "Imported-log hardware keeps the raw text, a normalized value and the normalization method; the source is the log, not this machine.");

        var unknown = LogHardware.Read(LogSession.Empty with
        {
            CpuLine = "Mystery CPU | 8 Threads",
            GpuName = "Mystery GPU",
            Run = LogRunContext.Empty with { GpuDriver = "2.0.310" }
        });
        Assert(unknown.CpuVendor.Value is null && unknown.GpuVendor.Value is null && unknown.GpuDriver is { Raw: "2.0.310", Value: null, Method: "NO_KNOWN_NORMALIZATION" },
            "Without a vendor token or a known driver form nothing is normalized: no guess, and the raw value stays.");
        var amd = LogHardware.Read(LogSession.Empty with { GpuName = "AMD Radeon RX 7900 XT", Run = LogRunContext.Empty with { GpuDriver = "551.61.0.0" } });
        Assert(amd.GpuDriver.Value is null, "The NVIDIA driver trim is applied to NVIDIA adapters only.");
        Assert(LogHardware.Read(LogSession.Empty).GpuVendor.Method == "NOT_STATED",
            "A log that states no hardware yields NOT_STATED.");
    }
}
