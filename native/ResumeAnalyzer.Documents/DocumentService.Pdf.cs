using System.IO;
using System.Text;
using System.Windows.Media;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using ResumeAnalyzer.Domain;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Exceptions;
using PigDocument = UglyToad.PdfPig.PdfDocument;
using SharpDocument = PdfSharp.Pdf.PdfDocument;

namespace ResumeAnalyzer.Documents;

public sealed partial class DocumentService
{
    private static string ReadPdf(byte[] bytes, List<string> warnings, out int? pages, CancellationToken token)
    {
        if (bytes.AsSpan(0, Math.Min(bytes.Length, 1024)).IndexOf("%PDF-"u8) < 0)
            throw new DocumentException("The PDF is corrupt (missing PDF signature).");
        try
        {
            using var pdf = PigDocument.Open(bytes, new ParsingOptions { UseLenientParsing = false, MaxStackDepth = 128, UseActualText = true });
            if (pdf.IsEncrypted) throw new DocumentException("Encrypted PDFs are not supported. Save an unprotected PDF copy.");
            pages = pdf.NumberOfPages;
            if (pages is > 1000) throw new DocumentException("The PDF exceeds the 1,000-page safety limit.");
            var text = new StringBuilder();
            var emptyPages = 0;
            var columns = false;
            for (var index = 1; index <= pdf.NumberOfPages; index++)
            {
                token.ThrowIfCancellationRequested();
                var page = pdf.GetPage(index);
                if (page.Letters.Count > MaximumTextCharacters - text.Length) throw TextTooLong();
                var words = page.GetWords().Where(word => !string.IsNullOrWhiteSpace(word.Text)).ToList();
                token.ThrowIfCancellationRequested();
                if (words.Count == 0) { emptyPages++; continue; }
                if (text.Length > 0) AppendBounded(text, "\n\n");
                AppendBounded(text, OrderPdfWords(words, page.Width, ref columns, token));
            }
            if (string.IsNullOrWhiteSpace(text.ToString()))
                throw new DocumentException("This PDF contains no selectable text. It may be scanned or image-only; OCR is not included. Export a text-based PDF, DOCX, RTF, or TXT instead.");
            warnings.Add("PDF text order is estimated from glyph positions; verify bullets, tables, spacing, and reading order against the original.");
            if (columns) warnings.Add("A possible two-column PDF layout was detected and read left column then right column within full-width heading bands. Review the inferred order.");
            if (emptyPages > 0) warnings.Add($"{emptyPages} PDF page(s) contained no selectable text and were skipped; image-only pages require OCR.");
            return text.ToString();
        }
        catch (PdfDocumentEncryptedException)
        {
            throw new DocumentException("The PDF is encrypted or password-protected. Save an unprotected PDF copy before importing.");
        }
    }

    private sealed class PdfLine(Word first)
    {
        public double Top { get; } = first.BoundingBox.Top;
        public List<Word> Words { get; } = [first];
    }

    private static string OrderPdfWords(List<Word> words, double width, ref bool detectedColumns, CancellationToken token)
    {
        var lines = new List<PdfLine>();
        foreach (var word in words.OrderByDescending(word => word.BoundingBox.Top).ThenBy(word => word.BoundingBox.Left))
        {
            token.ThrowIfCancellationRequested();
            if (lines.Count == 0 || Math.Abs(lines[^1].Top - word.BoundingBox.Top) > Math.Max(2, Math.Min(6, word.BoundingBox.Height * .45)))
                lines.Add(new PdfLine(word));
            else lines[^1].Words.Add(word);
        }
        foreach (var line in lines) line.Words.Sort((a, b) => a.BoundingBox.Left.CompareTo(b.BoundingBox.Left));
        // A repeated, wide central gutter suggests two columns. This is deliberately heuristic.
        var candidates = new List<double>();
        foreach (var line in lines)
            for (var i = 1; i < line.Words.Count; i++)
            {
                var left = line.Words[i - 1].BoundingBox.Right;
                var right = line.Words[i].BoundingBox.Left;
                var middle = (left + right) / 2;
                if (right - left >= width * .07 && middle > width * .22 && middle < width * .78) candidates.Add(middle);
            }
        var bucket = candidates.GroupBy(x => (int)Math.Round(x / Math.Max(1, width * .06)))
            .OrderByDescending(group => group.Count()).FirstOrDefault();
        double? split = bucket is not null && bucket.Count() >= Math.Max(3, lines.Count * .25) ? bucket.Average() : null;
        var output = new StringBuilder();
        if (split is null)
        {
            foreach (var line in lines) AppendLine(line.Words);
        }
        else
        {
            detectedColumns = true;
            var left = new List<List<Word>>();
            var right = new List<List<Word>>();
            foreach (var line in lines)
            {
                token.ThrowIfCancellationRequested();
                if (line.Words.Any(word => word.BoundingBox.Left < split && word.BoundingBox.Right > split))
                {
                    FlushColumns();
                    AppendLine(line.Words);
                }
                else
                {
                    left.Add(line.Words.Where(word => word.BoundingBox.Left < split).ToList());
                    right.Add(line.Words.Where(word => word.BoundingBox.Left >= split).ToList());
                }
            }
            FlushColumns();

            void FlushColumns()
            {
                foreach (var row in left) AppendLine(row);
                foreach (var row in right) AppendLine(row);
                left.Clear();
                right.Clear();
            }
        }
        return output.ToString();

        void AppendLine(List<Word> row)
        {
            if (row.Count == 0) return;
            if (output.Length > 0) AppendBounded(output, "\n");
            foreach (var word in row)
            {
                if (!ReferenceEquals(word, row[0])) AppendBounded(output, " ");
                AppendBounded(output, word.Text);
            }
        }
    }

    private static void WritePdf(Stream output, string text, string title, CancellationToken token)
    {
        const string family = "Segoe UI";
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Replace("\t", "    ", StringComparison.Ordinal);
        // Do not silently emit missing-glyph boxes. Use the same installed font for validation and embedding.
        if (!new Typeface(family).TryGetGlyphTypeface(out var typeface))
            throw new DocumentException("The Windows Segoe UI font is unavailable. Choose DOCX or TXT export.");
        foreach (var rune in normalized.EnumerateRunes())
        {
            token.ThrowIfCancellationRequested();
            if (rune.Value != '\n' && (!typeface.CharacterToGlyphMap.TryGetValue(rune.Value, out var glyph) || glyph == 0))
                throw new DocumentException($"The PDF font cannot represent character U+{rune.Value:X4}. Choose DOCX or TXT to preserve the complete text.");
        }
        using var pdf = new SharpDocument();
        pdf.Info.Title = title;
        pdf.Info.Creator = "Resume Analyzer (offline)";
        var font = new XFont(family, 10, XFontStyleEx.Regular, new XPdfFontOptions(PdfFontEncoding.Unicode, PdfFontEmbedding.TryComputeSubset));
        XGraphics? graphics = null;
        const double margin = 48;
        const double lineHeight = 15;
        double y = 0;
        double pageWidth = 0;
        double pageHeight = 0;
        try
        {
            NewPage();
            foreach (var paragraph in normalized.Split('\n'))
            {
                token.ThrowIfCancellationRequested();
                if (paragraph.Length == 0) { NextLine(""); continue; }
                var offset = 0;
                while (offset < paragraph.Length)
                {
                    token.ThrowIfCancellationRequested();
                    var length = Math.Min(512, paragraph.Length - offset);
                    // Binary search a fitting prefix; avoid quadratic work on very long unbroken lines.
                    var low = 1;
                    var high = length;
                    while (low <= high)
                    {
                        var middle = (low + high) / 2;
                        if (graphics!.MeasureString(paragraph.Substring(offset, middle), font).Width <= pageWidth - margin * 2)
                        { length = middle; low = middle + 1; }
                        else high = middle - 1;
                    }
                    if (offset + length < paragraph.Length)
                    {
                        var space = paragraph.LastIndexOf(' ', offset + length - 1, length);
                        if (space > offset) length = space - offset;
                        if (char.IsHighSurrogate(paragraph[offset + length - 1])) length--;
                    }
                    if (length <= 0) throw new DocumentException("A PDF glyph cannot fit on the page. Choose DOCX or TXT export.");
                    NextLine(paragraph.Substring(offset, length));
                    offset += length;
                    while (offset < paragraph.Length && paragraph[offset] == ' ') offset++;
                }
            }
            graphics!.Dispose();
            graphics = null;
            token.ThrowIfCancellationRequested();
            pdf.Save(output, closeStream: false);
        }
        finally { graphics?.Dispose(); }

        void NewPage()
        {
            graphics?.Dispose();
            var page = pdf.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            pageWidth = page.Width.Point;
            pageHeight = page.Height.Point;
            graphics = XGraphics.FromPdfPage(page);
            y = margin;
        }
        void NextLine(string line)
        {
            if (y + lineHeight > pageHeight - margin) NewPage();
            if (line.Length > 0) graphics!.DrawString(line, font, XBrushes.Black, new XPoint(margin, y + 11));
            y += lineHeight;
        }
    }
}
