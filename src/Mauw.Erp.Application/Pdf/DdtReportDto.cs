namespace Mauw.Erp.Application;

public sealed class DdtReportDto
{
    public string DocumentNumber { get; set; } = "DDT-0001";
    public DateTime IssueDate { get; set; } = DateTime.Today;
    public string CustomerName { get; set; } = "Cliente";
    public string CustomerAddress { get; set; } = "Via Roma 1, Milano";
    public string CustomerVatNumber { get; set; } = "IT01234567890";
    public string? Notes { get; set; }
    public byte[]? LogoBytes { get; set; }
    public List<DdtLineDto> Lines { get; set; } = new();
    public decimal TaxableAmount { get; set; }
    public decimal TotalDue { get; set; }
}

public sealed class DdtLineDto
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SizeColor { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
}
