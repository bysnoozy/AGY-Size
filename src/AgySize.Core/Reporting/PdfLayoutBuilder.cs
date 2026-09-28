using System.Text;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace AgySize.Core.Reporting;

/// <summary>
/// Small helper that lays out simple flowing content (headings, wrapped paragraphs, separators) on
/// an A4 <see cref="PdfDocument"/>, starting a new page automatically when content no longer fits.
/// Not a general-purpose layout engine: just enough to produce a clean, readable client report
/// without pulling in a full document-model dependency.
/// </summary>
internal sealed class PdfLayoutBuilder
{
    private const double MarginLeft = 40;
    private const double MarginRight = 40;
    private const double MarginTop = 40;
    private const double MarginBottom = 48;

    private readonly PdfDocument _document;

    private PdfPage _page = null!;
    private XGraphics _gfx = null!;
    private double _pageHeight;
    private double _y;

    public PdfLayoutBuilder(PdfDocument document)
    {
        _document = document;
        StartNewPage();
    }

    public double ContentWidth { get; private set; }

    public double CurrentY => _y;

    public double LeftMargin => MarginLeft;

    public XGraphics Graphics => _gfx;

    private void StartNewPage()
    {
        _page = _document.AddPage();
        _page.Size = PdfSharp.PageSize.A4;
        _gfx = XGraphics.FromPdfPage(_page);
        _pageHeight = _page.Height.Point;
        ContentWidth = _page.Width.Point - MarginLeft - MarginRight;
        _y = MarginTop;
    }

    /// <summary>
    /// Starts a new page if less than <paramref name="height"/> points remain before the bottom margin.
    /// </summary>
    public void EnsureSpace(double height)
    {
        if (_y + height > _pageHeight - MarginBottom)
        {
            StartNewPage();
        }
    }

    public void AddSpace(double height) => _y += height;

    public void DrawLine(string text, XFont font, XBrush brush, double height, XStringFormat? format = null)
    {
        _gfx.DrawString(text, font, brush, new XRect(MarginLeft, _y, ContentWidth, height), format ?? XStringFormats.TopLeft);
        _y += height;
    }

    /// <summary>
    /// Draws a label on the left and a value on the right of the same line.
    /// </summary>
    public void DrawLabelValueLine(string label, string value, XFont font, XBrush brush, double height)
    {
        _gfx.DrawString(label, font, brush, new XRect(MarginLeft, _y, ContentWidth, height), XStringFormats.TopLeft);
        _gfx.DrawString(value, font, brush, new XRect(MarginLeft, _y, ContentWidth, height), XStringFormats.TopRight);
        _y += height;
    }

    public double MeasureWrappedHeight(string text, XFont font)
    {
        var lineHeight = font.GetHeight();
        return WrapLines(text, font, ContentWidth).Count * lineHeight;
    }

    /// <summary>
    /// Word-wraps and draws <paramref name="text"/> within the content width, advancing the cursor
    /// by the consumed height. Draws each line directly (rather than via XTextFormatter) so the
    /// height used for pagination and the height actually drawn can never disagree.
    /// </summary>
    public void DrawWrapped(string text, XFont font, XBrush brush)
    {
        var lineHeight = font.GetHeight();
        foreach (var line in WrapLines(text, font, ContentWidth))
        {
            _gfx.DrawString(line, font, brush, new XRect(MarginLeft, _y, ContentWidth, lineHeight), XStringFormats.TopLeft);
            _y += lineHeight;
        }
    }

    public void DrawSeparator()
    {
        _gfx.DrawLine(XPens.LightGray, MarginLeft, _y, MarginLeft + ContentWidth, _y);
        _y += 1;
    }

    /// <summary>
    /// Draws a single bulleted line (a "case by case" list item, e.g. one affected file path).
    /// Callers are expected to pre-shorten <paramref name="text"/> so it fits on one line; call
    /// <see cref="EnsureSpace"/> with <paramref name="lineHeight"/> first so long lists can flow
    /// across a page break.
    /// </summary>
    public void DrawListItem(string text, XFont font, XBrush brush, double lineHeight)
    {
        const double bulletWidth = 12;
        _gfx.DrawString("•", font, brush, new XRect(MarginLeft, _y, bulletWidth, lineHeight), XStringFormats.TopLeft);
        _gfx.DrawString(text, font, brush, new XRect(MarginLeft + bulletWidth, _y, ContentWidth - bulletWidth, lineHeight), XStringFormats.TopLeft);
        _y += lineHeight;
    }

    /// <summary>
    /// Draws a small filled square marker (used as a severity indicator) at the left margin, vertically
    /// centered on a line of the given height.
    /// </summary>
    public void DrawMarker(XBrush brush, double lineHeight)
    {
        const double size = 8;
        var top = _y + (lineHeight - size) / 2;
        _gfx.DrawRectangle(brush, MarginLeft, top, size, size);
    }

    private List<string> WrapLines(string text, XFont font, double width)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var current = new StringBuilder();

        foreach (var rawWord in words)
        {
            // A "word" with no spaces (e.g. a long path with no whitespace) can still be wider than
            // the page on its own; hard-break it at the character level so nothing ever overflows.
            foreach (var word in SplitOversizedWord(rawWord, font, width))
            {
                var candidate = current.Length == 0 ? word : $"{current} {word}";
                if (current.Length > 0 && _gfx.MeasureString(candidate, font).Width > width)
                {
                    lines.Add(current.ToString());
                    current.Clear();
                    current.Append(word);
                }
                else
                {
                    current.Clear();
                    current.Append(candidate);
                }
            }
        }

        if (current.Length > 0)
        {
            lines.Add(current.ToString());
        }

        return lines.Count == 0 ? new List<string> { string.Empty } : lines;
    }

    private IEnumerable<string> SplitOversizedWord(string word, XFont font, double width)
    {
        if (word.Length == 0 || _gfx.MeasureString(word, font).Width <= width)
        {
            yield return word;
            yield break;
        }

        var chunk = new StringBuilder();
        foreach (var ch in word)
        {
            var candidate = chunk.ToString() + ch;
            if (chunk.Length > 0 && _gfx.MeasureString(candidate, font).Width > width)
            {
                yield return chunk.ToString();
                chunk.Clear();
            }

            chunk.Append(ch);
        }

        if (chunk.Length > 0)
        {
            yield return chunk.ToString();
        }
    }
}
