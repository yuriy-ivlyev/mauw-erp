using Microsoft.EntityFrameworkCore;
using Mauw.Erp.Infrastructure.DbContext;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Api.Endpoints;
using Mauw.Erp.Infrastructure.Reporting;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Configure PostgreSQL connection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
    "Host=localhost;Database=mauw_erp;Username=postgres;Password=postgres;Port=5432";

builder.Services.AddDbContext<MauwDbContext>(options =>
    options.UseNpgsql(connectionString)
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

// Register application services
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IContactService, ContactService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IDdtService, DdtService>();

// Register domain services
builder.Services.AddScoped<Mauw.Erp.Domain.Services.PricingEngine>();
builder.Services.AddScoped<IPdfGenerator, QuestPdfGenerator>();

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
app.MapOrderEndpoints();
app.MapInvoiceEndpoints();
app.MapDdtEndpoints();

// Fallback to index.html for Angular routing
app.MapFallbackToFile("index.html");

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
