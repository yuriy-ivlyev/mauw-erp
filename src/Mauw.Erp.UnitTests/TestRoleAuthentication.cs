using System.Security.Claims;
using System.Text.Encodings.Web;
using Mauw.Erp.Api.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mauw.Erp.UnitTests;

/// <summary>Replaces Entra/cookie authentication in test hosts with a role taken from a request header.</summary>
internal static class TestRoleAuthentication
{
    public const string Scheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
    public const string RoleHeader = "X-Test-Role";

    public static IServiceCollection AddTestRoleAuthorization(this IServiceCollection services)
    {
        services.AddAuthentication(Scheme).AddScheme<AuthenticationSchemeOptions, Handler>(Scheme, _ => { });
        return services.AddAppAuthorization();
    }

    public static HttpClient CreateTestClient(Uri address, string? role)
    {
        var client = new HttpClient { BaseAddress = address };
        if (role is not null)
            client.DefaultRequestHeaders.Add(RoleHeader, role);
        return client;
    }

    private sealed class Handler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(RoleHeader, out var role))
                return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Role, role.ToString())], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
