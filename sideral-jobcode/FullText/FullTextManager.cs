using System.Diagnostics;
using System.Text.RegularExpressions;
using dependancies.Global;
using dependancies.SharedClasses;
using Microsoft.EntityFrameworkCore;
using sideral_context;
using sideral_context.Dms;
using sideral_jobcode.Settings;

namespace sideral_jobcode.FullText;

/// <summary>
/// Configuration du serveur full text (indexation / recherche des documents) et test de connexion.
/// Les documents sont indexés sous un code application : le changer rend inaccessibles ceux indexés sous l'ancien code.
/// </summary>
public partial class FullTextManager : IScoped
{
    public const string HTTP_CLIENT = "fulltext";
    public const int URL_MAX_LENGTH = 500;
    public const int APPLICATION_CODE_MAX_LENGTH = 50;
    public static readonly TimeSpan CHECK_TIMEOUT = TimeSpan.FromSeconds(5);

    public static readonly FullTextSchedule DEFAULT_SCHEDULE = new(IntervalSeconds: 30, BatchSize: 10, MaxRetryDelayMinutes: 60);
    public const int INTERVAL_MIN = 5, INTERVAL_MAX = 86_400;
    public const int BATCH_MIN = 1, BATCH_MAX = 100;
    public const int RETRY_MIN = 1, RETRY_MAX = 1_440;

    private const string PREFIX = "fulltext.";
    private const string KEY_ENABLED = PREFIX + "enabled";
    private const string KEY_URL = PREFIX + "url";
    private const string KEY_APPLICATION_CODE = PREFIX + "applicationCode";
    private const string KEY_INTERVAL = PREFIX + "intervalSeconds";
    private const string KEY_BATCH_SIZE = PREFIX + "batchSize";
    private const string KEY_MAX_RETRY_DELAY = PREFIX + "maxRetryDelayMinutes";

    private readonly SideralDbContext _db;
    private readonly SettingsStore _settings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FullTextQueue _queue;

    public FullTextManager(SideralDbContext db, SettingsStore settings, IHttpClientFactory httpClientFactory, FullTextQueue queue)
    {
        _db = db;
        _settings = settings;
        _httpClientFactory = httpClientFactory;
        _queue = queue;
    }

    /// <summary>Configuration effective (pour les appels au serveur full text).</summary>
    public async Task<FullTextSettings> GetSettings(CancellationToken ct = default)
    {
        var section = await _settings.GetSection(PREFIX, ct);
        string? Value(string key) => section.GetValueOrDefault(key)?.Value;
        int Int(string key, int fallback, int min, int max) => int.TryParse(Value(key), out var v) ? Math.Clamp(v, min, max) : fallback;
        return new FullTextSettings(
            Value(KEY_ENABLED) == "true",
            Value(KEY_URL),
            Value(KEY_APPLICATION_CODE),
            new FullTextSchedule(
                Int(KEY_INTERVAL, DEFAULT_SCHEDULE.IntervalSeconds, INTERVAL_MIN, INTERVAL_MAX),
                Int(KEY_BATCH_SIZE, DEFAULT_SCHEDULE.BatchSize, BATCH_MIN, BATCH_MAX),
                Int(KEY_MAX_RETRY_DELAY, DEFAULT_SCHEDULE.MaxRetryDelayMinutes, RETRY_MIN, RETRY_MAX)));
    }

    public async Task<FullTextConfigView> GetConfig(CancellationToken ct = default)
    {
        var section = await _settings.GetSection(PREFIX, ct);
        var settings = await GetSettings(ct);
        var last = section.Values.MaxBy(s => s.UpdatedOn);
        return new FullTextConfigView(
            settings.Enabled,
            settings.Url,
            settings.ApplicationCode,
            settings.Schedule,
            last?.UpdatedOn,
            last?.UpdatedBy is { } u ? $"{u.Firstname} {u.Lastname}" : null);
    }

    public async Task<FullTextConfigView> UpdateConfig(long userId, bool enabled, string? url, string? applicationCode, FullTextSchedule schedule, CancellationToken ct = default)
    {
        var current = await GetSettings(ct);
        url = _NullIfEmpty(url)?.TrimEnd('/');
        applicationCode = _NullIfEmpty(applicationCode);

        if (url is not null)
        {
            _CheckUrl(url);
        }
        if (applicationCode is not null && !_ApplicationCodeRegex().IsMatch(applicationCode))
        {
            throw new ApiException("Invalid application code", $"Le code application ne peut contenir que des lettres, chiffres, « - » et « _ » ({APPLICATION_CODE_MAX_LENGTH} caractères maximum).", ApiException.TypeError.MODEL_ERROR, "FT_004");
        }
        _CheckRange(schedule.IntervalSeconds, INTERVAL_MIN, INTERVAL_MAX, "Le délai entre deux passages doit être compris entre {0} et {1} secondes.");
        _CheckRange(schedule.BatchSize, BATCH_MIN, BATCH_MAX, "La taille des lots doit être comprise entre {0} et {1} documents.");
        _CheckRange(schedule.MaxRetryDelayMinutes, RETRY_MIN, RETRY_MAX, "Le délai maximal entre deux tentatives doit être compris entre {0} et {1} minutes.");
        if (enabled && (url is null || applicationCode is null))
        {
            throw new ApiException("Incomplete configuration", "Pour activer le serveur full text, l'URL et le code application sont obligatoires.", ApiException.TypeError.MODEL_ERROR, "FT_005");
        }

        await _settings.Set(KEY_ENABLED, enabled ? "true" : "false", userId, ct);
        await _settings.Set(KEY_URL, url, userId, ct);
        await _settings.Set(KEY_APPLICATION_CODE, applicationCode, userId, ct);
        await _settings.Set(KEY_INTERVAL, schedule.IntervalSeconds.ToString(), userId, ct);
        await _settings.Set(KEY_BATCH_SIZE, schedule.BatchSize.ToString(), userId, ct);
        await _settings.Set(KEY_MAX_RETRY_DELAY, schedule.MaxRetryDelayMinutes.ToString(), userId, ct);
        await _settings.Save(ct);

        // Les documents indexés sous l'ancien code ne sont plus accessibles : tout est réindexé sous le nouveau
        if (current.ApplicationCode is not null && applicationCode is not null && current.ApplicationCode != applicationCode)
        {
            await _QueueAll(onlyFailed: false, ct);
        }
        // Réveil de l'indexeur : il applique tout de suite la nouvelle planification
        _queue.Notify();

        return await GetConfig(ct);
    }

    /// <summary>Nombre de documents (hors corbeille) par état d'indexation.</summary>
    public async Task<FullTextStatsView> GetStats(CancellationToken ct = default)
    {
        var counts = await _db.Documents
            .Where(d => d.DeletedOn == null)
            .GroupBy(d => d.FullTextStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Status, g => g.Count, ct);

        var retrying = await _db.Documents.CountAsync(d => d.DeletedOn == null && d.FullTextStatus == FullTextStatus.PENDING && d.FullTextAttempts > 0, ct);

        return new FullTextStatsView(
            counts.GetValueOrDefault(FullTextStatus.INDEXED),
            counts.GetValueOrDefault(FullTextStatus.PENDING),
            retrying,
            counts.GetValueOrDefault(FullTextStatus.FAILED),
            _queue.Runs);
    }

    /// <summary>
    /// Documents (hors corbeille) en attente d'indexation et / ou en échec, paginés.
    /// Échecs d'abord, puis les documents en attente dans l'ordre de traitement de l'indexeur.
    /// </summary>
    public async Task<PaginatedResult<FullTextQueueItemView>> GetQueue(FullTextStatus? status, Pagination pagination, CancellationToken ct = default)
    {
        if (status == FullTextStatus.INDEXED)
        {
            throw new ApiException("Invalid filter", "Seuls les documents en attente ou en échec peuvent être listés.", ApiException.TypeError.BAD_REQUEST, "FT_006");
        }

        return await _db.Documents
            .AsNoTracking()
            .Where(d => d.DeletedOn == null)
            .Where(status is null, d => d.FullTextStatus != FullTextStatus.INDEXED)
            .Where(status is not null, d => d.FullTextStatus == status)
            .OrderByDescending(d => d.FullTextStatus == FullTextStatus.FAILED)
            // Ordre de traitement : nouveaux documents (sans tentative programmée), puis prochaines tentatives
            .ThenBy(d => d.FullTextNextAttemptOn != null)
            .ThenBy(d => d.FullTextNextAttemptOn)
            .ThenBy(d => d.Id)
            .Select(d => new FullTextQueueItemView(
                d.Id,
                d.Name,
                d.FolderId,
                d.Folder!.Name,
                d.FullTextStatus,
                d.FullTextAttempts,
                d.FullTextNextAttemptOn,
                d.FullTextError,
                d.UpdatedOn ?? d.CreatedOn))
            .ToPaginatedAsync(pagination, ct);
    }

    /// <summary>Remet en file d'indexation tous les documents (ou seulement ceux en échec). Renvoie le nombre de documents concernés.</summary>
    public async Task<int> Reindex(bool onlyFailed, CancellationToken ct = default)
    {
        var count = await _QueueAll(onlyFailed, ct);
        _queue.Notify();
        return count;
    }

    private Task<int> _QueueAll(bool onlyFailed, CancellationToken ct) =>
        _db.Documents
            .Where(d => d.DeletedOn == null)
            .Where(onlyFailed, d => d.FullTextStatus == FullTextStatus.FAILED || (d.FullTextStatus == FullTextStatus.PENDING && d.FullTextAttempts > 0))
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.FullTextStatus, FullTextStatus.PENDING)
                .SetProperty(d => d.FullTextAttempts, 0)
                .SetProperty(d => d.FullTextNextAttemptOn, (DateTime?)null)
                .SetProperty(d => d.FullTextError, (string?)null), ct);

    /// <summary>
    /// Vérifie que le serveur répond (réponse HTTP hors 5xx). url null = URL enregistrée,
    /// ce qui permet de tester une URL avant de l'enregistrer.
    /// </summary>
    public async Task<FullTextStatusView> Check(string? url = null, CancellationToken ct = default)
    {
        var settings = await GetSettings(ct);
        url = _NullIfEmpty(url)?.TrimEnd('/') ?? settings.Url;

        if (url is null)
        {
            return new FullTextStatusView(false, null, null, "Aucune URL configurée.", DateTime.UtcNow);
        }
        _CheckUrl(url);

        // SokaFT n'a pas d'endpoint de santé : toute réponse HTTP sur l'URL racine (404 compris) prouve que le serveur répond
        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(CHECK_TIMEOUT);
        var watch = Stopwatch.StartNew();
        try
        {
            using var response = await _httpClientFactory.CreateClient(HTTP_CLIENT).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            var status = (int)response.StatusCode;
            var (reachable, message) = status >= 500
                ? (false, $"Le serveur répond avec une erreur (HTTP {status}).")
                : (true, "Le serveur full text répond.");
            return new FullTextStatusView(reachable, status, watch.ElapsedMilliseconds, message, DateTime.UtcNow);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new FullTextStatusView(false, null, null, $"Pas de réponse au bout de {CHECK_TIMEOUT.TotalSeconds:0} secondes.", DateTime.UtcNow);
        }
        catch (HttpRequestException ex)
        {
            return new FullTextStatusView(false, null, null, $"Serveur injoignable : {ex.Message}", DateTime.UtcNow);
        }
    }

    private static void _CheckUrl(string url)
    {
        if (url.Length > URL_MAX_LENGTH || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new ApiException("Invalid URL", "L'URL du serveur full text doit être une adresse http(s) valide (ex. https://ft.coregeek.fr).", ApiException.TypeError.MODEL_ERROR, "FT_002");
        }
    }

    private static void _CheckRange(int value, int min, int max, string message)
    {
        if (value < min || value > max)
        {
            throw new ApiException("Invalid schedule", string.Format(message, min, max), ApiException.TypeError.MODEL_ERROR, "FT_007");
        }
    }

    private static string? _NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex("^[A-Za-z0-9_-]{1,50}$")]
    private static partial Regex _ApplicationCodeRegex();
}
