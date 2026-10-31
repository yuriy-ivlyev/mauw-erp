using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.UnitTests.Application;

public class ContactTaxRegimeDtoTests
{
    [Theory]
    [InlineData("IT", "Domestic", null, null)]
    [InlineData("DE", "IntraEu", 0, "N3.2")]
    [InlineData("GR", "IntraEu", 0, "N3.2")]
    [InlineData("CH", "ExtraEu", 0, "N3.1")]
    [InlineData("US", "ExtraEu", 0, "N3.1")]
    [InlineData("GB", "ExtraEu", 0, "N3.1")]
    public void ContactDtosExposeTheTaxRegimeAndTheForcedClassification(
        string country, string regime, int? rate, string? nature)
    {
        var contact = new Contact("Cliente estero") { CountryCode = country };
        decimal? expectedRate = rate;

        var summary = ContactMapper.MapToSummary(contact);
        var detail = ContactMapper.MapToDetail(contact);

        Assert.Equal(country, summary.CountryCode);
        Assert.Equal(regime, summary.TaxRegime);
        Assert.Equal(expectedRate, summary.ForcedVatRate);
        Assert.Equal(nature, summary.ForcedVatNature);
        Assert.Equal(regime, detail.TaxRegime);
        Assert.Equal(expectedRate, detail.ForcedVatRate);
        Assert.Equal(nature, detail.ForcedVatNature);
    }

    [Fact]
    public void MalformedCountryExposesNoRegimeInsteadOfGuessingOne()
    {
        var summary = ContactMapper.MapToSummary(new Contact("Cliente") { CountryCode = "Italia" });

        Assert.Null(summary.TaxRegime);
        Assert.Null(summary.ForcedVatRate);
        Assert.Null(summary.ForcedVatNature);
    }
}
