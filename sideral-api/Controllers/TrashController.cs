using Microsoft.AspNetCore.Mvc;
using sideral_api.Models.Dms;
using sideral_jobcode.Dms;

namespace sideral_api.Controllers;

/// <summary>Corbeille de la GED (éléments que l'utilisateur peut gérer).</summary>
[Route("api/trash")]
public class TrashController : BaseController
{
    private readonly TrashManager _trashManager;

    public TrashController(TrashManager trashManager)
    {
        _trashManager = trashManager;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TrashItemDto>>> GetAll(CancellationToken ct)
    {
        return (await _trashManager.List(LoggedUserId, IsAdmin, ct)).Select(TrashItemDto.From).ToList();
    }

    /// <summary>Vide la corbeille (suppression définitive).</summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> EmptyTrash(CancellationToken ct)
    {
        await _trashManager.Empty(LoggedUserId, IsAdmin, ct);
        return NoContent();
    }

    [HttpPost("folders/{id:long}/restore")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RestoreFolder(long id, CancellationToken ct)
    {
        await _trashManager.RestoreFolder(LoggedUserId, IsAdmin, id, ct);
        return NoContent();
    }

    [HttpPost("documents/{id:long}/restore")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RestoreDocument(long id, CancellationToken ct)
    {
        await _trashManager.RestoreDocument(LoggedUserId, IsAdmin, id, ct);
        return NoContent();
    }

    [HttpDelete("folders/{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> PurgeFolder(long id, CancellationToken ct)
    {
        await _trashManager.PurgeFolder(LoggedUserId, IsAdmin, id, ct);
        return NoContent();
    }

    [HttpDelete("documents/{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> PurgeDocument(long id, CancellationToken ct)
    {
        await _trashManager.PurgeDocument(LoggedUserId, IsAdmin, id, ct);
        return NoContent();
    }
}
