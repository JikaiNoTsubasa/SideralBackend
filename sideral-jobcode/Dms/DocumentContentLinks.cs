using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using dependancies.SharedClasses;
using Microsoft.Extensions.Options;
using sideral_jobcode.Auth;

namespace sideral_jobcode.Dms;

public record ContentLinkClaims(long VersionId, long UserId, bool Download);

/// <summary>
/// Liens signés (HMAC) vers le contenu d'une version : utilisables directement dans &lt;img&gt;, &lt;iframe&gt;,
/// &lt;video&gt; ou un lien de téléchargement, qui n'envoient pas le bearer. Les droits sont revérifiés à chaque accès.
/// </summary>
public class DocumentContentLinks : ISingleton
{
    public static readonly TimeSpan VALIDITY = TimeSpan.FromHours(1);

    private readonly byte[] _key;

    public DocumentContentLinks(IOptions<JwtSettings> settings)
    {
        // Clé dérivée : un lien ne peut pas servir de bearer et inversement
        _key = HMACSHA256.HashData(Encoding.UTF8.GetBytes(settings.Value.Secret), "sideral-document-content"u8);
    }

    public ContentLinkView Create(long versionId, long userId, bool download)
    {
        var expiresOn = DateTimeOffset.UtcNow.Add(VALIDITY);
        var payload = Base64Url.EncodeToString(Encoding.UTF8.GetBytes($"{versionId}.{userId}.{expiresOn.ToUnixTimeSeconds()}.{(download ? 1 : 0)}"));
        return new ContentLinkView($"{payload}.{_Sign(payload)}", expiresOn.UtcDateTime);
    }

    public ContentLinkClaims Read(string? token)
    {
        var parts = token?.Split('.');
        if (parts is not { Length: 2 } ||
            !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(_Sign(parts[0])), Encoding.ASCII.GetBytes(parts[1])))
        {
            throw _Invalid();
        }

        string[] values;
        try
        {
            values = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(parts[0])).Split('.');
        }
        catch (FormatException)
        {
            throw _Invalid();
        }

        if (values.Length != 4 ||
            !long.TryParse(values[0], CultureInfo.InvariantCulture, out var versionId) ||
            !long.TryParse(values[1], CultureInfo.InvariantCulture, out var userId) ||
            !long.TryParse(values[2], CultureInfo.InvariantCulture, out var expires) ||
            DateTimeOffset.FromUnixTimeSeconds(expires) < DateTimeOffset.UtcNow)
        {
            throw _Invalid();
        }

        return new ContentLinkClaims(versionId, userId, values[3] == "1");
    }

    private string _Sign(string payload) => Base64Url.EncodeToString(HMACSHA256.HashData(_key, Encoding.ASCII.GetBytes(payload)));

    private static ApiException _Invalid() =>
        new("Invalid content link", "Lien expiré ou invalide : rechargez la page.", ApiException.TypeError.UNAUTHORIZED, "DMS_030");
}
