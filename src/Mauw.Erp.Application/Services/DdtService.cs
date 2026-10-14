using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.Application.Services;

/// <summary>
/// DDT service implementation.
/// </summary>
public class DdtService(
    IUnitOfWork unitOfWork,
    IValidator<CreateOrderItemRequest> itemValidator,
    PricingEngine pricingEngine)
    : IDdtService
{
    public async Task<OperationResult<DdtDto>> CreateDdtAsync(CreateDdtRequest request)
    {
        // Verify contact exists
        var contact = await unitOfWork.Contacts.GetByIdAsync(request.ContactId);
        if (contact == null)
            return OperationResult<DdtDto>.Fail("Contact not found");

        // Create DDT
        var ddt = new DdtDocument(request.ContactId, request.DdtNumber);
        
        await unitOfWork.DdtDocuments.AddAsync(ddt);
        await unitOfWork.SaveChangesAsync();

        var dto = DdtMapper.MapToDto(ddt);
        return OperationResult<DdtDto>.Ok(dto);
    }

    public async Task<OperationResult<DdtDto>> GetDdtAsync(Guid id)
    {
        var ddt = await unitOfWork.DdtDocuments.GetByIdAsync(id);
        if (ddt == null)
            return OperationResult<DdtDto>.Fail("DDT not found");

        var dto = DdtMapper.MapToDto(ddt);
        return OperationResult<DdtDto>.Ok(dto);
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

        // Get variant with product information
        var variant = await unitOfWork.ProductVariants.GetByIdAsync(request.VariantId);
        if (variant?.Product == null)
            return OperationResult<DdtDto>.Fail("Variant not found");

        // Calculate pricing (use 0% discount for DDT)
        var pricingResult = pricingEngine.CalculatePrice(
            purchaseCost: variant.PurchaseCost,
            markupRate: variant.Product.DefaultMarkupRate,
            discountRate: 0,
            vatRate: variant.Product.VatRate);

        // Create item
        var item = new DdtItem(
            ddtDocumentId: ddtId,
            variantId: request.VariantId,
            quantity: request.Quantity,
            unitNetPrice: pricingResult.FinalNetPrice,
            vatRate: variant.Product.VatRate);

        // Add item and update totals
        ddt.AddItem(item);
        
        await unitOfWork.DdtDocuments.UpdateAsync(ddt);
        await unitOfWork.SaveChangesAsync();

        var dto = DdtMapper.MapToDto(ddt);
        return OperationResult<DdtDto>.Ok(dto);
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
}