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
    PricingEngine pricingEngine,
    ItalianAccountingCalculator accountingCalculator,
    ICompanySettingsService companySettingsService,
    IInvoicePdfGenerator pdfGenerator)
    : IDdtService
{
    private static readonly CultureInfo ItalianCulture = CultureInfo.GetCultureInfo("it-IT");

    public async Task<OperationResult<DdtDto>> CreateDdtAsync(CreateDdtRequest request)
    {
        var contact = await unitOfWork.Contacts.GetByIdAsync(request.ContactId);
        if (contact == null)
            return OperationResult<DdtDto>.Fail("Contact not found");

        var existing = await unitOfWork.DdtDocuments.GetByNumberAsync(request.DdtNumber);
        if (existing is not null)
            return OperationResult<DdtDto>.Fail($"Un DDT con numero '{request.DdtNumber}' esiste già.");

        var ddt = new DdtDocument(request.ContactId, request.DdtNumber);

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
        var ddts = await unitOfWork.DdtDocuments.GetByContactIdAsync(contactId);
        var paginated = ddts
            .OrderByDescending(d => d.DdtDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var total = ddts.Count();
        var dtos = DdtMapper.MapToListDto(paginated);
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

        var variant = await unitOfWork.ProductVariants.GetByIdAsync(request.VariantId);
        if (variant == null)
            return OperationResult<DdtDto>.Fail("Variant not found");
        var product = await unitOfWork.Products.GetByIdAsync(variant.ProductId);
        if (product == null)
            return OperationResult<DdtDto>.Fail("Product not found");

        var pricingResult = pricingEngine.CalculatePrice(
            purchaseCost: variant.PurchaseCost,
            markupRate: product.DefaultMarkupRate,
            discountRate: 0,
            vatRate: product.VatRate);

        var line = accountingCalculator.CalculateLine(new DocumentLineInput(request.Quantity,
            request.UnitGrossPrice ?? request.UnitNetPrice ?? pricingResult.ListPrice,
            request.VatRate ?? product.VatRate, request.NatureCode, request.DiscountPercent ?? 0m,
            PriceIncludesVat: request.UnitGrossPrice.HasValue));
        if (!line.Success)
            return OperationResult<DdtDto>.Fail(line.Error);

        var item = new DdtItem(
            ddtDocumentId: ddtId,
            variantId: request.VariantId,
            quantity: request.Quantity,
            unitNetPrice: line.Data!.UnitNetPrice,
            vatRate: line.Data.VatRate,
            natureCode: line.Data.NatureCode,
            discountPercent: line.Data.DiscountPercent);

        var accounting = ddt.AddItem(item, accountingCalculator);
        if (!accounting.Success)
            return OperationResult<DdtDto>.Fail(accounting.Error);

        await unitOfWork.DdtDocuments.UpdateAsync(ddt);
        await unitOfWork.SaveChangesAsync();

        return OperationResult<DdtDto>.Ok(DdtMapper.MapToDto(ddt));
    }

    public async Task<OperationResult<bool>> DeleteDdtAsync(Guid id)
    {
        var ddt = await unitOfWork.DdtDocuments.GetByIdAsync(id);
        if (ddt == null)
            return OperationResult<bool>.Fail("DDT not found");

        await unitOfWork.DdtDocuments.DeleteAsync(ddt);
        await unitOfWork.SaveChangesAsync();

        return OperationResult<bool>.Ok(true);
    }

    public async Task<OperationResult<byte[]>> GetDdtPdfAsync(Guid id)
    {
        var ddt = await unitOfWork.DdtDocuments.GetByIdAsync(id);
        if (ddt == null)
            return OperationResult<byte[]>.Fail("DDT not found");

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
                Pec: company?.PecEmail),
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
                ShippingCost: 0m)
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
