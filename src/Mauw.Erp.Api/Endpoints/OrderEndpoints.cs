using Mauw.Erp.Application.Validators;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Enums;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Services;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Api.Endpoints;

/// <summary>
/// Order management endpoints for creating and managing sales orders.
/// </summary>
public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/orders")
            .WithName("Orders")
            .WithOpenApi();

        group.MapPost("/", CreateOrder)
            .WithName("CreateOrder")
            .WithSummary("Create a new sales order");

        group.MapGet("/{id}", GetOrder)
            .WithName("GetOrder")
            .WithSummary("Get order details by ID");

        group.MapGet("/", ListOrders)
            .WithName("ListOrders")
            .WithSummary("List orders with pagination");

        group.MapPost("/{id}/items", AddOrderItem)
            .WithName("AddOrderItem")
            .WithSummary("Add an item to an order");

        group.MapPost("/{id}/confirm", ConfirmOrder)
            .WithName("ConfirmOrder")
            .WithSummary("Confirm an order");
    }

    private static async Task<IResult> CreateOrder(
        CreateOrderRequest request,
        MauwDbContext dbContext,
        CreateOrderRequestValidator validator,
        PricingEngine pricingEngine)
    {
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
            return Results.BadRequest(validationResult.Errors);

        // Verify contact exists
        var contact = await dbContext.Contacts.FirstOrDefaultAsync(c => c.Id == request.ContactId);
        if (contact == null)
            return Results.NotFound("Customer/Contact not found");

        // Generate order number (simple format: ORD-YYYYMMDD-XXXX)
        var orderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";

        // Use contact's default discount rate if not specified
        var discountRate = request.DiscountRate ?? contact.DefaultDiscountRate;

        // Create order
        var order = new Order(
            contactId: request.ContactId,
            orderNumber: orderNumber,
            customerDiscountRate: discountRate);

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        return Results.Created($"/api/v1/orders/{order.Id}", new OrderDto(
            order.Id,
            order.ContactId,
            order.OrderNumber,
            order.Status.ToString(),
            order.OrderDate,
            order.CustomerDiscountRate,
            order.TaxableTotal,
            order.VatTotal,
            order.GrossTotal,
            []
        ));
    }

    private static async Task<IResult> GetOrder(
        Guid id,
        MauwDbContext dbContext)
    {
        var order = await dbContext.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Variant)
            .ThenInclude(v => v.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            return Results.NotFound();

        var items = order.Items.Select(i => new OrderItemDto(
            i.Id,
            i.VariantId,
            i.Quantity,
            i.UnitNetPrice,
            i.LineNetTotal,
            i.VatRate,
            i.LineVatTotal,
            i.LineGrossTotal
        )).ToList();

        return Results.Ok(new OrderDto(
            order.Id,
            order.ContactId,
            order.OrderNumber,
            order.Status.ToString(),
            order.OrderDate,
            order.CustomerDiscountRate,
            order.TaxableTotal,
            order.VatTotal,
            order.GrossTotal,
            items
        ));
    }

    private static async Task<IResult> ListOrders(
        int page = 1,
        int pageSize = 10,
        MauwDbContext dbContext = null!)
    {
        const int maxPageSize = 100;
        if (pageSize > maxPageSize)
            pageSize = maxPageSize;

        var totalCount = await dbContext.Orders.CountAsync();
        var orders = await dbContext.Orders
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(o => o.Items)
            .ToListAsync();

        var items = orders.Select(o => new OrderDto(
            o.Id,
            o.ContactId,
            o.OrderNumber,
            o.Status.ToString(),
            o.OrderDate,
            o.CustomerDiscountRate,
            o.TaxableTotal,
            o.VatTotal,
            o.GrossTotal,
            []
        )).ToList();

        return Results.Ok(new
        {
            items,
            pageInfo = new
            {
                page,
                pageSize,
                totalCount,
                totalPages = (int)Math.Ceiling(totalCount / (decimal)pageSize)
            }
        });
    }

    private static async Task<IResult> AddOrderItem(
        Guid id,
        CreateOrderItemRequest request,
        MauwDbContext dbContext,
        PricingEngine pricingEngine)
    {
        var order = await dbContext.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order == null)
            return Results.NotFound("Order not found");

        if (order.Status != OrderStatus.Draft)
            return Results.BadRequest("Cannot add items to a confirmed order");

        // Get variant with product information
        var variant = await dbContext.ProductVariants
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == request.VariantId);

        if (variant == null)
            return Results.NotFound("Product variant not found");

        // Calculate pricing using the order's customer discount rate
        var pricingResult = pricingEngine.CalculatePrice(
            purchaseCost: variant.PurchaseCost,
            markupRate: variant.Product.DefaultMarkupRate,
            discountRate: order.CustomerDiscountRate,
            vatRate: variant.Product.VatRate);

        // Create order item with calculated pricing
        var item = new OrderItem(
            orderId: order.Id,
            variantId: request.VariantId,
            quantity: request.Quantity,
            unitNetPrice: pricingResult.FinalNetPrice,
            vatRate: variant.Product.VatRate);

        // Add item through order which will update totals
        order.AddItem(item);

        dbContext.OrderItems.Add(item);
        await dbContext.SaveChangesAsync();

        return Results.Created($"/api/v1/orders/{id}/items/{item.Id}", new OrderItemDto(
            item.Id,
            item.VariantId,
            item.Quantity,
            item.UnitNetPrice,
            item.LineNetTotal,
            item.VatRate,
            item.LineVatTotal,
            item.LineGrossTotal
        ));
    }

    private static async Task<IResult> ConfirmOrder(
        Guid id,
        MauwDbContext dbContext)
    {
        var order = await dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            return Results.NotFound();

        if (order.Status != OrderStatus.Draft)
            return Results.BadRequest("Only draft orders can be confirmed");

        try
        {
            order.Confirm();
            await dbContext.SaveChangesAsync();

            return Results.Ok(new OrderDto(
                order.Id,
                order.ContactId,
                order.OrderNumber,
                order.Status.ToString(),
                order.OrderDate,
                order.CustomerDiscountRate,
                order.TaxableTotal,
                order.VatTotal,
                order.GrossTotal,
                []
            ));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }
}
