using System.Globalization;
using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Dtos.Pdf;
using Mauw.Erp.Contracts.Interfaces;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Accounting;
using Mauw.Erp.Domain.Exceptions;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.Application.Services;

/// <summary>
/// Invoice service implementation.
/// </summary>
public class InvoiceService(
    IUnitOfWork unitOfWork,
    IValidator<CreateInvoiceRequest> createValidator,
    ItalianAccountingCalculator accountingCalculator,
    ICompanySettingsService companySettingsService,
    IInvoicePdfGenerator pdfGenerator,
    IDocumentPricingService documentPricing)
    : IInvoiceService
{
    private static readonly CultureInfo ItalianCulture = CultureInfo.GetCultureInfo("it-IT");

    public async Task<OperationResult<InvoiceDto>> CreateInvoiceAsync(CreateInvoiceRequest request)
    {
        var validationResult = createValidator.Validate(request);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.Message}"));
            return OperationResult<InvoiceDto>.Fail(errors);
        }

        var contact = await unitOfWork.Contacts.GetByIdAsync(request.ContactId);
        if (contact == null)
            return OperationResult<InvoiceDto>.Fail("Contact not found");

        var now = DateTime.UtcNow;
        var prefix = $"INV-{now:MMyyyy}-";
        var count = await unitOfWork.Invoices.CountInvoicesByPrefixAsync(prefix);

        var invoice = new Invoice(
            contactId: request.ContactId,
            invoiceNumber: $"{prefix}{(count + 1):D3}"
        );

        await unitOfWork.Invoices.AddAsync(invoice);
        await unitOfWork.SaveChangesAsync();

        return OperationResult<InvoiceDto>.Ok(InvoiceMapper.MapToDto(invoice));
    }

    public async Task<OperationResult<InvoiceDto>> GetInvoiceAsync(Guid id)
    {
        var invoice = await unitOfWork.Invoices.GetByIdAsync(id);
        if (invoice == null)
            return OperationResult<InvoiceDto>.Fail("Invoice not found");

        return OperationResult<InvoiceDto>.Ok(InvoiceMapper.MapToDto(invoice));
    }

    public async Task<OperationResult<PagedResult<InvoiceDto>>> GetInvoicesByContactAsync(Guid contactId, int page, int pageSize)
    {
        var (invoices, total) = await unitOfWork.Invoices.GetByContactIdPagedAsync(contactId, page, pageSize);
        var dtos = InvoiceMapper.MapToListDto(invoices);
        var result = new PagedResult<InvoiceDto>(dtos, page, pageSize, total);

        return OperationResult<PagedResult<InvoiceDto>>.Ok(result);
    }

    public async Task<OperationResult<InvoiceDto>> AddItemAsync(Guid invoiceId, CreateInvoiceItemRequest request)
    {
        var invoice = await unitOfWork.Invoices.GetByIdAsync(invoiceId);
        if (invoice == null)
            return OperationResult<InvoiceDto>.Fail("Invoice not found");

        var variant = await unitOfWork.ProductVariants.GetByIdAsync(request.VariantId);
        if (variant == null)
            return OperationResult<InvoiceDto>.Fail("Variant not found");

        var product = await unitOfWork.Products.GetByIdAsync(variant.ProductId);
        if (product == null)
            return OperationResult<InvoiceDto>.Fail("Product not found");

        var pricing = await documentPricing.PrepareAsync(invoice.ContactId,
            [new(request.VariantId, request.Quantity, request.UnitNetPrice, request.UnitGrossPrice,
                request.SourceOrderItemId.HasValue || request.SourceLineId.HasValue ? request.VatRate : request.VatRate ?? product.VatRate,
                request.NatureCode, request.DiscountPercent,
                request.PriceListId, request.OverrideReason, request.IsCustomerReturn, request.SourceLineId,
                SourceOrderItemId: request.SourceOrderItemId)],
            DateTimeOffset.UtcNow);
        if (!pricing.Success)
            return OperationResult<InvoiceDto>.Fail(pricing.Error);
        var prepared = pricing.Data!.Lines[0];
        var line = prepared.FiscalLine;

        var item = new InvoiceItem(
            invoiceId: invoiceId,
            variantId: request.VariantId,
            quantity: request.Quantity,
            unitNetPrice: line.UnitNetPrice,
            vatRate: line.VatRate,
            natureCode: line.NatureCode,
            discountPercent: line.DiscountPercent
        ) { PricingSnapshot = prepared.Snapshot };

        var accounting = invoice.AddItem(item, accountingCalculator);
        if (!accounting.Success)
            return OperationResult<InvoiceDto>.Fail(accounting.Error);
        await unitOfWork.Invoices.UpdateAsync(invoice);
        try
        {
            await unitOfWork.SaveChangesAsync();
        }
        catch (ConcurrencyException ex)
        {
            return OperationResult<InvoiceDto>.Fail(ex.Message);
        }

        var updated = await unitOfWork.Invoices.GetByIdAsync(invoiceId);
        return OperationResult<InvoiceDto>.Ok(InvoiceMapper.MapToDto(updated!));
    }

    public async Task<OperationResult<InvoiceDto>> RecordPaymentAsync(Guid invoiceId, RecordPaymentRequest request)
    {
        var invoice = await unitOfWork.Invoices.GetByIdAsync(invoiceId);
        if (invoice == null)
            return OperationResult<InvoiceDto>.Fail("Invoice not found");

        var payment = new Payment(
            invoiceId: invoiceId,
            amount: request.Amount,
            paymentDate: request.PaymentDate
        );

        invoice.RecordPayment(request.Amount);
        await unitOfWork.Invoices.UpdateAsync(invoice);
        try
        {
            await unitOfWork.SaveChangesAsync();
        }
        catch (ConcurrencyException ex)
        {
            return OperationResult<InvoiceDto>.Fail(ex.Message);
        }

        var updated = await unitOfWork.Invoices.GetByIdAsync(invoiceId);
        return OperationResult<InvoiceDto>.Ok(InvoiceMapper.MapToDto(updated!));
    }

    public async Task<OperationResult<bool>> DeleteInvoiceAsync(Guid id)
    {
        var invoice = await unitOfWork.Invoices.GetByIdAsync(id);
        if (invoice == null)
            return OperationResult<bool>.Fail("Invoice not found");

        await unitOfWork.Invoices.DeleteAsync(invoice);
        try
        {
            await unitOfWork.SaveChangesAsync();
        }
        catch (ConcurrencyException ex)
        {
            return OperationResult<bool>.Fail(ex.Message);
        }

        return OperationResult<bool>.Ok(true);
    }

    public async Task<OperationResult<byte[]>> GetInvoicePdfAsync(Guid id)
    {
        var invoice = await unitOfWork.Invoices.GetByIdAsync(id);
        if (invoice != null)
        {
            var model = await BuildInvoicePdfModelAsync(invoice);
            if (!model.Success)
                return OperationResult<byte[]>.Fail(model.Error!);
            return OperationResult<byte[]>.Ok(await pdfGenerator.GenerateInvoicePdfAsync(model.Data!));
        }

        var creditNote = await unitOfWork.CreditNotes.GetByIdAsync(id);
        if (creditNote == null)
            return OperationResult<byte[]>.Fail("Invoice not found");

        var relatedInvoice = await unitOfWork.Invoices.GetByIdAsync(creditNote.InvoiceId);
        if (relatedInvoice == null)
            return OperationResult<byte[]>.Fail("Invoice not found");

        var creditNoteModel = await BuildCreditNotePdfModelAsync(creditNote, relatedInvoice);
        if (!creditNoteModel.Success)
            return OperationResult<byte[]>.Fail(creditNoteModel.Error!);
        return OperationResult<byte[]>.Ok(await pdfGenerator.GenerateInvoicePdfAsync(creditNoteModel.Data!));
    }

    private async Task<OperationResult<InvoicePdfModel>> BuildInvoicePdfModelAsync(Invoice invoice)
    {
        var agent = await DocumentPdfMapper.GetAgentAsync(unitOfWork, invoice.ContactId);
        if (!agent.Success)
            return OperationResult<InvoicePdfModel>.Fail(agent.Error!);
        var companyResult = DocumentPdfMapper.RequireCompanyForPdf(
            await companySettingsService.GetCompanyAsync());
        if (!companyResult.Success)
            return OperationResult<InvoicePdfModel>.Fail(companyResult.Error!);
        var company = companyResult.Data!;
        var sellerAddress = company?.LegalHeadquarters
            ?? company?.OperationalSites.FirstOrDefault(s => s.IsMainOperationalSite)
            ?? company?.OperationalSites.FirstOrDefault();
        var customerAddress = invoice.Contact?.LegalHeadquarters ?? invoice.Contact?.Addresses.FirstOrDefault();

        var lines = invoice.Items.Select(item => new InvoicePdfLineModel(
            Sku: item.Variant?.Product?.SupplierCode ?? item.Variant?.Barcode ?? string.Empty,
            Description: string.Join(" ",
                    new[]
                    {
                        item.Variant?.Product?.Name,
                        item.Variant?.Color?.Name,
                        item.Variant?.Size?.Name
                    }.Where(part => !string.IsNullOrWhiteSpace(part)))
                .Trim(),
            SeasonCollection: null,
            Pieces: item.Quantity,
            UnitPrice: item.UnitNetPrice,
            DiscountPercent: item.DiscountPercent,
            VatRate: item.VatRate,
            LineTotal: item.LineNetTotal,
            NatureCode: item.NatureCode,
            ModelCode: item.Variant?.Product?.SupplierCode,
            Color: item.Variant?.Color?.Name,
            Size: item.Variant?.Size?.Name
        )).ToList();

        var accounting = accountingCalculator.Calculate(invoice.Items.Select(item => new DocumentLineInput(
            item.Quantity, item.UnitNetPrice, item.VatRate, item.NatureCode, item.DiscountPercent)));
        if (!accounting.Success)
            return OperationResult<InvoicePdfModel>.Fail(accounting.Error);
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

        return OperationResult<InvoicePdfModel>.Ok(new InvoicePdfModel(
            DocumentNumber: invoice.InvoiceNumber,
            DocumentType: "TD01",
            IssueDate: DateOnly.FromDateTime(invoice.InvoiceDate),
            Currency: "EUR",
            Seller: BuildSeller(company, sellerAddress),
            Customer: BuildCustomer(invoice.Contact, customerAddress),
            Payment: new InvoicePdfPaymentInfo(
                "A vista",
                "MP01",
                DateOnly.FromDateTime(invoice.InvoiceDate),
                company?.Iban,
                company?.BicSwift,
                company?.BankName,
                calculated.TotalGross),
            Lines: lines,
            VatSummary: vatSummary,
            Totals: new InvoicePdfTotals(
                TotalPieces: invoice.Items.Sum(i => i.Quantity),
                TotalNet: calculated.TotalTaxable,
                TotalVat: calculated.TotalVat,
                TotalGross: calculated.TotalGross,
                ShippingCost: 0m),
            Agent: agent.Data,
            FiscalNotes: company?.InvoiceNotes
        ));
    }

    private async Task<OperationResult<InvoicePdfModel>> BuildCreditNotePdfModelAsync(CreditNote creditNote, Invoice relatedInvoice)
    {
        var agent = await DocumentPdfMapper.GetAgentAsync(unitOfWork, relatedInvoice.ContactId);
        if (!agent.Success)
            return OperationResult<InvoicePdfModel>.Fail(agent.Error!);
        var companyResult = DocumentPdfMapper.RequireCompanyForPdf(
            await companySettingsService.GetCompanyAsync());
        if (!companyResult.Success)
            return OperationResult<InvoicePdfModel>.Fail(companyResult.Error!);
        var company = companyResult.Data!;
        var sellerAddress = company?.LegalHeadquarters
            ?? company?.OperationalSites.FirstOrDefault(s => s.IsMainOperationalSite)
            ?? company?.OperationalSites.FirstOrDefault();
        var customerAddress = relatedInvoice.Contact?.LegalHeadquarters ?? relatedInvoice.Contact?.Addresses.FirstOrDefault();
        var originalGroups = relatedInvoice.Items.Select(item => (item.VatRate, item.NatureCode)).Distinct().ToArray();
        if (originalGroups.Length != 1)
            return OperationResult<InvoicePdfModel>.Fail("La nota di credito richiede la ripartizione esplicita per aliquota e Natura della fattura originale.");
        var (vatRate, natureCode) = originalGroups[0];
        var accounting = accountingCalculator.Calculate(
            [new DocumentLineInput(1m, creditNote.TaxableTotal, vatRate, natureCode)]);
        if (!accounting.Success)
            return OperationResult<InvoicePdfModel>.Fail(accounting.Error);
        var calculated = accounting.Data!;
        if (calculated.TotalVat != creditNote.VatTotal || calculated.TotalGross != creditNote.GrossTotal)
            return OperationResult<InvoicePdfModel>.Fail("I totali della nota di credito non coincidono con il calcolo per aliquota e Natura.");

        return OperationResult<InvoicePdfModel>.Ok(new InvoicePdfModel(
            DocumentNumber: creditNote.CreditNoteNumber,
            DocumentType: "NOTA CREDITO",
            IssueDate: DateOnly.FromDateTime(creditNote.CreditNoteDate),
            Currency: "EUR",
            Seller: BuildSeller(company, sellerAddress),
            Customer: BuildCustomer(relatedInvoice.Contact, customerAddress),
            Payment: new InvoicePdfPaymentInfo("Storno", "MP01", null, null, null, null),
            Lines:
            [
                new InvoicePdfLineModel(
                    Sku: string.Empty,
                    Description: string.IsNullOrWhiteSpace(creditNote.Reason)
                        ? $"Nota di credito per fattura {relatedInvoice.InvoiceNumber}"
                        : creditNote.Reason!,
                    SeasonCollection: null,
                    Pieces: 1,
                    UnitPrice: creditNote.TaxableTotal,
                    DiscountPercent: null,
                    VatRate: vatRate,
                    LineTotal: calculated.Lines[0].LineNetTotal,
                    NatureCode: natureCode)
            ],
            VatSummary:
            [
                new InvoiceVatBreakdownModel(
                    VatRate: vatRate,
                    TaxableAmount: creditNote.TaxableTotal,
                    VatAmount: creditNote.VatTotal,
                    ExemptionNature: natureCode)
            ],
            Totals: new InvoicePdfTotals(
                TotalPieces: 1,
                TotalNet: creditNote.TaxableTotal,
                TotalVat: creditNote.VatTotal,
                TotalGross: creditNote.GrossTotal,
                ShippingCost: 0m),
            FiscalNotes: creditNote.Reason,
            CustomerOrderRef: relatedInvoice.InvoiceNumber,
            Agent: agent.Data
        ));
    }

    private static InvoicePdfSellerDetails BuildSeller(CompanySettingsDto? company, CompanyAddressSiteDto? sellerAddress) =>
        new(
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
            IsShareCapitalFullyPaid: company?.IsShareCapitalFullyPaid ?? false,
            ReaOffice: company?.ReaOffice,
            LegalForm: "Società a responsabilità limitata",
            IsSoleShareholder: company?.IsSoleShareholder,
            IsInLiquidation: company?.IsInLiquidation ?? false
        );

    private static InvoicePdfCustomerDetails BuildCustomer(Contact? contact, ContactAddress? customerAddress) =>
        new(
            BusinessName: contact?.BusinessName ?? string.Empty,
            VatNumber: contact?.VatNumber,
            FiscalCode: contact?.TaxCode,
            Address: customerAddress?.Street ?? string.Empty,
            City: customerAddress?.City ?? string.Empty,
            PostalCode: customerAddress?.ZipCode ?? string.Empty,
            Province: customerAddress?.Province ?? string.Empty,
            SdiCode: contact?.SdiCode,
            Pec: contact?.PecEmail
        );

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
