using dependancies.Global;
using dependancies.Security;
using dependancies.SharedClasses;
using Microsoft.EntityFrameworkCore;
using sideral_context;
using sideral_context.Users;
using sideral_jobcode.Auth;
using sideral_jobcode.Storage;

namespace sideral_jobcode.Users;

public class UserManager : IScoped
{
    public const int PASSWORD_MIN_LENGTH = 8;

    private readonly SideralDbContext _db;
    private readonly IHash _hash;
    private readonly AuthManager _authManager;
    private readonly AvatarStorage _avatarStorage;

    public UserManager(SideralDbContext db, IHash hash, AuthManager authManager, AvatarStorage avatarStorage)
    {
        _db = db;
        _hash = hash;
        _authManager = authManager;
        _avatarStorage = avatarStorage;
    }

    private IQueryable<User> _Users => _db.Users.Include(u => u.Roles).Where(u => !u.Deleted);

    public async Task<PaginatedResult<User>> GetAll(Pagination pagination, string? search, CancellationToken ct = default)
    {
        var term = search?.Trim().ToLower();

        return await _Users
            .AsNoTracking()
            .Where(!string.IsNullOrEmpty(term), u =>
                u.Login.ToLower().Contains(term!) ||
                u.Email.ToLower().Contains(term!) ||
                u.Firstname.ToLower().Contains(term!) ||
                u.Lastname.ToLower().Contains(term!))
            .OrderBy(u => u.Lastname).ThenBy(u => u.Firstname)
            .ToPaginatedAsync(pagination, ct);
    }

    public async Task<User> GetById(long id, CancellationToken ct = default)
    {
        return await _Users.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new ApiException("User not found", $"Utilisateur {id} introuvable.", ApiException.TypeError.RESSOURCE_NOT_FOUND, "USR_001");
    }

    public async Task<List<Role>> GetRoles(CancellationToken ct = default)
    {
        return await _db.Roles.AsNoTracking().OrderBy(r => r.Id).ToListAsync(ct);
    }

    public async Task<User> Create(string login, string email, string firstname, string lastname, string? jobTitle, string password, IEnumerable<string> roleKeys, bool forceUpdatePassword = false, CancellationToken ct = default)
    {
        login = login.Trim();
        email = email.Trim();

        await _CheckUnicity(login, email, null, ct);
        _CheckPassword(password);

        var user = new User
        {
            Login = login,
            Email = email,
            Firstname = firstname.Trim(),
            Lastname = lastname.Trim(),
            JobTitle = string.IsNullOrWhiteSpace(jobTitle) ? null : jobTitle.Trim(),
            Password = _hash.Hash(password),
            ForceUpdatePassword = forceUpdatePassword,
            Roles = await _ResolveRoles(roleKeys, ct)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return user;
    }

    public async Task<User> Update(long id, string email, string firstname, string lastname, string? jobTitle, bool isActive, IEnumerable<string> roleKeys, CancellationToken ct = default)
    {
        var user = await GetById(id, ct);
        email = email.Trim();

        await _CheckUnicity(null, email, id, ct);

        var roles = await _ResolveRoles(roleKeys, ct);
        var loseAdmin = user.Roles.Any(r => r.Key == Role.ADMIN) && (!roles.Any(r => r.Key == Role.ADMIN) || !isActive);
        if (loseAdmin)
        {
            await _CheckNotLastAdmin(id, ct);
        }

        user.Email = email;
        user.Firstname = firstname.Trim();
        user.Lastname = lastname.Trim();
        user.JobTitle = string.IsNullOrWhiteSpace(jobTitle) ? null : jobTitle.Trim();
        user.Roles.Clear();
        foreach (var role in roles)
        {
            user.Roles.Add(role);
        }
        user.UpdatedOn = DateTime.UtcNow;

        if (user.IsActive && !isActive)
        {
            await _authManager.RevokeAll(id, ct);
        }
        user.IsActive = isActive;

        await _db.SaveChangesAsync(ct);
        return user;
    }

    /// <summary>Suppression logique : les données restent en base mais l'utilisateur ne peut plus se connecter.</summary>
    public async Task Delete(long id, CancellationToken ct = default)
    {
        var user = await GetById(id, ct);

        if (user.Roles.Any(r => r.Key == Role.ADMIN))
        {
            await _CheckNotLastAdmin(id, ct);
        }

        user.Deleted = true;
        user.IsActive = false;
        user.UpdatedOn = DateTime.UtcNow;
        await _authManager.RevokeAll(id, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<User> UpdateProfile(long id, string email, string firstname, string lastname, string? jobTitle, CancellationToken ct = default)
    {
        var user = await GetById(id, ct);
        email = email.Trim();

        await _CheckUnicity(null, email, id, ct);

        user.Email = email;
        user.Firstname = firstname.Trim();
        user.Lastname = lastname.Trim();
        user.JobTitle = string.IsNullOrWhiteSpace(jobTitle) ? null : jobTitle.Trim();
        user.UpdatedOn = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return user;
    }

    public async Task<User> UpdatePreferences(long id, string theme, string accentColor, CancellationToken ct = default)
    {
        var user = await GetById(id, ct);
        theme = theme.Trim().ToLower();
        accentColor = accentColor.Trim().ToLower();

        if (!UserPreferences.THEMES.Contains(theme))
        {
            throw new ApiException("Unknown theme", $"Thème inconnu : {theme}.", ApiException.TypeError.MODEL_ERROR, "USR_009");
        }
        if (!UserPreferences.ACCENT_COLORS.Contains(accentColor))
        {
            throw new ApiException("Unknown accent color", $"Couleur inconnue : {accentColor}.", ApiException.TypeError.MODEL_ERROR, "USR_010");
        }

        user.Theme = theme;
        user.AccentColor = accentColor;
        user.UpdatedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return user;
    }

    /// <summary>Remplace l'avatar ; l'ancien fichier est supprimé une fois la base à jour.</summary>
    public async Task<User> UpdateAvatar(long id, Stream content, long length, CancellationToken ct = default)
    {
        var user = await GetById(id, ct);
        var previous = user.Avatar;

        var fileName = await _avatarStorage.Save(id, content, length, ct);
        user.Avatar = fileName;
        user.UpdatedOn = DateTime.UtcNow;
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            _avatarStorage.Delete(fileName);
            throw;
        }

        _avatarStorage.Delete(previous);
        return user;
    }

    public async Task<User> DeleteAvatar(long id, CancellationToken ct = default)
    {
        var user = await GetById(id, ct);
        var previous = user.Avatar;

        user.Avatar = null;
        user.UpdatedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _avatarStorage.Delete(previous);
        return user;
    }

    public async Task ChangePassword(long id, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var user = await GetById(id, ct);

        if (!_hash.CompareTextAndHash(currentPassword, user.Password))
        {
            throw new ApiException("Wrong password", "Le mot de passe actuel est incorrect.", ApiException.TypeError.BAD_REQUEST, "USR_004");
        }

        _CheckPassword(newPassword);

        user.Password = _hash.Hash(newPassword);
        user.ForceUpdatePassword = false;
        user.UpdatedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task _CheckUnicity(string? login, string email, long? excludedId, CancellationToken ct)
    {
        if (login is not null && await _db.Users.AnyAsync(u => u.Id != excludedId && u.Login.ToLower() == login.ToLower(), ct))
        {
            throw new ApiException("Login already used", $"L'identifiant « {login} » est déjà utilisé.", ApiException.TypeError.RESSOURCE_ALREADY_EXISTS, "USR_002");
        }

        if (await _db.Users.AnyAsync(u => u.Id != excludedId && u.Email.ToLower() == email.ToLower(), ct))
        {
            throw new ApiException("Email already used", $"L'adresse « {email} » est déjà utilisée.", ApiException.TypeError.RESSOURCE_ALREADY_EXISTS, "USR_003");
        }
    }

    private static void _CheckPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < PASSWORD_MIN_LENGTH)
        {
            throw new ApiException("Password too weak", $"Le mot de passe doit contenir au moins {PASSWORD_MIN_LENGTH} caractères.", ApiException.TypeError.MODEL_ERROR, "USR_005");
        }
    }

    private async Task<List<Role>> _ResolveRoles(IEnumerable<string> roleKeys, CancellationToken ct)
    {
        var keys = roleKeys.Select(k => k.ToUpper()).Distinct().ToList();
        if (keys.Count == 0)
        {
            keys.Add(Role.USER);
        }

        var roles = await _db.Roles.Where(r => keys.Contains(r.Key)).ToListAsync(ct);
        var unknown = keys.Except(roles.Select(r => r.Key)).ToList();
        if (unknown.Count > 0)
        {
            throw new ApiException("Unknown role", $"Rôle(s) inconnu(s) : {string.Join(", ", unknown)}.", ApiException.TypeError.MODEL_ERROR, "USR_006");
        }

        return roles;
    }

    private async Task _CheckNotLastAdmin(long excludedId, CancellationToken ct)
    {
        var otherAdmins = await _Users.AnyAsync(u => u.Id != excludedId && u.IsActive && u.Roles.Any(r => r.Key == Role.ADMIN), ct);
        if (!otherAdmins)
        {
            throw new ApiException("Last administrator", "Impossible de retirer le dernier administrateur actif.", ApiException.TypeError.CONFLICT, "USR_007");
        }
    }
}
