using System.Data;
using System.Globalization;
using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Dtos.Pdf;
using Mauw.Erp.Contracts.Interfaces;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Accounting;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Exceptions;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.Application.Services;

/// <summary>
/// Invoice service implementation.
/// </summary>
public class InvoiceService(
    IUnitOfWork unitOfWork,
    ItalianAccountingCalculator accountingCalculator,
    ICompanySettingsService companySettingsService,
    IInvoicePdfGenerator pdfGenerator)
    : IInvoiceService
{
    private static readonly CultureInfo ItalianCulture = CultureInfo.GetCultureInfo("it-IT");

    public async Task<OperationResult<InvoiceDto>> GenerateFromOrderAsync(Guid orderId)
    {
        if (orderId == Guid.Empty)
            return OperationResult<InvoiceDto>.Fail("Ordine non valido.");

        var transactionStarted = false;
        async Task<OperationResult<InvoiceDto>> RollbackFailureAsync(string error)
        {
            if (transactionStarted)
            {
                await unitOfWork.RollbackAsync();
                transactionStarted = false;
            }
            return OperationResult<InvoiceDto>.Fail(error);
        }

        try
        {
            await unitOfWork.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            transactionStarted = true;
            await unitOfWork.Invoices.AcquireOrderInvoiceLockAsync(orderId);

            var order = await unitOfWork.Orders.GetByIdAsync(orderId);
            if (order is null)
                return await RollbackFailureAsync("Ordine non trovato.");
            if (order.Status != OrderStatus.Completed)
                return await RollbackFailureAsync("È possibile generare la fattura solo per un ordine completato.");
            if (await unitOfWork.Invoices.GetByOrderIdAsync(orderId) is not null)
                return await RollbackFailureAsync("Fattura già generata per questo ordine.");

            var now = DateTime.UtcNow;
            var prefix = $"FAT-{now.ToString("yyyyMM", CultureInfo.InvariantCulture)}-";
            var invoice = new Invoice(order.ContactId, string.Empty)
            {
                Contact = order.Contact,
                OrderId = order.Id,
                InvoiceDate = now
            };
            var synchronized = invoice.SyncFromOrder(order, accountingCalculator);
            if (!synchronized.Success)
                return await RollbackFailureAsync(synchronized.Error);
            var confirmation = invoice.Confirm(accountingCalculator, now);
            if (!confirmation.Success)
                return await RollbackFailureAsync(confirmation.Error);

            await unitOfWork.Invoices.AcquireInvoiceNumberLockAsync(prefix);
            var number = await AllocateNextInvoiceNumberAsync(prefix);
            if (!number.Success)
                return await RollbackFailureAsync(number.Error!);
            invoice.InvoiceNumber = number.Data!;

            await unitOfWork.Invoices.AddAsync(invoice);
            await unitOfWork.SaveChangesAsync();
            await unitOfWork.CommitAsync();
            transactionStarted = false;
            return OperationResult<InvoiceDto>.Ok(InvoiceMapper.MapToDto(invoice));
        }
        catch (ConcurrencyException ex)
        {
            if (transactionStarted)
                await unitOfWork.RollbackAsync();
            return OperationResult<InvoiceDto>.Fail(ex.Message);
        }
        catch
        {
            if (transactionStarted)
                await unitOfWork.RollbackAsync();
            throw;
        }
    }

    private async Task<OperationResult<string>> AllocateNextInvoiceNumberAsync(string prefix)
    {
        var existingNumbers = await unitOfWork.Invoices.GetInvoiceNumbersByPrefixAsync(prefix);
        var sequences = new List<int>(existingNumbers.Count);
        foreach (var invoiceNumber in existingNumbers)
        {
            if (!invoiceNumber.StartsWith(prefix, StringComparison.Ordinal) ||
                invoiceNumber.Length != prefix.Length + 5 ||
                !int.TryParse(invoiceNumber.AsSpan(prefix.Length), NumberStyles.None,
                    CultureInfo.InvariantCulture, out var sequence))
                return OperationResult<string>.Fail(
                    $"La numerazione fatture del periodo {prefix} contiene un numero non valido; nessuna fattura è stata creata.");
            sequences.Add(sequence);
        }

        sequences.Sort();
        for (var index = 0; index < sequences.Count; index++)
        {
            if (sequences[index] != index + 1)
                return OperationResult<string>.Fail(
                    $"La numerazione fatture del periodo {prefix} non è contigua; verificare i documenti prima di proseguire.");
        }

        if (sequences.Count >= 99999)
            return OperationResult<string>.Fail($"La numerazione fatture del periodo {prefix} ha esaurito i progressivi.");

        return OperationResult<string>.Ok($"{prefix}{(sequences.Count + 1).ToString("D5", CultureInfo.InvariantCulture)}");
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
        return await GetInvoicesAsync(contactId, null, page, pageSize);
    }

    public async Task<OperationResult<PagedResult<InvoiceDto>>> GetInvoicesAsync(
        Guid? contactId,
        string? search,
        int page,
        int pageSize,
        string? status = null,
        DateOnly? dateFrom = null,
        DateOnly? dateTo = null,
        bool unpaidOnly = false,
        string? sortBy = null,
        bool sortDescending = true)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            return OperationResult<PagedResult<InvoiceDto>>.Fail("Page must be positive and pageSize must be between 1 and 100.");
        if (dateFrom.HasValue && dateTo.HasValue && dateFrom.Value > dateTo.Value)
            return OperationResult<PagedResult<InvoiceDto>>.Fail("La data iniziale non può essere successiva alla data finale.");
        if (!string.IsNullOrWhiteSpace(status) &&
            !InvoiceStatuses.IsSupported(status.Trim()))
            return OperationResult<PagedResult<InvoiceDto>>.Fail($"Unsupported invoice status '{status}'.");

        var (invoices, total) = await unitOfWork.Invoices.GetPagedAsync(
            contactId, search, page, pageSize, status, dateFrom, dateTo, unpaidOnly, sortBy, sortDescending);
        var dtos = InvoiceMapper.MapToListDto(invoices);
        var result = new PagedResult<InvoiceDto>(dtos, page, pageSize, total);

        return OperationResult<PagedResult<InvoiceDto>>.Ok(result);
    }

    public async Task<OperationResult<InvoiceDto>> RecordPaymentAsync(Guid invoiceId, RecordPaymentRequest request)
    {
        var invoice = await unitOfWork.Invoices.GetByIdAsync(invoiceId);
        if (invoice == null)
            return OperationResult<InvoiceDto>.Fail("Invoice not found");
        if (request.Amount <= 0m ||
            decimal.Round(request.Amount, 2, MidpointRounding.AwayFromZero) != request.Amount)
            return OperationResult<InvoiceDto>.Fail("Payment amount must be positive and have at most two decimal places.");
        if (string.IsNullOrWhiteSpace(request.PaymentMethod) || request.PaymentMethod.Trim().Length > 50)
            return OperationResult<InvoiceDto>.Fail("Specify a payment method of at most 50 characters.");
        if (request.TransactionDetails?.Length > 500)
            return OperationResult<InvoiceDto>.Fail("Payment details cannot exceed 500 characters.");
        var paymentMethod = request.PaymentMethod.Trim();
        var transactionDetails = string.IsNullOrWhiteSpace(request.TransactionDetails)
            ? null
            : request.TransactionDetails.Trim();
        if (string.Equals(paymentMethod, "Bonifico bancario", StringComparison.OrdinalIgnoreCase) &&
            transactionDetails is null)
            return OperationResult<InvoiceDto>.Fail("Specify the bank transfer reference (CRO/TRN).");
        if (invoice.GrossTotal <= 0m)
            return OperationResult<InvoiceDto>.Fail("A payment cannot be recorded on an invoice without a positive total.");
        if (request.Amount > invoice.GrossTotal - invoice.PaidAmount)
            return OperationResult<InvoiceDto>.Fail("Payment amount exceeds the invoice balance.");

        var payment = new Payment(
            invoiceId: invoiceId,
            amount: request.Amount,
            paymentMethod: paymentMethod,
            transactionDetails: transactionDetails,
            paymentDate: request.PaymentDate
        );

        var recordedPayment = invoice.RecordPayment(request.Amount);
        if (!recordedPayment.Success)
            return OperationResult<InvoiceDto>.Fail(recordedPayment.Error ?? "Pagamento non registrato.");

        invoice.Payments.Add(payment);
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

        if (invoice.ShippingCost > 0m && invoice.ShippingVatRate.HasValue)
        {
            lines.Add(new InvoicePdfLineModel(
                Sku: string.Empty,
                Description: "Spese di spedizione",
                SeasonCollection: null,
                Pieces: 1m,
                UnitPrice: invoice.ShippingCost,
                DiscountPercent: null,
                VatRate: invoice.ShippingVatRate.Value,
                LineTotal: invoice.ShippingCost,
                NatureCode: invoice.ShippingNatureCode));
        }

        var accountingLines = invoice.Items.Select(item => new DocumentLineInput(
            item.Quantity, item.UnitNetPrice, item.VatRate, item.NatureCode, item.DiscountPercent)).ToList();
        if (invoice.ShippingCost > 0m && invoice.ShippingVatRate.HasValue)
            accountingLines.Add(new DocumentLineInput(
                1m, invoice.ShippingCost, invoice.ShippingVatRate.Value, invoice.ShippingNatureCode));
        var accounting = accountingCalculator.Calculate(accountingLines);
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
                TotalNet: calculated.TotalTaxable - invoice.ShippingCost,
                TotalVat: calculated.TotalVat,
                TotalGross: calculated.TotalGross,
                ShippingCost: invoice.ShippingCost),
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
