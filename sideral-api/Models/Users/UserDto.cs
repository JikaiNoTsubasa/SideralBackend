using sideral_context.Users;

namespace sideral_api.Models.Users;

public record UserDto(
    long Id,
    string Login,
    string Email,
    string Firstname,
    string Lastname,
    string? JobTitle,
    bool IsActive,
    bool ForceUpdatePassword,
    IEnumerable<string> Roles,
    DateTime CreatedOn,
    DateTime? LastLoginOn)
{
    public static UserDto From(User user) => new(
        user.Id,
        user.Login,
        user.Email,
        user.Firstname,
        user.Lastname,
        user.JobTitle,
        user.IsActive,
        user.ForceUpdatePassword,
        user.Roles.Select(r => r.Key).Order(),
        user.CreatedOn,
        user.LastLoginOn);
}

public record RoleDto(long Id, string Key, string Name)
{
    public static RoleDto From(Role role) => new(role.Id, role.Key, role.Name);
}
