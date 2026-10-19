using Microsoft.AspNetCore.Mvc;
using sideral_api.Models.Principals;
using sideral_jobcode.Principals;

namespace sideral_api.Controllers;

/// <summary>Recherche d'utilisateurs et de groupes (attribution de droits, membres de groupe).</summary>
[Route("api/principals")]
public class PrincipalsController : BaseController
{
    private readonly PrincipalManager _principalManager;

    public PrincipalsController(PrincipalManager principalManager)
    {
        _principalManager = principalManager;
    }

    /// <summary>?search=&amp;users=true&amp;groups=true : 20 résultats max par type.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PrincipalDto>>> Search([FromQuery] bool users = true, [FromQuery] bool groups = true, CancellationToken ct = default)
    {
        return (await _principalManager.Search(_search, users, groups, ct)).Select(PrincipalDto.From).ToList();
    }
}
