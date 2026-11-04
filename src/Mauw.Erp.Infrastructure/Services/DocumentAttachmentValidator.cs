using Mauw.Erp.Application.Services;

namespace Mauw.Erp.Infrastructure.Services;

internal static class DocumentAttachmentValidator
{
    public const long MaximumFileSize = 20 * 1024 * 1024;

    public static async Task<OperationResult<MemoryStream>> ReadAndValidateAsync(
        Stream stream,
        string fileName,
        string contentType,
        long length,
        CancellationToken cancellationToken)
    {
        if (length <= 0 || length > MaximumFileSize)
            return OperationResult<MemoryStream>.Fail("Allegare un file non vuoto di massimo 20 MB.");
        if (contentType is not ("application/pdf" or "image/png" or "image/jpeg"))
            return OperationResult<MemoryStream>.Fail("Sono ammessi solo file PDF, PNG e JPEG.");
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if ((contentType, extension) is not (("application/pdf", ".pdf")
            or ("image/png", ".png") or ("image/jpeg", ".jpg") or ("image/jpeg", ".jpeg")))
            return OperationResult<MemoryStream>.Fail("Estensione e formato dell'allegato non corrispondono.");

        var buffer = new MemoryStream();
        try
        {
            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + read > MaximumFileSize)
                {
                    buffer.Dispose();
                    return OperationResult<MemoryStream>.Fail("Il documento supera 20 MB.");
                }
                await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
            }
            if (!ValidSignature(buffer.GetBuffer().AsSpan(0, (int)buffer.Length), contentType))
            {
                buffer.Dispose();
                return OperationResult<MemoryStream>.Fail(
                    "Il contenuto del file non corrisponde al formato dichiarato.");
            }

            buffer.Position = 0;
            return OperationResult<MemoryStream>.Ok(buffer);
        }
        catch
        {
            buffer.Dispose();
            throw;
        }
    }

    private static bool ValidSignature(ReadOnlySpan<byte> bytes, string contentType) =>
        contentType switch
        {
            "application/pdf" => bytes.StartsWith("%PDF-"u8),
            "image/png" => bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            "image/jpeg" => bytes.Length >= 3
                && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            _ => false
        };
}
