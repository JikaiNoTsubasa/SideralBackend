using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using sideral_api.Models.Auth;
using sideral_jobcode.Auth;

namespace sideral_api.Controllers;

[Route("api/auth")]
public class AuthController : BaseController
{
    private readonly AuthManager _authManager;

    public AuthController(AuthManager authManager)
    {
        _authManager = authManager;
    }

    /// <summary>Authentification par identifiant (ou email) et mot de passe.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(Program.AUTH_RATE_LIMIT)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _authManager.Login(request.Login, request.Password, ClientIp, ct);
        return AuthResponse.From(result);
    }

    /// <summary>Renouvelle le bearer à partir d'un refresh token (le refresh token est renouvelé à chaque appel).</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(Program.AUTH_RATE_LIMIT)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var result = await _authManager.Refresh(request.RefreshToken, ClientIp, ct);
        return AuthResponse.From(result);
    }

    /// <summary>Révoque le refresh token de la session courante.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request, CancellationToken ct)
    {
        await _authManager.Logout(LoggedUserId, request.RefreshToken, ct);
        return NoContent();
    }
}
