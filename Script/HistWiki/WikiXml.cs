using System.Xml;

namespace HistWiki;

public sealed record Rev(
    long Id, long? ParentId, string Timestamp, string Contributor, string Comment, string Text, bool TextMissing);

public sealed class PageData
{
    public string Title = string.Empty;
    public int Ns;
    public long PageId;
    public string SourceFile = string.Empty;
    public List<Rev> Revs = [];
}

// Streaming MediaWiki export reader. Any malformed XML throws: the caller
// treats a throw as a corrupt source and fails closed.
public static class WikiXml
{
    public static IEnumerable<PageData> Pages(string path)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            CheckCharacters = true
        };
        using var stream = File.OpenRead(path);
        using var reader = XmlReader.Create(stream, settings);
        var file = Path.GetFileName(path);
        reader.MoveToContent();
        while (!reader.EOF)
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "page")
            {
                yield return ReadPage(reader, file);
            }
            else
            {
                reader.Read();
            }
        }
    }

    // Walks the child elements of the current element. The handler must fully
    // consume the element it is given (content read, Skip, or Children).
    private static void Children(XmlReader reader, Action<string> handler)
    {
        if (reader.IsEmptyElement)
        {
            reader.Read();
            return;
        }

        var depth = reader.Depth;
        reader.Read();
        while (!(reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth))
        {
            if (reader.EOF)
            {
                throw new XmlException("Unexpected end of XML inside an element.");
            }

            if (reader.NodeType == XmlNodeType.Element)
            {
                handler(reader.LocalName);
            }
            else
            {
                reader.Read();
            }
        }

        reader.Read();
    }

    private static PageData ReadPage(XmlReader reader, string file)
    {
        var page = new PageData { SourceFile = file };
        Children(reader, name =>
        {
            switch (name)
            {
                case "title": page.Title = reader.ReadElementContentAsString(); break;
                case "ns": page.Ns = reader.ReadElementContentAsInt(); break;
                case "id": page.PageId = reader.ReadElementContentAsLong(); break;
                case "revision": page.Revs.Add(ReadRevision(reader)); break;
                default: reader.Skip(); break;
            }
        });
        return page;
    }

    private static Rev ReadRevision(XmlReader reader)
    {
        long id = 0;
        long? parent = null;
        string ts = "", user = "", comment = "", text = "";
        var textMissing = false;
        Children(reader, name =>
        {
            switch (name)
            {
                case "id": id = reader.ReadElementContentAsLong(); break;
                case "parentid": parent = reader.ReadElementContentAsLong(); break;
                case "timestamp": ts = reader.ReadElementContentAsString(); break;
                case "comment":
                    if (reader.GetAttribute("deleted") is not null) { comment = "(deleted)"; reader.Skip(); }
                    else comment = reader.ReadElementContentAsString();
                    break;
                case "contributor":
                    if (reader.GetAttribute("deleted") is not null) { user = "(deleted)"; reader.Skip(); break; }
                    Children(reader, inner =>
                    {
                        if (inner == "username" || inner == "ip") user = reader.ReadElementContentAsString();
                        else reader.Skip();
                    });
                    break;
                case "text":
                    if (reader.GetAttribute("deleted") is not null) { textMissing = true; reader.Skip(); }
                    else text = reader.ReadElementContentAsString();
                    break;
                default: reader.Skip(); break;
            }
        });
        return new Rev(id, parent, ts, user, comment, text, textMissing);
    }
}
