using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using sideral_context.Users;

namespace sideral_context.Dms;

/// <summary>Version d'un document (Major.Minor). Le fichier est stocké sous StorageKey (voir DocumentStorage).</summary>
public class DocumentVersion
{
    public const int COMMENT_MAX_LENGTH = 1000;

    [Key]
    public long Id { get; set; }

    [ForeignKey(nameof(Document))]
    public long DocumentId { get; set; }
    public Document? Document { get; set; }

    public int Major { get; set; }
    public int Minor { get; set; }

    /// <summary>Nom du fichier physique (aléatoire). Une restauration réutilise le fichier de la version restaurée.</summary>
    [MaxLength(64)]
    public required string StorageKey { get; set; }

    /// <summary>Nom du fichier envoyé par l'utilisateur (information uniquement).</summary>
    [MaxLength(Document.NAME_MAX_LENGTH)]
    public required string OriginalName { get; set; }

    /// <summary>Type MIME déterminé par le serveur (signature binaire), jamais celui fourni par le client.</summary>
    [MaxLength(100)]
    public required string ContentType { get; set; }

    public long Size { get; set; }

    /// <summary>Empreinte SHA-256 (hexadécimal) du contenu.</summary>
    [MaxLength(64)]
    public required string Sha256 { get; set; }

    [MaxLength(COMMENT_MAX_LENGTH)]
    public string? Comment { get; set; }

    [ForeignKey(nameof(CreatedBy))]
    public long CreatedById { get; set; }
    public User? CreatedBy { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public string Label => $"{Major}.{Minor}";
}
