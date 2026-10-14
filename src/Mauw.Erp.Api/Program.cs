using FluentValidation;
using Mauw.Erp.Api;
using Mauw.Erp.Application;
using Mauw.Erp.Domain;

var builder = WebApplication.CreateBuilder(args);

var inventoryRepository = new InMemoryInventoryRepository();
inventoryRepository.Seed(new ProductVariant(Guid.NewGuid(), "SKU-1000", "Sample product", 10, 9.50m));

builder.Services.AddOpenApi();
builder.Services.AddValidatorsFromAssemblyContaining<ScanStockMovementCommandValidator>();
builder.Services.AddSingleton<IInventoryRepository>(inventoryRepository);
builder.Services.AddScoped<ScanStockMovementCommandHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapInventoryEndpoints();

app.Run();
