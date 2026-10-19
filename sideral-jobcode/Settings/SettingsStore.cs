using dependancies.SharedClasses;
using Microsoft.EntityFrameworkCore;
using sideral_context;
using sideral_context.Settings;

namespace sideral_jobcode.Settings;

/// <summary>Lecture / écriture des paramètres applicatifs (table app_settings).</summary>
public class SettingsStore : IScoped
{
    private readonly SideralDbContext _db;

    public SettingsStore(SideralDbContext db)
    {
        _db = db;
    }

    /// <summary>Paramètres dont la clé commence par le préfixe donné (ex. "fulltext.").</summary>
    public async Task<Dictionary<string, AppSetting>> GetSection(string prefix, CancellationToken ct = default)
    {
        return await _db.AppSettings
            .AsNoTracking()
            .Include(s => s.UpdatedBy)
            .Where(s => s.Key.StartsWith(prefix))
            .ToDictionaryAsync(s => s.Key, ct);
    }

    /// <summary>Prépare l'écriture (enregistrée au SaveChanges suivant, voir Save).</summary>
    public async Task Set(string key, string? value, long userId, CancellationToken ct = default)
    {
        var setting = await _db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (setting is null)
        {
            _db.AppSettings.Add(new AppSetting { Key = key, Value = value, UpdatedById = userId });
        }
        else if (setting.Value != value)
        {
            setting.Value = value;
            setting.UpdatedOn = DateTime.UtcNow;
            setting.UpdatedById = userId;
        }
    }

    public Task Save(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
