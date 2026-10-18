using PdfSharp.Fonts;

namespace Mauw.Erp.Infrastructure.Reporting.MigraDoc;

internal sealed class EmbeddedFontResolver : IFontResolver
{
    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
    {
        var style = (bold, italic) switch
        {
            (true, true) => "BoldItalic",
            (true, false) => "Bold",
            (false, true) => "Italic",
            _ => "Regular"
        };

        // MigraDoc also resolves predefined fonts (including Courier New) before rendering.
        return familyName.ToLowerInvariant() switch
        {
            "arial" or "noto sans" or "courier new" or "times new roman" or "symbol" or "wingdings"
                => new FontResolverInfo($"NotoSans-{style}"),
            _ => null
        };
    }

    public byte[] GetFont(string faceName)
    {
        var assembly = typeof(EmbeddedFontResolver).Assembly;
        using var stream = assembly.GetManifestResourceStream(
            $"Mauw.Erp.Infrastructure.Reporting.MigraDoc.Fonts.{faceName}.ttf")
            ?? throw new InvalidOperationException($"Embedded PDF font '{faceName}' was not found.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
