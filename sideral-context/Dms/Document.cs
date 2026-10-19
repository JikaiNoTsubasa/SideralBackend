using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using sideral_context.Users;

namespace sideral_context.Dms;

/// <summary>
/// Document de la GED. Le contenu est porté par les versions (la version courante est la plus récente).
/// Extrait (checkout) = CheckedOutById renseigné : seul cet utilisateur peut alors le modifier.
/// </summary>
public class Document
{
    public const int NAME_MAX_LENGTH = 255;
    public const int DESCRIPTION_MAX_LENGTH = 2000;

    [Key]
    public long Id { get; set; }

    [ForeignKey(nameof(Folder))]
    public long FolderId { get; set; }
    public Folder? Folder { get; set; }

    /// <summary>Nom affiché, extension comprise (jamais utilisé pour le stockage physique).</summary>
    [MaxLength(NAME_MAX_LENGTH)]
    public required string Name { get; set; }

    [MaxLength(DESCRIPTION_MAX_LENGTH)]
    public string? Description { get; set; }

    [ForeignKey(nameof(CreatedBy))]
    public long CreatedById { get; set; }
    public User? CreatedBy { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedOn { get; set; }

    [ForeignKey(nameof(CheckedOutBy))]
    public long? CheckedOutById { get; set; }
    public User? CheckedOutBy { get; set; }
    public DateTime? CheckedOutOn { get; set; }

    public DateTime? DeletedOn { get; set; }

    [ForeignKey(nameof(DeletedBy))]
    public long? DeletedById { get; set; }
    public User? DeletedBy { get; set; }

    public ICollection<DocumentVersion> Versions { get; set; } = [];
}
