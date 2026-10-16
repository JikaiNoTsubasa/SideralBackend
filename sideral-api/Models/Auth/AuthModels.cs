using System.ComponentModel.DataAnnotations;
using sideral_api.Models.Users;
using sideral_jobcode.Auth;

namespace sideral_api.Models.Auth;

public class LoginRequest
{
    /// <summary>Identifiant ou adresse email.</summary>
    [Required]
    public string Login { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class RefreshRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

public record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresOn,
    string RefreshToken,
    DateTime RefreshTokenExpiresOn,
    UserDto User)
{
    public static AuthResponse From(AuthResult result) => new(
        result.AccessToken,
        result.AccessTokenExpiresOn,
        result.RefreshToken,
        result.RefreshTokenExpiresOn,
        UserDto.From(result.User));
}
