using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using sideral_api.Models.Admin;
using sideral_context.Dms;
using sideral_jobcode.FullText;

namespace sideral_api.Controllers;

/// <summary>Administration : configuration du serveur full text.</summary>
[Route("api/admin/fulltext")]
[Authorize(Policy = Program.ADMIN_POLICY)]
public class AdminFullTextController : BaseController
{
    private readonly FullTextManager _fullTextManager;

    public AdminFullTextController(FullTextManager fullTextManager)
    {
        _fullTextManager = fullTextManager;
    }

    [HttpGet]
    public async Task<ActionResult<FullTextConfigDto>> Get(CancellationToken ct)
    {
        return FullTextConfigDto.From(await _fullTextManager.GetConfig(ct));
    }

    [HttpPut]
    public async Task<ActionResult<FullTextConfigDto>> Update([FromBody] FullTextConfigRequest request, CancellationToken ct)
    {
        return FullTextConfigDto.From(await _fullTextManager.UpdateConfig(LoggedUserId, request.Enabled, request.Url, request.ApplicationCode,
            new FullTextSchedule(request.IntervalSeconds, request.BatchSize, request.MaxRetryDelayMinutes), ct));
    }

    /// <summary>Teste la connexion au serveur (corps facultatif pour tester une URL non enregistrée).</summary>
    [HttpPost("check")]
    public async Task<ActionResult<FullTextStatusDto>> Check([FromBody] FullTextCheckRequest? request, CancellationToken ct)
    {
        return FullTextStatusDto.From(await _fullTextManager.Check(request?.Url, ct));
    }

    /// <summary>Nombre de documents par état d'indexation.</summary>
    [HttpGet("stats")]
    public async Task<ActionResult<FullTextStatsDto>> Stats(CancellationToken ct)
    {
        return FullTextStatsDto.From(await _fullTextManager.GetStats(ct));
    }

    /// <summary>Documents en attente et / ou en échec (?status=PENDING|FAILED, absent = les deux ; ?page=&amp;limit=).</summary>
    [HttpGet("queue")]
    public async Task<ActionResult<IEnumerable<FullTextQueueItemDto>>> Queue([FromQuery] FullTextStatus? status, CancellationToken ct)
    {
        var result = await _fullTextManager.GetQueue(status, _pagination, ct);
        return OkWithMeta(result.Items.Select(FullTextQueueItemDto.From), result.Meta);
    }

    /// <summary>Remet en file d'indexation tous les documents (?onlyFailed=true : seulement ceux en échec).</summary>
    [HttpPost("reindex")]
    public async Task<ActionResult<ReindexResultDto>> Reindex([FromQuery] bool onlyFailed, CancellationToken ct)
    {
        return new ReindexResultDto(await _fullTextManager.Reindex(onlyFailed, ct));
    }
}
