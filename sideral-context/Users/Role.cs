using System.ComponentModel.DataAnnotations;

namespace sideral_context.Users;

public class Role
{
    public const string ADMIN = "ADMIN";
    public const string USER = "USER";

    [Key]
    public long Id { get; set; }

    /// <summary>Clé technique unique, utilisée dans les claims du bearer.</summary>
    [MaxLength(50)]
    public required string Key { get; set; }

    [MaxLength(100)]
    public required string Name { get; set; }

    public ICollection<User> Users { get; set; } = [];
}
