using System.ComponentModel.DataAnnotations;

namespace sideral_api.Models.Users;

public class CreateUserRequest
{
    [Required, MaxLength(100)]
    public string Login { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Firstname { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Lastname { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? JobTitle { get; set; }

    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;

    public List<string> Roles { get; set; } = [];
}

public class UpdateUserRequest
{
    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Firstname { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Lastname { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? JobTitle { get; set; }

    public bool IsActive { get; set; } = true;

    public List<string> Roles { get; set; } = [];
}

public class UpdateProfileRequest
{
    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Firstname { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Lastname { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? JobTitle { get; set; }
}

public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;
}

public class UpdatePreferencesRequest
{
    /// <summary>light, dark ou system.</summary>
    [Required, MaxLength(20)]
    public string Theme { get; set; } = string.Empty;

    /// <summary>indigo, blue, teal, green, orange, rose ou violet.</summary>
    [Required, MaxLength(20)]
    public string AccentColor { get; set; } = string.Empty;
}
