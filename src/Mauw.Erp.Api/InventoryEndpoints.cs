using FluentValidation;
using Mauw.Erp.Application;

namespace Mauw.Erp.Api;

public static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/inventory/scan", async (
            ScanStockMovementCommand command,
            IValidator<ScanStockMovementCommand> validator,
            ScanStockMovementCommandHandler handler) =>
        {
            var validationResult = await validator.ValidateAsync(command);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors
                    .GroupBy(x => x.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage).ToArray());

                return Results.ValidationProblem(errors);
            }

            var variant = await handler.HandleAsync(command);
            return Results.Ok(new { variant.Id, variant.Barcode, variant.OnHandQuantity });
        });
    }
}
