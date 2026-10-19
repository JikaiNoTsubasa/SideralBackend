using System.ComponentModel.DataAnnotations;

namespace sideral_context.Users;

public class User
{
    [Key]
    public long Id { get; set; }

    [MaxLength(100)]
    public required string Login { get; set; }

    [MaxLength(255)]
    public required string Email { get; set; }

    [MaxLength(100)]
    public required string Firstname { get; set; }

    [MaxLength(100)]
    public required string Lastname { get; set; }

    [MaxLength(150)]
    public string? JobTitle { get; set; }

    /// <summary>Nom du fichier avatar dans le stockage local (voir sideral_jobcode.Storage.AvatarStorage).</summary>
    [MaxLength(100)]
    public string? Avatar { get; set; }

    /// <summary>Hash PBKDF2 du mot de passe (voir dependancies.Security.HashPBKDF2).</summary>
    [MaxLength(255)]
    public required string Password { get; set; }

    public bool IsActive { get; set; } = true;
    public bool ForceUpdatePassword { get; set; } = false;
    public bool Deleted { get; set; } = false;

    /// <summary>Thème d'affichage (voir UserPreferences.THEMES).</summary>
    [MaxLength(20)]
    public string Theme { get; set; } = UserPreferences.THEME_LIGHT;

    /// <summary>Couleur principale de l'interface (voir UserPreferences.ACCENT_COLORS).</summary>
    [MaxLength(20)]
    public string AccentColor { get; set; } = UserPreferences.DEFAULT_ACCENT;

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedOn { get; set; }
    public DateTime? LastLoginOn { get; set; }

    public ICollection<Role> Roles { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
