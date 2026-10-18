using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Mauw.Erp.Api.Endpoints;

/// <summary>
/// Order management endpoints for creating and managing sales orders.
/// Thin layer delegating to IOrderService.
/// </summary>
public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/orders")
            .WithName("Orders");

        group.MapPost("/", CreateOrder)
            .WithName("CreateOrder")
            .WithSummary("Create a new sales order");

        group.MapGet("/{id}", GetOrder)
            .WithName("GetOrder")
            .WithSummary("Get order details by ID");

        group.MapGet("/", ListOrders)
            .WithName("ListOrders")
            .WithSummary("List orders with pagination");

        group.MapGet("/{id}/pdf", GetOrderPdf)
            .WithName("GetOrderPdf")
            .WithSummary("Generate and download order PDF");

        group.MapPost("/{id}/items", AddOrderItem)
            .WithName("AddOrderItem")
            .WithSummary("Add an item to an order");

        group.MapPost("/{id}/confirm", ConfirmOrder)
            .WithName("ConfirmOrder")
            .WithSummary("Confirm an order");
    }

    private static async Task<Results<Created<OrderDto>, BadRequest<string>>> CreateOrder(
        CreateOrderRequest request,
        IOrderService orderService)
    {
        var result = await orderService.CreateOrderAsync(request);

        if (!result.Success)
            return TypedResults.BadRequest(result.Error);

        return TypedResults.Created($"/api/v1/orders/{result.Data!.Id}", result.Data);
    }

    private static async Task<Results<Ok<OrderDto>, NotFound>> GetOrder(
        Guid id,
        IOrderService orderService)
    {
        var result = await orderService.GetOrderAsync(id);

        if (!result.Success)
            return TypedResults.NotFound();

        return TypedResults.Ok(result.Data!);
    }

    private static async Task<Ok<PagedResult<OrderDto>>> ListOrders(
        IOrderService orderService,
        string? search = null,
        string? status = null,
        string? orderType = null,
        int page = 1,
        int pageSize = 20)
    {
        var result = await orderService.GetAllOrdersAsync(search, status, orderType, page, pageSize);
        return TypedResults.Ok(result.Data!);
    }

    private static async Task<Results<FileContentHttpResult, NotFound, BadRequest<string>>> GetOrderPdf(
        Guid id,
        IOrderService orderService)
    {
        var result = await orderService.GetOrderPdfAsync(id);
        if (!result.Success)
            return result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true
                ? TypedResults.NotFound()
                : TypedResults.BadRequest(result.Error!);

        return TypedResults.File(result.Data!, "application/pdf", $"ordine-{id}.pdf");
    }

    private static async Task<Results<Created<OrderItemDto>, BadRequest<string>, NotFound>> AddOrderItem(
        Guid id,
        CreateOrderItemRequest request,
        IOrderService orderService)
    {
        var result = await orderService.AddItemAsync(id, request);

        if (!result.Success)
        {
            if (result.Error != null && result.Error.Contains("not found"))
                return TypedResults.NotFound();
            return TypedResults.BadRequest(result.Error);
        }

        var orderDto = result.Data!;
        var itemDto = orderDto.Items.LastOrDefault();
        if (itemDto != null)
            return TypedResults.Created($"/api/v1/orders/{id}/items/{itemDto.Id}", itemDto);

        return TypedResults.BadRequest("Item not created");
    }

    private static async Task<Results<Ok<OrderDto>, BadRequest<string>, NotFound>> ConfirmOrder(
        Guid id,
        IOrderService orderService)
    {
        var result = await orderService.ConfirmOrderAsync(id);

        if (result.Success) return TypedResults.Ok(result.Data!);
        if (result.Error != null && result.Error.Contains("not found"))
            return TypedResults.NotFound();
        return TypedResults.BadRequest(result.Error);
    }
}
