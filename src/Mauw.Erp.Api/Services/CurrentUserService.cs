using System.Security.Claims;
using Mauw.Erp.Application.Services;

namespace Mauw.Erp.Api.Services;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public string? UserId =>
        User?.FindFirstValue("oid") ?? User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? Email =>
        User?.FindFirstValue("preferred_username") ?? User?.FindFirstValue(ClaimTypes.Email);

    public string? FullName =>
        User?.FindFirstValue("name") ?? User?.FindFirstValue(ClaimTypes.Name);

    public IReadOnlyList<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];

    public bool HasRole(string role) => User?.IsInRole(role) ?? false;
}
