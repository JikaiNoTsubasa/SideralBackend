using sideral_jobcode.Principals;

namespace sideral_api.Models.Principals;

/// <summary>Utilisateur (type "user") ou groupe (type "group"). Firstname/Lastname/Avatar : utilisateurs uniquement.</summary>
public record PrincipalDto(string Type, long Id, string Name, string? Detail, string? Firstname, string? Lastname, string? Avatar)
{
    public static PrincipalDto From(PrincipalView p) => new(p.Type, p.Id, p.Name, p.Detail, p.Firstname, p.Lastname, p.Avatar);
}
