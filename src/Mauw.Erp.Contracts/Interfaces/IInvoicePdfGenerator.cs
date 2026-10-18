using Mauw.Erp.Contracts.Dtos.Pdf;

namespace Mauw.Erp.Contracts.Interfaces;

/// <summary>
/// Generates a binary PDF for an Italian fashion wholesale invoice.
/// Implementations live in Infrastructure; no MigraDoc types surface here.
/// </summary>
public interface IInvoicePdfGenerator
{
    Task<byte[]> GenerateInvoicePdfAsync(InvoicePdfModel model, CancellationToken ct = default);
}
