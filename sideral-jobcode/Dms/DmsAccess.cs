using dependancies.SharedClasses;
using Microsoft.EntityFrameworkCore;
using sideral_context;
using sideral_context.Dms;

namespace sideral_jobcode.Dms;

/// <summary>Chargement de l'arbre des dossiers et des droits d'un utilisateur (voir FolderTree).</summary>
public class DmsAccess : IScoped
{
    private readonly SideralDbContext _db;

    public DmsAccess(SideralDbContext db)
    {
        _db = db;
    }

    public async Task<FolderTree> Load(long userId, bool isAdmin, CancellationToken ct = default)
    {
        var nodes = await _db.Folders
            .AsNoTracking()
            .Select(f => new FolderNode(f.Id, f.ParentId, f.Name, f.InheritPermissions, f.DeletedOn != null))
            .ToListAsync(ct);

        var ownLevels = new Dictionary<long, PermissionLevel>();
        if (!isAdmin)
        {
            var permissions = await _db.FolderPermissions
                .AsNoTracking()
                .Where(p => p.UserId == userId || p.Group!.Members.Any(m => m.Id == userId))
                .Select(p => new { p.FolderId, p.Level })
                .ToListAsync(ct);

            foreach (var p in permissions)
            {
                if (p.Level > ownLevels.GetValueOrDefault(p.FolderId))
                    ownLevels[p.FolderId] = p.Level;
            }
        }

        return new FolderTree(nodes, ownLevels, isAdmin);
    }

    /// <summary>
    /// Vérifie l'accès à un dossier. Un dossier invisible pour l'utilisateur renvoie "introuvable"
    /// (on ne révèle pas son existence) ; un dossier visible sans le droit suffisant renvoie "interdit".
    /// </summary>
    public static void Demand(FolderTree tree, long folderId, PermissionLevel level, string forbiddenMessage)
    {
        if (!tree.Can(folderId, PermissionLevel.Read))
        {
            throw FolderNotFound(folderId);
        }
        if (tree.Level(folderId) < level)
        {
            throw new ApiException("Insufficient permission", forbiddenMessage, ApiException.TypeError.FORBIDDEN, "DMS_010");
        }
    }

    public static ApiException FolderNotFound(long folderId) =>
        new("Folder not found", $"Dossier {folderId} introuvable.", ApiException.TypeError.RESSOURCE_NOT_FOUND, "DMS_011");

    public static ApiException DocumentNotFound(long documentId) =>
        new("Document not found", $"Document {documentId} introuvable.", ApiException.TypeError.RESSOURCE_NOT_FOUND, "DMS_012");
}
