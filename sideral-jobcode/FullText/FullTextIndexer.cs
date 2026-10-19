using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using sideral_context;
using sideral_context.Dms;
using sideral_jobcode.Storage;

namespace sideral_jobcode.FullText;

/// <summary>
/// Tâche de fond : envoie au serveur full text les documents en attente (statut PENDING), par lots.
/// Réveillée par FullTextQueue.Notify() à chaque nouveau document / version, sinon selon la planification
/// (Admin > Full Text Server : délai entre deux passages, taille des lots ; lot plein = passage suivant immédiat).
/// Serveur injoignable : nouvelle tentative avec un délai croissant (1, 2, 4... min, plafonné par la planification).
/// Rien n'est envoyé tant que le full text est désactivé : les documents restent en attente.
/// </summary>
public class FullTextIndexer : BackgroundService
{
    /// <summary>Délai d'attente quand la configuration ne peut pas être lue (base indisponible...).</summary>
    private static readonly TimeSpan ERROR_DELAY = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopes;
    private readonly FullTextQueue _queue;
    private readonly FullTextClient _client;
    private readonly DocumentTextExtractor _extractor;
    private readonly DocumentStorage _storage;
    private readonly ILogger<FullTextIndexer> _logger;

    public FullTextIndexer(IServiceScopeFactory scopes, FullTextQueue queue, FullTextClient client, DocumentTextExtractor extractor, DocumentStorage storage, ILogger<FullTextIndexer> logger)
    {
        _scopes = scopes;
        _queue = queue;
        _client = client;
        _extractor = extractor;
        _storage = storage;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var (batchFull, delay) = (false, ERROR_DELAY);
            try
            {
                (batchFull, delay) = await _RunBatch(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Base indisponible, migration non appliquée... : on réessaie au passage suivant
                _logger.LogError(ex, "Full text indexing batch failed.");
                _queue.RecordRun(0, ex.Message);
            }

            if (batchFull)
            {
                continue;
            }
            try
            {
                _queue.ScheduleNextRun(DateTime.UtcNow + delay);
                await _queue.WaitAsync(delay, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Traite un lot. Renvoie BatchFull = il reste probablement des documents à traiter tout de suite,
    /// et le délai avant le passage suivant.
    /// </summary>
    private async Task<(bool BatchFull, TimeSpan Delay)> _RunBatch(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SideralDbContext>();
        var settings = await scope.ServiceProvider.GetRequiredService<FullTextManager>().GetSettings(ct);
        var schedule = settings.Schedule;
        var delay = TimeSpan.FromSeconds(schedule.IntervalSeconds);
        if (!settings.Enabled || settings.Url is null || settings.ApplicationCode is null)
        {
            return (false, delay);
        }

        var now = DateTime.UtcNow;
        var documents = await db.Documents
            .AsNoTracking()
            .Where(d => d.DeletedOn == null && d.FullTextStatus == FullTextStatus.PENDING && (d.FullTextNextAttemptOn == null || d.FullTextNextAttemptOn <= now))
            .OrderBy(d => d.Id)
            .Take(schedule.BatchSize)
            .Select(d => new
            {
                d.Id,
                d.Name,
                d.FullTextIdentifier,
                d.UpdatedOn,
                d.FullTextAttempts,
                Current = d.Versions.OrderByDescending(v => v.Major).ThenByDescending(v => v.Minor).Select(v => new { v.StorageKey, v.ContentType }).First()
            })
            .ToListAsync(ct);

        var sent = 0;
        foreach (var document in documents)
        {
            // Mise à jour conditionnelle : si le document a changé pendant l'envoi (nouvelle version, renommage),
            // il reste en attente pour être réindexé
            IQueryable<Document> Unchanged() => db.Documents.Where(d =>
                d.Id == document.Id && d.FullTextStatus == FullTextStatus.PENDING && d.UpdatedOn == document.UpdatedOn);

            var path = _storage.Find(document.Current.StorageKey);
            if (path is null)
            {
                await Unchanged().ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.FullTextStatus, FullTextStatus.FAILED)
                    .SetProperty(d => d.FullTextError, "Fichier introuvable sur le serveur."), ct);
                continue;
            }

            string text;
            string? extractionError = null;
            try
            {
                text = _extractor.Extract(path, document.Current.ContentType, document.Name);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Fichier corrompu ou protégé : on indexe au moins le nom
                _logger.LogWarning(ex, "Text extraction failed for document {DocumentId}.", document.Id);
                text = string.Empty;
                extractionError = $"Texte non extrait ({ex.Message}) : seul le nom est indexé.";
            }

            try
            {
                await _client.Index(settings, document.FullTextIdentifier.ToString(), document.Name, text, ct);
            }
            catch (FullTextUnavailableException ex)
            {
                var attempts = document.FullTextAttempts + 1;
                var retryDelay = TimeSpan.FromMinutes(Math.Min(Math.Pow(2, attempts - 1), schedule.MaxRetryDelayMinutes));
                _logger.LogWarning("Full text indexing of document {DocumentId} failed (attempt {Attempts}, retry in {Delay}): {Message}", document.Id, attempts, retryDelay, ex.Message);
                var message = ex.Message[..Math.Min(ex.Message.Length, 1000)];
                await Unchanged().ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.FullTextAttempts, attempts)
                    .SetProperty(d => d.FullTextNextAttemptOn, DateTime.UtcNow + retryDelay)
                    .SetProperty(d => d.FullTextError, message), ct);
                // Le serveur est probablement indisponible pour les autres documents aussi
                _queue.RecordRun(sent, message);
                return (false, delay);
            }

            await Unchanged().ExecuteUpdateAsync(s => s
                .SetProperty(d => d.FullTextStatus, FullTextStatus.INDEXED)
                .SetProperty(d => d.FullTextIndexedOn, DateTime.UtcNow)
                .SetProperty(d => d.FullTextAttempts, 0)
                .SetProperty(d => d.FullTextNextAttemptOn, (DateTime?)null)
                .SetProperty(d => d.FullTextError, extractionError), ct);
            sent++;
        }

        if (sent > 0)
        {
            _logger.LogInformation("Full text: {Count} document(s) sent for indexing.", sent);
        }
        _queue.RecordRun(sent, null);
        return (documents.Count == schedule.BatchSize, delay);
    }
}
