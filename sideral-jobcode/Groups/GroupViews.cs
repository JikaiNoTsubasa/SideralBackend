using sideral_jobcode.Principals;

namespace sideral_jobcode.Groups;

public record GroupView(long Id, string Name, string? Description, int MemberCount, DateTime CreatedOn);

public record GroupDetailView(long Id, string Name, string? Description, DateTime CreatedOn, List<PrincipalView> Members);
