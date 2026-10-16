using Microsoft.EntityFrameworkCore;
using Mauw.Erp.Infrastructure.DbContext;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Application.Extensions;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Api.Endpoints;
using Mauw.Erp.Api.Extensions;
using Mauw.Erp.Infrastructure.Reporting;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Observability: OTel tracing, metrics and structured logging
builder.AddObservability();

// Configure PostgreSQL connection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
    "Host=localhost;Database=mauw_erp;Username=postgres;Password=postgres;Port=5432";

builder.Services.AddDbContext<MauwDbContext>(options =>
    options
        .UseNpgsql(connectionString)
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
);

// Register custom validators (no reflection)
builder.Services.AddScoped<IValidator<CreateProductRequest>, CreateProductRequestValidator>();
builder.Services.AddScoped<IValidator<CreateProductVariantRequest>, CreateProductVariantRequestValidator>();
builder.Services.AddScoped<IValidator<CreateContactRequest>, CreateContactRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateContactRequest>, UpdateContactRequestValidator>();
builder.Services.AddScoped<IValidator<CreateOrderRequest>, CreateOrderRequestValidator>();
builder.Services.AddScoped<IValidator<CreateOrderItemRequest>, CreateOrderItemRequestValidator>();
builder.Services.AddScoped<IValidator<BarcodeScanRequest>, BarcodeScanRequestValidator>();
builder.Services.AddScoped<IValidator<CreateInvoiceRequest>, CreateInvoiceRequestValidator>();

// Register repositories
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Register domain services
builder.Services.AddScoped<Mauw.Erp.Domain.Services.PricingEngine>();
builder.Services.AddScoped<IPdfGenerator, QuestPdfGenerator>();

// Register application services with tracing decorators
builder.Services.AddApplicationServices();

// Memory cache (usato da CompanySettingsService)
builder.Services.AddMemoryCache();

// Add Controllers
builder.Services.AddControllers();

// Add API Explorer for Swagger
builder.Services.AddEndpointsApiExplorer();

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

// Serve static files (Angular app)
app.UseStaticFiles();

// Map endpoints
app.MapProductEndpoints();
app.MapSupportEndpoints();
app.MapInventoryEndpoints();
app.MapContactEndpoints();
app.MapCompanySettingsEndpoints();
app.MapOrderEndpoints();
app.MapInvoiceEndpoints();
app.MapDdtEndpoints();

// Fallback to index.html for Angular routing
app.MapFallbackToFile("index.html");

// Migrate database on startup and initialize seed data
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<MauwDbContext>();
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        dbContext.Database.Migrate();
        await DbInitializer.InitializeAsync(dbContext);
    }
    catch (Exception ex)
    {
        startupLogger.LogCritical(ex, "Database initialization failed");
        throw;
    }
}

app.Run();