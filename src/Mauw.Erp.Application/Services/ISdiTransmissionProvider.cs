namespace Mauw.Erp.Application.Services;

public interface ISdiTransmissionProvider
{
    Task<SdiTransmissionResult> TransmitAsync(
        string fileName,
        byte[] xmlContent,
        CancellationToken cancellationToken = default);
}

public sealed record SdiTransmissionResult(bool Success, string? TransmissionId, string? ErrorMessage);
