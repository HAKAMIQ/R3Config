using System.Text;

namespace HistWiki;

public static class Program
{
    private const string Usage =
        "HistWiki run --export <dir> --schema <xml> --snapshot <xml> --out <dir> [--declarations <csv>] [--reference <dir>] [--previous <dir>] [--verify-determinism]\n" +
        "HistWiki selftest";

    public static int Main(string[] args)
    {
        if (args.Length == 0) { Console.Error.WriteLine(Usage); return 2; }
        if (args[0] == "selftest") return SelfTest.Run();
        if (args[0] == "classify-removals")
        {
            var opts = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 1; i + 1 < args.Length; i += 2) opts[args[i]] = args[i + 1];
            if (!opts.TryGetValue("--events", out var ev) || !opts.TryGetValue("--schema-status", out var st) || !opts.TryGetValue("--out", out var od)) { Console.Error.WriteLine("HistWiki classify-removals --events <02-events.csv> --schema-status <17-status.csv> --out <dir>"); return 2; }
            return Removals.Run(ev, st, od);
        }
        if (args[0] != "run") { Console.Error.WriteLine(Usage); return 2; }

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var flags = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal)) { Console.Error.WriteLine(Usage); return 2; }
            if (args[i] == "--verify-determinism") { flags.Add(args[i]); continue; }
            if (i + 1 >= args.Length) { Console.Error.WriteLine(Usage); return 2; }
            map[args[i]] = args[++i];
        }

        string Need(string name)
        {
            if (!map.TryGetValue(name, out var value)) throw new ArgumentException($"Missing {name}.");
            return value;
        }

        var declarations = map.TryGetValue("--declarations", out var d) ? d : Path.Combine(AppContext.BaseDirectory, "identity-declarations.csv");
        var options = new Options(Need("--export"), Need("--schema"), Need("--snapshot"), Need("--out"), declarations, map.GetValueOrDefault("--reference"), map.GetValueOrDefault("--previous"));

        var started = DateTime.UtcNow;
        var result = Pipeline.Run(options);
        Outputs.WriteAll(result, options.OutDir);
        var determinism = "NOT_RUN";
        if (flags.Contains("--verify-determinism"))
        {
            var second = Path.Combine(options.OutDir, "_determinism-check");
            var again = Pipeline.Run(options with { OutDir = second });
            Outputs.WriteAll(again, second);
            determinism = CompareDirs(options.OutDir, second) ? "IDENTICAL" : "DIFFERENT";
            Directory.Delete(second, true);
        }

        var gates = Gates.Evaluate(result, determinism);
        File.WriteAllText(Path.Combine(options.OutDir, "20-Gate-Report.md"), gates.Markdown(), new UTF8Encoding(true));
        File.WriteAllText(Path.Combine(options.OutDir, "21-Gate-Report.json"), gates.Json(), new UTF8Encoding(true));
        Console.WriteLine(gates.Markdown());
        Console.WriteLine($"Elapsed: {(DateTime.UtcNow - started).TotalSeconds:F1}s");
        return gates.AllPass ? 0 : 1;
    }

    private static bool CompareDirs(string a, string b)
    {
        var filesA = Directory.EnumerateFiles(a, "*", SearchOption.TopDirectoryOnly).Select(Path.GetFileName).Where(f => !f!.StartsWith("20-", StringComparison.Ordinal) && !f.StartsWith("21-", StringComparison.Ordinal) && !f.StartsWith("23-", StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToList();
        var filesB = Directory.EnumerateFiles(b, "*", SearchOption.TopDirectoryOnly).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList();
        if (!filesA.SequenceEqual(filesB)) return false;
        return filesA.All(f => Pipeline.Sha256File(Path.Combine(a, f!)) == Pipeline.Sha256File(Path.Combine(b, f!)));
    }
}
