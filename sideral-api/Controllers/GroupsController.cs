using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using sideral_api.Models.Groups;
using sideral_jobcode.Groups;

namespace sideral_api.Controllers;

/// <summary>Groupes d'utilisateurs (administration).</summary>
[Route("api/groups")]
[Authorize(Policy = Program.ADMIN_POLICY)]
public class GroupsController : BaseController
{
    private readonly GroupManager _groupManager;

    public GroupsController(GroupManager groupManager)
    {
        _groupManager = groupManager;
    }

    /// <summary>Liste paginée (?page=&amp;limit=&amp;search=).</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<GroupDto>>> GetAll(CancellationToken ct)
    {
        var result = await _groupManager.GetAll(_pagination, _search, ct);
        return OkWithMeta(result.Items.Select(GroupDto.From), result.Meta);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<GroupDetailDto>> Get(long id, CancellationToken ct)
    {
        return GroupDetailDto.From(await _groupManager.Get(id, ct));
    }

    [HttpPost]
    [ProducesResponseType<GroupDetailDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<GroupDetailDto>> Create([FromBody] GroupRequest request, CancellationToken ct)
    {
        var group = await _groupManager.Create(request.Name, request.Description, request.MemberIds, ct);
        return CreatedAtAction(nameof(Get), new { id = group.Id }, GroupDetailDto.From(group));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<GroupDetailDto>> Update(long id, [FromBody] GroupRequest request, CancellationToken ct)
    {
        return GroupDetailDto.From(await _groupManager.Update(id, request.Name, request.Description, request.MemberIds, ct));
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await _groupManager.Delete(id, ct);
        return NoContent();
    }
}
