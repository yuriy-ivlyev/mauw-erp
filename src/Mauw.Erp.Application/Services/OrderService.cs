using Mauw.Erp.Application.Diagnostics;
using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
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
    ILogger<OrderService> logger)
    : IOrderService
{
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

        var order = new Order(
            contactId: request.ContactId,
            orderNumber: $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}",
            customerDiscountRate: request.DiscountRate ?? 0
        );

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

        var item = new OrderItem(
            orderId: orderId,
            variantId: request.VariantId,
            quantity: request.Quantity,
            unitNetPrice: pricingResult.FinalNetPrice,
            vatRate: product.VatRate
        );

        order.AddItem(item);
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

    // --- [LoggerMessage] source-generated log methods (zero-allocation hot path) ---

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Order {OrderNumber} created for contact {ContactId}")]
    private static partial void LogOrderCreated(ILogger logger, string orderNumber, Guid contactId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Order creation validation failed for contact {ContactId}: {Errors}")]
    private static partial void LogOrderValidationFailed(ILogger logger, Guid contactId, string errors);
}