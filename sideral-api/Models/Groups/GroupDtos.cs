using System.ComponentModel.DataAnnotations;
using sideral_api.Models.Principals;
using sideral_context.Groups;
using sideral_jobcode.Groups;

namespace sideral_api.Models.Groups;

public record GroupDto(long Id, string Name, string? Description, int MemberCount, DateTime CreatedOn)
{
    public static GroupDto From(GroupView g) => new(g.Id, g.Name, g.Description, g.MemberCount, g.CreatedOn);
}

public record GroupDetailDto(long Id, string Name, string? Description, DateTime CreatedOn, IEnumerable<PrincipalDto> Members)
{
    public static GroupDetailDto From(GroupDetailView g) => new(g.Id, g.Name, g.Description, g.CreatedOn, g.Members.Select(PrincipalDto.From).ToList());
}

public class GroupRequest
{
    [Required, MaxLength(Group.NAME_MAX_LENGTH)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(Group.DESCRIPTION_MAX_LENGTH)]
    public string? Description { get; set; }

    public List<long> MemberIds { get; set; } = [];
}
