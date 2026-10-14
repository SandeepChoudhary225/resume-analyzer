using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;

using ResumeAnalyzer.Domain;

namespace ResumeAnalyzer.Documents;

public sealed partial class DocumentService
{
    private const int MaximumZipEntries = 1024;
    private const long MaximumExpandedZipBytes = 64 * 1024 * 1024;
    private const long MaximumZipEntryBytes = 16 * 1024 * 1024;
    private const string WordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string StrictWordNamespace = "http://purl.oclc.org/ooxml/wordprocessingml/main";
    private const string CompatibilityNamespace = "http://schemas.openxmlformats.org/markup-compatibility/2006";

    private static string ReadDocx(byte[] bytes, List<string> warnings, CancellationToken token)
    {
        if (bytes.AsSpan().StartsWith(new byte[] { 0xD0, 0xCF, 0x11, 0xE0 }))
            throw new DocumentException("This document is encrypted or is a legacy DOC file. Save an unprotected DOCX copy.");
        using var input = new MemoryStream(bytes, writable: false);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read);
        if (archive.Entries.Count > MaximumZipEntries)
            throw new DocumentException("The DOCX exceeds the 1,024-entry ZIP safety limit.");
        var entries = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        long expanded = 0;
        foreach (var entry in archive.Entries)
        {
            token.ThrowIfCancellationRequested();
            var name = entry.FullName;
            if (name.StartsWith('/') || name.Contains('\\') || name.Contains(':') || name.Contains('%') ||
                name.Split('/').Any(part => part is ".." or ".") || name.Any(char.IsControl))
                throw new DocumentException("The DOCX contains an unsafe ZIP entry path.");
            if (entries.ContainsKey(name)) throw new DocumentException("The DOCX contains ambiguous duplicate ZIP entries.");
            if (entry.Length > MaximumZipEntryBytes || expanded + entry.Length > MaximumExpandedZipBytes)
                throw new DocumentException("The DOCX exceeds the expanded ZIP safety limit (16 MB per entry, 64 MB total).");
            if (name.Contains("vba", StringComparison.OrdinalIgnoreCase) || name.Contains("/activeX/", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("/embeddings/", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                throw new DocumentException("Macro-enabled DOCX files and embedded active content are not supported. Save a clean DOCX copy.");
            using var part = entry.Open();
            var data = ReadBounded(part, Math.Min(MaximumZipEntryBytes, MaximumExpandedZipBytes - expanded), token);
            if (data.LongLength != entry.Length) throw new DocumentException("The DOCX contains a corrupt ZIP entry.");
            expanded += data.LongLength;
            entries.Add(name, data);
        }
        if (!entries.TryGetValue("[Content_Types].xml", out var types) || !entries.TryGetValue("word/document.xml", out var body))
            throw new DocumentException("The file is not a valid DOCX package (required document parts are missing).");
        var isWordDocument = false;
        VisitXml(types, reader =>
        {
            if (reader.NodeType != XmlNodeType.Element) return;
            var type = reader.GetAttribute("ContentType") ?? "";
            if (type.Contains("macro", StringComparison.OrdinalIgnoreCase) || type.Contains("vba", StringComparison.OrdinalIgnoreCase) ||
                type.Contains("activeX", StringComparison.OrdinalIgnoreCase))
                throw new DocumentException("Macro-enabled documents are not supported. Save an ordinary DOCX copy.");
            if (reader.GetAttribute("PartName") == "/word/document.xml" &&
                type == "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml") isWordDocument = true;
        }, token);
        if (!isWordDocument) throw new DocumentException("The package is not an ordinary, unprotected DOCX document.");

        var hasExternalLinks = false;
        foreach (var pair in entries.Where(pair => pair.Key.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)))
            VisitXml(pair.Value, reader =>
            {
                if (string.Equals(reader.GetAttribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase)) hasExternalLinks = true;
            }, token);
        if (hasExternalLinks) warnings.Add("External links/resources were not opened; only text stored in the DOCX was imported.");
        if (entries.Keys.Any(name => name.StartsWith("word/media/", StringComparison.OrdinalIgnoreCase)))
            warnings.Add("DOCX images were ignored. Text inside images requires OCR and is not imported.");

        var headers = entries.Where(pair => IsWordPart(pair.Key, "header")).OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).ToList();
        var footers = entries.Where(pair => IsWordPart(pair.Key, "footer")).OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).ToList();
        var text = new StringBuilder();
        foreach (var header in headers) AppendBounded(text, ReadWordXml(header.Value, "hdr", warnings, token) + "\n");
        AppendBounded(text, ReadWordXml(body, "document", warnings, token));
        foreach (var footer in footers) AppendBounded(text, "\n" + ReadWordXml(footer.Value, "ftr", warnings, token));
        if (headers.Count + footers.Count > 0)
            warnings.Add("Headers appear once before the body and footers once after it, in part-name order; section/page repetition is not reconstructed.");
        warnings.Add("DOCX paragraphs and table cells follow package reading order; columns, floating text, list numbering, and visual layout may differ.");
        return text.ToString();
    }

    private static bool IsWordPart(string name, string prefix) =>
        name.StartsWith("word/" + prefix, StringComparison.OrdinalIgnoreCase) &&
        name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) && name.Count(c => c == '/') == 1;

    private static string ReadWordXml(byte[] bytes, string root, List<string> warnings, CancellationToken token)
    {
        var text = new StringBuilder();
        var inText = false;
        var sawRoot = false;
        var hasBody = root != "document";
        var hasComplexContent = false;
        var alternatives = new Stack<(int Depth, bool Selected)>();
        var skippedDepth = -1;
        VisitXml(bytes, reader =>
        {
            if (skippedDepth >= 0)
            {
                if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == skippedDepth) skippedDepth = -1;
                return;
            }
            var word = reader.NamespaceURI is WordNamespace or StrictWordNamespace;
            if (reader.NodeType == XmlNodeType.Element)
            {
                if (!sawRoot)
                {
                    if (!word || reader.LocalName != root) throw new DocumentException("The DOCX has an invalid Word document part.");
                    sawRoot = true;
                }
                if (reader.NamespaceURI == CompatibilityNamespace)
                {
                    if (reader.LocalName == "AlternateContent")
                    {
                        hasComplexContent = true;
                        if (!reader.IsEmptyElement) alternatives.Push((reader.Depth, false));
                    }
                    else if (reader.LocalName is "Choice" or "Fallback" && alternatives.Count > 0 &&
                        reader.Depth == alternatives.Peek().Depth + 1)
                    {
                        var alternative = alternatives.Pop();
                        var requires = reader.GetAttribute("Requires");
                        var supported = reader.LocalName == "Fallback" ||
                            (!string.IsNullOrWhiteSpace(requires) && requires.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                                .All(prefix => reader.LookupNamespace(prefix) is WordNamespace or StrictWordNamespace));
                        // Only one representation is displayed by Word. Reading both Choice and
                        // Fallback duplicates text (and can manufacture extra skill evidence).
                        if (!alternative.Selected && supported) alternative.Selected = true;
                        else if (!reader.IsEmptyElement) skippedDepth = reader.Depth;
                        alternatives.Push(alternative);
                    }
                    return;
                }
                if (!word) return;
                if (reader.LocalName == "body") hasBody = true;
                if (reader.LocalName == "t") inText = !reader.IsEmptyElement;
                if (reader.LocalName == "tab") AppendBounded(text, "\t");
                if (reader.LocalName is "br" or "cr") AppendBounded(text, "\n");
                if (reader.LocalName is "drawing" or "pict" or "altChunk" or "del" or "ins" or "txbxContent") hasComplexContent = true;
                if (reader.IsEmptyElement && reader.LocalName == "p") AppendBounded(text, "\n");
            }
            else if (reader.NodeType == XmlNodeType.EndElement && reader.NamespaceURI == CompatibilityNamespace &&
                reader.LocalName == "AlternateContent")
            {
                alternatives.Pop();
            }
            else if (reader.NodeType == XmlNodeType.EndElement && word)
            {
                if (reader.LocalName == "t") inText = false;
                if (reader.LocalName is "p" or "tr") AppendBounded(text, "\n");
                if (reader.LocalName == "tc") AppendBounded(text, "\t");
            }
            else if (inText && reader.NodeType is XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.SignificantWhitespace or XmlNodeType.Whitespace)
                AppendBounded(text, reader.Value);
        }, token);
        if (!sawRoot || !hasBody) throw new DocumentException("The DOCX is corrupt (missing document body).");
        if (hasComplexContent && !warnings.Any(w => w.StartsWith("Complex DOCX", StringComparison.Ordinal)))
            warnings.Add("Complex DOCX content was flattened: drawings/imported or unsupported compatibility content may be omitted, and tracked changes/floating text need review.");
        return text.ToString();
    }

    private static void VisitXml(byte[] bytes, Action<XmlReader> visit, CancellationToken token)
    {
        using var input = new MemoryStream(bytes, writable: false);
        using var reader = XmlReader.Create(input, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumZipEntryBytes,
            MaxCharactersFromEntities = 1024,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true
        });
        while (reader.Read())
        {
            token.ThrowIfCancellationRequested();
            if (reader.Depth > 128) throw new DocumentException("The DOCX XML exceeds the nesting safety limit.");
            visit(reader);
        }
    }

    private static void AppendBounded(StringBuilder text, string value)
    {
        if (text.Length + value.Length > MaximumTextCharacters) throw TextTooLong();
        text.Append(value);
    }

    private static void WriteDocx(Stream output, string text, CancellationToken token)
    {
        XmlConvert.VerifyXmlChars(text);
        using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        WriteZipText(archive, "[Content_Types].xml", """
            <?xml version="1.0" encoding="utf-8"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>
            """);
        WriteZipText(archive, "_rels/.rels", """
            <?xml version="1.0" encoding="utf-8"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>
            """);
        using var part = archive.CreateEntry("word/document.xml", CompressionLevel.Optimal).Open();
        using var writer = XmlWriter.Create(part, new XmlWriterSettings { Encoding = new UTF8Encoding(false), CloseOutput = false });
        writer.WriteStartDocument();
        writer.WriteStartElement("w", "document", WordNamespace);
        writer.WriteStartElement("w", "body", WordNamespace);
        foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            token.ThrowIfCancellationRequested();
            writer.WriteStartElement("w", "p", WordNamespace);
            writer.WriteStartElement("w", "r", WordNamespace);
            var segments = line.Split('\t');
            for (var i = 0; i < segments.Length; i++)
            {
                if (i > 0) writer.WriteElementString("w", "tab", WordNamespace, "");
                writer.WriteStartElement("w", "t", WordNamespace);
                writer.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
                writer.WriteString(segments[i]);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteZipText(ZipArchive archive, string path, string text)
    {
        using var stream = archive.CreateEntry(path, CompressionLevel.Optimal).Open();
        WriteUtf8(stream, text);
    }
}
