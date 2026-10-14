using FluentValidation;
using Mauw.Erp.Contracts;

namespace Mauw.Erp.UnitTests;

public class CatalogValidationTests
{
    private readonly IValidator<CreateProductRequest> _validator = new CreateProductRequestValidator();

    [Fact]
    public void CreateProductRequest_ShouldRejectEmptySupplierCode()
    {
        var request = new CreateProductRequest
        {
            SupplierId = Guid.NewGuid(),
            SupplierCode = "",
            InternalCode = "INT-001",
            Name = "Widget",
            DefaultMarkupRate = 30m,
            VatRate = 22m,
            Variants =
            [
                new CreateProductVariantRequest
                {
                    Size = "M",
                    Color = "Blue",
                    Barcode = "123456789",
                    PurchaseCost = 10m,
                    StockQuantity = 10
                }
            ]
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateProductRequest.SupplierCode));
    }

    [Fact]
    public void CreateProductRequest_ShouldAcceptValidProductWithVariants()
    {
        var request = new CreateProductRequest
        {
            SupplierId = Guid.NewGuid(),
            SupplierCode = "SUP-001",
            InternalCode = "INT-001",
            Name = "Widget",
            DefaultMarkupRate = 30m,
            VatRate = 22m,
            Variants =
            [
                new CreateProductVariantRequest
                {
                    Size = "M",
                    Color = "Blue",
                    Barcode = "123456789",
                    PurchaseCost = 10m,
                    StockQuantity = 10
                }
            ]
        };

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }
}
