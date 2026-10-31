using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.UnitTests.Domain;

public class CustomerTaxPolicyTests
{
    [Theory]
    [InlineData("IT", CustomerTaxRegime.Domestic, null)]
    [InlineData("DE", CustomerTaxRegime.IntraEu, "N3.2")]
    [InlineData("FR", CustomerTaxRegime.IntraEu, "N3.2")]
    [InlineData("GR", CustomerTaxRegime.IntraEu, "N3.2")]
    [InlineData("EL", CustomerTaxRegime.IntraEu, "N3.2")]
    [InlineData("CH", CustomerTaxRegime.ExtraEu, "N3.1")]
    [InlineData("US", CustomerTaxRegime.ExtraEu, "N3.1")]
    [InlineData("GB", CustomerTaxRegime.ExtraEu, "N3.1")]
    [InlineData("de", CustomerTaxRegime.IntraEu, "N3.2")]
    [InlineData(" us ", CustomerTaxRegime.ExtraEu, "N3.1")]
    [InlineData(null, CustomerTaxRegime.Domestic, null)]
    [InlineData("", CustomerTaxRegime.Domestic, null)]
    public void ResolveMapsTheCountryToTheTaxRegime(string? country, CustomerTaxRegime regime, string? nature)
    {
        var result = CustomerTaxPolicy.Resolve(country);

        Assert.True(result.Success, result.Error);
        decimal? expectedRate = nature is null ? null : 0m;
        Assert.Equal(regime, result.Data!.Regime);
        Assert.Equal(nature, result.Data.ForcedVatNature);
        Assert.Equal(expectedRate, result.Data.ForcedVatRate);
        Assert.Equal(nature is not null, result.Data.IsVatForced);
    }

    [Theory]
    [InlineData("EL", "GR")]
    [InlineData("it", "IT")]
    [InlineData(null, "IT")]
    public void ResolveNormalizesTheCountryCode(string? country, string expected) =>
        Assert.Equal(expected, CustomerTaxPolicy.Resolve(country).Data!.CountryCode);

    [Theory]
    [InlineData("ITA")]
    [InlineData("I")]
    [InlineData("Italia")]
    [InlineData("1T")]
    [InlineData("D?")]
    public void ResolveRejectsMalformedCountryCodes(string country)
    {
        var result = CustomerTaxPolicy.Resolve(country);

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Contains("ISO 3166-1", result.Error);
        Assert.False(CustomerTaxPolicy.Classify(country, 0m, "N3.2").Success);
    }

    [Theory]
    [InlineData("DE", true)]
    [InlineData("GR", true)]
    [InlineData("EL", true)]
    [InlineData("IT", false)]
    [InlineData("CH", false)]
    [InlineData("GB", false)]
    [InlineData("US", false)]
    public void IsIntraEuCountryExcludesItalyAndNonMembers(string country, bool expected) =>
        Assert.Equal(expected, CustomerTaxPolicy.IsIntraEuCountry(country));

    [Theory]
    [InlineData(22, null)]
    [InlineData(10, "N6.9")]
    [InlineData(0, "N2.2")]
    [InlineData(null, null)]
    public void DomesticCustomersKeepTheRequestedClassification(int? vatRate, string? nature)
    {
        var rate = vatRate.HasValue ? (decimal?)vatRate.Value : null;

        var result = CustomerTaxPolicy.Classify("IT", rate, nature);

        Assert.True(result.Success, result.Error);
        Assert.Equal(rate, result.Data!.VatRate);
        Assert.Equal(nature, result.Data.NatureCode);
    }

    [Theory]
    [InlineData("DE", "N3.2")]
    [InlineData("FR", "N3.2")]
    [InlineData("GR", "N3.2")]
    [InlineData("CH", "N3.1")]
    [InlineData("US", "N3.1")]
    [InlineData("GB", "N3.1")]
    public void ForeignCustomersGetTheForcedClassificationWhenNothingIsRequested(string country, string nature)
    {
        var result = CustomerTaxPolicy.Classify(country, null, null);

        Assert.True(result.Success, result.Error);
        Assert.Equal(0m, result.Data!.VatRate);
        Assert.Equal(nature, result.Data.NatureCode);
    }

    [Theory]
    [InlineData("DE", 0, "N3.2")]
    [InlineData("DE", 0, " n3.2 ")]
    [InlineData("DE", 0, null)]
    [InlineData("GR", null, "N3.2")]
    [InlineData("US", 0, "N3.1")]
    public void ForeignCustomersAcceptTheMatchingClassification(string country, int? vatRate, string? nature)
    {
        var result = CustomerTaxPolicy.Classify(country, vatRate, nature);

        Assert.True(result.Success, result.Error);
        Assert.Equal(0m, result.Data!.VatRate);
        Assert.Equal(country == "US" ? "N3.1" : "N3.2", result.Data.NatureCode);
    }

    [Theory]
    [InlineData("DE", 22, null)]
    [InlineData("DE", 22, "N3.2")]
    [InlineData("FR", 4, null)]
    [InlineData("GR", 10, "N3.2")]
    [InlineData("CH", 22, "N3.1")]
    [InlineData("US", 22, null)]
    public void ForeignCustomersRejectAnExplicitTaxableRate(string country, int vatRate, string? nature)
    {
        var result = CustomerTaxPolicy.Classify(country, vatRate, nature);

        Assert.False(result.Success);
        Assert.Contains("aliquota IVA ammessa è 0%", result.Error);
    }

    [Theory]
    [InlineData("DE", "N3.1")]
    [InlineData("GR", "N2.2")]
    [InlineData("CH", "N3.2")]
    [InlineData("US", "N7")]
    [InlineData("GB", "N3.2")]
    public void ForeignCustomersRejectAMismatchingNature(string country, string nature)
    {
        var result = CustomerTaxPolicy.Classify(country, 0m, nature);

        Assert.False(result.Success);
        Assert.Contains("Natura IVA ammessa", result.Error);
    }

    [Fact]
    public void ClassificationIsDeterministicAndDoesNotMutateTheResolution()
    {
        var resolution = CustomerTaxPolicy.Resolve("DE").Data!;

        var first = CustomerTaxPolicy.Classify(resolution, null, null);
        var second = CustomerTaxPolicy.Classify(resolution, null, null);

        Assert.Equal(first.Data, second.Data);
        Assert.Equal(new CustomerTaxResolution("DE", CustomerTaxRegime.IntraEu, 0m, "N3.2"), resolution);
    }
}
