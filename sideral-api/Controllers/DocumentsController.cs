using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using sideral_api.Models.Dms;
using sideral_jobcode.Dms;
using sideral_jobcode.Storage;

namespace sideral_api.Controllers;

/// <summary>GED : documents, versions, extraction (checkout) et contenu.</summary>
[Route("api/documents")]
public class DocumentsController : BaseController
{
    private readonly DocumentManager _documentManager;

    public DocumentsController(DocumentManager documentManager)
    {
        _documentManager = documentManager;
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<DocumentDto>> Get(long id, CancellationToken ct)
    {
        return DocumentDto.From(await _documentManager.Get(LoggedUserId, IsAdmin, id, ct));
    }

    /// <summary>Documents extraits par l'utilisateur connecté.</summary>
    [HttpGet("checked-out")]
    public async Task<ActionResult<IEnumerable<CheckedOutDocumentDto>>> CheckedOut(CancellationToken ct)
    {
        return (await _documentManager.CheckedOutByMe(LoggedUserId, IsAdmin, ct)).Select(CheckedOutDocumentDto.From).ToList();
    }

    /// <summary>Renomme et/ou modifie la description.</summary>
    [HttpPut("{id:long}")]
    public async Task<ActionResult<DocumentDto>> Update(long id, [FromBody] UpdateDocumentRequest request, CancellationToken ct)
    {
        return DocumentDto.From(await _documentManager.Update(LoggedUserId, IsAdmin, id, request.Name, request.Description, ct));
    }

    [HttpPut("{id:long}/move")]
    public async Task<ActionResult<DocumentDto>> Move(long id, [FromBody] MoveDocumentRequest request, CancellationToken ct)
    {
        return DocumentDto.From(await _documentManager.Move(LoggedUserId, IsAdmin, id, request.FolderId, ct));
    }

    /// <summary>Envoie le document dans la corbeille.</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await _documentManager.Delete(LoggedUserId, IsAdmin, id, ct);
        return NoContent();
    }

    /// <summary>Extrait le document (verrou d'édition).</summary>
    [HttpPost("{id:long}/checkout")]
    public async Task<ActionResult<DocumentDto>> Checkout(long id, CancellationToken ct)
    {
        return DocumentDto.From(await _documentManager.Checkout(LoggedUserId, IsAdmin, id, ct));
    }

    /// <summary>Annule l'extraction (auteur de l'extraction, gestionnaire du dossier ou administrateur).</summary>
    [HttpDelete("{id:long}/checkout")]
    public async Task<ActionResult<DocumentDto>> CancelCheckout(long id, CancellationToken ct)
    {
        return DocumentDto.From(await _documentManager.CancelCheckout(LoggedUserId, IsAdmin, id, ct));
    }

    /// <summary>
    /// Archive une nouvelle version (multipart/form-data : "file", "major" = true pour une version majeure, "comment").
    /// Libère l'extraction.
    /// </summary>
    [HttpPost("{id:long}/versions")]
    [RequestSizeLimit(DocumentStorage.MAX_SIZE + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = DocumentStorage.MAX_SIZE + 1024 * 1024)]
    public async Task<ActionResult<DocumentDto>> Checkin(long id, IFormFile file, [FromForm] bool major, [FromForm] string? comment, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return DocumentDto.From(await _documentManager.Checkin(LoggedUserId, IsAdmin, id, stream, file.Length, file.FileName, major, comment, ct));
    }

    /// <summary>Restaure une ancienne version (nouvelle version mineure avec le même contenu).</summary>
    [HttpPost("{id:long}/versions/{versionId:long}/restore")]
    public async Task<ActionResult<DocumentDto>> RestoreVersion(long id, long versionId, CancellationToken ct)
    {
        return DocumentDto.From(await _documentManager.RestoreVersion(LoggedUserId, IsAdmin, id, versionId, ct));
    }

    /// <summary>Lien signé vers le contenu (?versionId= facultatif, version courante par défaut ; ?download=true pour télécharger).</summary>
    [HttpGet("{id:long}/link")]
    public async Task<ActionResult<ContentLinkDto>> Link(long id, [FromQuery] long? versionId, [FromQuery] bool download, CancellationToken ct)
    {
        return ContentLinkDto.From(await _documentManager.CreateContentLink(LoggedUserId, IsAdmin, id, versionId, download, ct));
    }

    /// <summary>
    /// Contenu d'une version, via un lien signé (anonyme pour &lt;img&gt;, &lt;iframe&gt;, &lt;video&gt;...).
    /// Les types non prévisualisables sont toujours servis en téléchargement.
    /// </summary>
    [HttpGet("content")]
    [AllowAnonymous]
    [Produces("application/octet-stream")]
    public async Task<IActionResult> GetContent([FromQuery] string? token, CancellationToken ct)
    {
        var content = await _documentManager.OpenContent(token, ct);

        var disposition = new ContentDispositionHeaderValue(content.Inline ? "inline" : "attachment");
        disposition.SetHttpFileName(content.FileName);
        Response.Headers.ContentDisposition = disposition.ToString();
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "private, max-age=3600";
        // Aucun script ne doit s'exécuter dans un fichier servi (le visualiseur PDF des navigateurs ne supporte pas le sandbox)
        if (content.ContentType != DocumentStorage.PDF)
        {
            Response.Headers.ContentSecurityPolicy = "sandbox";
        }

        return PhysicalFile(content.Path, content.ContentType, enableRangeProcessing: true);
    }

    /// <summary>Journal d'activité du document (?page=&amp;limit=).</summary>
    [HttpGet("{id:long}/activity")]
    public async Task<ActionResult<IEnumerable<ActivityDto>>> GetActivity(long id, CancellationToken ct)
    {
        var result = await _documentManager.GetActivity(LoggedUserId, IsAdmin, id, _pagination, ct);
        return OkWithMeta(result.Items.Select(ActivityDto.From), result.Meta);
    }
}
