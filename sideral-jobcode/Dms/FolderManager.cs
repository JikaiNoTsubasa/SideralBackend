using dependancies.Global;
using dependancies.SharedClasses;
using Microsoft.EntityFrameworkCore;
using sideral_context;
using sideral_context.Dms;
using sideral_jobcode.Principals;

namespace sideral_jobcode.Dms;

/// <summary>GED : navigation, dossiers et droits d'accès.</summary>
public class FolderManager : IScoped
{
    private readonly SideralDbContext _db;
    private readonly DmsAccess _access;
    private readonly DmsNames _names;
    private readonly DmsActivityLog _activity;

    public FolderManager(SideralDbContext db, DmsAccess access, DmsNames names, DmsActivityLog activity)
    {
        _db = db;
        _access = access;
        _names = names;
        _activity = activity;
    }

    /// <summary>Contenu d'un dossier (folderId null = racine) : sous-dossiers visibles et documents.</summary>
    public async Task<FolderContentView> Browse(long userId, bool isAdmin, long? folderId, CancellationToken ct = default)
    {
        var tree = await _access.Load(userId, isAdmin, ct);

        if (folderId is not { } id)
        {
            var roots = await _LoadFolderItems(tree, tree.VisibleChildren(null).Select(n => n.Id).ToList(), ct);
            return new FolderContentView(null, true, [], isAdmin ? PermissionLevel.Manage : PermissionLevel.Read,
                CanCreateFolder: isAdmin, CanUpload: false, roots, []);
        }

        DmsAccess.Demand(tree, id, PermissionLevel.Read, string.Empty);
        var node = tree.Find(id)!;
        var level = tree.Level(id);

        var folders = await _LoadFolderItems(tree, tree.VisibleChildren(id).Select(n => n.Id).ToList(), ct);
        var documents = await DocumentManager.QueryItems(_db.Documents.Where(d => d.FolderId == id && d.DeletedOn == null), ct);

        return new FolderContentView(
            new FolderRefView(node.Id, node.Name),
            node.InheritPermissions,
            tree.Breadcrumb(id).Select(n => new FolderRefView(n.Id, n.Name)).ToList(),
            level,
            CanCreateFolder: level >= PermissionLevel.Manage,
            CanUpload: level >= PermissionLevel.Write,
            folders,
            documents.OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase).ToList());
    }

    public async Task<FolderRefView> Create(long userId, bool isAdmin, long? parentId, string name, CancellationToken ct = default)
    {
        name = DmsNames.Validate(name);
        var tree = await _access.Load(userId, isAdmin, ct);

        if (parentId is { } pid)
        {
            DmsAccess.Demand(tree, pid, PermissionLevel.Manage, "Vous devez être gestionnaire du dossier pour y créer un sous-dossier.");
        }
        else if (!isAdmin)
        {
            throw new ApiException("Admin only", "Seul un administrateur peut créer un dossier à la racine.", ApiException.TypeError.FORBIDDEN, "DMS_013");
        }

        await _names.EnsureAvailable(parentId, name, ct: ct);

        var folder = new Folder { Name = name, ParentId = parentId, CreatedById = userId };
        _db.Folders.Add(folder);
        await _db.SaveChangesAsync(ct);

        _activity.Add(userId, DmsAction.FOLDER_CREATED, folder.Id, details: parentId is { } p ? $"Dans « {tree.PathOf(p)} »" : "À la racine");
        await _db.SaveChangesAsync(ct);

        return new FolderRefView(folder.Id, folder.Name);
    }

    public async Task<FolderRefView> Rename(long userId, bool isAdmin, long folderId, string name, CancellationToken ct = default)
    {
        name = DmsNames.Validate(name);
        var tree = await _access.Load(userId, isAdmin, ct);
        DmsAccess.Demand(tree, folderId, PermissionLevel.Manage, "Vous devez être gestionnaire du dossier pour le renommer.");

        var folder = await _db.Folders.FirstAsync(f => f.Id == folderId, ct);
        if (folder.Name != name)
        {
            await _names.EnsureAvailable(folder.ParentId, name, exceptFolderId: folderId, ct: ct);
            _activity.Add(userId, DmsAction.FOLDER_RENAMED, folderId, details: $"« {folder.Name} » → « {name} »");
            folder.Name = name;
            folder.UpdatedOn = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return new FolderRefView(folder.Id, folder.Name);
    }

    /// <summary>Déplace un dossier (parentId null = racine, administrateurs uniquement).</summary>
    public async Task<FolderRefView> Move(long userId, bool isAdmin, long folderId, long? parentId, CancellationToken ct = default)
    {
        var tree = await _access.Load(userId, isAdmin, ct);
        DmsAccess.Demand(tree, folderId, PermissionLevel.Manage, "Vous devez être gestionnaire du dossier pour le déplacer.");

        if (parentId is { } pid)
        {
            DmsAccess.Demand(tree, pid, PermissionLevel.Manage, "Vous devez être gestionnaire du dossier de destination.");
            if (tree.SelfAndDescendants(folderId).Contains(pid))
            {
                throw new ApiException("Invalid move", "Impossible de déplacer un dossier dans lui-même ou dans l'un de ses sous-dossiers.", ApiException.TypeError.BAD_REQUEST, "DMS_014");
            }
        }
        else if (!isAdmin)
        {
            throw new ApiException("Admin only", "Seul un administrateur peut déplacer un dossier à la racine.", ApiException.TypeError.FORBIDDEN, "DMS_013");
        }

        var folder = await _db.Folders.FirstAsync(f => f.Id == folderId, ct);
        if (folder.ParentId != parentId)
        {
            await _names.EnsureAvailable(parentId, folder.Name, exceptFolderId: folderId, ct: ct);
            var from = folder.ParentId is { } oldParent ? tree.PathOf(oldParent) : "Racine";
            var to = parentId is { } newParent ? tree.PathOf(newParent) : "Racine";
            _activity.Add(userId, DmsAction.FOLDER_MOVED, folderId, details: $"De « {from} » vers « {to} »");
            folder.ParentId = parentId;
            folder.UpdatedOn = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return new FolderRefView(folder.Id, folder.Name);
    }

    /// <summary>Envoie le dossier (et tout son contenu) dans la corbeille.</summary>
    public async Task Delete(long userId, bool isAdmin, long folderId, CancellationToken ct = default)
    {
        var tree = await _access.Load(userId, isAdmin, ct);
        DmsAccess.Demand(tree, folderId, PermissionLevel.Manage, "Vous devez être gestionnaire du dossier pour le supprimer.");

        var ids = tree.SelfAndDescendants(folderId);
        if (await _db.Documents.AnyAsync(d => ids.Contains(d.FolderId) && d.DeletedOn == null && d.CheckedOutById != null, ct))
        {
            throw new ApiException("Checked out documents", "Le dossier contient des documents extraits : ils doivent être archivés ou leur extraction annulée avant la suppression.", ApiException.TypeError.CONFLICT, "DMS_015");
        }

        var folder = await _db.Folders.FirstAsync(f => f.Id == folderId, ct);
        folder.DeletedOn = DateTime.UtcNow;
        folder.DeletedById = userId;
        _activity.Add(userId, DmsAction.FOLDER_DELETED, folderId);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Droits propres du dossier et droits hérités des dossiers parents (visible par les lecteurs).</summary>
    public async Task<FolderPermissionsView> GetPermissions(long userId, bool isAdmin, long folderId, CancellationToken ct = default)
    {
        var tree = await _access.Load(userId, isAdmin, ct);
        DmsAccess.Demand(tree, folderId, PermissionLevel.Read, string.Empty);
        var node = tree.Find(folderId)!;

        // Chaîne d'héritage : on remonte tant que le dossier hérite de son parent
        var chain = new List<FolderNode>();
        for (var current = node; current.InheritPermissions && current.ParentId is { } p && tree.Find(p) is { } parent; current = parent)
        {
            chain.Add(parent);
        }
        var chainIds = chain.Select(n => n.Id).ToList();

        var permissions = await _db.FolderPermissions
            .AsNoTracking()
            .Where(p => p.FolderId == folderId || chainIds.Contains(p.FolderId))
            .Where(p => p.User == null || !p.User.Deleted)
            .Select(p => new
            {
                p.FolderId,
                p.Level,
                User = p.User == null ? null : new { p.User.Id, p.User.Firstname, p.User.Lastname, p.User.JobTitle, p.User.Avatar },
                Group = p.Group == null ? null : new { p.Group.Id, p.Group.Name, MemberCount = p.Group.Members.Count }
            })
            .ToListAsync(ct);

        var entries = new List<PermissionEntryView>();
        var inherited = new List<InheritedPermissionView>();
        foreach (var p in permissions)
        {
            var principal = p.User is { } u
                ? PrincipalView.User(u.Id, u.Firstname, u.Lastname, u.JobTitle, u.Avatar)
                : PrincipalView.Group(p.Group!.Id, p.Group.Name, p.Group.MemberCount);

            if (p.FolderId == folderId)
            {
                entries.Add(new PermissionEntryView(principal, p.Level));
            }
            else
            {
                var from = tree.Find(p.FolderId)!;
                inherited.Add(new InheritedPermissionView(principal, p.Level, new FolderRefView(from.Id, from.Name)));
            }
        }

        return new FolderPermissionsView(
            folderId,
            node.ParentId is null,
            node.InheritPermissions,
            tree.Level(folderId) >= PermissionLevel.Manage,
            entries.OrderBy(e => e.Principal.Type).ThenBy(e => e.Principal.Name, StringComparer.CurrentCultureIgnoreCase).ToList(),
            inherited.OrderBy(e => chainIds.IndexOf(e.From.Id)).ThenBy(e => e.Principal.Name, StringComparer.CurrentCultureIgnoreCase).ToList());
    }

    /// <summary>Remplace les droits propres du dossier.</summary>
    public async Task<FolderPermissionsView> SetPermissions(long userId, bool isAdmin, long folderId, bool inheritPermissions, IReadOnlyCollection<PermissionEntryInput> entries, CancellationToken ct = default)
    {
        var tree = await _access.Load(userId, isAdmin, ct);
        DmsAccess.Demand(tree, folderId, PermissionLevel.Manage, "Vous devez être gestionnaire du dossier pour modifier ses droits.");

        foreach (var entry in entries)
        {
            if ((entry.UserId is null) == (entry.GroupId is null))
            {
                throw new ApiException("Invalid permission", "Chaque droit doit concerner un utilisateur ou un groupe.", ApiException.TypeError.MODEL_ERROR, "DMS_016");
            }
            if (!Enum.IsDefined(entry.Level) || entry.Level == PermissionLevel.None)
            {
                throw new ApiException("Invalid permission level", "Niveau de droit invalide.", ApiException.TypeError.MODEL_ERROR, "DMS_017");
            }
        }

        var userIds = entries.Where(e => e.UserId is not null).Select(e => e.UserId!.Value).ToList();
        var groupIds = entries.Where(e => e.GroupId is not null).Select(e => e.GroupId!.Value).ToList();
        if (userIds.Count != userIds.Distinct().Count() || groupIds.Count != groupIds.Distinct().Count())
        {
            throw new ApiException("Duplicate permission", "Un utilisateur ou un groupe ne peut apparaître qu'une fois.", ApiException.TypeError.MODEL_ERROR, "DMS_018");
        }
        if (await _db.Users.CountAsync(u => userIds.Contains(u.Id) && !u.Deleted, ct) != userIds.Count ||
            await _db.Groups.CountAsync(g => groupIds.Contains(g.Id), ct) != groupIds.Count)
        {
            throw new ApiException("Unknown principal", "Utilisateur ou groupe introuvable.", ApiException.TypeError.MODEL_ERROR, "DMS_019");
        }

        var folder = await _db.Folders.Include(f => f.Permissions).FirstAsync(f => f.Id == folderId, ct);
        _db.FolderPermissions.RemoveRange(folder.Permissions);
        foreach (var entry in entries)
        {
            _db.FolderPermissions.Add(new FolderPermission { FolderId = folderId, UserId = entry.UserId, GroupId = entry.GroupId, Level = entry.Level });
        }
        folder.InheritPermissions = folder.ParentId is null || inheritPermissions;
        folder.UpdatedOn = DateTime.UtcNow;

        _activity.Add(userId, DmsAction.PERMISSIONS_CHANGED, folderId,
            details: $"{entries.Count} droit(s) propre(s), héritage {(folder.InheritPermissions ? "activé" : "désactivé")}");
        await _db.SaveChangesAsync(ct);

        return await GetPermissions(userId, isAdmin, folderId, ct);
    }

    public async Task<PaginatedResult<ActivityView>> GetActivity(long userId, bool isAdmin, long folderId, Pagination pagination, CancellationToken ct = default)
    {
        var tree = await _access.Load(userId, isAdmin, ct);
        DmsAccess.Demand(tree, folderId, PermissionLevel.Read, string.Empty);
        var visibleChildIds = tree.VisibleChildren(folderId).Select(n => n.Id).ToList();
        return await _activity.ForFolder(folderId, visibleChildIds, pagination, ct);
    }

    private async Task<List<FolderItemView>> _LoadFolderItems(FolderTree tree, List<long> ids, CancellationToken ct)
    {
        var items = await _db.Folders
            .AsNoTracking()
            .Where(f => ids.Contains(f.Id))
            .Select(f => new
            {
                f.Id,
                f.Name,
                f.CreatedOn,
                f.UpdatedOn,
                CreatedBy = new UserRefView(f.CreatedBy!.Id, f.CreatedBy.Firstname, f.CreatedBy.Lastname, f.CreatedBy.Avatar)
            })
            .ToListAsync(ct);

        return items
            .Select(f => new FolderItemView(f.Id, f.Name, f.CreatedOn, f.UpdatedOn, f.CreatedBy, tree.Level(f.Id)))
            .OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
