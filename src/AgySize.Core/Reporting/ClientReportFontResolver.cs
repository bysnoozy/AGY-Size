using PdfSharp.Fonts;

namespace AgySize.Core.Reporting;

/// <summary>
/// Resolves a regular/bold TrueType font pair from the host OS for PDF generation. PDFsharp 6.x no
/// longer reads system fonts implicitly, so a resolver must be supplied. Rather than bundling a font
/// file with the app, this looks up a handful of well-known font locations on Windows, Linux and
/// macOS and uses the first one found.
/// </summary>
internal sealed class ClientReportFontResolver : IFontResolver
{
    private const string FamilyName = "Rapport";
    private const string RegularFace = "Rapport#Regular";
    private const string BoldFace = "Rapport#Bold";

    private static readonly string[] RegularCandidates =
    {
        @"C:\Windows\Fonts\segoeui.ttf",
        @"C:\Windows\Fonts\arial.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
        "/usr/share/fonts/truetype/liberation2/LiberationSans-Regular.ttf",
        "/Library/Fonts/Arial.ttf",
        "/System/Library/Fonts/Supplemental/Arial.ttf",
    };

    private static readonly string[] BoldCandidates =
    {
        @"C:\Windows\Fonts\segoeuib.ttf",
        @"C:\Windows\Fonts\arialbd.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
        "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf",
        "/usr/share/fonts/truetype/liberation2/LiberationSans-Bold.ttf",
        "/Library/Fonts/Arial Bold.ttf",
    };

    private byte[]? _regular;
    private byte[]? _bold;

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        new(isBold ? BoldFace : RegularFace);

    public byte[] GetFont(string faceName) => faceName == BoldFace
        ? _bold ??= LoadFirstAvailable(BoldCandidates, RegularCandidates)
        : _regular ??= LoadFirstAvailable(RegularCandidates, RegularCandidates);

    public static string Family => FamilyName;

    private static byte[] LoadFirstAvailable(string[] candidates, string[] fallback)
    {
        foreach (var path in candidates.Concat(fallback).Distinct())
        {
            if (File.Exists(path))
            {
                return File.ReadAllBytes(path);
            }
        }

        throw new InvalidOperationException(
            "Aucune police système n'a été trouvée pour générer le PDF. Installez une police TrueType " +
            "standard (ex. Segoe UI, Arial) sur cette machine, ou choisissez un autre format d'export.");
    }
}
