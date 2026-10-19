using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using sideral_context.Groups;
using sideral_context.Users;

namespace sideral_context.Dms;

/// <summary>Droit accordé sur un dossier à un utilisateur OU à un groupe (exactement l'un des deux).</summary>
public class FolderPermission
{
    [Key]
    public long Id { get; set; }

    [ForeignKey(nameof(Folder))]
    public long FolderId { get; set; }
    public Folder? Folder { get; set; }

    [ForeignKey(nameof(User))]
    public long? UserId { get; set; }
    public User? User { get; set; }

    [ForeignKey(nameof(Group))]
    public long? GroupId { get; set; }
    public Group? Group { get; set; }

    public PermissionLevel Level { get; set; }
}
