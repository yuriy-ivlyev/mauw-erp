using System.Buffers.Binary;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;
using PdfSharp.Drawing;

namespace Mauw.Erp.Infrastructure.Branding;

public sealed class CompanyLogoService(MauwDbContext db) : ICompanyLogoService
{
    public async Task<OperationResult<CompanyLogoDto>> GetAsync(string name, CancellationToken ct = default)
    {
        if (!DefaultCompanyLogos.Names.Contains(name))
            return OperationResult<CompanyLogoDto>.Fail("Tipo di logo non valido.");
        var logo = await db.Contacts.AsNoTracking().Where(company => company.IsInternalCompany)
            .Select(company => name == "horizontal" ? company.HorizontalLogoPng : company.CompactLogoPng)
            .SingleOrDefaultAsync(ct);
        return logo is null
            ? OperationResult<CompanyLogoDto>.Fail("Logo non trovato.")
            : OperationResult<CompanyLogoDto>.Ok(new(logo));
    }

    public async Task<OperationResult<bool>> UpdateAsync(
        string name, UpdateCompanyLogoRequest request, CancellationToken ct = default)
    {
        if (!DefaultCompanyLogos.Names.Contains(name))
            return OperationResult<bool>.Fail("Tipo di logo non valido.");
        var error = ValidatePng(request?.PngData);
        if (error is not null)
            return OperationResult<bool>.Fail(error);

        var company = await db.Contacts.SingleOrDefaultAsync(company => company.IsInternalCompany, ct);
        if (company is null)
            return OperationResult<bool>.Fail("Anagrafica aziendale non ancora configurata.");
        if (name == "horizontal")
            company.HorizontalLogoPng = request!.PngData;
        else
            company.CompactLogoPng = request!.PngData;
        await db.SaveChangesAsync(ct);
        return OperationResult<bool>.Ok(true);
    }

    public static string? ValidatePng(byte[]? data)
    {
        if (data is null || data.Length < 33 || data.Length > 2 * 1024 * 1024)
            return "Il logo deve essere un PNG non vuoto di massimo 2 MiB.";
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (!data.AsSpan(0, 8).SequenceEqual(signature) ||
            !data.AsSpan(12, 4).SequenceEqual("IHDR"u8))
            return "Formato PNG non valido.";
        var width = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(20, 4));
        if (width is 0 or > 4096 || height is 0 or > 4096)
            return "Il logo deve avere dimensioni comprese tra 1 e 4096 pixel per lato.";
        try
        {
            using var stream = new MemoryStream(data, writable: false);
            using var image = XImage.FromStream(stream);
            return image.PixelWidth == width && image.PixelHeight == height
                ? null : "Dimensioni PNG non valide.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or
                                   InvalidDataException or NotSupportedException or IndexOutOfRangeException)
        {
            return "Impossibile leggere il PNG: immagine danneggiata o non supportata.";
        }
    }
}
