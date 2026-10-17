using Mauw.Erp.Api.Services;
using Mauw.Erp.Application.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Mauw.Erp.Api.Extensions;

public static class AuthExtensions
{
    public static IServiceCollection AddMicrosoftEntraAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var azureAd      = configuration.GetSection("AzureAd");
        var tenantId     = azureAd["TenantId"]     ?? throw new InvalidOperationException("AzureAd:TenantId is missing");
        var clientId     = azureAd["ClientId"]     ?? throw new InvalidOperationException("AzureAd:ClientId is missing");
        var clientSecret = azureAd["ClientSecret"];   // present → confidential client (Web platform)
        var authority    = $"{azureAd["Instance"]?.TrimEnd('/')}/{tenantId}/v2.0";
        var isConfidential = !string.IsNullOrWhiteSpace(clientSecret);

        services.AddAuthentication(options =>
            {
                options.DefaultScheme          = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.Cookie.Name         = "Mauw.Session";
                options.Cookie.HttpOnly     = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite     = SameSiteMode.Strict;

                options.Events.OnRedirectToLogin = ctx =>
                {
                    if (ctx.Request.Path.StartsWithSegments("/api"))
                    {
                        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    }
                    ctx.Response.Redirect(ctx.RedirectUri);
                    return Task.CompletedTask;
                };

                options.Events.OnRedirectToAccessDenied = ctx =>
                {
                    if (ctx.Request.Path.StartsWithSegments("/api"))
                    {
                        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    }
                    ctx.Response.Redirect(ctx.RedirectUri);
                    return Task.CompletedTask;
                };
            })
            .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                options.Authority            = authority;
                options.ClientId             = clientId;
                options.ResponseType         = "code";
                options.UsePkce              = true;   // recommended for both public and confidential clients
                options.CallbackPath         = azureAd["CallbackPath"]         ?? "/signin-oidc";
                options.SignedOutCallbackPath = azureAd["SignedOutCallbackPath"] ?? "/signout-callback-oidc";
                options.SaveTokens           = false;

                // Confidential client (Web platform): send secret for server-side token exchange
                if (isConfidential)
                    options.ClientSecret = clientSecret;

                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "name",
                    RoleClaimType = "roles",
                    ValidateIssuer = true
                };
            });

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        return services;
    }
}
