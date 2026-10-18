namespace Mauw.Erp.Infrastructure.Branding;

public static class DefaultCompanyLogos
{
    public static readonly IReadOnlyList<string> Names = Array.AsReadOnly(new[] { "horizontal", "compact" });

    public static byte[] ReadPng(string name)
    {
        if (!Names.Contains(name))
            throw new ArgumentOutOfRangeException(nameof(name));

        using var stream = typeof(DefaultCompanyLogos).Assembly.GetManifestResourceStream(
            $"Mauw.Erp.Infrastructure.Reporting.MigraDoc.Images.mauw-{name}.png")
            ?? throw new InvalidOperationException($"Logo iniziale '{name}' non disponibile.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
