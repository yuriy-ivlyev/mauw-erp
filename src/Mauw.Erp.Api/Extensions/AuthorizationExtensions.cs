using Mauw.Erp.Api.Authorization;
using Mauw.Erp.Api.Services;
using Mauw.Erp.Application.Constants;
using Mauw.Erp.Application.Costs;
using Mauw.Erp.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mauw.Erp.Api.Extensions;

public static class AuthorizationExtensions
{
    /// <summary>
    /// Registers the role-based policies and a fallback policy: endpoints without explicit
    /// authorization metadata require an authenticated user. Public endpoints must opt in with AllowAnonymous.
    /// </summary>
    public static IServiceCollection AddAppAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationHandler, HistoricalStockAccessHandler>();
        // Cost visibility is role based: make sure it can be resolved wherever the role policies are used.
        services.AddHttpContextAccessor();
        services.TryAddScoped<ICurrentUserService, CurrentUserService>();
        services.AddCostVisibility();

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(AppPolicies.AnyRole, policy => policy.RequireRole(AppRoles.All))
            .AddPolicy(AppPolicies.AdminOnly, policy => policy.RequireRole(AppRoles.Admin))
            .AddPolicy(AppPolicies.CatalogManagement, policy => policy.RequireRole(AppRoles.Admin, AppRoles.Warehouse))
            .AddPolicy(AppPolicies.WarehouseOperations, policy => policy.RequireRole(AppRoles.Admin, AppRoles.Warehouse))
            .AddPolicy(AppPolicies.SalesOperations, policy => policy.RequireRole(AppRoles.Admin, AppRoles.Sales))
            .AddPolicy(AppPolicies.Billing, policy => policy.RequireRole(AppRoles.Admin, AppRoles.Accounting, AppRoles.Sales))
            .AddPolicy(AppPolicies.DdtDocuments, policy => policy.RequireRole(AppRoles.Admin, AppRoles.Warehouse, AppRoles.Sales))
            .AddPolicy(AppPolicies.StockMovementWrite, policy => policy.RequireRole(AppRoles.Admin, AppRoles.Warehouse, AppRoles.Sales))
            .AddPolicy(AppPolicies.InventoryStockRead, policy => policy
                .RequireRole(AppRoles.All)
                .AddRequirements(new HistoricalStockAccessRequirement()));

        return services;
    }
}
