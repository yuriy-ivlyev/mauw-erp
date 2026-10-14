using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.Application.Services;

/// <summary>
/// Invoice service implementation.
/// </summary>
public class InvoiceService(
    IUnitOfWork unitOfWork,
    IValidator<CreateInvoiceRequest> createValidator,
    PricingEngine pricingEngine)
    : IInvoiceService
{
    private readonly PricingEngine _pricingEngine = pricingEngine;

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

        var invoice = new Invoice(
            contactId: request.ContactId,
            invoiceNumber: $"INV-{DateTime.UtcNow:yyyyMMddHHmmss}"
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
        var invoices = await unitOfWork.Invoices.GetByContactIdAsync(contactId);
        var paginated = invoices
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var total = invoices.Count();
        var dtos = InvoiceMapper.MapToListDto(paginated);
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

        var item = new InvoiceItem(
            invoiceId: invoiceId,
            variantId: request.VariantId,
            quantity: request.Quantity,
            unitNetPrice: request.UnitNetPrice,
            vatRate: (decimal)request.UnitVatAmount / request.UnitNetPrice * 100 // Reverse-calculate VAT rate
        );

        invoice.AddItem(item);
        await unitOfWork.Invoices.UpdateAsync(invoice);
        await unitOfWork.SaveChangesAsync();

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
        await unitOfWork.SaveChangesAsync();

        var updated = await unitOfWork.Invoices.GetByIdAsync(invoiceId);
        return OperationResult<InvoiceDto>.Ok(InvoiceMapper.MapToDto(updated!));
    }

    public async Task<OperationResult<bool>> DeleteInvoiceAsync(Guid id)
    {
        var invoice = await unitOfWork.Invoices.GetByIdAsync(id);
        if (invoice == null)
            return OperationResult<bool>.Fail("Invoice not found");

        await unitOfWork.Invoices.DeleteAsync(invoice);
        await unitOfWork.SaveChangesAsync();

        return OperationResult<bool>.Ok(true);
    }
}