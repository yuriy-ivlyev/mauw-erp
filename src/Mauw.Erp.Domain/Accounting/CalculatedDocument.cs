using System.Collections.Immutable;

namespace Mauw.Erp.Domain.Accounting;

public sealed record CalculatedDocument(
    ImmutableArray<CalculatedDocumentLine> Lines,
    ImmutableArray<VatSummary> VatSummary,
    decimal TotalTaxable,
    decimal TotalVat,
    decimal TotalGross);
