using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using ResumeAnalyzer.Domain;

namespace ResumeAnalyzer.Documents;

/// <summary>Offline, bounded document conversion. No Office installation or network service is used.</summary>
public sealed partial class DocumentService : IDocumentParser, IExportService
{
    public const long MaximumFileBytes = 15 * 1024 * 1024;
    public const int MaximumTextCharacters = 200_000;

    public Task<DocumentData> ParseAsync(string path, CancellationToken cancellationToken = default) =>
        OnStaThread(() => Parse(path, cancellationToken), cancellationToken);

    public Task ExportResumeAsync(string text, string path, CancellationToken cancellationToken = default) =>
        OnStaThread(() =>
        {
            ValidateText(text);
            AtomicExport(path, stream =>
            {
                switch (Path.GetExtension(path).ToLowerInvariant())
                {
                    case ".txt": WriteUtf8(stream, text); break;
                    case ".docx": WriteDocx(stream, text, cancellationToken); break;
                    case ".pdf": WritePdf(stream, text, "Resume", cancellationToken); break;
                    case ".rtf":
                        var document = new FlowDocument();
                        foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            document.Blocks.Add(new Paragraph(new Run(line)));
                        }
                        new TextRange(document.ContentStart, document.ContentEnd).Save(stream, DataFormats.Rtf);
                        break;
                    default: throw new DocumentException("Unsupported resume export format. Choose TXT, RTF, DOCX, or PDF.");
                }
            }, cancellationToken);
            return true;
        }, cancellationToken);

    public Task ExportAnalysisAsync(AnalysisResult result, string path, CancellationToken cancellationToken = default) =>
        OnStaThread(() =>
        {
            ArgumentNullException.ThrowIfNull(result);
            var text = AnalysisText(result);
            AtomicExport(path, stream =>
            {
                switch (Path.GetExtension(path).ToLowerInvariant())
                {
                    case ".txt": WriteUtf8(stream, text); break;
                    case ".html": case ".htm": WriteUtf8(stream, AnalysisHtml(result, text)); break;
                    case ".pdf": WritePdf(stream, text, "Resume analysis", cancellationToken); break;
                    default: throw new DocumentException("Unsupported analysis export format. Choose HTML, PDF, or TXT.");
                }
            }, cancellationToken);
            return true;
        }, cancellationToken);

    private static DocumentData Parse(string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is not (".txt" or ".rtf" or ".docx" or ".pdf"))
            throw new DocumentException("Unsupported document format. Choose PDF, DOCX, RTF, or TXT (not legacy DOC).");
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length > MaximumFileBytes) throw new DocumentException("The document exceeds the 15 MB import limit.");
        if (file.Length == 0) throw new DocumentException("The document is empty.");
        var bytes = ReadBounded(file, MaximumFileBytes, token);
        var warnings = new List<string>();
        int? pages = null;
        var text = extension switch
        {
            ".txt" => ReadPlainText(bytes),
            ".rtf" => ReadRtf(bytes, token),
            ".docx" => ReadDocx(bytes, warnings, token),
            ".pdf" => ReadPdf(bytes, warnings, out pages, token),
            _ => throw new DocumentException("Unsupported document format.")
        };
        token.ThrowIfCancellationRequested();
        if (text.Length > MaximumTextCharacters) throw TextTooLong();
        // Normalize line endings only: preserve indentation, bullets, and accents.
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();
        ValidateText(text);
        return new DocumentData(Path.GetFileName(path), text, pages, extension[1..].ToUpperInvariant(), warnings);
    }

    private static string ReadPlainText(byte[] bytes)
    {
        // StreamReader's automatic BOM detection replaces the supplied strict decoder with
        // a replacement-fallback encoding. Select a strict encoding ourselves so malformed
        // UTF-16/UTF-32 (and BOM-prefixed UTF-8) cannot silently change resume evidence.
        Encoding encoding = new UTF8Encoding(true, true);
        var prefix = bytes.AsSpan();
        if (prefix.StartsWith(new byte[] { 0xff, 0xfe, 0x00, 0x00 })) encoding = new UTF32Encoding(false, true, true);
        else if (prefix.StartsWith(new byte[] { 0x00, 0x00, 0xfe, 0xff })) encoding = new UTF32Encoding(true, true, true);
        else if (prefix.StartsWith(new byte[] { 0xff, 0xfe })) encoding = new UnicodeEncoding(false, true, true);
        else if (prefix.StartsWith(new byte[] { 0xfe, 0xff })) encoding = new UnicodeEncoding(true, true, true);
        using var input = new MemoryStream(bytes, writable: false);
        using var reader = new StreamReader(input, encoding, detectEncodingFromByteOrderMarks: false);
        var builder = new StringBuilder();
        var buffer = new char[4096];
        int count;
        while ((count = reader.Read(buffer, 0, buffer.Length)) != 0)
        {
            if (builder.Length + count > MaximumTextCharacters) throw TextTooLong();
            builder.Append(buffer, 0, count);
        }
        if (builder.ToString().Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t' or '\f')))
            throw new DocumentException("The TXT document contains binary data. Save it as UTF-8 or BOM-marked Unicode text.");
        return builder.ToString();
    }

    private static string ReadRtf(byte[] bytes, CancellationToken token)
    {
        ValidateRtf(bytes, token);
        using var input = new MemoryStream(bytes, writable: false);
        var document = new FlowDocument();
        var range = new TextRange(document.ContentStart, document.ContentEnd);
        range.Load(input, DataFormats.Rtf);
        token.ThrowIfCancellationRequested();
        return range.Text;
    }

    private static void ValidateRtf(byte[] bytes, CancellationToken token)
    {
        var source = Encoding.Latin1.GetString(bytes);
        if (!source.StartsWith("{\\rtf1", StringComparison.Ordinal))
            throw new DocumentException("The RTF document is corrupt or has an unsupported RTF version.");
        // WPF never receives fields, linked/embedded objects, pictures, or binary payloads.
        // This deliberately favors safe text import over rich RTF feature compatibility.
        var prohibited = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "object", "objdata", "objclass", "field", "fldinst", "pict", "shppict", "nonshppict",
            "bin", "datastore", "htmltag", "htmlrtf", "filetbl", "fontemb", "fontfile", "shp", "shpinst"
        };
        var depth = 0;
        for (var i = 0; i < source.Length; i++)
        {
            if ((i & 4095) == 0) token.ThrowIfCancellationRequested();
            var c = source[i];
            if (c == '{')
            {
                if (++depth > 128) throw new DocumentException("The RTF document exceeds the nesting safety limit.");
            }
            else if (c == '}')
            {
                if (--depth < 0) throw new DocumentException("The RTF document is corrupt (unbalanced groups).");
                if (depth == 0 && source.AsSpan(i + 1).Trim().Length != 0)
                    throw new DocumentException("The RTF document is corrupt (trailing data).");
            }
            else if (c == '\\')
            {
                var start = ++i;
                while (i < source.Length && char.IsAsciiLetter(source[i])) i++;
                if (i > start)
                {
                    if (prohibited.Contains(source[start..i]))
                        throw new DocumentException("This RTF contains fields, images, or embedded/external resources. Save a text-only RTF or TXT copy before importing.");
                    i--;
                }
                else if (i < source.Length && source[i] == '\'') i += 2;
            }
        }
        if (depth != 0) throw new DocumentException("The RTF document is corrupt (unbalanced groups).");
    }

    private static void ValidateText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new DocumentException("The document contains no readable text.");
        if (text.Length > MaximumTextCharacters) throw TextTooLong();
    }

    private static DocumentException TextTooLong() => new("The document exceeds the 200,000-character text limit.");

    private static byte[] ReadBounded(Stream stream, long maximum, CancellationToken token)
    {
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            token.ThrowIfCancellationRequested();
            if (output.Length + read > maximum) throw new DocumentException("The document exceeds an expanded-data safety limit.");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    private static void WriteUtf8(Stream stream, string text)
    {
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true);
        writer.Write(text);
    }

    private static void AtomicExport(string path, Action<Stream> write, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var destination = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(destination)!;
        if (!Directory.Exists(directory)) throw new DocumentException("The export folder does not exist.");
        // Never truncate the destination; only replace it after serialization and disk flush succeed.
        var temporary = Path.Combine(directory, $".resume-export-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                write(output);
                token.ThrowIfCancellationRequested();
                output.Flush(flushToDisk: true);
            }
            token.ThrowIfCancellationRequested();
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (IOException) { /* A cleanup failure must not hide the conversion failure. */ }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static Task<T> OnStaThread<T>(Func<T> operation, CancellationToken token)
    {
        if (token.IsCancellationRequested) return Task.FromCanceled<T>(token);
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                token.ThrowIfCancellationRequested();
                completion.TrySetResult(operation());
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { completion.TrySetCanceled(token); }
            catch (DocumentException exception) { completion.TrySetException(exception); }
            catch (Exception exception)
            {
                var message = exception switch
                {
                    FileNotFoundException or DirectoryNotFoundException => "The document or folder could not be found.",
                    UnauthorizedAccessException => "Access was denied. Choose a readable document or writable export folder.",
                    DecoderFallbackException => "The TXT document has an unsupported encoding. Save it as UTF-8 or BOM-marked Unicode.",
                    IOException => "The document could not be read or written. It may be corrupt, locked, or unavailable.",
                    _ => "The document is corrupt, encrypted, or contains unsupported content. Try saving a fresh, unprotected copy."
                };
                completion.TrySetException(new DocumentException(message));
            }
        }) { IsBackground = true, Name = "ResumeAnalyzer document conversion" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
