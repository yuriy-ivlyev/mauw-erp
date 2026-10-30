using Mauw.Erp.Application.Validation;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.UnitTests.Application;

public sealed class CreateOrderRequestValidatorTests
{
    [Theory]
    [InlineData("StandardShipping")]
    [InlineData("CashDesk")]
    [InlineData("cashdesk")]
    public void Validate_accepts_supported_fulfillment_types(string fulfillmentType)
    {
        var result = new CreateOrderRequestValidator().Validate(new CreateOrderRequest
        {
            ContactId = Guid.NewGuid(),
            FulfillmentType = fulfillmentType
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_allows_the_default_fulfillment_type()
    {
        var result = new CreateOrderRequestValidator().Validate(new CreateOrderRequest
        {
            ContactId = Guid.NewGuid()
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_rejects_unsupported_fulfillment_types()
    {
        var result = new CreateOrderRequestValidator().Validate(new CreateOrderRequest
        {
            ContactId = Guid.NewGuid(),
            FulfillmentType = "InvalidType"
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateOrderRequest.FulfillmentType));
    }

    [Fact]
    public void Validate_rejects_internal_notes_over_the_maximum_length()
    {
        var result = new CreateOrderRequestValidator().Validate(new CreateOrderRequest
        {
            ContactId = Guid.NewGuid(),
            InternalNotes = new string('x', 2001)
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateOrderRequest.InternalNotes));
    }
}
