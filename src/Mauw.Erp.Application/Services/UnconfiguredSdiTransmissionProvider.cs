namespace Mauw.Erp.Application.Services;

public sealed class UnconfiguredSdiTransmissionProvider : ISdiTransmissionProvider
{
    public Task<SdiTransmissionResult> TransmitAsync(
        string fileName,
        byte[] xmlContent,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new SdiTransmissionResult(
            false, null, "Il provider SDI non è configurato per questo ambiente."));
    }
}
