using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using sideral_jobcode.Storage;

namespace sideral_api.Controllers;

/// <summary>
/// Sert les avatars. Accès anonyme pour pouvoir les afficher via &lt;img src&gt; (qui n'envoie pas le bearer) :
/// les noms de fichiers sont aléatoires et non devinables.
/// </summary>
[Route("api/avatars")]
[AllowAnonymous]
public class AvatarsController : BaseController
{
    private readonly AvatarStorage _avatarStorage;

    public AvatarsController(AvatarStorage avatarStorage)
    {
        _avatarStorage = avatarStorage;
    }

    [HttpGet("{fileName}")]
    [Produces("image/jpeg", "image/png", "image/webp")]
    public IActionResult Get(string fileName)
    {
        var file = _avatarStorage.Find(fileName);
        if (file is null)
        {
            return NotFound();
        }

        // Le nom change à chaque envoi : le contenu d'une URL ne change jamais
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        Response.Headers.XContentTypeOptions = "nosniff";
        return PhysicalFile(file.Value.Path, file.Value.ContentType);
    }
}
