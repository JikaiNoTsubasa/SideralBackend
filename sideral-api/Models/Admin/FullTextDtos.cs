using System.ComponentModel.DataAnnotations;
using sideral_context.Dms;
using sideral_jobcode.FullText;

namespace sideral_api.Models.Admin;

/// <summary>Planification de l'indexeur.</summary>
public record FullTextScheduleDto(int IntervalSeconds, int BatchSize, int MaxRetryDelayMinutes)
{
    public static FullTextScheduleDto From(FullTextSchedule s) => new(s.IntervalSeconds, s.BatchSize, s.MaxRetryDelayMinutes);
}

public record FullTextConfigDto(bool Enabled, string? Url, string? ApplicationCode, FullTextScheduleDto Schedule, DateTime? UpdatedOn, string? UpdatedBy)
{
    public static FullTextConfigDto From(FullTextConfigView c) => new(c.Enabled, c.Url, c.ApplicationCode, FullTextScheduleDto.From(c.Schedule), c.UpdatedOn, c.UpdatedBy);
}

public record FullTextStatusDto(bool Reachable, int? StatusCode, long? LatencyMs, string Message, DateTime CheckedOn)
{
    public static FullTextStatusDto From(FullTextStatusView s) => new(s.Reachable, s.StatusCode, s.LatencyMs, s.Message, s.CheckedOn);
}

/// <summary>Dernier passage de l'indexeur et prochain passage prévu.</summary>
public record FullTextRunInfoDto(DateTime? LastRunOn, int LastRunSent, string? LastRunError, DateTime? NextRunOn)
{
    public static FullTextRunInfoDto From(FullTextRunInfo r) => new(r.LastRunOn, r.LastRunSent, r.LastRunError, r.NextRunOn);
}

/// <summary>Documents (hors corbeille) par état d'indexation. Retrying = en attente après une erreur temporaire (inclus dans Pending).</summary>
public record FullTextStatsDto(int Indexed, int Pending, int Retrying, int Failed, FullTextRunInfoDto Runs)
{
    public static FullTextStatsDto From(FullTextStatsView s) => new(s.Indexed, s.Pending, s.Retrying, s.Failed, FullTextRunInfoDto.From(s.Runs));
}

/// <summary>Document en attente d'indexation ou en échec.</summary>
public record FullTextQueueItemDto(long DocumentId, string Name, long FolderId, string FolderName, FullTextStatus Status, int Attempts, DateTime? NextAttemptOn, string? Error, DateTime UpdatedOn)
{
    public static FullTextQueueItemDto From(FullTextQueueItemView i) => new(i.DocumentId, i.Name, i.FolderId, i.FolderName, i.Status, i.Attempts, i.NextAttemptOn, i.Error, i.UpdatedOn);
}

public record ReindexResultDto(int Queued);

public class FullTextConfigRequest
{
    public bool Enabled { get; set; }

    [MaxLength(FullTextManager.URL_MAX_LENGTH)]
    public string? Url { get; set; }

    [MaxLength(FullTextManager.APPLICATION_CODE_MAX_LENGTH)]
    public string? ApplicationCode { get; set; }

    [Range(FullTextManager.INTERVAL_MIN, FullTextManager.INTERVAL_MAX)]
    public int IntervalSeconds { get; set; } = 30;

    [Range(FullTextManager.BATCH_MIN, FullTextManager.BATCH_MAX)]
    public int BatchSize { get; set; } = 10;

    [Range(FullTextManager.RETRY_MIN, FullTextManager.RETRY_MAX)]
    public int MaxRetryDelayMinutes { get; set; } = 60;
}

/// <summary>Test de connexion : URL vide = URL enregistrée.</summary>
public class FullTextCheckRequest
{
    [MaxLength(FullTextManager.URL_MAX_LENGTH)]
    public string? Url { get; set; }
}
