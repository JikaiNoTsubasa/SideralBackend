using System.Security.Cryptography;
using System.Text.Unicode;
using System.Text.RegularExpressions;
using dependancies.SharedClasses;
using Microsoft.Extensions.Options;

namespace sideral_jobcode.Storage;

/// <summary>Fichier enregistré par DocumentStorage.</summary>
public record StoredFile(string StorageKey, long Size, string Sha256, string ContentType);

/// <summary>
/// Stockage local des fichiers de la GED dans {RootPath}/documents/{2 premiers caractères}/{clé}.
/// Tous les types sont acceptés ; le type MIME est déterminé par signature binaire (jamais celui du client)
/// et sert uniquement à décider si un aperçu dans le navigateur est possible.
/// </summary>
public partial class DocumentStorage : ISingleton
{
    public const long MAX_SIZE = 100L * 1024 * 1024;

    public const string OCTET_STREAM = "application/octet-stream";
    public const string TEXT_PLAIN = "text/plain";
    public const string PDF = "application/pdf";

    /// <summary>Types affichables dans le navigateur (aperçu).</summary>
    public static readonly IReadOnlySet<string> PREVIEWABLE_TYPES = new HashSet<string>
    {
        PDF, TEXT_PLAIN,
        "image/png", "image/jpeg", "image/gif", "image/webp",
        "video/mp4", "video/webm",
        "audio/mpeg", "audio/ogg", "audio/wav"
    };

    private const int SNIFF_LENGTH = 8192;

    private readonly string _directory;

    public DocumentStorage(IOptions<StorageSettings> settings)
    {
        _directory = Path.Combine(Path.GetFullPath(settings.Value.RootPath), "documents");
        Directory.CreateDirectory(_directory);
    }

    /// <summary>Enregistre le flux sur disque (sans le charger en mémoire) en calculant son empreinte et son type.</summary>
    public async Task<StoredFile> Save(Stream content, long length, CancellationToken ct = default)
    {
        if (length <= 0)
        {
            throw new ApiException("Empty file", "Le fichier est vide.", ApiException.TypeError.MODEL_ERROR, "DMS_001");
        }
        if (length > MAX_SIZE)
        {
            throw new ApiException("File too large", $"Le fichier ne doit pas dépasser {MAX_SIZE / 1024 / 1024} Mo.", ApiException.TypeError.MODEL_ERROR, "DMS_002");
        }

        var key = Guid.NewGuid().ToString("N");
        var path = _PathOf(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var head = new byte[SNIFF_LENGTH];
        var headLength = 0;
        long size = 0;

        try
        {
            await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await content.ReadAsync(buffer, ct)) > 0)
                {
                    size += read;
                    if (size > MAX_SIZE)
                    {
                        throw new ApiException("File too large", $"Le fichier ne doit pas dépasser {MAX_SIZE / 1024 / 1024} Mo.", ApiException.TypeError.MODEL_ERROR, "DMS_002");
                    }
                    if (headLength < SNIFF_LENGTH)
                    {
                        var count = Math.Min(read, SNIFF_LENGTH - headLength);
                        Array.Copy(buffer, 0, head, headLength, count);
                        headLength += count;
                    }
                    sha.AppendData(buffer, 0, read);
                    await file.WriteAsync(buffer.AsMemory(0, read), ct);
                }
            }

            if (size == 0)
            {
                throw new ApiException("Empty file", "Le fichier est vide.", ApiException.TypeError.MODEL_ERROR, "DMS_001");
            }
        }
        catch
        {
            Delete(key);
            throw;
        }

        return new StoredFile(key, size, Convert.ToHexStringLower(sha.GetHashAndReset()), DetectContentType(head.AsSpan(0, headLength)));
    }

    /// <summary>Suppression "best effort" : un fichier orphelin ne doit pas faire échouer la requête.</summary>
    public void Delete(string? key)
    {
        if (key is null || !IsValidKey(key))
        {
            return;
        }
        try
        {
            File.Delete(_PathOf(key));
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Chemin physique du fichier, ou null s'il n'existe pas.</summary>
    public string? Find(string key)
    {
        if (!IsValidKey(key))
        {
            return null;
        }
        var path = _PathOf(key);
        return File.Exists(path) ? path : null;
    }

    public static bool IsValidKey(string key) => _KeyRegex().IsMatch(key);

    private string _PathOf(string key) => Path.Combine(_directory, key[..2], key);

    /// <summary>Type MIME d'après les premiers octets. Texte = UTF-8 valide sans caractère nul.</summary>
    public static string DetectContentType(ReadOnlySpan<byte> b)
    {
        if (b.StartsWith("%PDF-"u8))
            return PDF;
        if (b.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return "image/png";
        if (b.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }))
            return "image/jpeg";
        if (b.StartsWith("GIF87a"u8) || b.StartsWith("GIF89a"u8))
            return "image/gif";
        if (b.Length >= 12 && b.StartsWith("RIFF"u8) && b[8..12].SequenceEqual("WEBP"u8))
            return "image/webp";
        if (b.Length >= 12 && b.StartsWith("RIFF"u8) && b[8..12].SequenceEqual("WAVE"u8))
            return "audio/wav";
        if (b.Length >= 12 && b[4..8].SequenceEqual("ftyp"u8))
            return b[8..12].SequenceEqual("qt  "u8) ? "video/quicktime" : "video/mp4";
        if (b.StartsWith(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }))
            return "video/webm";
        if (b.StartsWith("OggS"u8))
            return "audio/ogg";
        if (b.StartsWith("ID3"u8) || (b.Length >= 2 && b[0] == 0xFF && (b[1] & 0xE0) == 0xE0))
            return "audio/mpeg";
        if (b.StartsWith("PK\x03\x04"u8))
            return "application/zip";
        if (b.StartsWith(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }))
            return "application/x-ole-storage"; // anciens formats Office (.doc, .xls, .ppt)
        return _IsText(b) ? TEXT_PLAIN : OCTET_STREAM;
    }

    private static bool _IsText(ReadOnlySpan<byte> b)
    {
        if (b.IndexOf((byte)0) >= 0)
            return false;
        // Le dernier caractère multi-octets peut être coupé par la limite de lecture
        for (var trim = 0; trim < 4 && trim <= b.Length; trim++)
        {
            if (Utf8.IsValid(b[..^trim]))
                return true;
        }
        return false;
    }

    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex _KeyRegex();
}
