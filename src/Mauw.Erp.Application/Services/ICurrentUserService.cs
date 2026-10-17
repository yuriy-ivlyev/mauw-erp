namespace Mauw.Erp.Application.Services;

public interface ICurrentUserService
{
    string? UserId   { get; }
    string? Email    { get; }
    string? FullName { get; }
    IReadOnlyList<string> Roles { get; }
    bool HasRole(string role);
}
