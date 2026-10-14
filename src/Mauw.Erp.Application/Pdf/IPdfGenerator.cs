namespace Mauw.Erp.Application;

public interface IPdfGenerator
{
    byte[] GenerateInvoicePdf(InvoiceReportDto dto);
    byte[] GenerateDdtPdf(DdtReportDto dto);
}
