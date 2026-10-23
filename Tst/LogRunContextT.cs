using System;
using System.Linq;
using R3Config.Mdl;
using R3Config.Svc;

// Log run context: the facts an RPCS3 log states in its own words (firmware,
// renderer driver, configuration used, package VERSION, ISO boot/key/encryption,
// patches RPCS3 applied). Line formats are the ones real RPCS3 logs print;
// every value is taken from the line itself, and absence stays absence.
internal static class LogRunContextT
{
    private const string Dot = "\u00b7";

    private static string L(char level, string time, string body) => $"{Dot}{level} 0:{time} {body}";

    public static void Run()
    {
        AssertRunContext();
        AssertEffectiveConfigurationReplacesEarlierBlock();
        AssertAbsenceIsNotInvented();

        Console.WriteLine("Log run context (firmware, exact-name driver, config mode, package version, ISO boot/key/encryption, applied patches, effective configuration, absence) PASS.");
    }

    private static string[] Log(params string[] extra) =>
    [
        L('A', "00:01.136492", "{Vulkan Device Enumeration Thread} RSX: Found Vulkan-compatible GPU: 'GPU One' running on driver 551.61.0.0"),
        L('A', "00:01.136500", "{Vulkan Device Enumeration Thread} RSX: Found Vulkan-compatible GPU: 'GPU Two' running on driver 552.0.0.0"),
        L('A', "00:01.148324", "SYS: Firmware version: 4.87"),
        L('!', "00:02.064117", "SYS: Emulator::BootGame: path='X:/games/TEST00002', title"),
        L('!', "00:02.064334", "SYS: Selected config: mode=custom config, path=\"\""),
        L('!', "00:02.073873", "SYS: Serial: TEST00002"),
        L('!', "00:02.075000", "SYS: Version: APP_VER=01.06 VERSION=01.00"),
        L('!', "00:02.079132", "SYS: Used configuration:"),
        "Video:",
        "  Renderer: OpenGL",
        "  Resolution Scale: 100",
        L('!', "00:02.361862", "SYS: Selected config: mode=custom config, path=\"C:/rpcs3/config/custom_configs/config_TEST00002.yml\""),
        L('!', "00:02.485064", "SYS: Serial: TEST00002"),
        L('!', "00:02.490309", "SYS: Used configuration:"),
        "Video:",
        "  Renderer: Vulkan",
        "  Resolution Scale: 200",
        L('!', "00:03.000000", "SYS: Loading iso archive 'X:/games/test.iso'"),
        L('W', "00:03.100000", "ISO: init: Failed to open, or missing, key file: ''"),
        L('W', "00:03.100010", "ISO: init: Set 'enc type': NONE, 'reg count': 1: 'X:/games/test.iso'"),
        L('S', "00:04.000000", "PAT: Applied patch (hash='PPU-a1f33132af387c49d452e64fcb0704cb06b6d084', description='Disable Motion blur', author='a, b', patch_version='1.0', file_version='1.2') (<- 1)"),
        L('S', "00:04.000100", "PAT: Applied patch (hash='PPU-a1f33132af387c49d452e64fcb0704cb06b6d084', description='Disable Motion blur', author='a, b', patch_version='1.0', file_version='1.2') (<- 1)"),
        L('!', "00:05.000000", "RSX: Vulkan: Renderer initialized on device 'GPU Two'"),
        .. extra
    ];

    private static void AssertRunContext()
    {
        var session = Rpcs3LogParser.Parse(Log());
        var run = session.RunContext;

        Assert(run.FirmwareVersion == "4.87" && run.PackageVersion == "01.00" && session.AppVersion == "01.06",
            "Firmware and package VERSION are read from their own lines, separately from APP_VER.");
        Assert(run.GpuDriver == "552.0.0.0" && session.GpuName == "GPU Two",
            "The driver is that of the adapter that initialized the renderer, by exact name (not the first enumerated adapter).");
        Assert(run.ConfigMode == "custom" && run.CustomConfigFile && run.ConfigBlocks == 2,
            "The selected configuration mode, the presence of a custom config file and the number of configuration blocks are stated.");
        Assert(run.BootMedia == LogBootMedia.IsoArchive &&
               run.IsoKey == LogIsoKeyState.Missing &&
               run.IsoEncryption == "NONE",
            "ISO boot, the missing key file and the ISO's own encryption type are recorded as stated.");
        Assert(session.Problems.Count == 0,
            "A key file RPCS3 could not find for an unencrypted ISO is an observation, not a problem event.");
        Assert(run.AppliedPatches.Count == 1 &&
               run.AppliedPatches[0].Description == "Disable Motion blur" &&
               run.AppliedPatches[0].Hash == "PPU-a1f33132af387c49d452e64fcb0704cb06b6d084",
            "A patch RPCS3 reported applying is listed once with its hash and description.");
    }

    private static void AssertEffectiveConfigurationReplacesEarlierBlock()
    {
        var session = Rpcs3LogParser.Parse(Log());

        Assert(session.Settings.Count(item => item.Path == "Video/Renderer") == 1 &&
               session.Settings.Single(item => item.Path == "Video/Renderer").Value == "Vulkan" &&
               session.Settings.Single(item => item.Path == "Video/Resolution Scale").Value == "200",
            "RPCS3 prints the configuration again after a custom config loads; the last block is the effective one and replaces the first, never merges with it.");
        Assert(session.Renderer == "Vulkan",
            "The renderer comes from the effective configuration / renderer initialization, not from the superseded block.");

        var configOnly = Rpcs3LogParser.Parse(Log().Where(line => !line.Contains("Renderer initialized", StringComparison.Ordinal)));
        Assert(configOnly.Renderer == "Vulkan" && configOnly.RunContext.GpuDriver is null,
            "Without renderer initialization the renderer is the effective configuration's, and no adapter driver is guessed.");
    }

    private static void AssertAbsenceIsNotInvented()
    {
        var bare = Rpcs3LogParser.Parse(
        [
            L('!', "00:02.064117", "SYS: Emulator::BootGame: path='X', title"),
            L('!', "00:02.073873", "SYS: Serial: TEST00002"),
            L('A', "00:01.148324", "SYS: Firmware version: unknown")
        ]);
        var run = bare.RunContext;
        Assert(run.FirmwareVersion is null && run.GpuDriver is null && run.PackageVersion is null && run.ConfigMode is null &&
               run.BootMedia == LogBootMedia.NotStated && run.IsoKey == LogIsoKeyState.NotStated && run.IsoEncryption is null &&
               run.AppliedPatches.Count == 0 && run.ConfigBlocks == 0 && !run.CustomConfigFile,
            "A log that states none of these facts yields none: a non-numeric firmware, a missing driver or config line is not filled in.");
        Assert(LogSession.Empty.RunContext == LogRunContext.Empty,
            "An empty session has the empty run context.");
    }
}
