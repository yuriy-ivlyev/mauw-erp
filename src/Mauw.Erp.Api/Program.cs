using FluentValidation;
using Mauw.Erp.Api;
using Mauw.Erp.Application;
using Mauw.Erp.Domain;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddValidatorsFromAssemblyContaining<ScanStockMovementCommandValidator>();

builder.Services.AddSingleton<InMemoryInventoryRepository>();
builder.Services.AddSingleton<IInventoryRepository>(sp => sp.GetRequiredService<InMemoryInventoryRepository>());
builder.Services.AddScoped<ScanStockMovementCommandHandler>();

var repository = new InMemoryInventoryRepository();
repository.Seed(new ProductVariant(Guid.NewGuid(), "SKU-1000", "Sample product", 10, 9.50m));

builder.Services.AddSingleton(repository);
builder.Services.AddSingleton<IInventoryRepository>(sp => sp.GetRequiredService<InMemoryInventoryRepository>());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapInventoryEndpoints();

app.Run();
