using Mauw.Erp.Application.Constants;
using Mauw.Erp.Application.Services;

namespace Mauw.Erp.Application.Costs;

/// <summary>
/// Single source of truth for who may see purchase costs and stock valuation.
/// Filtering happens on the server (see <see cref="CostRedaction"/>); the UI is never the guard.
/// </summary>
public interface ICostVisibility
{
    /// <summary>Purchase cost, stock unit cost, valuation values and totals, cost source: Admin and Accounting.</summary>
    bool CanSeeCosts { get; }
}

public sealed class CostVisibility(ICurrentUserService currentUser) : ICostVisibility
{
    public bool CanSeeCosts => currentUser.HasRole(AppRoles.Admin) || currentUser.HasRole(AppRoles.Accounting);
}
