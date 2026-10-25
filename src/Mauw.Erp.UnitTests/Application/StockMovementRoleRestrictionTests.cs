using System.Net;
using System.Net.Http.Json;
using Mauw.Erp.Api.Endpoints;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Enums;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

public sealed class StockMovementRoleRestrictionTests
{
    private const string Path = "/api/v1/inventory/movements";

    public static TheoryData<string, StockMovementReason, decimal, HttpStatusCode> Cases()
    {
        var data = new TheoryData<string, StockMovementReason, decimal, HttpStatusCode>();
        foreach (var reason in Enum.GetValues<StockMovementReason>())
        {
            foreach (var quantity in new[] { 3m, -3m })
            {
                data.Add("Admin", reason, quantity, HttpStatusCode.OK);
                data.Add("Warehouse", reason, quantity, HttpStatusCode.OK);
                data.Add("Accounting", reason, quantity, HttpStatusCode.Forbidden);
                data.Add("Sales", reason, quantity, SalesOutcome(reason, quantity));
            }
        }
        data.Add(null!, StockMovementReason.ShowroomSale, -3m, HttpStatusCode.Unauthorized);
        return data;
    }

    private static HttpStatusCode SalesOutcome(StockMovementReason reason, decimal quantity) => reason switch
    {
        StockMovementReason.ShowroomSale => quantity < 0m ? HttpStatusCode.OK : HttpStatusCode.BadRequest,
        StockMovementReason.CustomerReturn => quantity > 0m ? HttpStatusCode.OK : HttpStatusCode.BadRequest,
        _ => HttpStatusCode.Forbidden
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task MovementIsAllowedOnlyForTheRoleAndReasonCombinationsOfThePolicy(
        string? role, StockMovementReason reason, decimal quantity, HttpStatusCode expected)
    {
        var stock = new Mock<IStockService>();
        stock.Setup(service => service.ExecuteMovementAsync(It.IsAny<CreateStockMovementCommand>()))
            .ReturnsAsync(OperationResult<StockItemDto>.Ok(new StockItemDto(
                Guid.NewGuid(), Guid.NewGuid(), "WH", "Deposito", Guid.NewGuid(), "BAR", "Prodotto", null, null,
                1m, 0m, 1m, null, null)));
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddSingleton(stock.Object);
        builder.Services.AddSingleton(Mock.Of<IInventoryService>());
        builder.Services.AddTestRoleAuthorization();
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapInventoryEndpoints();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = TestRoleAuthentication.CreateTestClient(new Uri(address), role);

        var response = await client.PostAsJsonAsync(Path, new CreateStockMovementCommand(
            Guid.NewGuid(), Guid.NewGuid(), (int)reason, quantity));

        Assert.Equal(expected, response.StatusCode);
        stock.Verify(service => service.ExecuteMovementAsync(It.IsAny<CreateStockMovementCommand>()),
            expected == HttpStatusCode.OK ? Times.Once() : Times.Never());
        await app.StopAsync();
    }

    [Fact]
    public async Task UndefinedReasonsAreRejectedByTheServiceForSales()
    {
        var stock = new Mock<IStockService>();
        stock.Setup(service => service.ExecuteMovementAsync(It.IsAny<CreateStockMovementCommand>()))
            .ReturnsAsync(OperationResult<StockItemDto>.Fail("Invalid stock movement reason"));
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddSingleton(stock.Object);
        builder.Services.AddSingleton(Mock.Of<IInventoryService>());
        builder.Services.AddTestRoleAuthorization();
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapInventoryEndpoints();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = TestRoleAuthentication.CreateTestClient(new Uri(address), "Sales");

        var response = await client.PostAsJsonAsync(Path, new CreateStockMovementCommand(
            Guid.NewGuid(), Guid.NewGuid(), 99, -1m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        stock.Verify(service => service.ExecuteMovementAsync(It.IsAny<CreateStockMovementCommand>()), Times.Once);
        await app.StopAsync();
    }
}
