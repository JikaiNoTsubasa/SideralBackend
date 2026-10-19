using Microsoft.AspNetCore.Mvc;
using sideral_api.Models.Dms;
using sideral_jobcode.Dms;
using sideral_jobcode.Storage;

namespace sideral_api.Controllers;

/// <summary>GED : navigation, dossiers, droits et dépôt de documents.</summary>
[Route("api/folders")]
public class FoldersController : BaseController
{
    private readonly FolderManager _folderManager;
    private readonly DocumentManager _documentManager;

    public FoldersController(FolderManager folderManager, DocumentManager documentManager)
    {
        _folderManager = folderManager;
        _documentManager = documentManager;
    }

    /// <summary>Racine : dossiers racine (administrateurs) ou dossiers partagés avec l'utilisateur.</summary>
    [HttpGet("root")]
    public async Task<ActionResult<FolderContentDto>> Root(CancellationToken ct)
    {
        return FolderContentDto.From(await _folderManager.Browse(LoggedUserId, IsAdmin, null, ct));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<FolderContentDto>> Get(long id, CancellationToken ct)
    {
        return FolderContentDto.From(await _folderManager.Browse(LoggedUserId, IsAdmin, id, ct));
    }

    [HttpPost]
    [ProducesResponseType<FolderRefDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<FolderRefDto>> Create([FromBody] CreateFolderRequest request, CancellationToken ct)
    {
        var folder = await _folderManager.Create(LoggedUserId, IsAdmin, request.ParentId, request.Name, ct);
        return CreatedAtAction(nameof(Get), new { id = folder.Id }, FolderRefDto.From(folder));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<FolderRefDto>> Rename(long id, [FromBody] RenameRequest request, CancellationToken ct)
    {
        return FolderRefDto.From(await _folderManager.Rename(LoggedUserId, IsAdmin, id, request.Name, ct));
    }

    [HttpPut("{id:long}/move")]
    public async Task<ActionResult<FolderRefDto>> Move(long id, [FromBody] MoveFolderRequest request, CancellationToken ct)
    {
        return FolderRefDto.From(await _folderManager.Move(LoggedUserId, IsAdmin, id, request.ParentId, ct));
    }

    /// <summary>Envoie le dossier et son contenu dans la corbeille.</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await _folderManager.Delete(LoggedUserId, IsAdmin, id, ct);
        return NoContent();
    }

    [HttpGet("{id:long}/permissions")]
    public async Task<ActionResult<FolderPermissionsDto>> GetPermissions(long id, CancellationToken ct)
    {
        return FolderPermissionsDto.From(await _folderManager.GetPermissions(LoggedUserId, IsAdmin, id, ct));
    }

    /// <summary>Remplace les droits propres du dossier (gestionnaires).</summary>
    [HttpPut("{id:long}/permissions")]
    public async Task<ActionResult<FolderPermissionsDto>> SetPermissions(long id, [FromBody] SetPermissionsRequest request, CancellationToken ct)
    {
        var entries = request.Entries.Select(e => new PermissionEntryInput(e.UserId, e.GroupId, e.Level)).ToList();
        return FolderPermissionsDto.From(await _folderManager.SetPermissions(LoggedUserId, IsAdmin, id, request.InheritPermissions, entries, ct));
    }

    /// <summary>Journal d'activité du dossier (?page=&amp;limit=).</summary>
    [HttpGet("{id:long}/activity")]
    public async Task<ActionResult<IEnumerable<ActivityDto>>> GetActivity(long id, CancellationToken ct)
    {
        var result = await _folderManager.GetActivity(LoggedUserId, IsAdmin, id, _pagination, ct);
        return OkWithMeta(result.Items.Select(ActivityDto.From), result.Meta);
    }

    /// <summary>Ajoute un document (multipart/form-data : "file", "description" facultatif). Tous types, 100 Mo max.</summary>
    [HttpPost("{id:long}/documents")]
    [RequestSizeLimit(DocumentStorage.MAX_SIZE + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = DocumentStorage.MAX_SIZE + 1024 * 1024)]
    [ProducesResponseType<DocumentDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<DocumentDto>> Upload(long id, IFormFile file, [FromForm] string? description, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var document = await _documentManager.Upload(LoggedUserId, IsAdmin, id, stream, file.Length, file.FileName, description, ct);
        return CreatedAtAction(nameof(DocumentsController.Get), "Documents", new { id = document.Id }, DocumentDto.From(document));
    }
}
