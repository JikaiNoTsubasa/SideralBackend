using dependancies.SharedClasses;
using Microsoft.EntityFrameworkCore;
using sideral_context;
using sideral_context.Dms;
using sideral_jobcode.Storage;

namespace sideral_jobcode.Dms;

/// <summary>
/// Corbeille de la GED. Seuls les éléments supprimés directement apparaissent (le contenu d'un dossier supprimé
/// le suit). Restauration et suppression définitive : gestionnaires du dossier concerné et administrateurs.
/// </summary>
public class TrashManager : IScoped
{
    public const string FOLDER = "folder";
    public const string DOCUMENT = "document";

    private readonly SideralDbContext _db;
    private readonly DmsAccess _access;
    private readonly DmsNames _names;
    private readonly DmsActivityLog _activity;
    private readonly DocumentStorage _storage;

    public TrashManager(SideralDbContext db, DmsAccess access, DmsNames names, DmsActivityLog activity, DocumentStorage storage)
    {
        _db = db;
        _access = access;
        _names = names;
        _activity = activity;
        _storage = storage;
    }

    public async Task<List<TrashItemView>> List(long userId, bool isAdmin, CancellationToken ct = default)
    {
        var tree = await _access.Load(userId, isAdmin, ct);
        return await _List(tree, ct);
    }

    public async Task RestoreFolder(long userId, bool isAdmin, long folderId, CancellationToken ct = default)
    {
        var tree = await _access.Load(userId, isAdmin, ct);
        var folder = await _db.Folders.FirstOrDefaultAsync(f => f.Id == folderId && f.DeletedOn != null, ct);
        if (folder is null || tree.Level(folderId) < PermissionLevel.Manage)
        {
            throw _NotInTrash();
        }
        if (tree.IsParentDeleted(folderId))
        {
            throw _ParentDeleted();
        }

        folder.Name = await _names.FirstAvailable(folder.ParentId, folder.Name, isFolder: true, ct);
        folder.DeletedOn = null;
        folder.DeletedById = null;
        _activity.Add(userId, DmsAction.FOLDER_RESTORED, folderId);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RestoreDocument(long userId, bool isAdmin, long documentId, CancellationToken ct = default)
    {
        var tree = await _access.Load(userId, isAdmin, ct);
        var document = await _db.Documents.FirstOrDefaultAsync(d => d.Id == documentId && d.DeletedOn != null, ct);
        if (document is null || tree.Level(document.FolderId) < PermissionLevel.Manage)
        {
            throw _NotInTrash();
        }
        if (tree.IsDeleted(document.FolderId))
        {
            throw _ParentDeleted();
        }

        document.Name = await _names.FirstAvailable(document.FolderId, document.Name, isFolder: false, ct);
        document.DeletedOn = null;
        document.DeletedById = null;
        _activity.Add(userId, DmsAction.DOCUMENT_RESTORED, document.FolderId, documentId);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Suppression définitive d'un dossier de la corbeille, avec tout son contenu et ses fichiers.</summary>
    public async Task PurgeFolder(long userId, bool isAdmin, long folderId, CancellationToken ct = default)
    {
        var tree = await _access.Load(userId, isAdmin, ct);
        if (tree.Find(folderId) is not { Deleted: true } || tree.IsParentDeleted(folderId) || tree.Level(folderId) < PermissionLevel.Manage)
        {
            throw _NotInTrash();
        }
        await _PurgeFolders(tree, [folderId], ct);
    }

    /// <summary>Suppression définitive d'un document de la corbeille (toutes ses versions et fichiers).</summary>
    public async Task PurgeDocument(long userId, bool isAdmin, long documentId, CancellationToken ct = default)
    {
        var tree = await _access.Load(userId, isAdmin, ct);
        var folderId = await _db.Documents.Where(d => d.Id == documentId && d.DeletedOn != null).Select(d => (long?)d.FolderId).FirstOrDefaultAsync(ct);
        if (folderId is null || tree.IsDeleted(folderId.Value) || tree.Level(folderId.Value) < PermissionLevel.Manage)
        {
            throw _NotInTrash();
        }
        await _PurgeDocuments([documentId], ct);
    }

    /// <summary>Vide la corbeille (éléments que l'utilisateur peut gérer).</summary>
    public async Task Empty(long userId, bool isAdmin, CancellationToken ct = default)
    {
        var tree = await _access.Load(userId, isAdmin, ct);
        var items = await _List(tree, ct);
        await _PurgeDocuments(items.Where(i => i.Type == DOCUMENT).Select(i => i.Id).ToList(), ct);
        await _PurgeFolders(tree, items.Where(i => i.Type == FOLDER).Select(i => i.Id).ToList(), ct);
    }

    private async Task<List<TrashItemView>> _List(FolderTree tree, CancellationToken ct)
    {
        var folders = await _db.Folders
            .AsNoTracking()
            .Where(f => f.DeletedOn != null)
            .Select(f => new
            {
                f.Id,
                f.Name,
                f.ParentId,
                DeletedOn = f.DeletedOn!.Value,
                DeletedBy = f.DeletedBy == null ? null : new UserRefView(f.DeletedBy.Id, f.DeletedBy.Firstname, f.DeletedBy.Lastname, f.DeletedBy.Avatar)
            })
            .ToListAsync(ct);

        var documents = await _db.Documents
            .AsNoTracking()
            .Where(d => d.DeletedOn != null)
            .Select(d => new
            {
                d.Id,
                d.Name,
                d.FolderId,
                DeletedOn = d.DeletedOn!.Value,
                DeletedBy = d.DeletedBy == null ? null : new UserRefView(d.DeletedBy.Id, d.DeletedBy.Firstname, d.DeletedBy.Lastname, d.DeletedBy.Avatar)
            })
            .ToListAsync(ct);

        return folders
            .Where(f => !tree.IsParentDeleted(f.Id) && tree.Level(f.Id) >= PermissionLevel.Manage)
            .Select(f => new TrashItemView(FOLDER, f.Id, f.Name, f.ParentId is { } p ? tree.PathOf(p) : "Racine", f.DeletedOn, f.DeletedBy))
            .Concat(documents
                .Where(d => !tree.IsDeleted(d.FolderId) && tree.Level(d.FolderId) >= PermissionLevel.Manage)
                .Select(d => new TrashItemView(DOCUMENT, d.Id, d.Name, tree.PathOf(d.FolderId), d.DeletedOn, d.DeletedBy)))
            .OrderByDescending(i => i.DeletedOn)
            .ToList();
    }

    private async Task _PurgeFolders(FolderTree tree, List<long> folderIds, CancellationToken ct)
    {
        if (folderIds.Count == 0)
            return;

        var allIds = folderIds.SelectMany(tree.SelfAndDescendants).ToList();
        var keys = await _db.DocumentVersions.Where(v => allIds.Contains(v.Document!.FolderId)).Select(v => v.StorageKey).Distinct().ToListAsync(ct);

        // Les sous-dossiers, documents, versions, droits et journal suivent par cascade (clés étrangères)
        await _db.Folders.Where(f => folderIds.Contains(f.Id)).ExecuteDeleteAsync(ct);
        keys.ForEach(_storage.Delete);
    }

    private async Task _PurgeDocuments(List<long> documentIds, CancellationToken ct)
    {
        if (documentIds.Count == 0)
            return;

        var keys = await _db.DocumentVersions.Where(v => documentIds.Contains(v.DocumentId)).Select(v => v.StorageKey).Distinct().ToListAsync(ct);
        await _db.Documents.Where(d => documentIds.Contains(d.Id)).ExecuteDeleteAsync(ct);
        keys.ForEach(_storage.Delete);
    }

    private static ApiException _NotInTrash() =>
        new("Not in trash", "Élément introuvable dans la corbeille.", ApiException.TypeError.RESSOURCE_NOT_FOUND, "DMS_040");

    private static ApiException _ParentDeleted() =>
        new("Parent deleted", "Le dossier parent est lui aussi dans la corbeille : restaurez-le d'abord.", ApiException.TypeError.CONFLICT, "DMS_041");
}
