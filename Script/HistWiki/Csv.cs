using System.Globalization;
using System.Text;

namespace HistWiki;

// Minimal RFC-4180 writer/reader. Output is UTF-8 with BOM, LF line ends and
// every field quoted, so identical input always gives byte-identical files.
public static class Csv
{
    public static string Field(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            bool flag => flag ? "True" : "False",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
        return "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    public sealed class Writer : IDisposable
    {
        private readonly StreamWriter _writer;
        public int Rows { get; private set; }

        public Writer(string path, params string[] headers)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            _writer = new StreamWriter(path, false, new UTF8Encoding(true)) { NewLine = "\n" };
            _writer.WriteLine(string.Join(",", headers.Select(h => Field(h))));
        }

        public void Row(params object?[] fields)
        {
            _writer.WriteLine(string.Join(",", fields.Select(f => Field(f))));
            Rows++;
        }

        public void Dispose() => _writer.Dispose();
    }

    public static List<string[]> Read(string path)
    {
        var text = File.ReadAllText(path, Encoding.UTF8);
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        var any = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else quoted = false;
                }
                else field.Append(c);
                continue;
            }

            switch (c)
            {
                case '"': quoted = true; any = true; break;
                case ',': row.Add(field.ToString()); field.Clear(); any = true; break;
                case '\r': break;
                case '\n':
                    if (any || field.Length > 0) { row.Add(field.ToString()); rows.Add(row.ToArray()); }
                    row.Clear(); field.Clear(); any = false;
                    break;
                default: field.Append(c); any = true; break;
            }
        }

        if (any || field.Length > 0) { row.Add(field.ToString()); rows.Add(row.ToArray()); }
        return rows;
    }
}
