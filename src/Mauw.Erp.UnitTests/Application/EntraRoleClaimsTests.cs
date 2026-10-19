using System.Security.Claims;
using System.Security.Cryptography;
using Mauw.Erp.Api.Extensions;
using Mauw.Erp.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Mauw.Erp.UnitTests.Application;

public class EntraRoleClaimsTests
{
    [Theory]
    [InlineData("Admin", true, true)]
    [InlineData("Sales", true, true)]
    [InlineData("Accounting", true, false)]
    [InlineData("Cashier", true, false)]
    [InlineData("Warehouse", true, false)]
    [InlineData("Other", false, false)]
    [InlineData(null, false, false)]
    public async Task MappedEntraRolesSurviveCookieSerializationAndMatchPriceListPermissions(
        string? role, bool canRead, bool canConfigure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMicrosoftEntraAuth(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["AzureAd:Instance"] = "https://login.microsoftonline.com",
                ["AzureAd:TenantId"] = "test-tenant",
                ["AzureAd:ClientId"] = "test-client"
            }).Build());
        services.AddAuthorization();
        await using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme);
        var key = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));
        var issuer = "https://issuer.example.test";
        var tokenHandler = new JsonWebTokenHandler { MapInboundClaims = options.MapInboundClaims };
        var token = tokenHandler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = options.ClientId,
            Subject = new ClaimsIdentity(role is null ? [] : [new Claim("roles", role)]),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        });
        var validation = options.TokenValidationParameters.Clone();
        validation.ValidIssuer = issuer;
        validation.ValidAudience = options.ClientId;
        validation.IssuerSigningKey = key;
        var result = await tokenHandler.ValidateTokenAsync(token, validation);
        Assert.True(result.IsValid, result.Exception?.Message);
        if (role is not null)
            Assert.Contains(result.ClaimsIdentity.Claims, claim => claim.Type == ClaimTypes.Role && claim.Value == role);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(result.ClaimsIdentity),
            CookieAuthenticationDefaults.AuthenticationScheme);
        var restored = TicketSerializer.Default.Deserialize(TicketSerializer.Default.Serialize(ticket))!;
        var context = new DefaultHttpContext { User = restored.Principal };
        var user = new CurrentUserService(new HttpContextAccessor { HttpContext = context });
        Assert.Equal(role is null ? Array.Empty<string>() : [role], user.Roles);
        if (role is not null)
            Assert.True(user.HasRole(role));
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var readPolicy = new AuthorizationPolicyBuilder()
            .RequireRole("Admin", "Sales", "Accounting", "Cashier", "Warehouse").Build();
        var writePolicy = new AuthorizationPolicyBuilder().RequireRole("Admin", "Sales").Build();
        Assert.Equal(canRead, (await authorization.AuthorizeAsync(context.User, null, readPolicy)).Succeeded);
        Assert.Equal(canConfigure, (await authorization.AuthorizeAsync(context.User, null, writePolicy)).Succeeded);
    }

    [Fact]
    public void CurrentUserRolesUseEachAuthenticatedIdentityRoleClaimType()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new[]
            {
                new ClaimsIdentity([new Claim("roles", "Sales")], "oidc", "name", "roles"),
                new ClaimsIdentity([new Claim(ClaimTypes.Role, "Sales")], "cookie"),
                new ClaimsIdentity([new Claim(ClaimTypes.Role, "Admin")])
            })
        };
        var user = new CurrentUserService(new HttpContextAccessor { HttpContext = context });
        Assert.Equal(["Sales"], user.Roles);
    }
}
