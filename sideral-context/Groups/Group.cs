using System.ComponentModel.DataAnnotations;
using sideral_context.Users;

namespace sideral_context.Groups;

/// <summary>Groupe d'utilisateurs (équipe), utilisable dans les droits des dossiers.</summary>
public class Group
{
    public const int NAME_MAX_LENGTH = 100;
    public const int DESCRIPTION_MAX_LENGTH = 500;

    [Key]
    public long Id { get; set; }

    [MaxLength(NAME_MAX_LENGTH)]
    public required string Name { get; set; }

    [MaxLength(DESCRIPTION_MAX_LENGTH)]
    public string? Description { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedOn { get; set; }

    public ICollection<User> Members { get; set; } = [];
}
