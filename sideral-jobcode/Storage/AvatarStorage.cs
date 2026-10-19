using System.Text.RegularExpressions;
using dependancies.SharedClasses;
using Microsoft.Extensions.Options;

namespace sideral_jobcode.Storage;

/// <summary>
/// Stockage local des avatars dans {RootPath}/avatars.
/// Le nom de fichier contient un identifiant aléatoire : il change à chaque envoi (cache navigateur "immutable")
/// et n'est pas devinable, ce qui permet de servir les images sans authentification.
/// </summary>
public partial class AvatarStorage : ISingleton
{
    public const long MAX_SIZE = 2 * 1024 * 1024;

    private static readonly Dictionary<string, string> _CONTENT_TYPES = new()
    {
        [".jpg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp"
    };

    private readonly string _directory;

    public AvatarStorage(IOptions<StorageSettings> settings)
    {
        _directory = Path.Combine(Path.GetFullPath(settings.Value.RootPath), "avatars");
        Directory.CreateDirectory(_directory);
    }

    /// <summary>Enregistre l'image après vérification de sa taille et de son type réel (signature binaire). Renvoie le nom du fichier.</summary>
    public async Task<string> Save(long userId, Stream content, long length, CancellationToken ct = default)
    {
        if (length <= 0)
        {
            throw new ApiException("Empty file", "Le fichier est vide.", ApiException.TypeError.MODEL_ERROR, "AVT_001");
        }
        if (length > MAX_SIZE)
        {
            throw new ApiException("File too large", $"L'image ne doit pas dépasser {MAX_SIZE / 1024 / 1024} Mo.", ApiException.TypeError.MODEL_ERROR, "AVT_002");
        }

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();

        var extension = _DetectExtension(bytes)
            ?? throw new ApiException("Unsupported image", "Format d'image non supporté (JPEG, PNG ou WebP attendu).", ApiException.TypeError.MODEL_ERROR, "AVT_003");

        var fileName = $"{userId}_{Guid.NewGuid():N}{extension}";
        await File.WriteAllBytesAsync(Path.Combine(_directory, fileName), bytes, ct);
        return fileName;
    }

    /// <summary>Suppression "best effort" : un fichier orphelin ne doit pas faire échouer la requête.</summary>
    public void Delete(string? fileName)
    {
        if (fileName is null || !IsValidName(fileName))
        {
            return;
        }
        try
        {
            File.Delete(Path.Combine(_directory, fileName));
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Chemin physique et type MIME d'un avatar, ou null s'il n'existe pas (ou si le nom est invalide).</summary>
    public (string Path, string ContentType)? Find(string fileName)
    {
        if (!IsValidName(fileName))
        {
            return null;
        }
        var path = Path.Combine(_directory, fileName);
        return File.Exists(path) ? (path, _CONTENT_TYPES[Path.GetExtension(fileName)]) : null;
    }

    /// <summary>Protège contre les traversées de répertoire : seuls les noms générés par Save sont acceptés.</summary>
    public static bool IsValidName(string fileName) => _FileNameRegex().IsMatch(fileName);

    private static string? _DetectExtension(byte[] b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
            return ".jpg";
        if (b.Length >= 8 && b.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return ".png";
        if (b.Length >= 12 && b.AsSpan(0, 4).SequenceEqual("RIFF"u8) && b.AsSpan(8, 4).SequenceEqual("WEBP"u8))
            return ".webp";
        return null;
    }

    [GeneratedRegex(@"^\d+_[0-9a-f]{32}\.(jpg|png|webp)$")]
    private static partial Regex _FileNameRegex();
}
