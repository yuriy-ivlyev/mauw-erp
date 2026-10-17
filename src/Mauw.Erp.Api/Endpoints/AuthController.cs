using Mauw.Erp.Application.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;

namespace Mauw.Erp.Api.Endpoints;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>Avvia il flusso SSO verso Microsoft Entra ID.</summary>
    [HttpGet("login")]
    public IActionResult Login([FromQuery] string returnUrl = "/")
    {
        var redirectUri = Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
        return Challenge(
            new AuthenticationProperties { RedirectUri = redirectUri },
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    /// <summary>Termina la sessione locale e fa sign-out da Entra ID.</summary>
    [HttpGet("logout")]
    public IActionResult Logout() =>
        SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme);

    /// <summary>Restituisce le informazioni sull'utente autenticato.</summary>
    [HttpGet("user")]
    public IActionResult GetUser()
    {
        if (User.Identity?.IsAuthenticated != true)
            return Ok(new { isAuthenticated = false });

        return Ok(new
        {
            isAuthenticated = true,
            userId          = currentUser.UserId,
            email           = currentUser.Email,
            fullName        = currentUser.FullName,
            roles           = currentUser.Roles
        });
    }
}
