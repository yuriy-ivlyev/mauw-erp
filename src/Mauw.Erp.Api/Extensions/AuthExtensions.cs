using Mauw.Erp.Api.Services;
using Mauw.Erp.Application.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;

namespace Mauw.Erp.Api.Extensions;

public static class AuthExtensions
{
    public static IServiceCollection AddMicrosoftEntraAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApp(configuration.GetSection("AzureAd"));

        // Map Entra ID 'roles' app-role claim → ClaimTypes.Role so [Authorize(Roles=...)] works
        services.Configure<OpenIdConnectOptions>(
            OpenIdConnectDefaults.AuthenticationScheme,
            options => options.TokenValidationParameters.RoleClaimType = "roles");

        // Harden the application session cookie
        services.Configure<CookieAuthenticationOptions>(
            CookieAuthenticationDefaults.AuthenticationScheme,
            options =>
            {
                options.Cookie.Name       = "Mauw.Session";
                options.Cookie.HttpOnly   = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite   = SameSiteMode.Strict;

                // Return 401 for API paths instead of redirecting to Microsoft login
                options.Events.OnRedirectToLogin = context =>
                {
                    if (context.Request.Path.StartsWithSegments("/api"))
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    }
                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };

                options.Events.OnRedirectToAccessDenied = context =>
                {
                    if (context.Request.Path.StartsWithSegments("/api"))
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    }
                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };
            });

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        return services;
    }
}
