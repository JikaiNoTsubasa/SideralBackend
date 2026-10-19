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

    /// <summary>
    /// Identifiant du document sur le serveur full text (envoyé à l'indexation, renvoyé par la recherche).
    /// Généré par Sideral, indépendant de l'id technique.
    /// </summary>
    public Guid FullTextIdentifier { get; set; } = Guid.NewGuid();

    /// <summary>Indexation full text de la version courante (voir sideral_jobcode.FullText.FullTextIndexer).</summary>
    public FullTextStatus FullTextStatus { get; set; } = FullTextStatus.PENDING;
    public DateTime? FullTextIndexedOn { get; set; }
    public int FullTextAttempts { get; set; }
    /// <summary>Prochaine tentative après une erreur temporaire (serveur injoignable...).</summary>
    public DateTime? FullTextNextAttemptOn { get; set; }

    [MaxLength(1000)]
    public string? FullTextError { get; set; }

    public ICollection<DocumentVersion> Versions { get; set; } = [];

    /// <summary>Demande une (ré)indexation : contenu ou nom modifié.</summary>
    public void QueueFullText()
    {
        FullTextStatus = FullTextStatus.PENDING;
        FullTextAttempts = 0;
        FullTextNextAttemptOn = null;
        FullTextError = null;
    }
}
