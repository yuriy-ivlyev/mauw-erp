using Mauw.Erp.Contracts.Dtos;

namespace Mauw.Erp.Contracts.Interfaces;

public interface IInventoryStockPdfGenerator
{
    byte[] Generate(InventoryStockExportDto report, CompanySettingsDto company);
}
