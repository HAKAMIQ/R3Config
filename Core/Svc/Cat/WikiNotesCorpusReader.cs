using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

// Bounded, read-only MediaWiki export reader used only to feed
// WikiNotesParser across the whole corpus for the audit gate
// (Tst --wiki-notes-audit). It never writes to the settings catalog and is
// independent of WikiSvc.Analyze; it exists so the free-text/online-evidence
// audit does not have to touch the already-verified settings pipeline.
public static class WikiNotesCorpusReader
{
    private const long MaxXmlBytes = 1024L * 1024 * 1024;
    private const int MaxPages = 10_000;

    private static readonly Regex Serial = new(
        @"(?<![A-Z0-9])[BNM][A-Z]{3}\d{5}(?![A-Z0-9])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static IReadOnlyList<WikiPageEvidence> ReadAndParse(string xmlPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xmlPath);

        var results = new List<WikiPageEvidence>();
        var pageCount = 0;

        using var stream = new FileStream(
            xmlPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        using var reader = XmlReader.Create(
            stream,
            new XmlReaderSettings
            {
                CloseInput = false,
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                MaxCharactersFromEntities = 0,
                MaxCharactersInDocument = MaxXmlBytes,
                XmlResolver = null
            });

        reader.MoveToContent();
        while (!reader.EOF)
        {
            if (reader.NodeType != XmlNodeType.Element ||
                !reader.LocalName.Equals("page", StringComparison.Ordinal))
            {
                reader.Read();
                continue;
            }

            pageCount++;
            if (pageCount > MaxPages)
            {
                throw new InvalidDataException(
                    $"RPCS3 wiki XML exceeds the {MaxPages}-page audit limit.");
            }

            var page = (XElement)XNode.ReadFrom(reader);
            var title = page.Elements()
                .FirstOrDefault(element => element.Name.LocalName == "title")?
                .Value
                .Trim() ?? string.Empty;
            var namespaceText = page.Elements()
                .FirstOrDefault(element => element.Name.LocalName == "ns")?
                .Value;
            var parsedNamespace = string.IsNullOrWhiteSpace(namespaceText)
                ? 0
                : int.TryParse(
                    namespaceText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var ns)
                    ? ns
                    : -1;
            var isRedirect = page.Elements()
                .FirstOrDefault(element => element.Name.LocalName == "redirect") is not null;
            var latestRevision = page.Elements()
                .Where(element => element.Name.LocalName == "revision")
                .LastOrDefault();
            var text = latestRevision?.Elements()
                .FirstOrDefault(element => element.Name.LocalName == "text")?
                .Value ?? string.Empty;

            if (parsedNamespace != 0 ||
                isRedirect ||
                string.IsNullOrWhiteSpace(title) ||
                string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var serials = Serial.Matches(text)
                .Select(match => match.Value.ToUpperInvariant())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(serial => serial, StringComparer.Ordinal)
                .ToArray();

            results.Add(WikiNotesParser.Parse(title, serials, text));
        }

        return results;
    }
}

#pragma warning restore IDE0130
