using dependancies.Global;
using dependancies.SharedClasses;
using Microsoft.EntityFrameworkCore;
using sideral_context;
using sideral_context.Dms;
using sideral_context.Users;
using sideral_jobcode.Storage;

namespace sideral_jobcode.Dms;

/// <summary>
/// GED : documents, versions et extraction (checkout).
/// Un document extrait ne peut être modifié que par l'utilisateur qui l'a extrait ; l'archivage (checkin) crée
/// une nouvelle version et libère l'extraction. Déposer une nouvelle version sans extraction préalable est permis
/// si personne d'autre n'a extrait le document.
/// </summary>
public class DocumentManager : IScoped
{
    private readonly SideralDbContext _db;
    private readonly DmsAccess _access;
    private readonly DmsNames _names;
    private readonly DmsActivityLog _activity;
    private readonly DocumentStorage _storage;
    private readonly DocumentContentLinks _links;

    public DocumentManager(SideralDbContext db, DmsAccess access, DmsNames names, DmsActivityLog activity, DocumentStorage storage, DocumentContentLinks links)
    {
        _db = db;
        _access = access;
        _names = names;
        _activity = activity;
        _storage = storage;
        _links = links;
    }

    public async Task<DocumentView> Get(long userId, bool isAdmin, long documentId, CancellationToken ct = default)
    {
        var tree = await _access.Load(userId, isAdmin, ct);
        var document = await _db.Documents
            .AsNoTracking()
            .Where(d => d.Id == documentId && d.DeletedOn == null)
            .Select(d => new
            {
                d.Id,
                d.FolderId,
                d.Name,
                d.Description,
                d.CreatedOn,
                d.UpdatedOn,
                d.CheckedOutById,
                d.CheckedOutOn,
                CreatedBy = new UserRefView(d.CreatedBy!.Id, d.CreatedBy.Firstname, d.CreatedBy.Lastname, d.CreatedBy.Avatar),
                CheckedOutBy = d.CheckedOutBy == null ? null : new UserRefView(d.CheckedOutBy.Id, d.CheckedOutBy.Firstname, d.CheckedOutBy.Lastname, d.CheckedOutBy.Avatar),
                Versions = d.Versions
                    .OrderByDescending(v => v.Major).ThenByDescending(v => v.Minor)
                    .Select(v => new VersionView(v.Id, "", v.Major, v.Minor, v.OriginalName, v.ContentType, v.Size, v.Sha256, v.Comment, v.CreatedOn,
                        new UserRefView(v.CreatedBy!.Id, v.CreatedBy.Firstname, v.CreatedBy.Lastname, v.CreatedBy.Avatar)))
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (document is null || !tree.Can(document.FolderId, PermissionLevel.Read))
        {
            throw DmsAccess.DocumentNotFound(documentId);
        }

        var level = tree.Level(document.FolderId);
        return new DocumentView(
            document.Id,
            document.FolderId,
            document.Name,
            document.Description,
            document.CreatedOn,
            document.CreatedBy,
            document.UpdatedOn,
            document.CheckedOutBy,
            document.CheckedOutOn,
            level,
            document.CheckedOutById is { } owner && (owner == userId || level >= PermissionLevel.Manage),
            tree.Breadcrumb(document.FolderId).Select(n => new FolderRefView(n.Id, n.Name)).ToList(),
            document.Versions.Select(v => v with { Label = $"{v.Major}.{v.Minor}" }).ToList());
    }

    /// <summary>Ajoute un document (version 1.0) dans un dossier.</summary>
    public async Task<DocumentView> Upload(long userId, bool isAdmin, long folderId, Stream content, long length, string? fileName, string? description, CancellationToken ct = default)
    {
        var tree = await _access.Load(userId, isAdmin, ct);
        DmsAccess.Demand(tree, folderId, PermissionLevel.Write, "Vous devez être contributeur du dossier pour y ajouter des documents.");

        var name = DmsNames.FromFileName(fileName);
        await _names.EnsureAvailable(folderId, name, ct: ct);

        var stored = await _storage.Save(content, length, ct);
        try
        {
            var document = new Document
            {
                FolderId = folderId,
                Name = name,
                Description = _CleanDescription(description),
                CreatedById = userId,
                Versions = [_NewVersion(stored, name, 1, 0, null, userId)]
            };
            _db.Documents.Add(document);
            await _db.SaveChangesAsync(ct);

            _activity.Add(userId, DmsAction.DOCUMENT_CREATED, folderId, document.Id, "Version 1.0");
            await _db.SaveChangesAsync(ct);

            return await Get(userId, isAdmin, document.Id, ct);
        }
        catch
        {
            _storage.Delete(stored.StorageKey);
            throw;
        }
    }

    /// <summary>Renomme le document et/ou modifie sa description.</summary>
    public async Task<DocumentView> Update(long userId, bool isAdmin, long documentId, string name, string? description, CancellationToken ct = default)
    {
        var (_, document) = await _Load(userId, isAdmin, documentId, PermissionLevel.Write, "Vous devez être contributeur du dossier pour modifier ce document.", ct);
        _EnsureNotLockedByOther(document, userId);

        name = DmsNames.Validate(name);
        description = _CleanDescription(description);
        var changes = new List<string>();

        if (document.Name != name)
        {
            await _names.EnsureAvailable(document.FolderId, name, exceptDocumentId: documentId, ct: ct);
            changes.Add($"Renommé : « {document.Name} » → « {name} »");
            document.Name = name;
        }
        if (document.Description != description)
        {
            changes.Add("Description modifiée");
            document.Description = description;
        }

        if (changes.Count > 0)
        {
            document.UpdatedOn = DateTime.UtcNow;
            _activity.Add(userId, DmsAction.DOCUMENT_UPDATED, document.FolderId, documentId, string.Join(" ; ", changes));
            await _db.SaveChangesAsync(ct);
        }
        return await Get(userId, isAdmin, documentId, ct);
    }

    public async Task<DocumentView> Move(long userId, bool isAdmin, long documentId, long folderId, CancellationToken ct = default)
    {
        var (tree, document) = await _Load(userId, isAdmin, documentId, PermissionLevel.Manage, "Vous devez être gestionnaire du dossier pour en déplacer les documents.", ct);
        DmsAccess.Demand(tree, folderId, PermissionLevel.Write, "Vous devez être contributeur du dossier de destination.");
        _EnsureNotLockedByOther(document, userId);

        if (document.FolderId != folderId)
        {
            await _names.EnsureAvailable(folderId, document.Name, ct: ct);
            _activity.Add(userId, DmsAction.DOCUMENT_MOVED, folderId, documentId, $"De « {tree.PathOf(document.FolderId)} » vers « {tree.PathOf(folderId)} »");
            document.FolderId = folderId;
            document.UpdatedOn = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return await Get(userId, isAdmin, documentId, ct);
    }

    /// <summary>Envoie le document dans la corbeille.</summary>
    public async Task Delete(long userId, bool isAdmin, long documentId, CancellationToken ct = default)
    {
        var (_, document) = await _Load(userId, isAdmin, documentId, PermissionLevel.Manage, "Vous devez être gestionnaire du dossier pour supprimer ce document.", ct);
        if (document.CheckedOutById is not null)
        {
            throw new ApiException("Document checked out", "Le document est extrait : archivez-le ou annulez l'extraction avant de le supprimer.", ApiException.TypeError.CONFLICT, "DMS_021");
        }

        document.DeletedOn = DateTime.UtcNow;
        document.DeletedById = userId;
        _activity.Add(userId, DmsAction.DOCUMENT_DELETED, document.FolderId, documentId);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Extrait le document : personne d'autre ne peut le modifier jusqu'à l'archivage ou l'annulation.</summary>
    public async Task<DocumentView> Checkout(long userId, bool isAdmin, long documentId, CancellationToken ct = default)
    {
        var (_, document) = await _Load(userId, isAdmin, documentId, PermissionLevel.Write, "Vous devez être contributeur du dossier pour extraire ce document.", ct);
        if (document.CheckedOutById == userId)
        {
            return await Get(userId, isAdmin, documentId, ct);
        }
        _EnsureNotLockedByOther(document, userId);

        // Mise à jour conditionnelle : deux extractions simultanées ne peuvent pas réussir toutes les deux
        var now = DateTime.UtcNow;
        var updated = await _db.Documents
            .Where(d => d.Id == documentId && d.CheckedOutById == null)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.CheckedOutById, userId).SetProperty(d => d.CheckedOutOn, now), ct);
        if (updated == 0)
        {
            throw new ApiException("Document checked out", "Le document vient d'être extrait par un autre utilisateur.", ApiException.TypeError.CONFLICT, "DMS_020");
        }

        _activity.Add(userId, DmsAction.DOCUMENT_CHECKED_OUT, document.FolderId, documentId);
        await _db.SaveChangesAsync(ct);
        return await Get(userId, isAdmin, documentId, ct);
    }

    /// <summary>Annule l'extraction : par l'utilisateur qui a extrait le document, un gestionnaire du dossier ou un administrateur.</summary>
    public async Task<DocumentView> CancelCheckout(long userId, bool isAdmin, long documentId, CancellationToken ct = default)
    {
        var (tree, document) = await _Load(userId, isAdmin, documentId, PermissionLevel.Read, string.Empty, ct);
        if (document.CheckedOutById is not { } owner)
        {
            return await Get(userId, isAdmin, documentId, ct);
        }
        if (owner != userId && tree.Level(document.FolderId) < PermissionLevel.Manage)
        {
            throw new ApiException("Cannot cancel checkout", "Seul l'utilisateur ayant extrait le document ou un gestionnaire du dossier peut annuler l'extraction.", ApiException.TypeError.FORBIDDEN, "DMS_022");
        }

        _activity.Add(userId, DmsAction.DOCUMENT_CHECKOUT_CANCELLED, document.FolderId, documentId,
            owner != userId ? $"Extraction de {document.CheckedOutBy!.Firstname} {document.CheckedOutBy.Lastname} annulée" : null);
        document.CheckedOutById = null;
        document.CheckedOutOn = null;
        await _db.SaveChangesAsync(ct);
        return await Get(userId, isAdmin, documentId, ct);
    }

    /// <summary>Archive (checkin) : enregistre une nouvelle version, mineure (x.y+1) ou majeure (x+1.0), et libère l'extraction.</summary>
    public async Task<DocumentView> Checkin(long userId, bool isAdmin, long documentId, Stream content, long length, string? fileName, bool major, string? comment, CancellationToken ct = default)
    {
        var (_, document) = await _Load(userId, isAdmin, documentId, PermissionLevel.Write, "Vous devez être contributeur du dossier pour déposer une nouvelle version.", ct);
        _EnsureNotLockedByOther(document, userId);

        var current = await _CurrentVersion(documentId, ct);
        var (newMajor, newMinor) = major ? (current.Major + 1, 0) : (current.Major, current.Minor + 1);
        comment = _CleanComment(comment);

        var stored = await _storage.Save(content, length, ct);
        try
        {
            var version = _NewVersion(stored, DmsNames.FromFileName(fileName), newMajor, newMinor, comment, userId);
            version.DocumentId = documentId;
            _db.DocumentVersions.Add(version);
            _Release(document);
            _activity.Add(userId, DmsAction.DOCUMENT_CHECKED_IN, document.FolderId, documentId, _VersionDetails(newMajor, newMinor, comment));
            await _SaveVersion(ct);
        }
        catch
        {
            _storage.Delete(stored.StorageKey);
            throw;
        }
        return await Get(userId, isAdmin, documentId, ct);
    }

    /// <summary>Restaure une ancienne version : crée une nouvelle version mineure avec le même contenu.</summary>
    public async Task<DocumentView> RestoreVersion(long userId, bool isAdmin, long documentId, long versionId, CancellationToken ct = default)
    {
        var (_, document) = await _Load(userId, isAdmin, documentId, PermissionLevel.Write, "Vous devez être contributeur du dossier pour restaurer une version.", ct);
        _EnsureNotLockedByOther(document, userId);

        var source = await _db.DocumentVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == versionId && v.DocumentId == documentId, ct)
            ?? throw _VersionNotFound(versionId);
        var current = await _CurrentVersion(documentId, ct);
        if (current.Id == source.Id)
        {
            throw new ApiException("Already current", "Cette version est déjà la version courante.", ApiException.TypeError.BAD_REQUEST, "DMS_024");
        }

        var comment = $"Restauration de la version {source.Label}";
        _db.DocumentVersions.Add(new DocumentVersion
        {
            DocumentId = documentId,
            Major = current.Major,
            Minor = current.Minor + 1,
            StorageKey = source.StorageKey,
            OriginalName = source.OriginalName,
            ContentType = source.ContentType,
            Size = source.Size,
            Sha256 = source.Sha256,
            Comment = comment,
            CreatedById = userId
        });
        _Release(document);
        _activity.Add(userId, DmsAction.VERSION_RESTORED, document.FolderId, documentId, _VersionDetails(current.Major, current.Minor + 1, comment));
        await _SaveVersion(ct);
        return await Get(userId, isAdmin, documentId, ct);
    }

    /// <summary>Lien signé vers le contenu d'une version (versionId null = version courante).</summary>
    public async Task<ContentLinkView> CreateContentLink(long userId, bool isAdmin, long documentId, long? versionId, bool download, CancellationToken ct = default)
    {
        await _Load(userId, isAdmin, documentId, PermissionLevel.Read, string.Empty, ct);
        var id = versionId is { } vid
            ? await _db.DocumentVersions.Where(v => v.Id == vid && v.DocumentId == documentId).Select(v => (long?)v.Id).FirstOrDefaultAsync(ct) ?? throw _VersionNotFound(vid)
            : (await _CurrentVersion(documentId, ct)).Id;
        return _links.Create(id, userId, download);
    }

    /// <summary>Résout un lien signé : vérifie à nouveau que l'utilisateur est actif et a accès au document.</summary>
    public async Task<DocumentContent> OpenContent(string? token, CancellationToken ct = default)
    {
        var claims = _links.Read(token);

        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == claims.UserId && u.IsActive && !u.Deleted)
            .Select(u => new { IsAdmin = u.Roles.Any(r => r.Key == Role.ADMIN) })
            .FirstOrDefaultAsync(ct)
            ?? throw new ApiException("Invalid content link", "Lien expiré ou invalide : rechargez la page.", ApiException.TypeError.UNAUTHORIZED, "DMS_030");

        var version = await _db.DocumentVersions
            .AsNoTracking()
            .Where(v => v.Id == claims.VersionId && v.Document!.DeletedOn == null)
            .Select(v => new { v.StorageKey, v.ContentType, v.Major, v.Minor, v.DocumentId, v.Document!.FolderId, v.Document.Name })
            .FirstOrDefaultAsync(ct)
            ?? throw _VersionNotFound(claims.VersionId);

        var tree = await _access.Load(claims.UserId, user.IsAdmin, ct);
        if (!tree.Can(version.FolderId, PermissionLevel.Read))
        {
            throw DmsAccess.DocumentNotFound(version.DocumentId);
        }

        var path = _storage.Find(version.StorageKey)
            ?? throw new ApiException("File missing", "Le fichier est introuvable sur le serveur.", ApiException.TypeError.RESSOURCE_NOT_FOUND, "DMS_031");

        if (claims.Download)
        {
            _activity.Add(claims.UserId, DmsAction.DOCUMENT_DOWNLOADED, version.FolderId, version.DocumentId, $"Version {version.Major}.{version.Minor}");
            await _db.SaveChangesAsync(ct);
        }

        var inline = !claims.Download && DocumentStorage.PREVIEWABLE_TYPES.Contains(version.ContentType);
        return new DocumentContent(path, version.ContentType, version.Name, inline);
    }

    public async Task<PaginatedResult<ActivityView>> GetActivity(long userId, bool isAdmin, long documentId, Pagination pagination, CancellationToken ct = default)
    {
        await _Load(userId, isAdmin, documentId, PermissionLevel.Read, string.Empty, ct);
        return await _activity.ForDocument(documentId, pagination, ct);
    }

    /// <summary>Documents extraits par l'utilisateur.</summary>
    public async Task<List<CheckedOutDocumentView>> CheckedOutByMe(long userId, bool isAdmin, CancellationToken ct = default)
    {
        var tree = await _access.Load(userId, isAdmin, ct);
        var folderIds = await _db.Documents
            .Where(d => d.CheckedOutById == userId && d.DeletedOn == null)
            .Select(d => new { d.Id, d.FolderId })
            .ToDictionaryAsync(d => d.Id, d => d.FolderId, ct);

        var items = await QueryItems(_db.Documents.Where(d => d.CheckedOutById == userId && d.DeletedOn == null), ct);
        return items
            .Where(i => tree.Can(folderIds[i.Id], PermissionLevel.Read))
            .OrderByDescending(i => i.CheckedOutOn)
            .Select(i => new CheckedOutDocumentView(i, folderIds[i.Id], tree.PathOf(folderIds[i.Id])))
            .ToList();
    }

    /// <summary>Projection des documents avec leur version courante (utilisée aussi par la navigation).</summary>
    public static async Task<List<DocumentItemView>> QueryItems(IQueryable<Document> query, CancellationToken ct)
    {
        var rows = await query
            .AsNoTracking()
            .Select(d => new
            {
                d.Id,
                d.Name,
                d.Description,
                d.CheckedOutOn,
                CheckedOutBy = d.CheckedOutBy == null ? null : new UserRefView(d.CheckedOutBy.Id, d.CheckedOutBy.Firstname, d.CheckedOutBy.Lastname, d.CheckedOutBy.Avatar),
                Current = d.Versions
                    .OrderByDescending(v => v.Major).ThenByDescending(v => v.Minor)
                    .Select(v => new
                    {
                        v.Major,
                        v.Minor,
                        v.Size,
                        v.ContentType,
                        v.CreatedOn,
                        CreatedBy = new UserRefView(v.CreatedBy!.Id, v.CreatedBy.Firstname, v.CreatedBy.Lastname, v.CreatedBy.Avatar)
                    })
                    .First()
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new DocumentItemView(
                r.Id, r.Name, r.Description, $"{r.Current.Major}.{r.Current.Minor}", r.Current.Size, r.Current.ContentType,
                r.Current.CreatedOn, r.Current.CreatedBy, r.CheckedOutBy, r.CheckedOutOn))
            .ToList();
    }

    private async Task<(FolderTree Tree, Document Document)> _Load(long userId, bool isAdmin, long documentId, PermissionLevel level, string forbiddenMessage, CancellationToken ct)
    {
        var tree = await _access.Load(userId, isAdmin, ct);
        var document = await _db.Documents
            .Include(d => d.CheckedOutBy)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.DeletedOn == null, ct);

        if (document is null || !tree.Can(document.FolderId, PermissionLevel.Read))
        {
            throw DmsAccess.DocumentNotFound(documentId);
        }
        DmsAccess.Demand(tree, document.FolderId, level, forbiddenMessage);
        return (tree, document);
    }

    private static void _EnsureNotLockedByOther(Document document, long userId)
    {
        if (document.CheckedOutById is { } owner && owner != userId)
        {
            var by = document.CheckedOutBy is { } u ? $"{u.Firstname} {u.Lastname}" : "un autre utilisateur";
            throw new ApiException("Document checked out", $"Le document est extrait par {by} : il ne peut pas être modifié avant son archivage ou l'annulation de l'extraction.", ApiException.TypeError.CONFLICT, "DMS_020");
        }
    }

    private static void _Release(Document document)
    {
        document.CheckedOutById = null;
        document.CheckedOutOn = null;
        document.UpdatedOn = DateTime.UtcNow;
    }

    private async Task<DocumentVersion> _CurrentVersion(long documentId, CancellationToken ct) =>
        await _db.DocumentVersions
            .AsNoTracking()
            .Where(v => v.DocumentId == documentId)
            .OrderByDescending(v => v.Major).ThenByDescending(v => v.Minor)
            .FirstAsync(ct);

    /// <summary>L'index unique (document, major, minor) rejette deux nouvelles versions simultanées.</summary>
    private async Task _SaveVersion(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            throw new ApiException("Concurrent version", "Une autre version vient d'être déposée : rechargez le document et réessayez.", ApiException.TypeError.CONFLICT, "DMS_023", ex);
        }
    }

    private static DocumentVersion _NewVersion(StoredFile stored, string originalName, int major, int minor, string? comment, long userId) => new()
    {
        Major = major,
        Minor = minor,
        StorageKey = stored.StorageKey,
        OriginalName = originalName,
        ContentType = stored.ContentType,
        Size = stored.Size,
        Sha256 = stored.Sha256,
        Comment = comment,
        CreatedById = userId
    };

    private static string _VersionDetails(int major, int minor, string? comment) =>
        comment is null ? $"Version {major}.{minor}" : $"Version {major}.{minor} — {comment}";

    private static string? _CleanDescription(string? description)
    {
        description = description?.Trim();
        if (description?.Length > Document.DESCRIPTION_MAX_LENGTH)
        {
            throw new ApiException("Description too long", $"La description ne peut pas dépasser {Document.DESCRIPTION_MAX_LENGTH} caractères.", ApiException.TypeError.MODEL_ERROR, "DMS_007");
        }
        return string.IsNullOrEmpty(description) ? null : description;
    }

    private static string? _CleanComment(string? comment)
    {
        comment = comment?.Trim();
        if (comment?.Length > DocumentVersion.COMMENT_MAX_LENGTH)
        {
            throw new ApiException("Comment too long", $"Le commentaire ne peut pas dépasser {DocumentVersion.COMMENT_MAX_LENGTH} caractères.", ApiException.TypeError.MODEL_ERROR, "DMS_008");
        }
        return string.IsNullOrEmpty(comment) ? null : comment;
    }

    private static ApiException _VersionNotFound(long versionId) =>
        new("Version not found", $"Version {versionId} introuvable.", ApiException.TypeError.RESSOURCE_NOT_FOUND, "DMS_025");
}
