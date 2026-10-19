using dependancies.SharedClasses;

namespace sideral_jobcode.FullText;

/// <summary>
/// Réveil de l'indexeur (appelé quand un document est à (ré)indexer, pour ne pas attendre le prochain passage)
/// et suivi de ses passages, affiché dans l'administration.
/// </summary>
public class FullTextQueue : ISingleton
{
    private readonly SemaphoreSlim _signal = new(0, 1);
    private volatile FullTextRunInfo _runs = new(null, 0, null, null);

    public FullTextRunInfo Runs => _runs;

    public void RecordRun(int sent, string? error) => _runs = _runs with { LastRunOn = DateTime.UtcNow, LastRunSent = sent, LastRunError = error };

    public void ScheduleNextRun(DateTime? nextRunOn) => _runs = _runs with { NextRunOn = nextRunOn };

    public void Notify()
    {
        // Un seul réveil en attente suffit : l'indexeur traite tous les documents en attente
        if (_signal.CurrentCount == 0)
        {
            try
            {
                _signal.Release();
            }
            catch (SemaphoreFullException)
            {
            }
        }
    }

    /// <summary>Attend un réveil ou l'expiration du délai.</summary>
    public Task WaitAsync(TimeSpan timeout, CancellationToken ct) => _signal.WaitAsync(timeout, ct);
}
