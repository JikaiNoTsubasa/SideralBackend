using Microsoft.AspNetCore.Mvc;
using sideral_api.Models.Users;
using sideral_jobcode.Storage;
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

    /// <summary>Préférences d'affichage (thème et couleur principale).</summary>
    [HttpPut("preferences")]
    public async Task<ActionResult<UserDto>> UpdatePreferences([FromBody] UpdatePreferencesRequest request, CancellationToken ct)
    {
        return UserDto.From(await _userManager.UpdatePreferences(LoggedUserId, request.Theme, request.AccentColor, ct));
    }

    /// <summary>Envoi de l'avatar (multipart/form-data, champ "file") : JPEG, PNG ou WebP, 2 Mo max.</summary>
    [HttpPut("avatar")]
    [RequestSizeLimit(AvatarStorage.MAX_SIZE + 64 * 1024)]
    public async Task<ActionResult<UserDto>> UpdateAvatar(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return UserDto.From(await _userManager.UpdateAvatar(LoggedUserId, stream, file.Length, ct));
    }

    [HttpDelete("avatar")]
    public async Task<ActionResult<UserDto>> DeleteAvatar(CancellationToken ct)
    {
        return UserDto.From(await _userManager.DeleteAvatar(LoggedUserId, ct));
    }

    [HttpPut("password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        await _userManager.ChangePassword(LoggedUserId, request.CurrentPassword, request.NewPassword, ct);
        return NoContent();
    }
}
