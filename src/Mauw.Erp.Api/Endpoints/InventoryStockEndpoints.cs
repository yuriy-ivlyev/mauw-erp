using System.Globalization;
using System.Text;
using Mauw.Erp.Api.Authorization;
using Mauw.Erp.Application.Costs;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Enums;
using Mauw.Erp.Contracts.Interfaces;
using Mauw.Erp.Contracts.Requests;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Mauw.Erp.Api.Endpoints;

public static class InventoryStockEndpoints
{
    public static void MapInventoryStockEndpoints(this WebApplication app)
    {
        MapRoutes(app.MapGroup("/api/inventory").WithTags("Inventory stocks"));
        MapRoutes(app.MapGroup("/api/v1/inventory").WithTags("Inventory stocks"));
    }

    private static void MapRoutes(RouteGroupBuilder group)
    {
        group.RequireAuthorization(AppPolicies.InventoryStockRead);
        group.MapGet("/stocks", GetStocks);
        group.MapGet("/stocks/summary", GetStocksSummary);
        group.MapGet("/stocks/export", ExportFile);
        group.MapGet("/stocks/export.csv", ExportCsv);
        group.MapGet("/stocks/export.pdf", ExportPdf);
        group.MapGet("/stocks/{warehouseId:guid}/{productVariantId:guid}/movements", GetMovements);
        group.MapGet("/stocks/reconciliation/ambiguous-movements", GetAmbiguousMovements)
            .RequireAuthorization(AppPolicies.AdminOnly)
            .WithSummary("List ambiguous historical stock movements and suggested deltas");
    }

    private static async Task<Results<FileContentHttpResult, BadRequest<string>, Conflict<string>>> ExportFile(
        [AsParameters] InventoryStockFilterRequest request,
        IInventoryStockQueryService service,
        ICompanySettingsService companySettings,
        IInventoryStockPdfGenerator pdfGenerator,
        ICostVisibility costs,
        CancellationToken cancellationToken,
        string format = "csv")
    {
        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            return await ExportCsv(request, service, companySettings, costs, cancellationToken);
        if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
            return await ExportPdf(request, service, companySettings, pdfGenerator, costs, cancellationToken);
        return TypedResults.BadRequest("Formato non supportato. Usare format=csv oppure format=pdf.");
    }

    private static async Task<Results<Ok<InventoryStocksPageDto>, BadRequest<string>, Conflict<string>>> GetStocks(
        [AsParameters] InventoryStockFilterRequest request,
        IInventoryStockQueryService service,
        ICostVisibility costs,
        CancellationToken cancellationToken)
    {
        ApplySortPolicy(request, costs);
        var result = await service.GetStocksPageAsync(request, cancellationToken);
        if (result.Success)
            return TypedResults.Ok(costs.Redact(result.Data!));
        return IsHistoricalDataConflict(result.Error)
            ? TypedResults.Conflict(result.Error!)
            : TypedResults.BadRequest(result.Error ?? "Richiesta giacenze non valida.");
    }

    private static async Task<Results<Ok<InventoryStocksSummaryDto>, BadRequest<string>, Conflict<string>>> GetStocksSummary(
        IInventoryStockQueryService service,
        ICostVisibility costs,
        CancellationToken cancellationToken,
        string? search = null,
        Guid? warehouseId = null,
        Guid? categoryId = null,
        DateTime? atDate = null,
        bool onlyInStock = false,
        ValuationMethod valuationMethod = ValuationMethod.WeightedAverageCost,
        int pageSize = 20)
    {
        var request = new InventoryStockFilterRequest
        {
            Search = search,
            WarehouseId = warehouseId,
            CategoryId = categoryId,
            AtDate = atDate,
            OnlyInStock = onlyInStock,
            ValuationMethod = valuationMethod,
            Page = 1,
            PageSize = pageSize
        };
        var result = await service.GetStocksSummaryAsync(request, cancellationToken);
        if (result.Success)
            return TypedResults.Ok(costs.Redact(result.Data!));
        return IsHistoricalDataConflict(result.Error)
            ? TypedResults.Conflict(result.Error!)
            : TypedResults.BadRequest(result.Error ?? "Riepilogo giacenze non valido.");
    }

    private static async Task<Results<FileContentHttpResult, BadRequest<string>, Conflict<string>>> ExportCsv(
        [AsParameters] InventoryStockFilterRequest request,
        IInventoryStockQueryService service,
        ICompanySettingsService companySettings,
        ICostVisibility costs,
        CancellationToken cancellationToken)
    {
        ApplySortPolicy(request, costs);
        var result = await service.GetExportAsync(request, cancellationToken);
        if (!result.Success)
            return IsHistoricalDataConflict(result.Error)
                ? TypedResults.Conflict(result.Error!)
                : TypedResults.BadRequest(result.Error ?? "Esportazione non riuscita.");
        if (HasFractionalQuantities(result.Data!))
            return TypedResults.Conflict("La giacenza contiene quantita frazionarie: correggere i dati prima dell'esportazione.");

        var company = await companySettings.GetCompanyAsync();
        if (!company.Success)
            return TypedResults.BadRequest(company.Error ?? "Configurare i dati aziendali prima di esportare.");

        var content = BuildCsv(costs.Redact(result.Data!), company.Data!);
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(content)).ToArray();
        return TypedResults.File(
            bytes,
            "text/csv; charset=utf-8",
            $"giacenze-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    }

    private static async Task<Results<FileContentHttpResult, BadRequest<string>, Conflict<string>>> ExportPdf(
        [AsParameters] InventoryStockFilterRequest request,
        IInventoryStockQueryService service,
        ICompanySettingsService companySettings,
        IInventoryStockPdfGenerator pdfGenerator,
        ICostVisibility costs,
        CancellationToken cancellationToken)
    {
        ApplySortPolicy(request, costs);
        var result = await service.GetExportAsync(request, cancellationToken);
        if (!result.Success)
            return IsHistoricalDataConflict(result.Error)
                ? TypedResults.Conflict(result.Error!)
                : TypedResults.BadRequest(result.Error ?? "Esportazione non riuscita.");
        if (HasFractionalQuantities(result.Data!))
            return TypedResults.Conflict("La giacenza contiene quantita frazionarie: correggere i dati prima dell'esportazione.");

        var company = await companySettings.GetCompanyAsync();
        if (!company.Success)
            return TypedResults.BadRequest(company.Error ?? "Configurare i dati aziendali prima di esportare.");

        return TypedResults.File(
            pdfGenerator.Generate(costs.Redact(result.Data!), company.Data!),
            "application/pdf",
            $"giacenze-{DateTime.UtcNow:yyyyMMdd-HHmmss}.pdf");
    }

    private static async Task<Ok<InventoryStockMovementsResponseDto>> GetMovements(
        Guid warehouseId,
        Guid productVariantId,
        DateTime? atDate,
        IInventoryStockQueryService service,
        ICostVisibility costs,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20)
    {
        var result = await service.GetMovementsAsync(
            warehouseId,
            productVariantId,
            atDate,
            page,
            pageSize,
            cancellationToken);
        return TypedResults.Ok(costs.Redact(result.Data!));
    }

    private static async Task<Ok<InventoryMovementReconciliationResponseDto>> GetAmbiguousMovements(
        IInventoryStockQueryService service,
        CancellationToken cancellationToken,
        Guid? warehouseId = null,
        Guid? productVariantId = null,
        int page = 1,
        int pageSize = 50)
    {
        var result = await service.GetAmbiguousMovementsAsync(
            warehouseId,
            productVariantId,
            page,
            pageSize,
            cancellationToken);
        return TypedResults.Ok(result.Data!);
    }

    private static string BuildCsv(InventoryStockExportDto report, CompanySettingsDto company)
    {
        var builder = new StringBuilder();
        builder.Append("Azienda;").AppendLine(CsvText(company.BusinessName));
        builder.Append("Partita IVA;").AppendLine(CsvText(company.VatNumber ?? string.Empty));
        builder.Append("Generato il (UTC);").AppendLine(report.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        if (report.AtDate.HasValue)
            builder.Append("Giacenza fisica al (UTC);").AppendLine(report.AtDate.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(report.AllocationNote))
            builder.Append("Criterio di allocazione;").AppendLine(CsvText(report.AllocationNote));

        var includeCosts = report.IncludesCosts;
        builder.Append("SKU;Descrizione;Taglia;Colore;Deposito;Giacenza fisica;UM");
        if (includeCosts)
            builder.Append($";{InventoryStockValuationLabels.GetLabel(report.ValuationMethod)};Valore totale");
        builder.AppendLine();
        foreach (var item in report.Items)
        {
            builder.Append(CsvText(item.Sku)).Append(';')
                .Append(CsvText(item.Description)).Append(';')
                .Append(CsvText(item.Size ?? string.Empty)).Append(';')
                .Append(CsvText(item.Color ?? string.Empty)).Append(';')
                .Append(CsvText($"{item.WarehouseCode} - {item.WarehouseName}")).Append(';')
                .Append(item.QuantityOnHand.ToString("0", CultureInfo.InvariantCulture)).Append(';')
                .Append(CsvText(item.UnitOfMeasure));
            if (includeCosts)
                builder.Append(';')
                    .Append(item.UnitCost?.ToString("0.00######", CultureInfo.InvariantCulture) ?? string.Empty).Append(';')
                    .Append(item.TotalValue?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty);
            builder.AppendLine();
        }

        builder.Append("TOTALI;;;;;")
            .Append(report.Totals.TotalPhysicalQuantity.ToString("0", CultureInfo.InvariantCulture));
        if (includeCosts)
            builder.Append(";;;").Append(report.Totals.TotalValue!.Value.ToString("0.00", CultureInfo.InvariantCulture));
        builder.AppendLine();
        return builder.ToString();
    }

    private static void ApplySortPolicy(InventoryStockFilterRequest request, ICostVisibility costs) =>
        (request.SortBy, request.SortDescending) = costs.Sort(request.SortBy, request.SortDescending);

    private static string CsvText(string value)
    {
        var safe = value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r'
            ? "'" + value
            : value;
        return "\"" + safe.Replace("\"", "\"\"") + "\"";
    }

    private static bool IsHistoricalDataConflict(string? error) =>
        error?.StartsWith("Impossibile ricostruire la giacenza alla data richiesta", StringComparison.Ordinal) == true;

    private static bool HasFractionalQuantities(InventoryStockExportDto report) =>
        report.Items.Any(item =>
            item.QuantityOnHand != decimal.Truncate(item.QuantityOnHand) ||
            item.QuantityReserved != decimal.Truncate(item.QuantityReserved) ||
            item.QuantityIncoming != decimal.Truncate(item.QuantityIncoming) ||
            item.QuantityAvailable != decimal.Truncate(item.QuantityAvailable)) ||
        report.Totals.TotalPhysicalQuantity != decimal.Truncate(report.Totals.TotalPhysicalQuantity) ||
        report.Totals.TotalReservedQuantity != decimal.Truncate(report.Totals.TotalReservedQuantity) ||
        report.Totals.TotalIncomingQuantity != decimal.Truncate(report.Totals.TotalIncomingQuantity) ||
        report.Totals.TotalAvailableQuantity != decimal.Truncate(report.Totals.TotalAvailableQuantity);
}
