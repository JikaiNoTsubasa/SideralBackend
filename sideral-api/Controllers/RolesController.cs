using Microsoft.AspNetCore.Mvc;
using sideral_api.Models.Users;
using sideral_jobcode.Users;

namespace sideral_api.Controllers;

[Route("api/roles")]
public class RolesController : BaseController
{
    private readonly UserManager _userManager;

    public RolesController(UserManager userManager)
    {
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RoleDto>>> GetAll(CancellationToken ct)
    {
        return (await _userManager.GetRoles(ct)).Select(RoleDto.From).ToList();
    }
}
