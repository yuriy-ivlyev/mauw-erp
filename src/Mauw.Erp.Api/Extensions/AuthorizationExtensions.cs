using Mauw.Erp.Api.Authorization;
using Mauw.Erp.Application.Constants;
using Microsoft.AspNetCore.Authorization;

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
