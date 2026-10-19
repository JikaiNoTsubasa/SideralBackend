using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using sideral_api.Models.Wall;
using sideral_jobcode.LinkPreview;

namespace sideral_api.Controllers;

/// <summary>Aperçus des liens publiés sur le mur (titre, description, image ; vidéo YouTube).</summary>
[Route("api/link-preview")]
[EnableRateLimiting(Program.LINK_PREVIEW_RATE_LIMIT)]
public class LinkPreviewController : BaseController
{
    private readonly LinkPreviewManager _linkPreviewManager;

    public LinkPreviewController(LinkPreviewManager linkPreviewManager)
    {
        _linkPreviewManager = linkPreviewManager;
    }

    /// <summary>Aperçu du lien (?url=) ; 204 s'il n'est pas disponible (le lien reste simplement cliquable).</summary>
    [HttpGet]
    [ProducesResponseType<LinkPreviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Get([FromQuery] string? url, CancellationToken ct)
    {
        var preview = await _linkPreviewManager.Get(url, ct);
        // Cache navigateur : un même lien s'affiche souvent plusieurs fois dans le fil
        Response.Headers.CacheControl = "private, max-age=3600";
        return preview is null ? NoContent() : Ok(LinkPreviewDto.From(preview));
    }
}
