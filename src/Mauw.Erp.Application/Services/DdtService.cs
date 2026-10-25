using System.Data;
using System.Globalization;
using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Dtos.Pdf;
using Mauw.Erp.Contracts.Interfaces;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Accounting;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.Application.Services;

/// <summary>
/// DDT service implementation.
/// </summary>
public class DdtService(
    IUnitOfWork unitOfWork,
    IValidator<CreateOrderItemRequest> itemValidator,
    ItalianAccountingCalculator accountingCalculator,
    ICompanySettingsService companySettingsService,
    IInvoicePdfGenerator pdfGenerator,
    IDocumentPricingService documentPricing)
    : IDdtService
{
    private static readonly CultureInfo ItalianCulture = CultureInfo.GetCultureInfo("it-IT");

    public async Task<OperationResult<DdtDto>> CreateDdtAsync(CreateDdtRequest request)
    {
        if (request.WarehouseId == Guid.Empty)
            return OperationResult<DdtDto>.Fail("Il magazzino del DDT è obbligatorio.");

        var contact = await unitOfWork.Contacts.GetByIdAsync(request.ContactId);
        if (contact == null)
            return OperationResult<DdtDto>.Fail("Contact not found");

        var warehouse = await unitOfWork.Warehouses.GetByIdAsync(request.WarehouseId);
        if (warehouse is null || !warehouse.IsActive || !warehouse.IsSellable)
            return OperationResult<DdtDto>.Fail("Il magazzino non esiste, non è attivo o non è abilitato alla vendita.");

        var existing = await unitOfWork.DdtDocuments.GetByNumberAsync(request.DdtNumber);
        if (existing is not null)
            return OperationResult<DdtDto>.Fail($"Un DDT con numero '{request.DdtNumber}' esiste già.");

        var ddt = new DdtDocument(request.ContactId, request.DdtNumber, request.WarehouseId);

        await unitOfWork.DdtDocuments.AddAsync(ddt);
        await unitOfWork.SaveChangesAsync();

        return OperationResult<DdtDto>.Ok(DdtMapper.MapToDto(ddt));
    }

    public async Task<OperationResult<DdtDto>> GetDdtAsync(Guid id)
    {
        var ddt = await unitOfWork.DdtDocuments.GetByIdAsync(id);
        if (ddt == null)
            return OperationResult<DdtDto>.Fail("DDT not found");

        return OperationResult<DdtDto>.Ok(DdtMapper.MapToDto(ddt));
    }

    public async Task<OperationResult<PagedResult<DdtDto>>> GetDdtsByContactAsync(Guid contactId, int page, int pageSize)
    {
        var (ddts, total) = await unitOfWork.DdtDocuments.GetByContactIdPagedAsync(contactId, page, pageSize);
        var dtos = DdtMapper.MapToListDto(ddts);
        var result = new PagedResult<DdtDto>(dtos, page, pageSize, total);

        return OperationResult<PagedResult<DdtDto>>.Ok(result);
    }

    public async Task<OperationResult<DdtDto>> AddItemAsync(Guid ddtId, CreateOrderItemRequest request)
    {
        var validationResult = itemValidator.Validate(request);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.Message}"));
            return OperationResult<DdtDto>.Fail(errors);
        }

        var ddt = await unitOfWork.DdtDocuments.GetByIdAsync(ddtId);
        if (ddt == null)
            return OperationResult<DdtDto>.Fail("DDT not found");
        if (!ddt.WarehouseId.HasValue)
            return OperationResult<DdtDto>.Fail("Il DDT storico non ha un magazzino assegnato.");
        if (request.Quantity == int.MinValue)
            return OperationResult<DdtDto>.Fail("La quantità non è rappresentabile nel movimento di magazzino.");

        var warehouse = await unitOfWork.Warehouses.GetByIdAsync(ddt.WarehouseId.Value);
        if (warehouse is null || !warehouse.IsActive || !warehouse.IsSellable)
            return OperationResult<DdtDto>.Fail("Il magazzino del DDT non esiste, non è attivo o non è abilitato alla vendita.");

        var variant = await unitOfWork.ProductVariants.GetByIdAsync(request.VariantId);
        if (variant == null)
            return OperationResult<DdtDto>.Fail("Variant not found");
        var product = await unitOfWork.Products.GetByIdAsync(variant.ProductId);
        if (product == null)
            return OperationResult<DdtDto>.Fail("Product not found");

        var pricing = await documentPricing.PrepareAsync(ddt.ContactId,
            [new(request.VariantId, request.Quantity, request.UnitNetPrice, request.UnitGrossPrice,
                request.SourceOrderItemId.HasValue || request.SourceLineId.HasValue ? request.VatRate : request.VatRate ?? product.VatRate,
                request.NatureCode, request.DiscountPercent,
                request.PriceListId, request.OverrideReason, request.IsCustomerReturn, request.SourceLineId,
                SourceOrderItemId: request.SourceOrderItemId)],
            DateTimeOffset.UtcNow);
        if (!pricing.Success)
            return OperationResult<DdtDto>.Fail(pricing.Error);
        var prepared = pricing.Data!.Lines[0];
        var line = prepared.FiscalLine;

        var item = new DdtItem(
            ddtDocumentId: ddtId,
            variantId: request.VariantId,
            quantity: request.Quantity,
            unitNetPrice: line.UnitNetPrice,
            vatRate: line.VatRate,
            natureCode: line.NatureCode,
            discountPercent: line.DiscountPercent) { PricingSnapshot = prepared.Snapshot };

        var movement = StockMovement.FromDdtItem(ddt, item, ddt.WarehouseId.Value);
        await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            if (movement.QuantityDelta < 0)
            {
                var available = await unitOfWork.Stock.GetAvailableQtyAsync(
                    movement.WarehouseId!.Value, movement.VariantId);
                if (available < -movement.QuantityDelta.Value)
                {
                    await unitOfWork.RollbackAsync();
                    return OperationResult<DdtDto>.Fail(
                        $"Giacenza insufficiente per la variante {movement.VariantId}: disponibili {available}.");
                }
            }

            var accounting = ddt.AddItem(item, accountingCalculator);
            if (!accounting.Success)
            {
                await unitOfWork.RollbackAsync();
                return OperationResult<DdtDto>.Fail(accounting.Error);
            }

            await unitOfWork.Stock.RecordMovementAsync(movement, movement.QuantityDelta!.Value);
            await unitOfWork.DdtDocuments.UpdateAsync(ddt);
            await unitOfWork.SaveChangesAsync();
            await unitOfWork.CommitAsync();
        }
        catch
        {
            await unitOfWork.RollbackAsync();
            throw;
        }

        return OperationResult<DdtDto>.Ok(DdtMapper.MapToDto(ddt));
    }

    public async Task<OperationResult<bool>> DeleteDdtAsync(Guid id)
    {
        var ddt = await unitOfWork.DdtDocuments.GetByIdAsync(id);
        if (ddt == null)
            return OperationResult<bool>.Fail("DDT not found");

        var movements = (await unitOfWork.StockMovements.GetByReferenceIdAsync(ddt.Id)).ToList();
        var reversals = new List<StockMovement>(movements.Count);
        foreach (var movement in movements)
        {
            if (!movement.WarehouseId.HasValue || !movement.GetEffectiveQuantityDelta().HasValue)
                return OperationResult<bool>.Fail(
                    "Il DDT contiene un movimento storico non stornabile; il documento non è stato eliminato.");
            reversals.Add(StockMovement.FromDdtReversal(ddt, movement));
        }

        await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            foreach (var group in movements
                         .Where(movement => movement.GetEffectiveQuantityDelta() > 0)
                         .GroupBy(movement => (
                             WarehouseId: movement.WarehouseId!.Value,
                             VariantId: movement.VariantId)))
            {
                var available = await unitOfWork.Stock.GetAvailableQtyAsync(
                    group.Key.WarehouseId, group.Key.VariantId);
                var required = group.Sum(movement => movement.GetEffectiveQuantityDelta()!.Value);
                if (available < required)
                {
                    await unitOfWork.RollbackAsync();
                    return OperationResult<bool>.Fail(
                        $"Giacenza insufficiente per stornare il reso DDT della variante {group.Key.VariantId}.");
                }
            }

            foreach (var reversal in reversals)
                await unitOfWork.Stock.RecordMovementAsync(reversal, reversal.QuantityDelta!.Value);

            await unitOfWork.DdtDocuments.DeleteAsync(ddt);
            await unitOfWork.SaveChangesAsync();
            await unitOfWork.CommitAsync();
        }
        catch
        {
            await unitOfWork.RollbackAsync();
            throw;
        }

        return OperationResult<bool>.Ok(true);
    }

    public async Task<OperationResult<byte[]>> GetDdtPdfAsync(Guid id)
    {
        var ddt = await unitOfWork.DdtDocuments.GetByIdAsync(id);
        if (ddt == null)
            return OperationResult<byte[]>.Fail("DDT not found");

        var agent = await DocumentPdfMapper.GetAgentAsync(unitOfWork, ddt.ContactId);
        if (!agent.Success)
            return OperationResult<byte[]>.Fail(agent.Error!);
        var companyResult = await companySettingsService.GetCompanyAsync();
        var company = companyResult.Success ? companyResult.Data : null;
        var sellerAddress = company?.LegalHeadquarters
            ?? company?.OperationalSites.FirstOrDefault(s => s.IsMainOperationalSite)
            ?? company?.OperationalSites.FirstOrDefault();
        var customerAddress = ddt.Contact?.LegalHeadquarters ?? ddt.Contact?.Addresses.FirstOrDefault();

        var lines = ddt.Items.Select(item => new InvoicePdfLineModel(
            Sku: item.Variant?.Barcode ?? string.Empty,
            Description: string.Join(" ",
                    new[]
                    {
                        item.Variant?.Product?.Name,
                        item.Variant?.Size?.Name,
                        item.Variant?.Color?.Name
                    }.Where(part => !string.IsNullOrWhiteSpace(part)))
                .Trim(),
            SeasonCollection: null,
            Pieces: item.Quantity,
            UnitPrice: item.UnitNetPrice,
            DiscountPercent: item.DiscountPercent,
            VatRate: item.VatRate,
            LineTotal: item.LineNetTotal,
            NatureCode: item.NatureCode
        )).ToList();

        var accounting = accountingCalculator.Calculate(ddt.Items.Select(item => new DocumentLineInput(
            item.Quantity, item.UnitNetPrice, item.VatRate, item.NatureCode, item.DiscountPercent)));
        if (!accounting.Success)
            return OperationResult<byte[]>.Fail(accounting.Error);
        var calculated = accounting.Data!;
        var vatSummary = calculated.VatSummary
            .Select(group => new InvoiceVatBreakdownModel(group.VatRate, group.TaxableAmount, group.VatAmount, group.NatureCode))
            .ToList();
        for (var i = 0; i < lines.Count; i++)
        {
            var line = calculated.Lines[i];
            lines[i] = lines[i] with
            {
                UnitPrice = line.UnitNetPrice, DiscountPercent = line.DiscountPercent,
                VatRate = line.VatRate, NatureCode = line.NatureCode, LineTotal = line.LineNetTotal
            };
        }

        var model = new InvoicePdfModel(
            DocumentNumber: ddt.DdtNumber,
            DocumentType: "DDT",
            IssueDate: DateOnly.FromDateTime(ddt.DdtDate),
            Currency: "EUR",
            Seller: new InvoicePdfSellerDetails(
                BusinessName: company?.BusinessName ?? "Mauw S.r.l.",
                VatNumber: company?.VatNumber ?? string.Empty,
                FiscalCode: company?.TaxCode,
                Address: sellerAddress?.Street ?? string.Empty,
                City: sellerAddress?.City ?? string.Empty,
                PostalCode: sellerAddress?.ZipCode ?? string.Empty,
                Province: sellerAddress?.Province ?? string.Empty,
                Rea: BuildRea(company),
                ShareCapital: company?.ShareCapital?.ToString("N2", ItalianCulture),
                TaxRegime: company?.FiscalRegime ?? "RF01 - Ordinario",
                SdiCode: company?.SdiCode,
                Pec: company?.PecEmail,
                IsShareCapitalFullyPaid: company?.IsShareCapitalFullyPaid ?? false),
            Customer: new InvoicePdfCustomerDetails(
                BusinessName: ddt.Contact?.BusinessName ?? string.Empty,
                VatNumber: ddt.Contact?.VatNumber,
                FiscalCode: ddt.Contact?.TaxCode,
                Address: customerAddress?.Street ?? string.Empty,
                City: customerAddress?.City ?? string.Empty,
                PostalCode: customerAddress?.ZipCode ?? string.Empty,
                Province: customerAddress?.Province ?? string.Empty,
                SdiCode: ddt.Contact?.SdiCode,
                Pec: ddt.Contact?.PecEmail),
            Payment: new InvoicePdfPaymentInfo("A vista", "MP01", null, null, null, null),
            Lines: lines,
            VatSummary: vatSummary,
            Totals: new InvoicePdfTotals(
                TotalPieces: ddt.Items.Sum(i => i.Quantity),
                TotalNet: calculated.TotalTaxable,
                TotalVat: calculated.TotalVat,
                TotalGross: calculated.TotalGross,
                ShippingCost: 0m),
            Agent: agent.Data
        );

        return OperationResult<byte[]>.Ok(await pdfGenerator.GenerateInvoicePdfAsync(model));
    }

    private static string? BuildRea(CompanySettingsDto? company)
    {
        if (company == null)
            return null;

        if (string.IsNullOrWhiteSpace(company.ReaOffice))
            return company.ReaNumber;

        if (string.IsNullOrWhiteSpace(company.ReaNumber))
            return company.ReaOffice;

        return $"{company.ReaOffice}-{company.ReaNumber}";
    }
}
