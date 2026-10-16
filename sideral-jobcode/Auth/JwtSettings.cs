namespace sideral_jobcode.Auth;

public class JwtSettings
{
    public const string SECTION = "JwtSettings";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpirationInMinutes { get; set; } = 15;
    public int RefreshTokenExpirationInDays { get; set; } = 7;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Secret) || System.Text.Encoding.UTF8.GetByteCount(Secret) < 32)
            throw new InvalidOperationException($"{SECTION}:Secret must be at least 32 bytes long.");
        if (string.IsNullOrWhiteSpace(Issuer))
            throw new InvalidOperationException($"{SECTION}:Issuer is required.");
        if (string.IsNullOrWhiteSpace(Audience))
            throw new InvalidOperationException($"{SECTION}:Audience is required.");
    }
}
