namespace sideral_jobcode.Principals;

/// <summary>Utilisateur ou groupe pouvant recevoir des droits. Firstname/Lastname/Avatar : utilisateurs uniquement.</summary>
public record PrincipalView(string Type, long Id, string Name, string? Detail, string? Firstname, string? Lastname, string? Avatar)
{
    public const string USER = "user";
    public const string GROUP = "group";

    public static PrincipalView User(long id, string firstname, string lastname, string? jobTitle, string? avatar) =>
        new(USER, id, $"{firstname} {lastname}", jobTitle, firstname, lastname, avatar);

    public static PrincipalView Group(long id, string name, int memberCount) =>
        new(GROUP, id, name, $"{memberCount} membre(s)", null, null, null);
}
