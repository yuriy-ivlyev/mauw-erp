using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Accounting;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.Application.Services;

public sealed class CreditNoteService(
    IUnitOfWork unitOfWork,
    ItalianAccountingCalculator accountingCalculator) : ICreditNoteService
{
    public async Task<OperationResult<List<CreditNoteAvailableCreditDto>>> GetAvailableForContactAsync(
        Guid contactId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (contactId == Guid.Empty)
            return OperationResult<List<CreditNoteAvailableCreditDto>>.Fail("Cliente non valido.");

        var notes = await unitOfWork.CreditNotes.GetAvailableForContactAsync(contactId);
        return OperationResult<List<CreditNoteAvailableCreditDto>>.Ok(notes
            .Select(note => new CreditNoteAvailableCreditDto(
                note.Id,
                note.CreditNoteNumber,
                note.CreditNoteDate,
                note.GrossTotal,
                note.AllocatedAmount,
                note.RemainingCredit))
            .ToList());
    }

    public async Task<OperationResult<CreditNoteDetailDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (id == Guid.Empty)
            return OperationResult<CreditNoteDetailDto>.Fail("Nota di credito non valida.");

        var creditNote = await unitOfWork.CreditNotes.GetByIdAsync(id);
        if (creditNote is null)
            return OperationResult<CreditNoteDetailDto>.Fail("Nota di credito non trovata.");

        var invoice = await unitOfWork.Invoices.GetByIdAsync(creditNote.InvoiceId);
        if (invoice is null)
            return OperationResult<CreditNoteDetailDto>.Fail("Fattura collegata non trovata.");

        return OperationResult<CreditNoteDetailDto>.Ok(new CreditNoteDetailDto(
            creditNote.Id,
            creditNote.CreditNoteNumber,
            creditNote.CreditNoteDate,
            creditNote.InvoiceId,
            invoice.InvoiceNumber,
            invoice.InvoiceDate,
            invoice.ContactId,
            invoice.Contact?.BusinessName ?? string.Empty,
            creditNote.TaxableTotal,
            creditNote.VatTotal,
            creditNote.GrossTotal,
            creditNote.Reason,
            creditNote.ReturnToWarehouse,
            creditNote.WarehouseId,
            creditNote.SdiStatus.ToString(),
            creditNote.SdiFileName,
            creditNote.SdiTransmissionId,
            creditNote.SdiTransmittedAtUtc,
            creditNote.Items.Select(item => new CreditNoteItemDto(
                item.Id,
                item.SourceInvoiceItemId,
                item.VariantId,
                item.Description,
                item.IsShippingCharge,
                item.Quantity,
                item.UnitNetPrice,
                item.DiscountPercent,
                item.VatRate,
                item.NatureCode,
                item.LineNetTotal,
                item.Variant?.Barcode,
                item.Variant?.Product?.Name,
                item.Variant?.Size?.Name,
                item.Variant?.Color?.Name))
                .ToList()));
    }

    public async Task<OperationResult<CreditNotePreviewDto>> PreviewAsync(
        CreateCreditNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var requestError = ValidateRequest(request);
        if (requestError is not null)
            return OperationResult<CreditNotePreviewDto>.Fail(requestError);

        var invoice = await unitOfWork.Invoices.GetByIdAsync(request.InvoiceId);
        if (invoice is null)
            return OperationResult<CreditNotePreviewDto>.Fail("Fattura non trovata.");
        if (!IsEligible(invoice))
            return OperationResult<CreditNotePreviewDto>.Fail(
                "La nota di credito può essere emessa solo per fatture trasmesse o accettate dallo SDI.");

        var warehouseError = await ValidateWarehouseAsync(request, cancellationToken);
        if (warehouseError is not null)
            return OperationResult<CreditNotePreviewDto>.Fail(warehouseError);

        var prepared = Prepare(invoice, request);
        if (!prepared.Success)
            return OperationResult<CreditNotePreviewDto>.Fail(prepared.Error!);

        return OperationResult<CreditNotePreviewDto>.Ok(ToPreviewDto(invoice, prepared.Data!));
    }

    internal static string? ValidateRequest(CreateCreditNoteRequest? request)
    {
        if (request is null || request.InvoiceId == Guid.Empty)
            return "Fattura non valida.";
        if (!CreditNoteReasons.IsSupported(request.Reason))
            return "Selezionare una causale di storno valida.";
        if (request.Lines is null || request.Lines.Any(line => line is null))
            return "Indicare le righe della nota di credito.";
        if (request.Lines.Any(line =>
                line.IsShippingCharge == line.InvoiceItemId.HasValue ||
                line.InvoiceItemId == Guid.Empty ||
                line.Quantity <= 0m ||
                decimal.Round(line.Quantity, 8, MidpointRounding.AwayFromZero) != line.Quantity))
            return "Le righe devono indicare una quantità positiva valida e una riga fattura.";
        if (request.Lines
            .GroupBy(line => (line.InvoiceItemId, line.IsShippingCharge))
            .Any(group => group.Count() > 1))
            return "Una riga della fattura non può essere selezionata più di una volta.";
        if (!request.ReturnToWarehouse && request.WarehouseId.HasValue)
            return "Il magazzino è selezionabile solo per il rientro fisico della merce.";
        if (request.WarehouseId == Guid.Empty)
            return "Il magazzino selezionato non è valido.";

        return null;
    }

    internal async Task<string?> ValidateWarehouseAsync(
        CreateCreditNoteRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!request.ReturnToWarehouse)
            return null;
        if (!request.WarehouseId.HasValue)
            return "Selezionare il magazzino di destinazione per il rientro merce.";

        var warehouse = await unitOfWork.Warehouses.GetByIdAsync(request.WarehouseId.Value);
        if (warehouse is null || !warehouse.IsActive || warehouse.IsDeleted)
            return "Il magazzino selezionato non esiste o non è attivo.";

        return null;
    }

    internal OperationResult<PreparedCreditNote> Prepare(
        Invoice invoice,
        CreateCreditNoteRequest request)
    {
        var legacyCreditNote = invoice.CreditNotes.FirstOrDefault(note =>
            note.GrossTotal > 0m && note.Items.Count == 0);
        if (legacyCreditNote is not null)
            return OperationResult<PreparedCreditNote>.Fail(
                $"La fattura contiene la nota di credito storica {legacyCreditNote.CreditNoteNumber} senza dettaglio righe; " +
                "verificare manualmente le quantità già stornate prima di emetterne altre.");

        var creditedByInvoiceItem = invoice.CreditNotes
            .SelectMany(note => note.Items)
            .Where(item => item.SourceInvoiceItemId.HasValue)
            .GroupBy(item => item.SourceInvoiceItemId!.Value)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
        var creditedShipping = invoice.CreditNotes
            .SelectMany(note => note.Items)
            .Where(item => item.IsShippingCharge)
            .Sum(item => item.Quantity);

        var sources = new List<CreditNoteSourceLine>(invoice.Items.Count + 1);
        foreach (var item in invoice.Items)
        {
            var credited = creditedByInvoiceItem.GetValueOrDefault(item.Id);
            if (credited > item.Quantity)
                return OperationResult<PreparedCreditNote>.Fail(
                    $"Le quantità stornate della riga {item.Id} superano la quantità fatturata.");

            var variant = item.Variant;
            sources.Add(new CreditNoteSourceLine(
                item.Id,
                false,
                BuildDescription(variant),
                variant?.Barcode,
                item.Quantity,
                credited,
                item.Quantity - credited,
                item.UnitNetPrice,
                item.DiscountPercent,
                item.VatRate,
                item.NatureCode,
                item.LineNetTotal,
                item));
        }

        if (invoice.ShippingCost > 0m)
        {
            if (!invoice.ShippingVatRate.HasValue)
                return OperationResult<PreparedCreditNote>.Fail(
                    "Le spese di spedizione della fattura non hanno una classificazione IVA valida.");
            if (creditedShipping > 1m)
                return OperationResult<PreparedCreditNote>.Fail(
                    "La quantità stornata delle spese di spedizione supera la quantità fatturata.");

            sources.Add(new CreditNoteSourceLine(
                null,
                true,
                "Spese di spedizione",
                null,
                1m,
                creditedShipping,
                1m - creditedShipping,
                invoice.ShippingCost,
                0m,
                invoice.ShippingVatRate.Value,
                invoice.ShippingNatureCode,
                invoice.ShippingCost,
                null));
        }

        var sourceByKey = sources.ToDictionary(
            source => (source.InvoiceItemId, source.IsShippingCharge));
        var selectedSources = new List<CreditNoteSourceLine>(request.Lines.Count);
        var accountingInputs = new List<DocumentLineInput>(request.Lines.Count);
        foreach (var requestedLine in request.Lines)
        {
            if (!sourceByKey.TryGetValue(
                    (requestedLine.InvoiceItemId, requestedLine.IsShippingCharge), out var source))
                return OperationResult<PreparedCreditNote>.Fail(
                    "Una delle righe selezionate non appartiene alla fattura.");
            if (requestedLine.Quantity > source.RemainingQuantity)
                return OperationResult<PreparedCreditNote>.Fail(
                    $"La quantità da stornare per '{source.Description}' supera il residuo fatturato " +
                    $"({source.RemainingQuantity}).");
            if (request.ReturnToWarehouse && !source.IsShippingCharge &&
                (requestedLine.Quantity != decimal.Truncate(requestedLine.Quantity) ||
                 requestedLine.Quantity > int.MaxValue))
                return OperationResult<PreparedCreditNote>.Fail(
                    $"La quantità da rientrare a magazzino per '{source.Description}' deve essere un numero intero.");

            selectedSources.Add(source);
            accountingInputs.Add(new DocumentLineInput(
                requestedLine.Quantity,
                source.UnitNetPrice,
                source.VatRate,
                source.NatureCode,
                source.DiscountPercent));
        }

        if (request.Reason == CreditNoteReasons.FullCancellation && request.Lines.Count > 0)
        {
            var activeSources = sources.Where(source => source.RemainingQuantity > 0m).ToArray();
            if (request.Lines.Count != activeSources.Length ||
                activeSources.Any(source =>
                    !request.Lines.Any(line =>
                        line.InvoiceItemId == source.InvoiceItemId &&
                        line.IsShippingCharge == source.IsShippingCharge &&
                        line.Quantity == source.RemainingQuantity)))
                return OperationResult<PreparedCreditNote>.Fail(
                    "Lo storno totale deve includere tutte le quantità residue della fattura.");
        }

        var calculation = accountingCalculator.Calculate(accountingInputs);
        if (!calculation.Success)
            return OperationResult<PreparedCreditNote>.Fail(calculation.Error!);

        var lines = selectedSources
            .Zip(calculation.Data!.Lines, (source, calculated) =>
                new PreparedCreditNoteLine(source, calculated))
            .ToList();
        var available = sources
            .Select(source => new CreditNoteSelectableLineDto(
                source.InvoiceItemId,
                source.IsShippingCharge,
                source.Description,
                source.Sku,
                source.OriginalQuantity,
                source.CreditedQuantity,
                source.RemainingQuantity,
                source.UnitNetPrice,
                source.DiscountPercent,
                source.VatRate,
                source.NatureCode,
                source.LineNetTotal))
            .ToList();

        return OperationResult<PreparedCreditNote>.Ok(
            new PreparedCreditNote(calculation.Data, lines, available));
    }

    private static CreditNotePreviewDto ToPreviewDto(
        Invoice invoice,
        PreparedCreditNote prepared) =>
        new(
            invoice.Id,
            prepared.Document.TotalTaxable,
            prepared.Document.TotalVat,
            prepared.Document.TotalGross,
            prepared.Document.VatSummary
                .Select(summary => new CreditNoteVatSummaryDto(
                    summary.VatRate, summary.NatureCode, summary.TaxableAmount, summary.VatAmount))
                .ToList(),
            prepared.AvailableLines,
            prepared.Lines.Select(line => new CreditNotePreviewLineDto(
                line.Source.InvoiceItemId,
                line.Source.IsShippingCharge,
                line.Source.Description,
                line.Source.Sku,
                line.Calculation.Quantity,
                line.Calculation.UnitNetPrice,
                line.Calculation.DiscountPercent,
                line.Calculation.VatRate,
                line.Calculation.NatureCode,
                line.Calculation.LineNetTotal))
                .ToList());

    private static bool IsEligible(Invoice invoice) =>
        invoice.SdiStatus is SdiStatus.Transmitted or SdiStatus.Accepted;

    private static string BuildDescription(ProductVariant? variant)
    {
        if (variant is null)
            return "Articolo fatturato";
        var details = string.Join(" ", new[] { variant.Size?.Name, variant.Color?.Name }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(details)
            ? variant.Product?.Name ?? variant.Barcode
            : $"{variant.Product?.Name ?? variant.Barcode} {details}";
    }
}

internal sealed record CreditNoteSourceLine(
    Guid? InvoiceItemId,
    bool IsShippingCharge,
    string Description,
    string? Sku,
    decimal OriginalQuantity,
    decimal CreditedQuantity,
    decimal RemainingQuantity,
    decimal UnitNetPrice,
    decimal DiscountPercent,
    decimal VatRate,
    string? NatureCode,
    decimal LineNetTotal,
    InvoiceItem? InvoiceItem);

internal sealed record PreparedCreditNoteLine(
    CreditNoteSourceLine Source,
    CalculatedDocumentLine Calculation);

internal sealed record PreparedCreditNote(
    CalculatedDocument Document,
    IReadOnlyList<PreparedCreditNoteLine> Lines,
    List<CreditNoteSelectableLineDto> AvailableLines);
