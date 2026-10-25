# Refactoring Roadmap: Fluent API → Services-Based Architecture

## Status: FASE 1 COMPLETATA ✅

### Completato (ProductEndpoints come modello)

#### 1. Validatori Custom (NO Reflection)
- **File**: `src/Mauw.Erp.Application/Validation/`
  - `IValidator.cs` - interfaccia generica senza reflection
  - `RequestValidators.cs` - 8 validatori concreti (Product, Contact, Order, Invoice, Inventory)
  
- **Pattern**: Validazione logica pura, senza FluentValidation
```csharp
public class CreateProductRequestValidator : IValidator<CreateProductRequest>
{
    public ValidationResult Validate(CreateProductRequest request) { ... }
}
```

#### 2. Repository Pattern
- **Interfacce**: `src/Mauw.Erp.Domain/Repositories/IRepositories.cs`
  - `IRepository<T>` - base generica
  - `IUnitOfWork` - transaction management
  - Specifiche: `IProductRepository`, `IContactRepository`, `IOrderRepository`, `IInvoiceRepository`

- **Implementazioni**: `src/Mauw.Erp.Infrastructure/Repositories/RepositoryImplementations.cs`
  - `Repository<T>` - base con CRUD
  - `ProductRepository`, `ContactRepository`, etc.
  - `UnitOfWork` - Lazy-loaded property pattern

#### 3. Application Services
- **File**: `src/Mauw.Erp.Application/Services/ApplicationServices.cs`
  - `OperationResult<T>` - Result pattern (no exceptions)
  - `PagedResult<T>` - pagination generica
  - `IProductService` + `ProductService` ✅
  - `IContactService` + `ContactService` ✅ (da completare altri)

#### 4. Mappers Manuali (NO AutoMapper)
- **File**: `src/Mauw.Erp.Application/Mappers/EntityMappers.cs`
  - `ProductMapper` - Entity ↔ ProductDto
  - `ContactMapper` - Entity ↔ ContactDto  
  - `OrderMapper` - Entity ↔ OrderDto
  - `InvoiceMapper` - Entity ↔ InvoiceDto
  - Zero reflection, Type-safe, Composable

#### 5. ProductEndpoints - MODELLO REFACTORIZZATO ✅
```csharp
// PRIMA: DbContext diretto negli endpoint
public static async Task<Created<ProductDto>> CreateProduct(
    CreateProductRequest request,
    MauwDbContext dbContext,
    IValidator<CreateProductRequest> validator)
{ ... }

// DOPO: Service injection, thin controller
public static async Task<Results<Created<ProductDto>, BadRequest<string>>> CreateProduct(
    CreateProductRequest request,
    IProductService productService)
{
    var result = await productService.CreateProductAsync(request);
    if (!result.Success)
        return TypedResults.BadRequest(result.Error);
    return TypedResults.Created($"/api/v1/products/{result.Data!.Id}", result.Data);
}
```

#### 6. Dependency Injection (Program.cs)
```csharp
// Validatori
builder.Services.AddScoped<IValidator<CreateProductRequest>, CreateProductRequestValidator>();

// Repositories
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Services
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IContactService, ContactService>();
```

---

## FASE 2: Completare Endpoints Rimanenti

Seguire il **pattern ProductEndpoints** per:

### 1. ContactEndpoints
- Creare `IContactService` e `ContactService` ✅ (già creato, da testare)
- Refactorizzare endpoint per delegare a `IContactService`
- Usare `ContactMapper` per conversioni

### 2. OrderEndpoints
- Creare `IOrderService` (da creare):
  ```csharp
  Task<OperationResult<OrderDto>> CreateOrderAsync(CreateOrderRequest request);
  Task<OperationResult<OrderDto>> AddItemAsync(Guid orderId, CreateOrderItemRequest request);
  Task<OperationResult<OrderDto>> ConfirmAsync(Guid orderId);
  Task<OperationResult<PagedResult<OrderDto>>> GetOrdersByContactAsync(Guid contactId, int page, int pageSize);
  ```
- Registrare nel DI
- Refactorizzare endpoints

### 3. InvoiceEndpoints  
- Creare `IInvoiceService`:
  ```csharp
  Task<OperationResult<InvoiceDto>> CreateInvoiceAsync(CreateInvoiceRequest request);
  Task<OperationResult<InvoiceDto>> AddItemAsync(Guid invoiceId, CreateInvoiceItemRequest request);
  Task<OperationResult<InvoiceDto>> RecordPaymentAsync(Guid invoiceId, RecordPaymentRequest request);
  Task<OperationResult<byte[]>> GeneratePdfAsync(Guid invoiceId);
  ```
- Usare `IPdfGenerator` tramite DI
- Refactorizzare endpoints

### 4. InventoryEndpoints
- Creare `IInventoryService`:
  ```csharp
  Task<OperationResult<BarcodeVariantDto>> ScanBarcodeAsync(BarcodeScanRequest request);
  Task<OperationResult<PagedResult<StockMovementDto>>> GetMovementsAsync(Guid variantId, int page, int pageSize);
  ```
- Creare `IStockMovementRepository` (interfaccia) + implementazione ✅ (già creato)
- Refactorizzare endpoints

### 5. DdtEndpoints
- Creare `IDdtService`:
  ```csharp
  Task<OperationResult<DdtDto>> CreateDdtAsync(CreateDdtRequest request);
  Task<OperationResult<byte[]>> GeneratePdfAsync(Guid ddtId);
  ```
- Implementare CRUD completo (attualmente stub)
- Refactorizzare endpoints

---

## File Structure: DOPO Refactoring Completo

```
src/
├── Mauw.Erp.Api/
│   ├── Endpoints/
│   │   ├── ProductEndpoints.cs          ✅ REFACTORED
│   │   ├── ContactEndpoints.cs          ⏳ TODO
│   │   ├── OrderEndpoints.cs            ⏳ TODO
│   │   ├── InvoiceEndpoints.cs          ⏳ TODO
│   │   ├── InventoryEndpoints.cs        ⏳ TODO
│   │   └── DdtEndpoints.cs              ⏳ TODO
│   └── Program.cs                       ✅ REFACTORED
│
├── Mauw.Erp.Application/
│   ├── Services/
│   │   ├── ApplicationServices.cs       ✅ ProductService, ContactService
│   │   ├── OrderApplicationService.cs   ⏳ TODO
│   │   ├── InvoiceApplicationService.cs ⏳ TODO
│   │   └── InventoryApplicationService.cs ⏳ TODO
│   ├── Validation/
│   │   ├── IValidator.cs                ✅ NO Reflection interface
│   │   └── RequestValidators.cs         ✅ 8 validators
│   └── Mappers/
│       └── EntityMappers.cs             ✅ All entity mappers (NO AutoMapper)
│
├── Mauw.Erp.Domain/
│   ├── Entities/                        ✅ Business logic preserved
│   ├── Repositories/
│   │   └── IRepositories.cs             ✅ All interfaces
│   └── Services/
│       └── CatalogPriceCalculator.cs    ✅ Domain service (PricingEngine removed)
│
└── Mauw.Erp.Infrastructure/
    ├── DbContext/
    │   └── MauwDbContext.cs             ✅ Unchanged
    ├── Repositories/
    │   ├── RepositoryImplementations.cs ✅ All implementations + UnitOfWork
    └── Reporting/
        └── QuestPdfGenerator.cs         ✅ IPdfGenerator dependency
```

---

## Key Principles Applied

### 1. ❌ NO REFLECTION
- ✅ Custom validators instead of FluentValidation
- ✅ Manual mappers instead of AutoMapper
- ✅ Direct type conversion (Enum casting, etc.)

### 2. ✅ RESULT PATTERN (Not Exceptions)
```csharp
// Before: throw ValidationException
// After:  return OperationResult<T>.Fail(errors)
public record OperationResult<T>(bool Success, T? Data, string? Error = null);
```

### 3. ✅ LAYERED ARCHITECTURE
- **Endpoints**: HTTP mapping only (thin)
- **Services**: Business logic + validation
- **Repositories**: Data access
- **Domain**: Entities + business rules

### 4. ✅ DEPENDENCY INJECTION
- All services registered in DI container
- No `new` keyword in production code
- Interface-based dependencies

### 5. ✅ TYPE SAFETY
- Records for DTOs (immutable, type-safe)
- Enum conversions explicit
- No dynamic/reflection/string-based access

---

## Testing Completeness

After refactoring, test:

```bash
# Build (should pass with warnings only)
dotnet build

# Run (startup check)
dotnet run --project src/Mauw.Erp.Api

# Curl test ProductEndpoints
curl -X POST http://localhost:5000/api/v1/products \
  -H "Content-Type: application/json" \
  -d '{"supplierId":"...", "supplierCode":"TEST", ...}'

# Curl test other endpoints after refactoring
```

---

## Notes

- **ProductEndpoints** is the reference implementation
- **All DTO fields** must match Entity properties exactly
- **Mapper functions** should handle Enum↔int conversions explicitly
- **Services** return `OperationResult<T>` with unified error handling
- **Validation** happens in Services before Repository calls
- **Transaction management** available via `IUnitOfWork.BeginTransactionAsync()`
