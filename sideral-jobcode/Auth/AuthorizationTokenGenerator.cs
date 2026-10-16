using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using dependancies.SharedClasses;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using sideral_context.Users;

namespace sideral_jobcode.Auth;

/// <summary>
/// Génère les bearer JWT (courte durée) et les refresh tokens (opaques, longue durée).
/// </summary>
public class AuthorizationTokenGenerator : ISingleton
{
    public const string USER_ID_CLAIM = JwtRegisteredClaimNames.Sub;
    public const string LOGIN_CLAIM = JwtRegisteredClaimNames.UniqueName;
    public const string ROLE_CLAIM = "role";

    private readonly JwtSettings _settings;
    private readonly JsonWebTokenHandler _handler = new();

    public AuthorizationTokenGenerator(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }

    public (string token, DateTime expiresOn) GenerateAccessToken(User user)
    {
        var expires = DateTime.UtcNow.AddMinutes(_settings.ExpirationInMinutes);

        var claims = new List<Claim>
        {
            new(USER_ID_CLAIM, user.Id.ToString()),
            new(LOGIN_CLAIM, user.Login),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        claims.AddRange(user.Roles.Select(r => new Claim(ROLE_CLAIM, r.Key)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        });

        return (token, expires);
    }

    public (string token, string hash, DateTime expiresOn) GenerateRefreshToken()
    {
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
        return (token, HashRefreshToken(token), DateTime.UtcNow.AddDays(_settings.RefreshTokenExpirationInDays));
    }

    public static string HashRefreshToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
