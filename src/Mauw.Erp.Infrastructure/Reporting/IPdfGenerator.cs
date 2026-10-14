using Mauw.Erp.Contracts.Dtos;

namespace Mauw.Erp.Infrastructure.Reporting;

/// <summary>
/// Interface for PDF report generation.
/// </summary>
public interface IPdfGenerator
{
    byte[] GenerateInvoicePdf(InvoiceReportDto dto);
    byte[] GenerateDdtPdf(DdtReportDto dto);
}