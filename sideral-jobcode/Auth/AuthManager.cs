using dependancies.Security;
using dependancies.SharedClasses;
using Microsoft.EntityFrameworkCore;
using sideral_context;
using sideral_context.Users;

namespace sideral_jobcode.Auth;

public record AuthResult(
    string AccessToken,
    DateTime AccessTokenExpiresOn,
    string RefreshToken,
    DateTime RefreshTokenExpiresOn,
    User User);

public class AuthManager : IScoped
{
    private readonly SideralDbContext _db;
    private readonly IHash _hash;
    private readonly AuthorizationTokenGenerator _tokenGenerator;

    public AuthManager(SideralDbContext db, IHash hash, AuthorizationTokenGenerator tokenGenerator)
    {
        _db = db;
        _hash = hash;
        _tokenGenerator = tokenGenerator;
    }

    /// <summary>Authentifie par login ou email (insensible à la casse).</summary>
    public async Task<AuthResult> Login(string login, string password, string? ip, CancellationToken ct = default)
    {
        var normalized = login.Trim().ToLower();

        var user = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => !u.Deleted && (u.Login.ToLower() == normalized || u.Email.ToLower() == normalized), ct);

        if (user is null || !_hash.CompareTextAndHash(password, user.Password))
        {
            throw new ApiException("Failed login attempt", "Identifiant ou mot de passe incorrect.", ApiException.TypeError.UNAUTHORIZED, "AUTH_001");
        }

        if (!user.IsActive)
        {
            throw new ApiException("Inactive account", "Ce compte est désactivé.", ApiException.TypeError.FORBIDDEN, "AUTH_002");
        }

        user.LastLoginOn = DateTime.UtcNow;
        return await _IssueTokens(user, ip, ct);
    }

    /// <summary>
    /// Échange un refresh token contre une nouvelle paire de tokens (rotation).
    /// La réutilisation d'un token déjà révoqué révoque toute la famille de tokens de l'utilisateur.
    /// </summary>
    public async Task<AuthResult> Refresh(string refreshToken, string? ip, CancellationToken ct = default)
    {
        var hash = AuthorizationTokenGenerator.HashRefreshToken(refreshToken);

        var stored = await _db.RefreshTokens
            .Include(t => t.User!)
            .ThenInclude(u => u.Roles)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null || stored.User is null)
        {
            throw new ApiException("Invalid refresh token", "Session invalide, veuillez vous reconnecter.", ApiException.TypeError.UNAUTHORIZED, "AUTH_003");
        }

        if (stored.RevokedOn is not null)
        {
            await _RevokeAll(stored.UserId, ct);
            throw new ApiException("Refresh token reused", "Session invalide, veuillez vous reconnecter.", ApiException.TypeError.UNAUTHORIZED, "AUTH_004");
        }

        if (!stored.IsActive || stored.User.Deleted || !stored.User.IsActive)
        {
            throw new ApiException("Expired refresh token", "Session expirée, veuillez vous reconnecter.", ApiException.TypeError.UNAUTHORIZED, "AUTH_005");
        }

        var result = await _IssueTokens(stored.User, ip, ct, save: false);
        stored.RevokedOn = DateTime.UtcNow;
        stored.ReplacedByTokenHash = AuthorizationTokenGenerator.HashRefreshToken(result.RefreshToken);
        await _db.SaveChangesAsync(ct);

        return result;
    }

    public async Task Logout(long userId, string refreshToken, CancellationToken ct = default)
    {
        var hash = AuthorizationTokenGenerator.HashRefreshToken(refreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && t.UserId == userId, ct);

        if (stored is not null && stored.RevokedOn is null)
        {
            stored.RevokedOn = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task RevokeAll(long userId, CancellationToken ct = default)
    {
        await _RevokeAll(userId, ct);
    }

    private async Task<AuthResult> _IssueTokens(User user, string? ip, CancellationToken ct, bool save = true)
    {
        var (accessToken, accessExpires) = _tokenGenerator.GenerateAccessToken(user);
        var (refreshToken, refreshHash, refreshExpires) = _tokenGenerator.GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            ExpiresOn = refreshExpires,
            CreatedByIp = ip
        });

        // Purge des tokens expirés de l'utilisateur
        await _db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.ExpiresOn < DateTime.UtcNow)
            .ExecuteDeleteAsync(ct);

        if (save)
        {
            await _db.SaveChangesAsync(ct);
        }

        return new AuthResult(accessToken, accessExpires, refreshToken, refreshExpires, user);
    }

    private async Task _RevokeAll(long userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedOn == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedOn, now), ct);
    }
}
