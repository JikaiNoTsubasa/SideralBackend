using dependancies.SharedClasses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using sideral_api.Models.Users;
using sideral_jobcode.Users;

namespace sideral_api.Controllers;

[Route("api/users")]
[Authorize(Policy = Program.ADMIN_POLICY)]
public class UsersController : BaseController
{
    private readonly UserManager _userManager;

    public UsersController(UserManager userManager)
    {
        _userManager = userManager;
    }

    /// <summary>Liste paginée (?page=&amp;limit=&amp;search=). Pagination renvoyée dans les en-têtes page, page-size, page-count, total.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAll(CancellationToken ct)
    {
        var result = await _userManager.GetAll(_pagination, _search, ct);
        return OkWithMeta(result.Items.Select(UserDto.From), result.Meta);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<UserDto>> Get(long id, CancellationToken ct)
    {
        return UserDto.From(await _userManager.GetById(id, ct));
    }

    [HttpPost]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var user = await _userManager.Create(request.Login, request.Email, request.Firstname, request.Lastname, request.JobTitle, request.Password, request.Roles, ct: ct);
        return CreatedAtAction(nameof(Get), new { id = user.Id }, UserDto.From(user));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<UserDto>> Update(long id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var user = await _userManager.Update(id, request.Email, request.Firstname, request.Lastname, request.JobTitle, request.IsActive, request.Roles, ct);
        return UserDto.From(user);
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        if (id == LoggedUserId)
        {
            throw new ApiException("Cannot delete yourself", "Vous ne pouvez pas supprimer votre propre compte.", ApiException.TypeError.FORBIDDEN, "USR_008");
        }

        await _userManager.Delete(id, ct);
        return NoContent();
    }
}
