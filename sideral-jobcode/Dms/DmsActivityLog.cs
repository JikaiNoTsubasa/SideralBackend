using System.Linq.Expressions;
using dependancies.Global;
using dependancies.SharedClasses;
using Microsoft.EntityFrameworkCore;
using sideral_context;
using sideral_context.Dms;

namespace sideral_jobcode.Dms;

/// <summary>Journal d'activité de la GED. Add() n'enregistre pas : l'entrée part avec le SaveChanges de l'action.</summary>
public class DmsActivityLog : IScoped
{
    private readonly SideralDbContext _db;

    public DmsActivityLog(SideralDbContext db)
    {
        _db = db;
    }

    public void Add(long userId, DmsAction action, long folderId, long? documentId = null, string? details = null)
    {
        _db.DmsActivities.Add(new DmsActivity
        {
            UserId = userId,
            Action = action,
            FolderId = folderId,
            DocumentId = documentId,
            Details = details is { Length: > DmsActivity.DETAILS_MAX_LENGTH } ? details[..DmsActivity.DETAILS_MAX_LENGTH] : details
        });
    }

    /// <summary>Activité d'un dossier : le dossier lui-même, ses documents et les actions sur ses sous-dossiers visibles.</summary>
    public async Task<PaginatedResult<ActivityView>> ForFolder(long folderId, IReadOnlyCollection<long> visibleChildIds, Pagination pagination, CancellationToken ct = default)
    {
        return await _db.DmsActivities
            .AsNoTracking()
            .Where(a => a.FolderId == folderId || (a.DocumentId == null && visibleChildIds.Contains(a.FolderId)))
            .OrderByDescending(a => a.CreatedOn).ThenByDescending(a => a.Id)
            .Select(_ToView)
            .ToPaginatedAsync(pagination, ct);
    }

    public async Task<PaginatedResult<ActivityView>> ForDocument(long documentId, Pagination pagination, CancellationToken ct = default)
    {
        return await _db.DmsActivities
            .AsNoTracking()
            .Where(a => a.DocumentId == documentId)
            .OrderByDescending(a => a.CreatedOn).ThenByDescending(a => a.Id)
            .Select(_ToView)
            .ToPaginatedAsync(pagination, ct);
    }

    private static readonly Expression<Func<DmsActivity, ActivityView>> _ToView = a => new ActivityView(
        a.Id,
        a.Action,
        a.Details,
        a.CreatedOn,
        new UserRefView(a.User!.Id, a.User.Firstname, a.User.Lastname, a.User.Avatar),
        a.FolderId,
        a.Folder!.Name,
        a.DocumentId,
        a.Document != null ? a.Document.Name : null);
}
