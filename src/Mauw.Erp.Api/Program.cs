using Microsoft.EntityFrameworkCore;
using FluentValidation;
using Mauw.Erp.Infrastructure.DbContext;
using Mauw.Erp.Application.Validators;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Api.Endpoints;
using Mauw.Erp.Infrastructure.Reporting;

var builder = WebApplication.CreateBuilder(args);

// Configure PostgreSQL connection
var connectionString = builder.Configuration.GetConnectionString("PostgreSQL") ??
    "Host=localhost;Database=mauw_erp;Username=postgres;******;Port=5432";

builder.Services.AddDbContext<MauwDbContext>(options =>
    options.UseNpgsql(connectionString)
);

// Add FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<CreateProductRequestValidator>();

// Add services
builder.Services.AddScoped<PricingService>();
builder.Services.AddScoped<IPdfGenerator, QuestPdfGenerator>();

// Add Swagger/OpenAPI
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Mauw ERP API",
        Version = "v1",
        Description = "Italian ERP/WMS system with fiscal pricing compliance"
    });
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", corsPolicyBuilder =>
    {
        corsPolicyBuilder
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Mauw ERP API v1");
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");

// Map endpoints
app.MapProductEndpoints();
app.MapInventoryEndpoints();
app.MapContactEndpoints();
app.MapOrderEndpoints();
app.MapInvoiceEndpoints();
app.MapDdtEndpoints();

// Migrate database on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<MauwDbContext>();
    try
    {
        dbContext.Database.Migrate();
    }
    catch
    {
        // Database migration will fail if database doesn't exist, which is expected in development
    }
}

app.Run();

/// <summary>
/// Pricing service for calculating prices with Italian fiscal rules.
/// </summary>
public class PricingService
{
    private readonly Mauw.Erp.Domain.Services.PricingEngine _pricingEngine = new();

    public Mauw.Erp.Domain.Services.PricingEngine.PricingResult CalculatePricing(
        decimal purchaseCost,
        decimal markupRate,
        decimal discountRate,
        decimal vatRate)
    {
        return _pricingEngine.CalculatePrice(purchaseCost, markupRate, discountRate, vatRate);
    }
}
