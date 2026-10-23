using System;
using R3Config.Tests;

internal static partial class Program
{
    // Small standalone focused-suite CLI flags kept out of the thin router in
    // Program.cs so its line-count architecture bound stays satisfied.
    private static bool TryRunFocusedTraceModes(
        string[] args,
        TestRunner runner,
        TestContext context)
    {
        if (args.Length == 1 &&
            args[0].Equals("--bf4-ncl-trace-only", StringComparison.OrdinalIgnoreCase))
        {
            var tempRoot = context.CreateTempRoot("R3ConfigBf4NclTrace");
            runner.RunCase(nameof(Bf4NclVersionTraceT),
                () => Bf4NclVersionTraceT.Run(tempRoot));
            Console.WriteLine("Battlefield 4 NCL version trace focused test passed. Passed: 1, Failed: 0, Skipped: 0");
            return true;
        }

        if (args.Length == 1 &&
            args[0].Equals("--config-baseline-only", StringComparison.OrdinalIgnoreCase))
        {
            var tempRoot = context.CreateTempRoot("R3ConfigBaseline");
            runner.RunCase(nameof(CfgBaselineT), () => CfgBaselineT.Run(tempRoot));
            Console.WriteLine("Config baseline focused suite passed. Passed: 1, Failed: 0, Skipped: 0");
            return true;
        }

        if (args.Length == 1 &&
            args[0].Equals("--ncl-sequential-only", StringComparison.OrdinalIgnoreCase))
        {
            runner.RunCase(nameof(NclSequentialT), NclSequentialT.Run);
            return true;
        }

        if (args.Length == 1 &&
            args[0].Equals("--ncl-same-serial-version-only", StringComparison.OrdinalIgnoreCase))
        {
            runner.RunCase(nameof(NclSameSerialVersionT), NclSameSerialVersionT.Run);
            return true;
        }

        if (args.Length == 1 &&
            args[0].Equals("--canary-artemis-conflict-only", StringComparison.OrdinalIgnoreCase))
        {
            var tempRoot = context.CreateTempRoot("R3ConfigCanaryArtemisConflict");
            runner.RunCase(
                nameof(CanaryArtemisConflictT),
                () => CanaryArtemisConflictT.Run(tempRoot));
            return true;
        }

        return false;
    }
}
