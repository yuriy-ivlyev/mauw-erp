using System.Globalization;
using Mauw.Erp.Application.Diagnostics;
using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Dtos.Pdf;
using Mauw.Erp.Contracts.Interfaces;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Accounting;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Mauw.Erp.Application.Services;

/// <summary>
/// Order service implementation.
/// </summary>
public partial class OrderService(
    IUnitOfWork unitOfWork,
    IValidator<CreateOrderRequest> createValidator,
    IValidator<CreateOrderItemRequest> itemValidator,
    PricingEngine pricingEngine,
    ItalianAccountingCalculator accountingCalculator,
    ICompanySettingsService companySettingsService,
    IInvoicePdfGenerator pdfGenerator,
    ILogger<OrderService> logger)
    : IOrderService
{
    private static readonly CultureInfo ItalianCulture = CultureInfo.GetCultureInfo("it-IT");

    public async Task<OperationResult<OrderDto>> CreateOrderAsync(CreateOrderRequest request)
    {
        var validationResult = createValidator.Validate(request);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.Message}"));
            LogOrderValidationFailed(logger, request.ContactId, errors);
            return OperationResult<OrderDto>.Fail(errors);
        }

        var contact = await unitOfWork.Contacts.GetByIdAsync(request.ContactId);
        if (contact == null)
            return OperationResult<OrderDto>.Fail("Contact not found");

        var sw = System.Diagnostics.Stopwatch.StartNew();

        var orderType = Enum.TryParse<OrderType>(request.OrderType, out var parsedType)
            ? parsedType : OrderType.Standard;

        var now = DateTime.UtcNow;
        var prefix = orderType switch
        {
            OrderType.Preventivo => $"PRV-{now:MMyyyy}-",
            OrderType.WebOrder   => $"WEB-{now:MMyyyy}-",
            _                    => $"ORD-{now:MMyyyy}-",
        };
        var count = await unitOfWork.Orders.CountOrdersByPrefixAsync(prefix);
        var orderNumber = $"{prefix}{(count + 1):D3}";

        var order = new Order(
            contactId: request.ContactId,
            orderNumber: orderNumber,
            customerDiscountRate: request.DiscountRate ?? 0
        ) { OrderType = orderType };

        await unitOfWork.Orders.AddAsync(order);
        await unitOfWork.SaveChangesAsync();

        sw.Stop();
        TelemetryDiagnostics.OrdersCreated.Add(1);
        TelemetryDiagnostics.OperationDuration.Record(sw.Elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>("operation", "order.create"));

        LogOrderCreated(logger, order.OrderNumber, request.ContactId);
        return OperationResult<OrderDto>.Ok(OrderMapper.MapToDto(order));
    }

    public async Task<OperationResult<OrderDto>> GetOrderAsync(Guid id)
    {
        var order = await unitOfWork.Orders.GetByIdAsync(id);
        if (order == null)
            return OperationResult<OrderDto>.Fail("Order not found");

        return OperationResult<OrderDto>.Ok(OrderMapper.MapToDto(order));
    }

    public async Task<OperationResult<PagedResult<OrderDto>>> GetAllOrdersAsync(string? search, string? status, string? orderType, int page, int pageSize)
    {
        var (orders, total) = await unitOfWork.Orders.GetAllPagedAsync(search, status, orderType, page, pageSize);
        var orderList = orders.ToList();
        var orderIds = orderList.Select(o => o.Id).ToList();

        var invoices = orderIds.Count > 0
            ? (await unitOfWork.Invoices.GetByOrderIdsAsync(orderIds)).ToList()
            : [];

        var ddts = orderIds.Count > 0
            ? (await unitOfWork.DdtDocuments.GetByOrderIdsAsync(orderIds)).ToList()
            : [];

        var invoiceIds = invoices.Select(i => i.Id).ToList();
        var creditNotes = invoiceIds.Count > 0
            ? (await unitOfWork.CreditNotes.GetByInvoiceIdsAsync(invoiceIds)).ToList()
            : [];

        var dtos = orderList.Select(o =>
        {
            var invoice = invoices.FirstOrDefault(i => i.OrderId == o.Id);
            var ddt = ddts.FirstOrDefault(d => d.OrderId == o.Id);
            var creditNoteRefs = invoice != null
                ? creditNotes
                    .Where(cn => cn.InvoiceId == invoice.Id)
                    .Select(cn => new CreditNoteRefDto(cn.Id, cn.CreditNoteNumber, cn.CreditNoteDate, cn.GrossTotal, cn.Reason))
                    .ToList()
                : [];

            OrderLinkedDocumentsDto? linked = invoice != null || ddt != null
                ? new OrderLinkedDocumentsDto(invoice?.Id, invoice?.InvoiceNumber, ddt?.Id, ddt?.DdtNumber, creditNoteRefs)
                : null;

            return OrderMapper.MapToDto(o, linked);
        }).ToList();

        return OperationResult<PagedResult<OrderDto>>.Ok(new PagedResult<OrderDto>(dtos, page, pageSize, total));
    }

    public async Task<OperationResult<PagedResult<OrderDto>>> GetOrdersByContactAsync(Guid contactId, int page, int pageSize)
    {
        var orders = await unitOfWork.Orders.GetByContactIdAsync(contactId);
        var paginated = orders
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var total = orders.Count();
        var dtos = OrderMapper.MapToListDto(paginated);
        var result = new PagedResult<OrderDto>(dtos, page, pageSize, total);

        return OperationResult<PagedResult<OrderDto>>.Ok(result);
    }

    public async Task<OperationResult<OrderDto>> AddItemAsync(Guid orderId, CreateOrderItemRequest request)
    {
        var itemValidationResult = itemValidator.Validate(request);
        if (!itemValidationResult.IsValid)
        {
            var errors = string.Join("; ", itemValidationResult.Errors.Select(e => $"{e.PropertyName}: {e.Message}"));
            return OperationResult<OrderDto>.Fail(errors);
        }

        var order = await unitOfWork.Orders.GetByIdAsync(orderId);
        if (order == null)
            return OperationResult<OrderDto>.Fail("Order not found");

        var variant = await unitOfWork.ProductVariants.GetByIdAsync(request.VariantId);
        if (variant == null)
            return OperationResult<OrderDto>.Fail("Variant not found");

        var product = await unitOfWork.Products.GetByIdAsync(variant.ProductId);
        if (product == null)
            return OperationResult<OrderDto>.Fail("Product not found");

        var pricingResult = pricingEngine.CalculatePrice(
            variant.PurchaseCost,
            product.DefaultMarkupRate,
            order.CustomerDiscountRate,
            product.VatRate
        );

        var line = accountingCalculator.CalculateLine(new DocumentLineInput(request.Quantity,
            request.UnitGrossPrice ?? request.UnitNetPrice ?? pricingResult.ListPrice,
            request.VatRate ?? product.VatRate, request.NatureCode,
            request.DiscountPercent ?? order.CustomerDiscountRate, PriceIncludesVat: request.UnitGrossPrice.HasValue));
        if (!line.Success)
            return OperationResult<OrderDto>.Fail(line.Error);

        var item = new OrderItem(
            orderId: orderId,
            variantId: request.VariantId,
            quantity: request.Quantity,
            unitNetPrice: line.Data!.UnitNetPrice,
            vatRate: line.Data.VatRate,
            natureCode: line.Data.NatureCode,
            discountPercent: line.Data.DiscountPercent
        );

        var accounting = order.AddItem(item, accountingCalculator);
        if (!accounting.Success)
            return OperationResult<OrderDto>.Fail(accounting.Error);
        await unitOfWork.Orders.UpdateAsync(order);
        await unitOfWork.SaveChangesAsync();

        logger.LogInformation("Item {VariantId} (qty {Quantity}) added to order {OrderId}",
            request.VariantId, request.Quantity, orderId);

        var updated = await unitOfWork.Orders.GetByIdAsync(orderId);
        return OperationResult<OrderDto>.Ok(OrderMapper.MapToDto(updated!));
    }

    public async Task<OperationResult<OrderDto>> ConfirmOrderAsync(Guid orderId)
    {
        var order = await unitOfWork.Orders.GetByIdAsync(orderId);
        if (order == null)
            return OperationResult<OrderDto>.Fail("Order not found");

        try
        {
            order.Confirm();
            await unitOfWork.Orders.UpdateAsync(order);
            await unitOfWork.SaveChangesAsync();
        }
        catch (InvalidOperationException ex)
        {
            return OperationResult<OrderDto>.Fail(ex.Message);
        }

        logger.LogInformation("Order {OrderId} confirmed", orderId);

        var updated = await unitOfWork.Orders.GetByIdAsync(orderId);
        return OperationResult<OrderDto>.Ok(OrderMapper.MapToDto(updated!));
    }

    public async Task<OperationResult<bool>> DeleteOrderAsync(Guid id)
    {
        var order = await unitOfWork.Orders.GetByIdAsync(id);
        if (order == null)
            return OperationResult<bool>.Fail("Order not found");

        await unitOfWork.Orders.DeleteAsync(order);
        await unitOfWork.SaveChangesAsync();

        logger.LogInformation("Order {OrderId} deleted", id);
        return OperationResult<bool>.Ok(true);
    }

    public async Task<OperationResult<byte[]>> GetOrderPdfAsync(Guid orderId)
    {
        var order = await unitOfWork.Orders.GetCassaSessionWithItemsAsync(orderId);
        if (order == null)
            return OperationResult<byte[]>.Fail("Order not found");

        var agent = await DocumentPdfMapper.GetAgentAsync(unitOfWork, order.ContactId);
        if (!agent.Success)
            return OperationResult<byte[]>.Fail(agent.Error!);
        var companyResult = await companySettingsService.GetCompanyAsync();
        var company = companyResult.Success ? companyResult.Data : null;
        var sellerAddress = company?.LegalHeadquarters
            ?? company?.OperationalSites.FirstOrDefault(s => s.IsMainOperationalSite)
            ?? company?.OperationalSites.FirstOrDefault();
        var customerAddress = order.Contact?.LegalHeadquarters ?? order.Contact?.Addresses.FirstOrDefault();

        var seller = new InvoicePdfSellerDetails(
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
            IsShareCapitalFullyPaid: company?.IsShareCapitalFullyPaid ?? false
        );

        var customer = new InvoicePdfCustomerDetails(
            BusinessName: order.Contact?.BusinessName ?? string.Empty,
            VatNumber: order.Contact?.VatNumber,
            FiscalCode: order.Contact?.TaxCode,
            Address: customerAddress?.Street ?? string.Empty,
            City: customerAddress?.City ?? string.Empty,
            PostalCode: customerAddress?.ZipCode ?? string.Empty,
            Province: customerAddress?.Province ?? string.Empty,
            SdiCode: order.Contact?.SdiCode,
            Pec: order.Contact?.PecEmail
        );

        var lines = order.Items.Select(item => new InvoicePdfLineModel(
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
            Pieces: (int)item.Quantity,
            UnitPrice: item.UnitNetPrice,
            DiscountPercent: item.DiscountPercent,
            VatRate: item.VatRate,
            LineTotal: item.LineNetTotal,
            NatureCode: item.NatureCode
        )).ToList();

        var accounting = accountingCalculator.Calculate(order.Items.Select(item => new DocumentLineInput(
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

        var totals = new InvoicePdfTotals(
            TotalPieces: order.Items.Sum(i => (int)i.Quantity),
            TotalNet: calculated.TotalTaxable,
            TotalVat: calculated.TotalVat,
            TotalGross: calculated.TotalGross,
            ShippingCost: 0m
        );

        var model = new InvoicePdfModel(
            DocumentNumber: order.OrderNumber,
            DocumentType: "ORDINE",
            IssueDate: DateOnly.FromDateTime(order.OrderDate),
            Currency: "EUR",
            Seller: seller,
            Customer: customer,
            Payment: new InvoicePdfPaymentInfo(
                PaymentTerms: "A vista",
                PaymentMethod: "MP01",
                DueDate: null,
                Iban: null,
                Swift: null,
                BankName: null),
            Lines: lines,
            VatSummary: vatSummary,
            Totals: totals,
            CustomerOrderRef: order.DocumentReference,
            Agent: agent.Data
        );

        var bytes = await pdfGenerator.GenerateInvoicePdfAsync(model);
        return OperationResult<byte[]>.Ok(bytes);
    }

    public async Task<OperationResult<CassaSessionDetailDto>> CreateCassaSessionAsync(CreateCassaSessionRequest request)
    {
        var contact = await unitOfWork.Contacts.GetByIdAsync(request.ContactId);
        if (contact is null)
            return OperationResult<CassaSessionDetailDto>.Fail("Cliente non trovato");

        var accounting = CalculateCart(request.Items);
        if (!accounting.Success)
            return OperationResult<CassaSessionDetailDto>.Fail(accounting.Error);

        var now = DateTime.UtcNow;
        var prefix = $"ORD-{now:MMyyyy}-";
        var count = await unitOfWork.Orders.CountOrdersByPrefixAsync(prefix);
        var sessionCode = $"{prefix}{(count + 1):D3}";

        var order = new Order(request.ContactId, sessionCode) { OrderType = OrderType.CassaSession };
        order.SetDocumentReference(request.DocumentReference);
        order.Suspend(request.TerminalId);

        for (var i = 0; i < request.Items.Count; i++)
        {
            var item = request.Items[i];
            var line = accounting.Data!.Lines[i];
            order.Items.Add(new OrderItem(order.Id, item.VariantId, item.Quantity, line.UnitNetPrice, item.VatRate,
                item.MaxQuantity, line.NatureCode, line.DiscountPercent) { LineNetTotal = line.LineNetTotal });
        }
        order.TaxableTotal = accounting.Data!.TotalTaxable;
        order.VatTotal = accounting.Data.TotalVat;
        order.GrossTotal = accounting.Data.TotalGross;
        await unitOfWork.Orders.AddAsync(order);
        await unitOfWork.SaveChangesAsync();

        logger.LogInformation("Cassa session {Code} created", sessionCode);

        var loaded = await unitOfWork.Orders.GetCassaSessionWithItemsAsync(order.Id);
        return OperationResult<CassaSessionDetailDto>.Ok(MapCassaDetail(loaded!, contact.BusinessName));
    }

    public async Task<OperationResult<CassaSessionDetailDto>> UpdateCassaSessionAsync(Guid orderId, UpdateCassaSessionRequest request)
    {
        var order = await unitOfWork.Orders.GetCassaSessionWithItemsAsync(orderId);
        if (order is null)
            return OperationResult<CassaSessionDetailDto>.Fail("Sessione non trovata");

        var contact = await unitOfWork.Contacts.GetByIdAsync(request.ContactId);
        if (contact is null)
            return OperationResult<CassaSessionDetailDto>.Fail("Cliente non trovato");

        var accounting = CalculateCart(request.Items);
        if (!accounting.Success)
            return OperationResult<CassaSessionDetailDto>.Fail(accounting.Error);

        order.ContactId = request.ContactId;
        order.SetDocumentReference(request.DocumentReference);
        order.ClearItems();

        for (var i = 0; i < request.Items.Count; i++)
        {
            var item = request.Items[i];
            var line = accounting.Data!.Lines[i];
            var orderItem = new OrderItem(order.Id, item.VariantId, item.Quantity, line.UnitNetPrice, item.VatRate,
                item.MaxQuantity, line.NatureCode, line.DiscountPercent) { LineNetTotal = line.LineNetTotal };
            order.Items.Add(orderItem);
        }
        order.TaxableTotal = accounting.Data!.TotalTaxable;
        order.VatTotal = accounting.Data.TotalVat;
        order.GrossTotal = accounting.Data.TotalGross;

        order.Suspend(request.TerminalId);
        await unitOfWork.Orders.UpdateAsync(order);
        await unitOfWork.SaveChangesAsync();

        var loaded = await unitOfWork.Orders.GetCassaSessionWithItemsAsync(order.Id);
        return OperationResult<CassaSessionDetailDto>.Ok(MapCassaDetail(loaded!, contact.BusinessName));
    }

    public async Task<OperationResult<CassaSessionDetailDto>> ResumeCassaSessionAsync(Guid orderId, string? terminalId)
    {
        var order = await unitOfWork.Orders.GetCassaSessionWithItemsAsync(orderId);
        if (order is null)
            return OperationResult<CassaSessionDetailDto>.Fail("Sessione non trovata");

        order.Resume(terminalId);
        await unitOfWork.Orders.UpdateAsync(order);
        await unitOfWork.SaveChangesAsync();

        var contactName = order.Contact?.BusinessName ?? "";
        return OperationResult<CassaSessionDetailDto>.Ok(MapCassaDetail(order, contactName));
    }

    public async Task<OperationResult<bool>> TerminateCassaSessionAsync(Guid orderId)
    {
        var order = await unitOfWork.Orders.GetByIdAsync(orderId);
        if (order is null)
            return OperationResult<bool>.Fail("Sessione non trovata");

        order.Terminate();
        await unitOfWork.Orders.UpdateAsync(order);
        await unitOfWork.SaveChangesAsync();

        logger.LogInformation("Cassa session {OrderId} terminated", orderId);
        return OperationResult<bool>.Ok(true);
    }

    public async Task<OperationResult<IEnumerable<CassaSessionSummaryDto>>> GetSuspendedSessionsAsync()
    {
        var orders = await unitOfWork.Orders.GetSuspendedCassaSessionsAsync();
        var dtos = orders.Select(o => new CassaSessionSummaryDto(
            o.Id,
            o.OrderNumber,
            o.Contact?.BusinessName,
            o.Items.Count,
            o.UpdatedAt));

        return OperationResult<IEnumerable<CassaSessionSummaryDto>>.Ok(dtos);
    }

    public async Task<OperationResult<CassaSessionDetailDto>> GetCassaSessionAsync(Guid orderId)
    {
        var order = await unitOfWork.Orders.GetCassaSessionWithItemsAsync(orderId);
        if (order is null)
            return OperationResult<CassaSessionDetailDto>.Fail("Sessione non trovata");

        var contactName = order.Contact?.BusinessName ?? "";
        return OperationResult<CassaSessionDetailDto>.Ok(MapCassaDetail(order, contactName));
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

    public OperationResult<CassaPreviewDto> PreviewCassaCart(CassaPreviewRequest request)
    {
        var accounting = CalculateCart(request?.Items);
        if (!accounting.Success)
            return OperationResult<CassaPreviewDto>.Fail(accounting.Error);

        var document = accounting.Data!;
        return OperationResult<CassaPreviewDto>.Ok(new CassaPreviewDto(
            document.Lines.Select(line => new CassaPreviewLineDto(
                line.LineNumber, line.UnitNetPrice, line.DiscountPercent, line.VatRate,
                line.NatureCode, line.LineNetTotal)).ToList(),
            document.VatSummary.Select(group => new CassaPreviewVatDto(
                group.VatRate, group.NatureCode, group.TaxableAmount, group.VatAmount)).ToList(),
            document.TotalTaxable,
            document.TotalVat,
            document.TotalGross));
    }

    private AccountingResult<CalculatedDocument> CalculateCart(List<CassaCartItemRequest>? items)
    {
        if (items is null || items.Any(item => item is null))
            return AccountingResult<CalculatedDocument>.Fail("Il carrello e le sue righe non possono essere null.");
        return accountingCalculator.Calculate(items.Select(item => new DocumentLineInput(
            item.Quantity, item.UnitNetPrice, item.VatRate, item.NatureCode, item.DiscountPercent)));
    }

    private static CassaSessionDetailDto MapCassaDetail(Order order, string contactName) =>
        new(
            order.Id,
            order.OrderNumber,
            order.ContactId.ToString(),
            contactName,
            order.Contact?.VatNumber,
            order.DocumentReference,
            order.Items.Select(i => new CassaCartItemDto(
                i.VariantId,
                i.Variant?.Barcode ?? "",
                i.Variant?.ProductId.ToString() ?? "",
                i.Variant?.Product?.Name ?? "",
                i.Variant?.Size?.Name ?? "",
                i.Variant?.Color?.Name ?? "",
                i.UnitNetPrice,
                i.VatRate,
                i.Quantity,
                i.MaxQuantity,
                i.Variant?.WeightGrams,
                i.NatureCode,
                i.DiscountPercent
            )).ToList()
        );

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Order {OrderNumber} created for contact {ContactId}")]
    private static partial void LogOrderCreated(ILogger logger, string orderNumber, Guid contactId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Order creation validation failed for contact {ContactId}: {Errors}")]
    private static partial void LogOrderValidationFailed(ILogger logger, Guid contactId, string errors);
}
