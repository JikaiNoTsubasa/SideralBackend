using dependancies.Global;
using dependancies.SharedClasses;
using Microsoft.EntityFrameworkCore;
using sideral_context;
using sideral_context.Groups;
using sideral_jobcode.Principals;

namespace sideral_jobcode.Groups;

/// <summary>Groupes d'utilisateurs (administration).</summary>
public class GroupManager : IScoped
{
    private readonly SideralDbContext _db;

    public GroupManager(SideralDbContext db)
    {
        _db = db;
    }

    public async Task<PaginatedResult<GroupView>> GetAll(Pagination pagination, string? search, CancellationToken ct = default)
    {
        var term = search?.Trim().ToLower();
        return await _db.Groups
            .AsNoTracking()
            .Where(!string.IsNullOrEmpty(term), g => g.Name.ToLower().Contains(term!) || (g.Description != null && g.Description.ToLower().Contains(term!)))
            .OrderBy(g => g.Name)
            .Select(g => new GroupView(g.Id, g.Name, g.Description, g.Members.Count(m => !m.Deleted), g.CreatedOn))
            .ToPaginatedAsync(pagination, ct);
    }

    public async Task<GroupDetailView> Get(long id, CancellationToken ct = default)
    {
        var group = await _db.Groups
            .AsNoTracking()
            .Where(g => g.Id == id)
            .Select(g => new
            {
                g.Id,
                g.Name,
                g.Description,
                g.CreatedOn,
                Members = g.Members
                    .Where(m => !m.Deleted)
                    .OrderBy(m => m.Lastname).ThenBy(m => m.Firstname)
                    .Select(m => new { m.Id, m.Firstname, m.Lastname, m.JobTitle, m.Avatar })
                    .ToList()
            })
            .FirstOrDefaultAsync(ct)
            ?? throw _NotFound(id);

        return new GroupDetailView(group.Id, group.Name, group.Description, group.CreatedOn,
            group.Members.Select(m => PrincipalView.User(m.Id, m.Firstname, m.Lastname, m.JobTitle, m.Avatar)).ToList());
    }

    public async Task<GroupDetailView> Create(string name, string? description, IReadOnlyCollection<long> memberIds, CancellationToken ct = default)
    {
        name = await _CheckName(name, null, ct);
        var group = new Group { Name = name, Description = _Clean(description), Members = await _ResolveMembers(memberIds, ct) };
        _db.Groups.Add(group);
        await _db.SaveChangesAsync(ct);
        return await Get(group.Id, ct);
    }

    public async Task<GroupDetailView> Update(long id, string name, string? description, IReadOnlyCollection<long> memberIds, CancellationToken ct = default)
    {
        var group = await _db.Groups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == id, ct) ?? throw _NotFound(id);
        group.Name = await _CheckName(name, id, ct);
        group.Description = _Clean(description);
        group.UpdatedOn = DateTime.UtcNow;

        var members = await _ResolveMembers(memberIds, ct);
        group.Members.Clear();
        foreach (var member in members)
        {
            group.Members.Add(member);
        }

        await _db.SaveChangesAsync(ct);
        return await Get(id, ct);
    }

    /// <summary>Supprime le groupe (les droits accordés au groupe sont supprimés en cascade).</summary>
    public async Task Delete(long id, CancellationToken ct = default)
    {
        if (await _db.Groups.Where(g => g.Id == id).ExecuteDeleteAsync(ct) == 0)
        {
            throw _NotFound(id);
        }
    }

    private async Task<string> _CheckName(string name, long? exceptId, CancellationToken ct)
    {
        name = name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            throw new ApiException("Empty name", "Le nom du groupe est obligatoire.", ApiException.TypeError.MODEL_ERROR, "GRP_002");
        }
        if (name.Length > Group.NAME_MAX_LENGTH)
        {
            throw new ApiException("Name too long", $"Le nom ne peut pas dépasser {Group.NAME_MAX_LENGTH} caractères.", ApiException.TypeError.MODEL_ERROR, "GRP_003");
        }
        var lower = name.ToLower();
        if (await _db.Groups.AnyAsync(g => g.Id != exceptId && g.Name.ToLower() == lower, ct))
        {
            throw new ApiException("Group already exists", $"Un groupe nommé « {name} » existe déjà.", ApiException.TypeError.RESSOURCE_ALREADY_EXISTS, "GRP_004");
        }
        return name;
    }

    private async Task<List<sideral_context.Users.User>> _ResolveMembers(IReadOnlyCollection<long> memberIds, CancellationToken ct)
    {
        var ids = memberIds.Distinct().ToList();
        var users = await _db.Users.Where(u => ids.Contains(u.Id) && !u.Deleted).ToListAsync(ct);
        if (users.Count != ids.Count)
        {
            throw new ApiException("Unknown user", "Un ou plusieurs membres sont introuvables.", ApiException.TypeError.MODEL_ERROR, "GRP_005");
        }
        return users;
    }

    private static string? _Clean(string? description)
    {
        description = description?.Trim();
        if (description?.Length > Group.DESCRIPTION_MAX_LENGTH)
        {
            throw new ApiException("Description too long", $"La description ne peut pas dépasser {Group.DESCRIPTION_MAX_LENGTH} caractères.", ApiException.TypeError.MODEL_ERROR, "GRP_006");
        }
        return string.IsNullOrEmpty(description) ? null : description;
    }

    private static ApiException _NotFound(long id) =>
        new("Group not found", $"Groupe {id} introuvable.", ApiException.TypeError.RESSOURCE_NOT_FOUND, "GRP_001");
}
