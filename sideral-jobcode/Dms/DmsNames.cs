using dependancies.SharedClasses;
using Microsoft.EntityFrameworkCore;
using sideral_context;
using sideral_context.Dms;

namespace sideral_jobcode.Dms;

/// <summary>Validation et unicité des noms : dossiers et documents partagent le même espace de noms dans un dossier.</summary>
public class DmsNames : IScoped
{
    private static readonly char[] _INVALID_CHARS = ['/', '\\', ':', '*', '?', '"', '<', '>', '|'];

    private readonly SideralDbContext _db;

    public DmsNames(SideralDbContext db)
    {
        _db = db;
    }

    public static string Validate(string? name)
    {
        name = name?.Trim() ?? string.Empty;
        if (name.Length == 0 || name is "." or "..")
        {
            throw new ApiException("Invalid name", "Le nom ne peut pas être vide.", ApiException.TypeError.MODEL_ERROR, "DMS_003");
        }
        if (name.Length > Folder.NAME_MAX_LENGTH)
        {
            throw new ApiException("Name too long", $"Le nom ne peut pas dépasser {Folder.NAME_MAX_LENGTH} caractères.", ApiException.TypeError.MODEL_ERROR, "DMS_004");
        }
        if (name.IndexOfAny(_INVALID_CHARS) >= 0 || name.Any(char.IsControl))
        {
            throw new ApiException("Invalid name", "Le nom ne peut pas contenir les caractères / \\ : * ? \" < > |", ApiException.TypeError.MODEL_ERROR, "DMS_005");
        }
        return name;
    }

    /// <summary>Nom d'un fichier envoyé par le client : on ne garde que le nom (sans chemin) et on retire les caractères interdits.</summary>
    public static string FromFileName(string? fileName)
    {
        var name = Path.GetFileName((fileName ?? string.Empty).Replace('\\', '/'));
        name = new string(name.Where(c => !char.IsControl(c) && Array.IndexOf(_INVALID_CHARS, c) < 0).ToArray()).Trim();
        if (name.Length > Folder.NAME_MAX_LENGTH)
        {
            var extension = Path.GetExtension(name);
            name = name[..(Folder.NAME_MAX_LENGTH - extension.Length)] + extension;
        }
        return Validate(name);
    }

    public async Task<bool> IsAvailable(long? parentId, string name, long? exceptFolderId = null, long? exceptDocumentId = null, CancellationToken ct = default)
    {
        var lower = name.ToLower();
        var folderExists = await _db.Folders.AnyAsync(f =>
            f.ParentId == parentId && f.DeletedOn == null && f.Id != exceptFolderId && f.Name.ToLower() == lower, ct);
        if (folderExists || parentId is null)
        {
            return !folderExists;
        }
        return !await _db.Documents.AnyAsync(d =>
            d.FolderId == parentId && d.DeletedOn == null && d.Id != exceptDocumentId && d.Name.ToLower() == lower, ct);
    }

    public async Task EnsureAvailable(long? parentId, string name, long? exceptFolderId = null, long? exceptDocumentId = null, CancellationToken ct = default)
    {
        if (!await IsAvailable(parentId, name, exceptFolderId, exceptDocumentId, ct))
        {
            throw new ApiException("Name already used", $"Un élément nommé « {name} » existe déjà à cet emplacement.", ApiException.TypeError.RESSOURCE_ALREADY_EXISTS, "DMS_006");
        }
    }

    /// <summary>Premier nom libre parmi "nom.ext", "nom (2).ext", "nom (3).ext"... (restauration depuis la corbeille).</summary>
    public async Task<string> FirstAvailable(long? parentId, string name, bool isFolder, CancellationToken ct = default)
    {
        var extension = isFolder ? string.Empty : Path.GetExtension(name);
        var stem = name[..^extension.Length];
        var candidate = name;
        for (var i = 2; !await IsAvailable(parentId, candidate, ct: ct); i++)
        {
            candidate = $"{stem} ({i}){extension}";
        }
        return candidate;
    }
}
