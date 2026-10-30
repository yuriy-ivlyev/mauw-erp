using Mauw.Erp.Api.Authorization;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Application.Commands.Orders;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Security.Claims;

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
            .RequireAuthorization(AppPolicies.SalesOperations)
            .WithName("CreateOrder")
            .WithSummary("Create a new sales order");

        group.MapGet("/{id}", GetOrder)
            .RequireAuthorization(AppPolicies.AnyRole)
            .WithName("GetOrder")
            .WithSummary("Get order details by ID");

        group.MapGet("/", ListOrders)
            .RequireAuthorization(AppPolicies.SalesOperations)
            .WithName("ListOrders")
            .WithSummary("List orders with pagination");

        group.MapGet("/{id}/pdf", GetOrderPdf)
            .RequireAuthorization(AppPolicies.SalesOperations)
            .WithName("GetOrderPdf")
            .WithSummary("Generate and download order PDF");

        group.MapPost("/{id}/items", AddOrderItem)
            .RequireAuthorization(AppPolicies.SalesOperations)
            .WithName("AddOrderItem")
            .WithSummary("Add an item to an order");

        group.MapDelete("/{id}/items/{itemId}", RemoveDraftOrderItem)
            .RequireAuthorization(AppPolicies.SalesOperations)
            .WithName("RemoveDraftOrderItem")
            .WithSummary("Remove an item from a draft order");

        group.MapPost("/{id}/invoice-items", AddBillingOrderItem)
            .RequireAuthorization(AppPolicies.Billing)
            .WithName("AddCompletedOrderInvoiceItem")
            .WithSummary("Add a billing-only item to a completed order and synchronize its invoice");

        group.MapDelete("/{id}/invoice-items/{itemId}", RemoveCompletedOrderInvoiceItem)
            .RequireAuthorization(AppPolicies.Billing)
            .WithName("RemoveCompletedOrderInvoiceItem")
            .WithSummary("Exclude a completed order item and synchronize its invoice");

        group.MapPut("/{id}/items/{itemId}", UpdateOrderItem)
            .RequireAuthorization(AppPolicies.SalesOperations)
            .WithName("UpdateOrderItem")
            .WithSummary("Update an item on a draft order");

        group.MapPut("/{id}/shipping-cost", UpdateOrderShippingCost)
            .RequireAuthorization(AppPolicies.SalesOperations)
            .WithName("UpdateOrderShippingCost")
            .WithSummary("Update a draft order shipping charge and its VAT classification");

        group.MapPut("/{id}/invoice-shipping-cost", UpdateCompletedInvoiceShippingCost)
            .RequireAuthorization(AppPolicies.Billing)
            .WithName("UpdateCompletedOrderInvoiceShippingCost")
            .WithSummary("Update a completed order shipping charge and synchronize its invoice");

        group.MapPost("/{id}/confirm", ConfirmOrder)
            .RequireAuthorization(AppPolicies.SalesOperations)
            .WithName("ConfirmOrder")
            .WithSummary("Confirm an order");

        app.MapGroup("/api/v1/orders")
            .RequireAuthorization(AppPolicies.SalesOperations)
            .MapPost("/{id}/approve", ApproveOrder)
            .WithName("ApproveOrder")
            .WithSummary("Approve a pending order and reserve stock synchronously");

        app.MapGroup("/api/v1/orders")
            .RequireAuthorization(AppPolicies.SalesOperations)
            .MapPost("/{id}/finalize-cash-sale", FinalizeCashSale)
            .WithName("FinalizeCashSale")
            .WithSummary("Finalize a cash-desk sale and post stock synchronously");

        app.MapGroup("/api/v1/orders")
            .RequireAuthorization(AppPolicies.WarehouseOperations)
            .MapPost("/{id}/ship", ShipOrder)
            .WithName("ShipOrder")
            .WithSummary("Emit the shipping DDT and post stock synchronously");

        app.MapGroup("/api/v1/orders")
            .RequireAuthorization(AppPolicies.WarehouseOperations)
            .MapGet("/fulfillment-queue", ListFulfillmentQueue)
            .WithName("ListFulfillmentQueue")
            .WithSummary("List orders awaiting warehouse fulfillment");

        app.MapGroup("/api/v1/orders")
            .RequireAuthorization(AppPolicies.DdtDocuments)
            .MapPost("/{id}/cancel", CancelOrder)
            .WithName("CancelOrder")
            .WithSummary("Cancel an order and release its stock reservation");

        app.MapGroup("/api/v1/orders")
            .RequireAuthorization(AppPolicies.WarehouseOperations)
            .MapPost("/{id}/start-picking", StartPicking)
            .WithName("StartOrderPicking")
            .WithSummary("Assign an order to the authenticated warehouse operator");

        group.MapPost("/{id}/hold", PutOrderOnHold)
            .RequireAuthorization(AppPolicies.SalesOperations)
            .WithName("PutOrderOnHold")
            .WithSummary("Put an order on hold");

        group.MapPost("/{id}/resume", ResumeOrder)
            .RequireAuthorization(AppPolicies.SalesOperations)
            .WithName("ResumeOrder")
            .WithSummary("Resume an order from hold");

        group.MapPost("/{id}/complete", CompleteOrder)
            .RequireAuthorization(AppPolicies.SalesOperations)
            .WithName("CompleteOrder")
            .WithSummary("Complete a shipped order");

        group.MapPost("/{id}/cancel-remaining", CancelRemainingLines)
            .RequireAuthorization(AppPolicies.SalesOperations)
            .WithName("CancelRemainingOrderLines")
            .WithSummary("Close the unshipped remainder of a partially shipped order");
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
        int pageSize = 20,
        string? sortBy = null,
        bool sortDescending = false)
    {
        var request = new OrderFilterParams(search, status, orderType, page, pageSize, sortBy, sortDescending);
        var result = await orderService.GetAllOrdersAsync(
            request.Search, request.Status, request.OrderType, request.Page, request.PageSize, request.SortBy, request.SortDescending);
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

    private static async Task<Results<Created<OrderItemDto>, BadRequest<string>, NotFound>> AddBillingOrderItem(
        Guid id,
        AddCompletedOrderItemRequest request,
        IOrderService orderService)
    {
        var result = await orderService.AddCompletedBillingItemAsync(id, request);
        if (!result.Success)
        {
            if (result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
                return TypedResults.NotFound();
            return TypedResults.BadRequest(result.Error);
        }

        var item = result.Data!.Items.LastOrDefault();
        return item is null
            ? TypedResults.BadRequest("Billing item was not created.")
            : TypedResults.Created($"/api/v1/orders/{id}/invoice-items/{item.Id}", item);
    }

    private static async Task<Results<Ok<OrderDto>, BadRequest<string>, NotFound>> RemoveCompletedOrderInvoiceItem(
        Guid id,
        Guid itemId,
        IOrderService orderService)
    {
        var result = await orderService.RemoveCompletedBillingItemAsync(id, itemId);
        if (!result.Success)
        {
            if (result.Error?.Contains("non trovata", StringComparison.OrdinalIgnoreCase) == true ||
                result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
                return TypedResults.NotFound();
            return TypedResults.BadRequest(result.Error);
        }

        return TypedResults.Ok(result.Data!);
    }

    private static async Task<Results<Ok<OrderDto>, BadRequest<string>, NotFound>> UpdateOrderItem(
        Guid id,
        Guid itemId,
        CreateOrderItemRequest request,
        IOrderService orderService)
    {
        var result = await orderService.UpdateItemAsync(id, itemId, request);
        if (result.Success)
            return TypedResults.Ok(result.Data!);
        if (result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            return TypedResults.NotFound();
        return TypedResults.BadRequest(result.Error);
    }

    private static async Task<Results<Ok<OrderDto>, BadRequest<string>, NotFound>> RemoveDraftOrderItem(
        Guid id,
        Guid itemId,
        IOrderService orderService)
    {
        var result = await orderService.RemoveDraftItemAsync(id, itemId);
        if (result.Success)
            return TypedResults.Ok(result.Data!);
        if (result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            return TypedResults.NotFound();
        return TypedResults.BadRequest(result.Error);
    }

    private static async Task<Results<Ok<OrderDto>, BadRequest<string>, NotFound>> UpdateOrderShippingCost(
        Guid id,
        UpdateOrderShippingChargeRequest request,
        IOrderService orderService)
    {
        var result = await orderService.UpdateShippingChargeAsync(id, request);
        if (result.Success)
            return TypedResults.Ok(result.Data!);
        if (result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            return TypedResults.NotFound();
        return TypedResults.BadRequest(result.Error);
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

    private static async Task<Results<Ok<OrderFulfillmentResultDto>, BadRequest<string>, NotFound>> FinalizeCashSale(
        Guid id,
        FinalizeCashSaleRequest request,
        ISender sender)
    {
        var result = await sender.Send(new FinalizeCashSaleCommand(
            id, request.PaymentMethod, request.TransactionDetails));
        if (result.Success)
            return TypedResults.Ok(result.Data!);
        if (result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            return TypedResults.NotFound();
        return TypedResults.BadRequest(result.Error);
    }

    private static async Task<Results<Ok<OrderDto>, BadRequest<string>, NotFound>> ApproveOrder(
        Guid id,
        ISender sender)
    {
        var result = await sender.Send(new ApproveOrderCommand(id));
        if (result.Success)
            return TypedResults.Ok(result.Data!);
        if (result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            return TypedResults.NotFound();
        return TypedResults.BadRequest(result.Error);
    }

    private static async Task<Results<Ok<OrderFulfillmentResultDto>, BadRequest<string>, NotFound>> ShipOrder(
        Guid id,
        ShipOrderRequest request,
        ISender sender)
    {
        var result = await sender.Send(new CreateShipmentCommand(
            id, request.CarrierName, request.TrackingCode,
            request.Items, request.BoxCount, request.WeightKg));
        if (result.Success)
            return TypedResults.Ok(result.Data!);
        if (result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            return TypedResults.NotFound();
        return TypedResults.BadRequest(result.Error);
    }

    private static async Task<Ok<PagedResult<OrderDto>>> ListFulfillmentQueue(
        IOrderService orderService,
        int page = 1,
        int pageSize = 20)
    {
        var result = await orderService.GetFulfillmentQueueAsync(page, pageSize);
        return TypedResults.Ok(result.Data!);
    }

    private static async Task<Results<Ok<OrderDto>, BadRequest<string>, NotFound>> UpdateCompletedInvoiceShippingCost(
        Guid id,
        UpdateOrderShippingChargeRequest request,
        IOrderService orderService)
    {
        var result = await orderService.UpdateCompletedInvoiceShippingChargeAsync(id, request);
        if (!result.Success)
        {
            if (result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
                return TypedResults.NotFound();
            return TypedResults.BadRequest(result.Error);
        }

        return TypedResults.Ok(result.Data!);
    }

    private static async Task<Results<Ok<OrderDto>, BadRequest<string>, NotFound>> CancelOrder(
        Guid id,
        CancelOrderRequest request,
        ISender sender)
    {
        var result = await sender.Send(new CancelOrderCommand(id, request.Reason));
        if (result.Success)
            return TypedResults.Ok(result.Data!);
        if (result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            return TypedResults.NotFound();
        return TypedResults.BadRequest(result.Error);
    }

    private static async Task<IResult> StartPicking(
        Guid id,
        ClaimsPrincipal user,
        ISender sender)
    {
        var operatorClaim = user.FindFirst("oid")?.Value ??
            user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(operatorClaim, out var operatorId))
            return Results.BadRequest("The authenticated user does not have a valid warehouse operator ID.");

        return await UpdateWorkflow(id, new UpdateOrderWorkflowCommand(
            id, OrderWorkflowAction.StartPicking, WarehouseOperatorId: operatorId), sender);
    }

    private static async Task<IResult> PutOrderOnHold(
        Guid id,
        CancelOrderRequest request,
        ISender sender) =>
        await UpdateWorkflow(id, new UpdateOrderWorkflowCommand(
            id, OrderWorkflowAction.PutOnHold, Reason: request.Reason), sender);

    private static async Task<IResult> ResumeOrder(Guid id, ISender sender) =>
        await UpdateWorkflow(id, new UpdateOrderWorkflowCommand(id, OrderWorkflowAction.Resume), sender);

    private static async Task<IResult> CompleteOrder(Guid id, ISender sender) =>
        await UpdateWorkflow(id, new UpdateOrderWorkflowCommand(id, OrderWorkflowAction.Complete), sender);

    private static async Task<IResult> CancelRemainingLines(
        Guid id,
        CancelOrderRequest request,
        ISender sender) =>
        await UpdateWorkflow(id, new UpdateOrderWorkflowCommand(
            id, OrderWorkflowAction.CancelRemainingLines, Reason: request.Reason), sender);

    private static async Task<IResult> UpdateWorkflow(
        Guid id,
        UpdateOrderWorkflowCommand command,
        ISender sender)
    {
        var result = await sender.Send(command);
        if (result.Success)
            return Results.Ok(result.Data);
        if (result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            return Results.NotFound();
        return Results.BadRequest(result.Error);
    }
}
