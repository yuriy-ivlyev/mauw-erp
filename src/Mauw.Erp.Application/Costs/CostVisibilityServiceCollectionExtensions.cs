using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mauw.Erp.Application.Costs;

public static class CostVisibilityServiceCollectionExtensions
{
    /// <summary>Registers <see cref="ICostVisibility"/>; it needs an <c>ICurrentUserService</c> registration.</summary>
    public static IServiceCollection AddCostVisibility(this IServiceCollection services)
    {
        services.TryAddScoped<ICostVisibility, CostVisibility>();
        return services;
    }
}
