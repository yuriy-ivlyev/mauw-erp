using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Domain.Enums;

namespace Mauw.Erp.Application.Services;

/// <summary>
/// Result type for operations without exceptions.
/// </summary>
public record OperationResult<T>(bool Success, T? Data, string? Error = null)
{
    public static OperationResult<T> Ok(T data) => new(true, data);
    public static OperationResult<T> Fail(string error) => new(false, default, error);
}

/// <summary>
/// Paginated result.
/// </summary>
public record PagedResult<T>(List<T> Items, int PageNumber, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>
/// Product application service - business logic layer.
/// </summary>
public interface IProductService
{
    Task<OperationResult<ProductDto>> CreateProductAsync(CreateProductRequest request);
    Task<OperationResult<ProductDto>> GetProductAsync(Guid id);
    Task<OperationResult<PagedResult<ProductDto>>> GetProductsAsync(int page, int pageSize, string? search = null);
    Task<OperationResult<ProductDto>> UpdateProductAsync(Guid id, CreateProductRequest request);
    Task<OperationResult<bool>> DeleteProductAsync(Guid id);
    Task<OperationResult<BarcodeVariantDto>> GetVariantByBarcodeAsync(string barcode);
}

/// <summary>
/// Product service implementation.
/// </summary>
public class ProductService : IProductService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateProductRequest> _validator;

    public ProductService(IUnitOfWork unitOfWork, IValidator<CreateProductRequest> validator)
    {
        _unitOfWork = unitOfWork;
        _validator = validator;
    }

    public async Task<OperationResult<ProductDto>> CreateProductAsync(CreateProductRequest request)
    {
        var validationResult = _validator.Validate(request);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.Message}"));
            return OperationResult<ProductDto>.Fail(errors);
        }

        var product = new Product(
            supplierId: request.SupplierId,
            supplierCode: request.SupplierCode,
            internalCode: request.InternalCode,
            name: request.Name,
            defaultMarkupRate: request.DefaultMarkupRate,
            vatRate: request.VatRate,
            imageUrl: request.ImageUrl
        );

        foreach (var variantRequest in request.Variants)
        {
            var variant = new ProductVariant(
                productId: product.Id,
                size: variantRequest.Size,
                color: variantRequest.Color,
                barcode: variantRequest.Barcode,
                purchaseCost: variantRequest.PurchaseCost,
                stockQuantity: variantRequest.StockQuantity
            );
            product.Variants.Add(variant);
        }

        await _unitOfWork.Products.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        return OperationResult<ProductDto>.Ok(ProductMapper.MapToDto(product));
    }

    public async Task<OperationResult<ProductDto>> GetProductAsync(Guid id)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(id);
        if (product == null)
            return OperationResult<ProductDto>.Fail("Product not found");

        return OperationResult<ProductDto>.Ok(ProductMapper.MapToDto(product));
    }

    public async Task<OperationResult<PagedResult<ProductDto>>> GetProductsAsync(int page, int pageSize, string? search = null)
    {
        var products = await _unitOfWork.Products.GetPaginatedAsync(page, pageSize, search);
        var total = await _unitOfWork.Products.GetTotalCountAsync(search);

        var dtos = ProductMapper.MapToListDto(products);
        var result = new PagedResult<ProductDto>(dtos, page, pageSize, total);

        return OperationResult<PagedResult<ProductDto>>.Ok(result);
    }

    public async Task<OperationResult<ProductDto>> UpdateProductAsync(Guid id, CreateProductRequest request)
    {
        var validationResult = _validator.Validate(request);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.Message}"));
            return OperationResult<ProductDto>.Fail(errors);
        }

        var product = await _unitOfWork.Products.GetByIdAsync(id);
        if (product == null)
            return OperationResult<ProductDto>.Fail("Product not found");

        product.Update(
            supplierId: request.SupplierId,
            supplierCode: request.SupplierCode,
            internalCode: request.InternalCode,
            name: request.Name,
            defaultMarkupRate: request.DefaultMarkupRate,
            vatRate: request.VatRate,
            imageUrl: request.ImageUrl
        );

        // Update variants
        var oldVariants = product.Variants.ToList();
        foreach (var oldVariant in oldVariants)
        {
            product.Variants.Remove(oldVariant);
        }

        foreach (var variantRequest in request.Variants)
        {
            var variant = new ProductVariant(
                productId: product.Id,
                size: variantRequest.Size,
                color: variantRequest.Color,
                barcode: variantRequest.Barcode,
                purchaseCost: variantRequest.PurchaseCost,
                stockQuantity: variantRequest.StockQuantity
            );
            product.Variants.Add(variant);
        }

        await _unitOfWork.Products.UpdateAsync(product);
        await _unitOfWork.SaveChangesAsync();

        var updated = await _unitOfWork.Products.GetByIdAsync(id);
        return OperationResult<ProductDto>.Ok(ProductMapper.MapToDto(updated!));
    }

    public async Task<OperationResult<bool>> DeleteProductAsync(Guid id)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(id);
        if (product == null)
            return OperationResult<bool>.Fail("Product not found");

        await _unitOfWork.Products.DeleteAsync(product);
        await _unitOfWork.SaveChangesAsync();

        return OperationResult<bool>.Ok(true);
    }

    public async Task<OperationResult<BarcodeVariantDto>> GetVariantByBarcodeAsync(string barcode)
    {
        var variant = await _unitOfWork.ProductVariants.GetByBarcodeAsync(barcode);
        if (variant?.Product == null)
            return OperationResult<BarcodeVariantDto>.Fail("Variant not found");

        var dto = ProductMapper.MapBarcodeVariantToDto(variant, variant.Product);
        return OperationResult<BarcodeVariantDto>.Ok(dto);
    }
}

/// <summary>
/// Contact application service.
/// </summary>
public interface IContactService
{
    Task<OperationResult<ContactDto>> CreateContactAsync(CreateContactRequest request);
    Task<OperationResult<ContactDto>> GetContactAsync(Guid id);
    Task<OperationResult<PagedResult<ContactDto>>> GetContactsAsync(int page, int pageSize, string? search = null);
    Task<OperationResult<ContactDto>> UpdateContactAsync(Guid id, UpdateContactRequest request);
    Task<OperationResult<bool>> DeleteContactAsync(Guid id);
}

/// <summary>
/// Contact service implementation.
/// </summary>
public class ContactService : IContactService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateContactRequest> _createValidator;
    private readonly IValidator<UpdateContactRequest> _updateValidator;

    public ContactService(
        IUnitOfWork unitOfWork,
        IValidator<CreateContactRequest> createValidator,
        IValidator<UpdateContactRequest> updateValidator)
    {
        _unitOfWork = unitOfWork;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<OperationResult<ContactDto>> CreateContactAsync(CreateContactRequest request)
    {
        var validationResult = _createValidator.Validate(request);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.Message}"));
            return OperationResult<ContactDto>.Fail(errors);
        }

        var contactType = (ContactType)request.Type;
        var contact = new Contact(
            type: contactType,
            businessName: request.BusinessName,
            vatNumber: request.VatNumber,
            sdiCode: request.SdiCode,
            defaultDiscountRate: request.DefaultDiscountRate,
            defaultCommissionRate: request.DefaultCommissionRate,
            agentId: request.AgentId
        );

        await _unitOfWork.Contacts.AddAsync(contact);
        await _unitOfWork.SaveChangesAsync();

        return OperationResult<ContactDto>.Ok(ContactMapper.MapToDto(contact));
    }

    public async Task<OperationResult<ContactDto>> GetContactAsync(Guid id)
    {
        var contact = await _unitOfWork.Contacts.GetByIdAsync(id);
        if (contact == null)
            return OperationResult<ContactDto>.Fail("Contact not found");

        return OperationResult<ContactDto>.Ok(ContactMapper.MapToDto(contact));
    }

    public async Task<OperationResult<PagedResult<ContactDto>>> GetContactsAsync(int page, int pageSize, string? search = null)
    {
        var contacts = await _unitOfWork.Contacts.GetPaginatedAsync(page, pageSize, search);
        var total = await _unitOfWork.Contacts.GetTotalCountAsync(search);

        var dtos = ContactMapper.MapToListDto(contacts);
        var result = new PagedResult<ContactDto>(dtos, page, pageSize, total);

        return OperationResult<PagedResult<ContactDto>>.Ok(result);
    }

    public async Task<OperationResult<ContactDto>> UpdateContactAsync(Guid id, UpdateContactRequest request)
    {
        var validationResult = _updateValidator.Validate(request);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.Message}"));
            return OperationResult<ContactDto>.Fail(errors);
        }

        var contact = await _unitOfWork.Contacts.GetByIdAsync(id);
        if (contact == null)
            return OperationResult<ContactDto>.Fail("Contact not found");

        // Update only provided fields
        if (!string.IsNullOrWhiteSpace(request.BusinessName))
            contact.BusinessName = request.BusinessName;

        if (!string.IsNullOrWhiteSpace(request.VatNumber))
            contact.VatNumber = request.VatNumber;

        if (!string.IsNullOrWhiteSpace(request.SdiCode))
            contact.SdiCode = request.SdiCode;

        if (request.DefaultDiscountRate.HasValue)
            contact.DefaultDiscountRate = request.DefaultDiscountRate.Value;

        if (request.DefaultCommissionRate.HasValue)
            contact.DefaultCommissionRate = request.DefaultCommissionRate.Value;

        if (request.AgentId.HasValue)
            contact.AgentId = request.AgentId.Value;

        await _unitOfWork.Contacts.UpdateAsync(contact);
        await _unitOfWork.SaveChangesAsync();

        return OperationResult<ContactDto>.Ok(ContactMapper.MapToDto(contact));
    }

    public async Task<OperationResult<bool>> DeleteContactAsync(Guid id)
    {
        var contact = await _unitOfWork.Contacts.GetByIdAsync(id);
        if (contact == null)
            return OperationResult<bool>.Fail("Contact not found");

        await _unitOfWork.Contacts.DeleteAsync(contact);
        await _unitOfWork.SaveChangesAsync();

        return OperationResult<bool>.Ok(true);
    }
}
