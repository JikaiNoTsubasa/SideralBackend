using dependancies.SharedClasses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using sideral_context;
using sideral_context.Users;

namespace sideral_jobcode.Users;

/// <summary>
/// Crée un administrateur initial (section "Seed:Admin") si aucun utilisateur n'existe encore.
/// Le mot de passe devra être changé à la première connexion.
/// </summary>
public class AdminSeeder : IScoped
{
    private readonly SideralDbContext _db;
    private readonly UserManager _userManager;
    private readonly IConfiguration _config;
    private readonly ILogger<AdminSeeder> _logger;

    public AdminSeeder(SideralDbContext db, UserManager userManager, IConfiguration config, ILogger<AdminSeeder> logger)
    {
        _db = db;
        _userManager = userManager;
        _config = config;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var section = _config.GetSection("Seed:Admin");
        var login = section["Login"];
        var email = section["Email"];
        var password = section["Password"];

        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        if (await _db.Users.AnyAsync(ct))
        {
            return;
        }

        await _userManager.Create(login, email, section["Firstname"] ?? "Admin", section["Lastname"] ?? "Sideral", null, password, [Role.ADMIN, Role.USER], forceUpdatePassword: true, ct: ct);
        _logger.LogWarning("Initial administrator '{Login}' created. Change its password at first login.", login);
    }
}
