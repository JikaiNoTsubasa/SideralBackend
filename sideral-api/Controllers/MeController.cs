using Microsoft.AspNetCore.Mvc;
using sideral_api.Models.Users;
using sideral_jobcode.Users;

namespace sideral_api.Controllers;

[Route("api/me")]
public class MeController : BaseController
{
    private readonly UserManager _userManager;

    public MeController(UserManager userManager)
    {
        _userManager = userManager;
    }

    /// <summary>Profil de l'utilisateur connecté.</summary>
    [HttpGet]
    public async Task<ActionResult<UserDto>> Get(CancellationToken ct)
    {
        return UserDto.From(await _userManager.GetById(LoggedUserId, ct));
    }

    [HttpPut]
    public async Task<ActionResult<UserDto>> Update([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await _userManager.UpdateProfile(LoggedUserId, request.Email, request.Firstname, request.Lastname, request.JobTitle, ct);
        return UserDto.From(user);
    }

    [HttpPut("password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        await _userManager.ChangePassword(LoggedUserId, request.CurrentPassword, request.NewPassword, ct);
        return NoContent();
    }
}
