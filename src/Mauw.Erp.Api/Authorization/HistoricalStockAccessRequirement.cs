using Mauw.Erp.Application.Constants;
using Microsoft.AspNetCore.Authorization;

namespace Mauw.Erp.Api.Authorization;

/// <summary>Requests carrying an <c>atDate</c> query value read historical stock and need Admin or Accounting.</summary>
public sealed class HistoricalStockAccessRequirement : IAuthorizationRequirement
{
    public const string AtDateQueryKey = "atDate";
}

public sealed class HistoricalStockAccessHandler : AuthorizationHandler<HistoricalStockAccessRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, HistoricalStockAccessRequirement requirement)
    {
        // Without an HTTP request the query cannot be inspected, so the requirement stays unmet.
        if (context.Resource is not HttpContext http)
            return Task.CompletedTask;

        var isHistorical = !string.IsNullOrEmpty(http.Request.Query[HistoricalStockAccessRequirement.AtDateQueryKey].ToString());
        if (!isHistorical
            || context.User.IsInRole(AppRoles.Admin)
            || context.User.IsInRole(AppRoles.Accounting))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
