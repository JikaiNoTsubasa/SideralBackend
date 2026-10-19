using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using sideral_context.Users;

namespace sideral_context.Dms;

/// <summary>
/// Dossier de la GED. ParentId null = dossier racine (créé par un administrateur).
/// Supprimé = DeletedOn renseigné (corbeille) : tout son contenu est alors considéré comme supprimé.
/// </summary>
public class Folder
{
    public const int NAME_MAX_LENGTH = 255;

    [Key]
    public long Id { get; set; }

    [MaxLength(NAME_MAX_LENGTH)]
    public required string Name { get; set; }

    [ForeignKey(nameof(Parent))]
    public long? ParentId { get; set; }
    public Folder? Parent { get; set; }

    /// <summary>Si vrai, les droits du dossier parent s'appliquent en plus des droits propres.</summary>
    public bool InheritPermissions { get; set; } = true;

    [ForeignKey(nameof(CreatedBy))]
    public long CreatedById { get; set; }
    public User? CreatedBy { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedOn { get; set; }

    public DateTime? DeletedOn { get; set; }

    [ForeignKey(nameof(DeletedBy))]
    public long? DeletedById { get; set; }
    public User? DeletedBy { get; set; }

    public ICollection<Folder> Children { get; set; } = [];
    public ICollection<Document> Documents { get; set; } = [];
    public ICollection<FolderPermission> Permissions { get; set; } = [];
}
