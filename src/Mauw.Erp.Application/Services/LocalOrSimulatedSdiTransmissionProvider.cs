namespace Mauw.Erp.Application.Services;

public sealed class LocalOrSimulatedSdiTransmissionProvider : ISdiTransmissionProvider
{
    public Task<SdiTransmissionResult> TransmitAsync(
        string fileName,
        byte[] xmlContent,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(fileName) || xmlContent.Length == 0)
            return Task.FromResult(new SdiTransmissionResult(
                false, null, "Il file XML da trasmettere è vuoto o non valido."));

        return Task.FromResult(new SdiTransmissionResult(
            true, $"LOCAL-{Guid.NewGuid():N}", null));
    }
}
