using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CpuEvidence;

// Deterministic, offline extractor for "RPCS3 CPU DATA.xlsx". The workbook is community
// data (a spreadsheet by named list authors). Everything is read from the package itself:
// cell text, cell fill colors (the sheet's own legend gives their meaning), hyperlinks and
// cell notes. Unknown layout or an unmapped color fails closed (a warning row, never a guess).
public static class Program
{
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PkgRel = "http://schemas.openxmlformats.org/package/2006/relationships";

    private sealed record Cell(int Row, int Col, string Value, string Fill, string Ref);

    private sealed class Sheet
    {
        public string Name = "", State = "", Path = "";
        public readonly Dictionary<(int Row, int Col), Cell> Cells = [];
        public readonly Dictionary<string, string> Links = new(StringComparer.Ordinal);
        public readonly Dictionary<string, string> Notes = new(StringComparer.Ordinal);
        public int MaxRow => Cells.Count == 0 ? 0 : Cells.Keys.Max(k => k.Row);
        public string Text(int r, int c) => Cells.TryGetValue((r, c), out var cell) ? cell.Value : "";
    }

    private static string Q(object? v) => "\"" + Convert.ToString(v, CultureInfo.InvariantCulture)?.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    private sealed class Csv : IDisposable
    {
        private readonly StreamWriter _w;
        public int Rows;
        public Csv(string path, params string[] headers)
        {
            _w = new StreamWriter(path, false, new UTF8Encoding(true)) { NewLine = "\n" };
            _w.WriteLine(string.Join(",", headers.Select(Q)));
        }

        public void Row(params object?[] f) { _w.WriteLine(string.Join(",", f.Select(Q))); Rows++; }
        public void Dispose() => _w.Dispose();
    }

    private static string ColName(int c) { var s = ""; c++; while (c > 0) { c--; s = (char)('A' + c % 26) + s; c /= 26; } return s; }

    private static (int Row, int Col) Ref(string r)
    {
        var m = Regex.Match(r, "^([A-Z]+)(\\d+)$");
        var n = 0;
        foreach (var ch in m.Groups[1].Value) n = n * 26 + ch - 'A' + 1;
        return (int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) - 1, n - 1);
    }

    public static int Main(string[] args)
    {
        if (args.Length != 4 || args[0] != "run" && args[0] != "gate") { Console.Error.WriteLine("CpuEvidence run <xlsx> --out <dir>"); return 2; }
        var xlsx = args[1];
        var outDir = args[3];
        Directory.CreateDirectory(outDir);
        var warnings = new List<(string Code, string Detail)>();
        using var zip = ZipFile.OpenRead(xlsx);
        XDocument Load(string name) => XDocument.Load(zip.GetEntry(name)?.Open() ?? throw new InvalidDataException("Missing package part " + name));

        var strings = Load("xl/sharedStrings.xml").Root!.Elements(Ns + "si")
            .Select(si => string.Concat(si.Descendants(Ns + "t").Select(t => t.Value))).ToList();
        var styles = Load("xl/styles.xml").Root!;
        var fills = styles.Element(Ns + "fills")!.Elements(Ns + "fill")
            .Select(f => f.Descendants(Ns + "fgColor").FirstOrDefault()?.Attribute("rgb")?.Value ?? "").ToList();
        var xfFill = styles.Element(Ns + "cellXfs")!.Elements(Ns + "xf").Select(x => int.TryParse(x.Attribute("fillId")?.Value, out var id) ? id : 0).ToList();

        var wb = Load("xl/workbook.xml").Root!;
        var wbRels = Load("xl/_rels/workbook.xml.rels").Root!.Elements(PkgRel + "Relationship").ToDictionary(r => r.Attribute("Id")!.Value, r => r.Attribute("Target")!.Value);
        var sheets = new List<Sheet>();
        foreach (var s in wb.Element(Ns + "sheets")!.Elements(Ns + "sheet"))
        {
            var sheet = new Sheet
            {
                Name = s.Attribute("name")!.Value,
                State = s.Attribute("state")?.Value ?? "visible",
                Path = "xl/" + wbRels[s.Attribute(Rel + "id")!.Value]
            };
            var doc = Load(sheet.Path).Root!;
            foreach (var c in doc.Descendants(Ns + "c"))
            {
                var (row, col) = Ref(c.Attribute("r")!.Value);
                var t = c.Attribute("t")?.Value;
                var v = c.Element(Ns + "v")?.Value;
                var text = t == "s" && v is not null ? strings[int.Parse(v, CultureInfo.InvariantCulture)]
                    : c.Element(Ns + "is") is { } inline ? string.Concat(inline.Descendants(Ns + "t").Select(x => x.Value))
                    : v ?? "";
                var style = int.TryParse(c.Attribute("s")?.Value, out var si) ? si : 0;
                var fill = style < xfFill.Count && xfFill[style] < fills.Count ? fills[xfFill[style]] : "";
                if (text.Length > 0 || fill.Length > 0) sheet.Cells[(row, col)] = new Cell(row, col, text.Trim(), fill, c.Attribute("r")!.Value);
            }

            var relPath = sheet.Path.Replace("worksheets/", "worksheets/_rels/", StringComparison.Ordinal) + ".rels";
            if (zip.GetEntry(relPath) is not null)
            {
                var rels = Load(relPath).Root!.Elements(PkgRel + "Relationship").ToDictionary(r => r.Attribute("Id")!.Value, r => r.Attribute("Target")!.Value);
                foreach (var h in doc.Descendants(Ns + "hyperlink"))
                {
                    var rid = h.Attribute(Rel + "id")?.Value;
                    if (rid is not null && rels.TryGetValue(rid, out var url)) sheet.Links[h.Attribute("ref")!.Value] = url;
                }

                foreach (var target in Load(relPath).Root!.Elements(PkgRel + "Relationship").Where(r => r.Attribute("Type")!.Value.EndsWith("/comments", StringComparison.Ordinal)).Select(r => r.Attribute("Target")!.Value))
                {
                    var name = "xl/" + target.Replace("../", "", StringComparison.Ordinal);
                    foreach (var cm in Load(name).Root!.Descendants(Ns + "comment"))
                        sheet.Notes[cm.Attribute("ref")!.Value] = Regex.Replace(string.Concat(cm.Descendants(Ns + "t").Select(t => t.Value)), "\\s+", " ").Trim();
                }
            }

            sheets.Add(sheet);
        }

        using var sha = SHA256.Create();
        string hash;
        using (var fs = File.OpenRead(xlsx)) hash = Convert.ToHexString(sha.ComputeHash(fs));

        var gate = new List<(string Name, bool Pass, string Detail)>();
        var desktop = sheets[0];
        var mobile = sheets[1];
        var scaling = sheets[2];

        using (var w = new Csv(Path.Combine(outDir, "01-Workbook-Provenance.csv"), "Field", "Value"))
        {
            w.Row("SourceFileName", Path.GetFileName(xlsx));
            w.Row("SHA256", hash);
            w.Row("SizeBytes", new FileInfo(xlsx).Length);
            w.Row("DocumentProperties", zip.GetEntry("docProps/core.xml") is null ? "ABSENT (spreadsheet export carries no author/modified metadata)" : "present");
            w.Row("Kind", "COMMUNITY_SPREADSHEET (named list authors on the sheets)");
            foreach (var s in sheets) w.Row("Sheet", $"{s.Name.Replace('­', '~')} | state={s.State} | cells={s.Cells.Count} | hyperlinks={s.Links.Count} | notes={s.Notes.Count}");
        }

        // ---- changelogs and dates
        var changelog = new List<(string Sheet, string Version, string Date, string Text)>();
        foreach (var s in new[] { desktop, mobile })
        {
            foreach (var c in s.Cells.Values.OrderBy(c => c.Row).ThenBy(c => c.Col))
            {
                var m = Regex.Match(c.Value, "^(?<v>V\\d+(?:\\.\\d+)*)\\s*-?\\s*(?<d>\\d{1,2}/\\d{1,2}/\\d{4})$");
                if (!m.Success) continue;
                var date = DateTime.ParseExact(m.Groups["d"].Value, ["M/d/yyyy", "MM/dd/yyyy", "M/dd/yyyy", "MM/d/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None);
                changelog.Add((s.Name, m.Groups["v"].Value, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), s.Text(c.Row, c.Col + 1)));
            }
        }

        var lastUpdate = scaling.Cells.Values.Select(c => Regex.Match(c.Value, "Last Update:\\s*(\\d{1,2})\\s+(\\w+)\\s+(\\d{4})")).FirstOrDefault(m => m.Success);
        var scalingDate = lastUpdate is null ? "" : DateTime.ParseExact($"{lastUpdate.Groups[1].Value} {lastUpdate.Groups[2].Value} {lastUpdate.Groups[3].Value}", "d MMMM yyyy", CultureInfo.InvariantCulture).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        using (var w = new Csv(Path.Combine(outDir, "02-Changelog.csv"), "Sheet", "Version", "Date", "Text"))
        {
            foreach (var e in changelog.OrderBy(e => e.Sheet, StringComparer.Ordinal).ThenBy(e => e.Date, StringComparer.Ordinal).ThenBy(e => e.Version, StringComparer.Ordinal)) w.Row(e.Sheet, e.Version, e.Date, e.Text);
            w.Row(scaling.Name, "Last Update", scalingDate, "");
        }

        string Latest(string sheet) => changelog.Where(e => e.Sheet == sheet).OrderByDescending(e => e.Date, StringComparer.Ordinal).Select(e => $"{e.Version} {e.Date}").FirstOrDefault() ?? "";
        gate.Add(("Desktop latest changelog", Latest(desktop.Name) == "V3.0 2025-07-08", Latest(desktop.Name)));
        gate.Add(("Mobile latest changelog", Latest(mobile.Name) == "V0.2 2025-07-13", Latest(mobile.Name)));
        gate.Add(("Thread Scaling last update", scalingDate == "2026-06-06", scalingDate));

        // ---- tier lists
        int TierRows(Sheet s, string csv, string listName)
        {
            var header = s.Cells.Values.Where(c => c.Value == "S+").OrderBy(c => c.Row).ThenBy(c => c.Col).GroupBy(c => c.Row).FirstOrDefault(g => g.Any(c => s.Text(c.Row, c.Col + 1) == "S"));
            if (header is null) { warnings.Add(("TIER_HEADER_NOT_FOUND", s.Name)); return 0; }
            var count = 0;
            using var w = new Csv(csv, "List", "Block", "Tier", "CPU", "Cell", "FillRgb");
            var headerRow = header.Key;
            var blocks = header.OrderBy(c => c.Col).Select((c, i) => (Start: c.Col, Name: header.Count() > 1 ? (i == 0 ? "Stock" : "Overclocked") : "Main")).ToList();
            var tiers = Enumerable.Range(0, s.Cells.Keys.Max(k => k.Col) + 1).Where(c => Regex.IsMatch(s.Text(headerRow, c), "^(S\\+|S|A|B|C|D|E|F)( Tier)?$")).ToList();
            foreach (var col in tiers)
            {
                var block = blocks.LastOrDefault(b => b.Start <= col);
                var tier = s.Text(headerRow, col).Replace(" Tier", "", StringComparison.Ordinal);
                for (var r = headerRow + 1; r <= s.MaxRow; r++)
                {
                    if (!s.Cells.TryGetValue((r, col), out var cell) || cell.Value.Length == 0) continue;
                    if (s.Text(r, col + 1).Length > 0 && Regex.IsMatch(s.Text(r, col + 1), "^V\\d")) continue;
                    w.Row(listName, block.Name, tier, cell.Value, cell.Ref, cell.Fill);
                    count++;
                }
            }

            return count;
        }

        var desktopCount = TierRows(desktop, Path.Combine(outDir, "03-Desktop-Cpu-Tiers.csv"), "DESKTOP");
        var mobileCount = TierRows(mobile, Path.Combine(outDir, "04-Mobile-Cpu-Tiers.csv"), "MOBILE_WIP");
        gate.Add(("Desktop tier entries extracted", desktopCount > 100, desktopCount.ToString(CultureInfo.InvariantCulture)));
        gate.Add(("Mobile tier entries extracted", mobileCount > 30, mobileCount.ToString(CultureInfo.InvariantCulture)));

        // ---- thread scaling
        var legendLabels = new[] { ("Performs GREAT", "GREAT"), ("Performs GOOD", "GOOD"), ("Performs OK", "OK"), ("Performs BAD", "BAD"), ("Unplayable", "UNPLAYABLE"), ("Missing data", "MISSING_DATA") };
        var legend = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (prefix, grade) in legendLabels)
        {
            var label = scaling.Cells.Values.Where(c => c.Value.StartsWith(prefix, StringComparison.Ordinal) && c.Col == 18).OrderBy(c => c.Row).FirstOrDefault();
            var swatch = label is null ? null : scaling.Cells.GetValueOrDefault((label.Row, label.Col - 1));
            if (swatch is null || swatch.Fill.Length == 0) { warnings.Add(("LEGEND_SWATCH_NOT_FOUND", grade)); continue; }
            legend[swatch.Fill] = grade;
        }

        var headerCell = scaling.Cells.Values.FirstOrDefault(c => c.Value == "FPS Target");
        var rowsOut = 0;
        var unmapped = 0;
        var linksKept = 0;
        var games = new HashSet<string>(StringComparer.Ordinal);
        using (var w = new Csv(Path.Combine(outDir, "05-Thread-Scaling.csv"), "Game", "FpsTarget", "CpuTierLabel", "CoreThreadConfig", "EvidenceLabel", "Grade", "FillRgb", "Cell", "SourceLink", "CellNote"))
        {
            if (headerCell is null) warnings.Add(("THREAD_SCALING_HEADER_NOT_FOUND", scaling.Name));
            else
            {
                var hr = headerCell.Row;
                var gameCol = Enumerable.Range(0, 30).First(c => scaling.Text(hr, c) == "Game");
                var tierCol = Enumerable.Range(0, 30).First(c => scaling.Text(hr, c) == "CPU Tier");
                var fpsCol = Enumerable.Range(0, 30).First(c => scaling.Text(hr, c) == "FPS Target");
                var configCols = Enumerable.Range(gameCol + 1, 12).Where(c => Regex.IsMatch(scaling.Text(hr, c), "^\\d+C/\\d+T$")).ToList();
                for (var r = hr + 1; r <= scaling.MaxRow; r++)
                {
                    var game = scaling.Text(r, gameCol);
                    if (game.Length == 0 || game.StartsWith("Legend", StringComparison.Ordinal)) continue;
                    foreach (var col in configCols)
                    {
                        if (!scaling.Cells.TryGetValue((r, col), out var cell) || cell.Value.Length == 0) continue;
                        var grade = legend.GetValueOrDefault(cell.Fill, "UNMAPPED_COLOR");
                        if (grade == "UNMAPPED_COLOR") { unmapped++; warnings.Add(("UNMAPPED_COLOR", $"{game} {cell.Ref} {cell.Fill}")); }
                        if (Regex.IsMatch(cell.Value, "^[0-9.]+$")) warnings.Add(("NUMERIC_EVIDENCE_LABEL", $"{game} {cell.Ref} = {cell.Value} (looks like a date serial, not a result label)"));
                        var link = scaling.Links.GetValueOrDefault(cell.Ref, "");
                        if (link.Length > 0) linksKept++;
                        w.Row(game, scaling.Text(r, fpsCol), scaling.Text(r, tierCol), scaling.Text(hr, col), cell.Value, grade, cell.Fill, cell.Ref, link, scaling.Notes.GetValueOrDefault(cell.Ref, ""));
                        rowsOut++;
                        games.Add(game);
                    }
                }
            }
        }

        using (var w = new Csv(Path.Combine(outDir, "06-Legend.csv"), "FillRgb", "Grade"))
        {
            foreach (var (fill, grade) in legend.OrderBy(k => k.Value, StringComparer.Ordinal)) w.Row(fill, grade);
        }

        using (var w = new Csv(Path.Combine(outDir, "07-Notes-And-Warnings.csv"), "Kind", "Sheet", "Cell", "Text"))
        {
            foreach (var s in sheets)
            {
                foreach (var c in s.Cells.Values.Where(c => c.Value.Length > 60 || Regex.IsMatch(c.Value, "WIP|NOT FINAL|Missing data|Community|misinformation", RegexOptions.IgnoreCase)).OrderBy(c => c.Row).ThenBy(c => c.Col))
                    w.Row("SHEET_TEXT", s.Name.Replace('­', '~'), c.Ref, c.Value);
                foreach (var n in s.Notes.OrderBy(n => n.Key, StringComparer.Ordinal)) w.Row("CELL_NOTE", s.Name, n.Key, n.Value);
            }

            foreach (var (code, detail) in warnings.OrderBy(x => x.Code, StringComparer.Ordinal).ThenBy(x => x.Detail, StringComparer.Ordinal)) w.Row("EXTRACTOR_WARNING", "", code, detail);
        }

        gate.Add(("Thread Scaling cells extracted", rowsOut >= 30 && games.Count >= 20, $"{rowsOut} cells, {games.Count} games, {linksKept} with a source link, {unmapped} unmapped colors"));
        gate.Add(("Legend colors mapped", legend.Count == 6 && unmapped == 0, $"{legend.Count}/6 legend colors, unmapped cells={unmapped}"));
        gate.Add(("Sheets", sheets.Count == 4 && sheets[3].State == "hidden", string.Join(" / ", sheets.Select(s => s.State))));

        var report = new StringBuilder();
        foreach (var (name, pass, detail) in gate) report.AppendLine($"{(pass ? "PASS" : "FAIL")}  {name}: {detail}");
        File.WriteAllText(Path.Combine(outDir, "08-Extraction-Gates.txt"), report.ToString(), new UTF8Encoding(true));
        Console.Write(report);
        return gate.All(g => g.Pass) ? 0 : 1;
    }
}
