using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.Application.Services;

/// <summary>
/// Order application service - manages order operations.
/// </summary>
public interface IOrderService
{
    Task<OperationResult<OrderDto>> CreateOrderAsync(CreateOrderRequest request);
    Task<OperationResult<OrderDto>> GetOrderAsync(Guid id);
    Task<OperationResult<PagedResult<OrderDto>>> GetOrdersByContactAsync(Guid contactId, int page, int pageSize);
    Task<OperationResult<OrderDto>> AddItemAsync(Guid orderId, CreateOrderItemRequest request);
    Task<OperationResult<OrderDto>> ConfirmOrderAsync(Guid orderId);
    Task<OperationResult<bool>> DeleteOrderAsync(Guid id);
}

/// <summary>
/// Order service implementation.
/// </summary>
public class OrderService : IOrderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateOrderRequest> _createValidator;
    private readonly IValidator<CreateOrderItemRequest> _itemValidator;
    private readonly PricingEngine _pricingEngine;

    public OrderService(
        IUnitOfWork unitOfWork,
        IValidator<CreateOrderRequest> createValidator,
        IValidator<CreateOrderItemRequest> itemValidator,
        PricingEngine pricingEngine)
    {
        _unitOfWork = unitOfWork;
        _createValidator = createValidator;
        _itemValidator = itemValidator;
        _pricingEngine = pricingEngine;
    }

    public async Task<OperationResult<OrderDto>> CreateOrderAsync(CreateOrderRequest request)
    {
        var validationResult = _createValidator.Validate(request);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.Message}"));
            return OperationResult<OrderDto>.Fail(errors);
        }

        // Verify contact exists
        var contact = await _unitOfWork.Contacts.GetByIdAsync(request.ContactId);
        if (contact == null)
            return OperationResult<OrderDto>.Fail("Contact not found");

        var order = new Order(
            contactId: request.ContactId,
            orderNumber: $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}",
            customerDiscountRate: request.DiscountRate ?? 0
        );

        await _unitOfWork.Orders.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();

        return OperationResult<OrderDto>.Ok(OrderMapper.MapToDto(order));
    }

    public async Task<OperationResult<OrderDto>> GetOrderAsync(Guid id)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(id);
        if (order == null)
            return OperationResult<OrderDto>.Fail("Order not found");

        return OperationResult<OrderDto>.Ok(OrderMapper.MapToDto(order));
    }

    public async Task<OperationResult<PagedResult<OrderDto>>> GetOrdersByContactAsync(Guid contactId, int page, int pageSize)
    {
        var orders = await _unitOfWork.Orders.GetByContactIdAsync(contactId);
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
        var itemValidationResult = _itemValidator.Validate(request);
        if (!itemValidationResult.IsValid)
        {
            var errors = string.Join("; ", itemValidationResult.Errors.Select(e => $"{e.PropertyName}: {e.Message}"));
            return OperationResult<OrderDto>.Fail(errors);
        }

        var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
        if (order == null)
            return OperationResult<OrderDto>.Fail("Order not found");

        var variant = await _unitOfWork.ProductVariants.GetByIdAsync(request.VariantId);
        if (variant == null)
            return OperationResult<OrderDto>.Fail("Variant not found");

        // Calculate pricing
        var product = await _unitOfWork.Products.GetByIdAsync(variant.ProductId);
        if (product == null)
            return OperationResult<OrderDto>.Fail("Product not found");

        var pricingResult = _pricingEngine.CalculatePrice(
            variant.PurchaseCost,
            product.DefaultMarkupRate,
            order.CustomerDiscountRate,
            product.VatRate
        );

        var item = new OrderItem(
            orderId: orderId,
            variantId: request.VariantId,
            quantity: request.Quantity,
            unitNetPrice: pricingResult.FinalNetPrice,
            vatRate: product.VatRate
        );

        order.AddItem(item);
        await _unitOfWork.Orders.UpdateAsync(order);
        await _unitOfWork.SaveChangesAsync();

        var updated = await _unitOfWork.Orders.GetByIdAsync(orderId);
        return OperationResult<OrderDto>.Ok(OrderMapper.MapToDto(updated!));
    }

    public async Task<OperationResult<OrderDto>> ConfirmOrderAsync(Guid orderId)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
        if (order == null)
            return OperationResult<OrderDto>.Fail("Order not found");

        try
        {
            order.Confirm();
            await _unitOfWork.Orders.UpdateAsync(order);
            await _unitOfWork.SaveChangesAsync();
        }
        catch (InvalidOperationException ex)
        {
            return OperationResult<OrderDto>.Fail(ex.Message);
        }

        var updated = await _unitOfWork.Orders.GetByIdAsync(orderId);
        return OperationResult<OrderDto>.Ok(OrderMapper.MapToDto(updated!));
    }

    public async Task<OperationResult<bool>> DeleteOrderAsync(Guid id)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(id);
        if (order == null)
            return OperationResult<bool>.Fail("Order not found");

        await _unitOfWork.Orders.DeleteAsync(order);
        await _unitOfWork.SaveChangesAsync();

        return OperationResult<bool>.Ok(true);
    }
}

/// <summary>
/// Invoice application service - manages invoice operations.
/// </summary>
public interface IInvoiceService
{
    Task<OperationResult<InvoiceDto>> CreateInvoiceAsync(CreateInvoiceRequest request);
    Task<OperationResult<InvoiceDto>> GetInvoiceAsync(Guid id);
    Task<OperationResult<PagedResult<InvoiceDto>>> GetInvoicesByContactAsync(Guid contactId, int page, int pageSize);
    Task<OperationResult<InvoiceDto>> AddItemAsync(Guid invoiceId, CreateInvoiceItemRequest request);
    Task<OperationResult<InvoiceDto>> RecordPaymentAsync(Guid invoiceId, RecordPaymentRequest request);
    Task<OperationResult<bool>> DeleteInvoiceAsync(Guid id);
}

/// <summary>
/// Invoice service implementation.
/// </summary>
public class InvoiceService : IInvoiceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateInvoiceRequest> _createValidator;
    private readonly PricingEngine _pricingEngine;

    public InvoiceService(
        IUnitOfWork unitOfWork,
        IValidator<CreateInvoiceRequest> createValidator,
        PricingEngine pricingEngine)
    {
        _unitOfWork = unitOfWork;
        _createValidator = createValidator;
        _pricingEngine = pricingEngine;
    }

    public async Task<OperationResult<InvoiceDto>> CreateInvoiceAsync(CreateInvoiceRequest request)
    {
        var validationResult = _createValidator.Validate(request);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.Message}"));
            return OperationResult<InvoiceDto>.Fail(errors);
        }

        var contact = await _unitOfWork.Contacts.GetByIdAsync(request.ContactId);
        if (contact == null)
            return OperationResult<InvoiceDto>.Fail("Contact not found");

        var invoice = new Invoice(
            contactId: request.ContactId,
            invoiceNumber: $"INV-{DateTime.UtcNow:yyyyMMddHHmmss}"
        );

        await _unitOfWork.Invoices.AddAsync(invoice);
        await _unitOfWork.SaveChangesAsync();

        return OperationResult<InvoiceDto>.Ok(InvoiceMapper.MapToDto(invoice));
    }

    public async Task<OperationResult<InvoiceDto>> GetInvoiceAsync(Guid id)
    {
        var invoice = await _unitOfWork.Invoices.GetByIdAsync(id);
        if (invoice == null)
            return OperationResult<InvoiceDto>.Fail("Invoice not found");

        return OperationResult<InvoiceDto>.Ok(InvoiceMapper.MapToDto(invoice));
    }

    public async Task<OperationResult<PagedResult<InvoiceDto>>> GetInvoicesByContactAsync(Guid contactId, int page, int pageSize)
    {
        var invoices = await _unitOfWork.Invoices.GetByContactIdAsync(contactId);
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
        var invoice = await _unitOfWork.Invoices.GetByIdAsync(invoiceId);
        if (invoice == null)
            return OperationResult<InvoiceDto>.Fail("Invoice not found");

        var variant = await _unitOfWork.ProductVariants.GetByIdAsync(request.VariantId);
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
        await _unitOfWork.Invoices.UpdateAsync(invoice);
        await _unitOfWork.SaveChangesAsync();

        var updated = await _unitOfWork.Invoices.GetByIdAsync(invoiceId);
        return OperationResult<InvoiceDto>.Ok(InvoiceMapper.MapToDto(updated!));
    }

    public async Task<OperationResult<InvoiceDto>> RecordPaymentAsync(Guid invoiceId, RecordPaymentRequest request)
    {
        var invoice = await _unitOfWork.Invoices.GetByIdAsync(invoiceId);
        if (invoice == null)
            return OperationResult<InvoiceDto>.Fail("Invoice not found");

        var payment = new Payment(
            invoiceId: invoiceId,
            amount: request.Amount,
            paymentDate: request.PaymentDate
        );

        invoice.RecordPayment(request.Amount);
        await _unitOfWork.Invoices.UpdateAsync(invoice);
        await _unitOfWork.SaveChangesAsync();

        var updated = await _unitOfWork.Invoices.GetByIdAsync(invoiceId);
        return OperationResult<InvoiceDto>.Ok(InvoiceMapper.MapToDto(updated!));
    }

    public async Task<OperationResult<bool>> DeleteInvoiceAsync(Guid id)
    {
        var invoice = await _unitOfWork.Invoices.GetByIdAsync(id);
        if (invoice == null)
            return OperationResult<bool>.Fail("Invoice not found");

        await _unitOfWork.Invoices.DeleteAsync(invoice);
        await _unitOfWork.SaveChangesAsync();

        return OperationResult<bool>.Ok(true);
    }
}

/// <summary>
/// Inventory application service - manages stock movements and barcode operations.
/// </summary>
public interface IInventoryService
{
    Task<OperationResult<StockMovementDto>> ScanBarcodeAsync(BarcodeScanRequest request);
    Task<OperationResult<PagedResult<StockMovementDto>>> GetMovementsAsync(Guid variantId, int page, int pageSize);
    Task<OperationResult<BarcodeVariantDto>> GetVariantByBarcodeAsync(string barcode);
}

/// <summary>
/// Inventory service implementation.
/// </summary>
public class InventoryService : IInventoryService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<BarcodeScanRequest> _barcodeValidator;
    private readonly PricingEngine _pricingEngine;

    public InventoryService(
        IUnitOfWork unitOfWork,
        IValidator<BarcodeScanRequest> barcodeValidator,
        PricingEngine pricingEngine)
    {
        _unitOfWork = unitOfWork;
        _barcodeValidator = barcodeValidator;
        _pricingEngine = pricingEngine;
    }

    public async Task<OperationResult<StockMovementDto>> ScanBarcodeAsync(BarcodeScanRequest request)
    {
        var validationResult = _barcodeValidator.Validate(request);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.Message}"));
            return OperationResult<StockMovementDto>.Fail(errors);
        }

        var variant = await _unitOfWork.ProductVariants.GetByBarcodeAsync(request.Barcode);
        if (variant == null)
            return OperationResult<StockMovementDto>.Fail("Barcode not found");

        // Create stock movement
        var movementType = (StockMovementType)request.Type;
        var movement = new StockMovement(
            variantId: variant.Id,
            type: movementType,
            quantity: request.Quantity,
            unitCost: variant.PurchaseCost,
            referenceDoc: request.ReferenceDoc ?? ""
        );

        // Update variant stock
        variant.UpdateStockQuantity(request.Quantity);

        await _unitOfWork.StockMovements.AddAsync(movement);
        await _unitOfWork.ProductVariants.UpdateAsync(variant);
        await _unitOfWork.SaveChangesAsync();

        var dto = new StockMovementDto(
            Id: movement.Id,
            VariantId: movement.VariantId,
            Type: movement.Type.ToString(),
            Quantity: movement.Quantity,
            UnitCost: movement.UnitCost,
            ReferenceDoc: movement.ReferenceDoc,
            CreatedAt: movement.CreatedAt
        );

        return OperationResult<StockMovementDto>.Ok(dto);
    }

    public async Task<OperationResult<PagedResult<StockMovementDto>>> GetMovementsAsync(Guid variantId, int page, int pageSize)
    {
        var movements = await _unitOfWork.StockMovements.GetByVariantIdAsync(variantId);
        var paginated = movements
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var total = movements.Count();
        var dtos = paginated.Select(m => new StockMovementDto(
            Id: m.Id,
            VariantId: m.VariantId,
            Type: m.Type.ToString(),
            Quantity: m.Quantity,
            UnitCost: m.UnitCost,
            ReferenceDoc: m.ReferenceDoc,
            CreatedAt: m.CreatedAt
        )).ToList();

        var result = new PagedResult<StockMovementDto>(dtos, page, pageSize, total);
        return OperationResult<PagedResult<StockMovementDto>>.Ok(result);
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
/// DDT (Transport Document) application service - manages DDT operations.
/// </summary>
public interface IDdtService
{
    Task<OperationResult<DdtDto>> CreateDdtAsync(CreateDdtRequest request);
    Task<OperationResult<DdtDto>> GetDdtAsync(Guid id);
    Task<OperationResult<PagedResult<DdtDto>>> GetDdtsByContactAsync(Guid contactId, int page, int pageSize);
    Task<OperationResult<DdtDto>> AddItemAsync(Guid ddtId, CreateOrderItemRequest request);
    Task<OperationResult<bool>> DeleteDdtAsync(Guid id);
}

/// <summary>
/// DDT service implementation.
/// </summary>
public class DdtService : IDdtService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateOrderItemRequest> _itemValidator;
    private readonly PricingEngine _pricingEngine;

    public DdtService(
        IUnitOfWork unitOfWork,
        IValidator<CreateOrderItemRequest> itemValidator,
        PricingEngine pricingEngine)
    {
        _unitOfWork = unitOfWork;
        _itemValidator = itemValidator;
        _pricingEngine = pricingEngine;
    }

    public async Task<OperationResult<DdtDto>> CreateDdtAsync(CreateDdtRequest request)
    {
        // Verify contact exists
        var contact = await _unitOfWork.Contacts.GetByIdAsync(request.ContactId);
        if (contact == null)
            return OperationResult<DdtDto>.Fail("Contact not found");

        // Create DDT
        var ddt = new DdtDocument(request.ContactId, request.DdtNumber);
        
        await _unitOfWork.DdtDocuments.AddAsync(ddt);
        await _unitOfWork.SaveChangesAsync();

        var dto = DdtMapper.MapToDto(ddt);
        return OperationResult<DdtDto>.Ok(dto);
    }

    public async Task<OperationResult<DdtDto>> GetDdtAsync(Guid id)
    {
        var ddt = await _unitOfWork.DdtDocuments.GetByIdAsync(id);
        if (ddt == null)
            return OperationResult<DdtDto>.Fail("DDT not found");

        var dto = DdtMapper.MapToDto(ddt);
        return OperationResult<DdtDto>.Ok(dto);
    }

    public async Task<OperationResult<PagedResult<DdtDto>>> GetDdtsByContactAsync(Guid contactId, int page, int pageSize)
    {
        var ddts = await _unitOfWork.DdtDocuments.GetByContactIdAsync(contactId);
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
        var validationResult = _itemValidator.Validate(request);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.Message}"));
            return OperationResult<DdtDto>.Fail(errors);
        }

        var ddt = await _unitOfWork.DdtDocuments.GetByIdAsync(ddtId);
        if (ddt == null)
            return OperationResult<DdtDto>.Fail("DDT not found");

        // Get variant with product information
        var variant = await _unitOfWork.ProductVariants.GetByIdAsync(request.VariantId);
        if (variant?.Product == null)
            return OperationResult<DdtDto>.Fail("Variant not found");

        // Calculate pricing (use 0% discount for DDT)
        var pricingResult = _pricingEngine.CalculatePrice(
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
        
        await _unitOfWork.DdtDocuments.UpdateAsync(ddt);
        await _unitOfWork.SaveChangesAsync();

        var dto = DdtMapper.MapToDto(ddt);
        return OperationResult<DdtDto>.Ok(dto);
    }

    public async Task<OperationResult<bool>> DeleteDdtAsync(Guid id)
    {
        var ddt = await _unitOfWork.DdtDocuments.GetByIdAsync(id);
        if (ddt == null)
            return OperationResult<bool>.Fail("DDT not found");

        await _unitOfWork.DdtDocuments.DeleteAsync(ddt);
        await _unitOfWork.SaveChangesAsync();

        return OperationResult<bool>.Ok(true);
    }
}
