using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using sideral_context.Users;

namespace sideral_context.Dms;

public enum DmsAction
{
    FOLDER_CREATED,
    FOLDER_RENAMED,
    FOLDER_MOVED,
    FOLDER_DELETED,
    FOLDER_RESTORED,
    PERMISSIONS_CHANGED,
    DOCUMENT_CREATED,
    DOCUMENT_UPDATED,
    DOCUMENT_MOVED,
    DOCUMENT_DOWNLOADED,
    DOCUMENT_CHECKED_OUT,
    DOCUMENT_CHECKOUT_CANCELLED,
    DOCUMENT_CHECKED_IN,
    VERSION_RESTORED,
    DOCUMENT_DELETED,
    DOCUMENT_RESTORED
}

/// <summary>
/// Journal d'activité de la GED. FolderId = dossier concerné (ou dossier du document au moment de l'action),
/// DocumentId = document concerné le cas échéant.
/// </summary>
public class DmsActivity
{
    public const int DETAILS_MAX_LENGTH = 1000;

    [Key]
    public long Id { get; set; }

    [ForeignKey(nameof(Folder))]
    public long FolderId { get; set; }
    public Folder? Folder { get; set; }

    [ForeignKey(nameof(Document))]
    public long? DocumentId { get; set; }
    public Document? Document { get; set; }

    [ForeignKey(nameof(User))]
    public long UserId { get; set; }
    public User? User { get; set; }

    [MaxLength(40)]
    public DmsAction Action { get; set; }

    [MaxLength(DETAILS_MAX_LENGTH)]
    public string? Details { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
}
