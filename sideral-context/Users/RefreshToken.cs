using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace sideral_context.Users;

public class RefreshToken
{
    [Key]
    public long Id { get; set; }

    [ForeignKey(nameof(User))]
    public long UserId { get; set; }
    public User? User { get; set; }

    /// <summary>SHA-256 du token : le token en clair n'est jamais stocké.</summary>
    [MaxLength(128)]
    public required string TokenHash { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresOn { get; set; }
    public DateTime? RevokedOn { get; set; }

    [MaxLength(128)]
    public string? ReplacedByTokenHash { get; set; }

    [MaxLength(64)]
    public string? CreatedByIp { get; set; }

    [NotMapped]
    public bool IsActive => RevokedOn is null && ExpiresOn > DateTime.UtcNow;
}
