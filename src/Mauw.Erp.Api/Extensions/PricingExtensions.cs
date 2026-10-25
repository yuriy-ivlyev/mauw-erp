using System.Text.Json.Serialization;
using Mauw.Erp.Domain.Pricing;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.Repositories;

namespace Mauw.Erp.Api.Extensions;

public static class PricingExtensions
{
    public static IServiceCollection AddPricingApi(this IServiceCollection services)
    {
        services.AddScoped<IPriceListRepository, PriceListRepository>();
        services.AddScoped<IPriceFormulaRepository, PriceFormulaRepository>();
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<PriceCalculationType>()));
        services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter<PriceCalculationType>()));
        return services;
    }
}
