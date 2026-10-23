using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

// Read/write of the Wiki notes/online sidecar (Data/wiki-notes.json). The
// sidecar is optional: without it the Game Settings screen simply has no notes
// or online evidence to show. It never carries settings and is never merged
// into cat.json.
public sealed class WikiEvidenceCatalog
{
    public const int SchemaVersion = 1;
    public const string FileName = "wiki-notes.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private WikiEvidenceCatalog(WikiEvidenceDocument document)
    {
        Document = document;
    }

    public static WikiEvidenceCatalog Empty { get; } = new(
        new WikiEvidenceDocument(
            SchemaVersion,
            string.Empty,
            string.Empty,
            string.Empty,
            []));

    public WikiEvidenceDocument Document { get; }

    public IReadOnlyList<WikiPageEvidence> Pages => Document.Pages;

    public bool IsEmpty => Document.Pages.Count == 0;

    public static WikiEvidenceCatalog FromPages(
        string dataSource,
        string dataVersion,
        string sourceHash,
        IEnumerable<WikiPageEvidence> pages,
        string? sourceFile = null)
    {
        ArgumentNullException.ThrowIfNull(pages);

        return new WikiEvidenceCatalog(new WikiEvidenceDocument(
            SchemaVersion,
            dataSource,
            dataVersion,
            sourceHash,
            [.. pages
                .Select(page => page with
                {
                    // The same note text is never carried twice on one page.
                    Notes = [.. page.Notes.DistinctBy(note => note.Text, StringComparer.Ordinal)]
                })
                .Where(page => page.Notes.Count > 0 || page.Online.Count > 0)
                .OrderBy(page => page.Page, StringComparer.Ordinal)],
            sourceFile));
    }

    public static WikiEvidenceCatalog Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var bytes = R3Bound.ReadBytes(
            path,
            R3Limits.PatCatBy,
            "wiki notes catalog");

        WikiEvidenceDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<WikiEvidenceDocument>(
                bytes,
                Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                "Wiki notes catalog JSON is invalid.",
                ex);
        }

        if (document is null ||
            document.Schema != SchemaVersion ||
            document.Pages is null ||
            document.Pages.Any(page =>
                page is null ||
                string.IsNullOrWhiteSpace(page.Page) ||
                page.Serials is null ||
                page.Notes is null ||
                page.Online is null))
        {
            throw new InvalidDataException(
                "Wiki notes catalog is empty, invalid, or an unsupported schema.");
        }

        return new WikiEvidenceCatalog(document);
    }

    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(
            path,
            JsonSerializer
                .Serialize(Document, Options)
                .Replace("\r\n", "\n", StringComparison.Ordinal),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public WikiPageEvidence? Find(string serial, string? wikiPage) =>
        GameSettingsProjector.Find(Pages, serial, wikiPage);
}

#pragma warning restore IDE0130
