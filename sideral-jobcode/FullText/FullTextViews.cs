using sideral_context.Dms;

namespace sideral_jobcode.FullText;

/// <summary>Planification de l'indexeur : délai entre deux passages, taille des lots, délai maximal entre deux tentatives.</summary>
public record FullTextSchedule(int IntervalSeconds, int BatchSize, int MaxRetryDelayMinutes);

/// <summary>Configuration effective du serveur full text.</summary>
public record FullTextSettings(bool Enabled, string? Url, string? ApplicationCode, FullTextSchedule Schedule);

/// <summary>Configuration exposée à l'administration.</summary>
public record FullTextConfigView(
    bool Enabled,
    string? Url,
    string? ApplicationCode,
    FullTextSchedule Schedule,
    DateTime? UpdatedOn,
    string? UpdatedBy);

/// <summary>Résultat du test de connexion au serveur full text.</summary>
public record FullTextStatusView(bool Reachable, int? StatusCode, long? LatencyMs, string Message, DateTime CheckedOn);

/// <summary>
/// Documents (hors corbeille) par état d'indexation (Retrying = en attente après une erreur temporaire) et
/// activité de l'indexeur depuis le démarrage de l'API.
/// </summary>
public record FullTextStatsView(int Indexed, int Pending, int Retrying, int Failed, FullTextRunInfo Runs);

/// <summary>Dernier passage de l'indexeur et prochain passage prévu (null = pas encore exécuté / indexation désactivée).</summary>
public record FullTextRunInfo(DateTime? LastRunOn, int LastRunSent, string? LastRunError, DateTime? NextRunOn);

/// <summary>Document en attente d'indexation ou en échec.</summary>
public record FullTextQueueItemView(
    long DocumentId,
    string Name,
    long FolderId,
    string FolderName,
    FullTextStatus Status,
    int Attempts,
    DateTime? NextAttemptOn,
    string? Error,
    DateTime UpdatedOn);
