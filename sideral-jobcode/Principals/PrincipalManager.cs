using dependancies.SharedClasses;
using Microsoft.EntityFrameworkCore;
using sideral_context;

namespace sideral_jobcode.Principals;

/// <summary>Recherche d'utilisateurs et de groupes, accessible à tout utilisateur connecté (attribution de droits, membres).</summary>
public class PrincipalManager : IScoped
{
    public const int MAX_RESULTS = 20;

    private readonly SideralDbContext _db;

    public PrincipalManager(SideralDbContext db)
    {
        _db = db;
    }

    public async Task<List<PrincipalView>> Search(string? search, bool includeUsers, bool includeGroups, CancellationToken ct = default)
    {
        var term = search?.Trim().ToLower() ?? string.Empty;
        var result = new List<PrincipalView>();

        if (includeGroups)
        {
            var groups = await _db.Groups
                .AsNoTracking()
                .Where(g => term == "" || g.Name.ToLower().Contains(term))
                .OrderBy(g => g.Name)
                .Take(MAX_RESULTS)
                .Select(g => new { g.Id, g.Name, MemberCount = g.Members.Count(m => !m.Deleted) })
                .ToListAsync(ct);
            result.AddRange(groups.Select(g => PrincipalView.Group(g.Id, g.Name, g.MemberCount)));
        }

        if (includeUsers)
        {
            var users = await _db.Users
                .AsNoTracking()
                .Where(u => !u.Deleted && u.IsActive)
                .Where(u => term == "" ||
                    u.Firstname.ToLower().Contains(term) ||
                    u.Lastname.ToLower().Contains(term) ||
                    (u.Firstname + " " + u.Lastname).ToLower().Contains(term) ||
                    u.Login.ToLower().Contains(term))
                .OrderBy(u => u.Lastname).ThenBy(u => u.Firstname)
                .Take(MAX_RESULTS)
                .Select(u => new { u.Id, u.Firstname, u.Lastname, u.JobTitle, u.Avatar })
                .ToListAsync(ct);
            result.AddRange(users.Select(u => PrincipalView.User(u.Id, u.Firstname, u.Lastname, u.JobTitle, u.Avatar)));
        }

        return result;
    }
}
