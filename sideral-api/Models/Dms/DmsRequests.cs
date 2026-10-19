using System.ComponentModel.DataAnnotations;
using sideral_context.Dms;

namespace sideral_api.Models.Dms;

public class CreateFolderRequest
{
    /// <summary>Dossier parent ; null = racine (administrateurs).</summary>
    public long? ParentId { get; set; }

    [Required, MaxLength(Folder.NAME_MAX_LENGTH)]
    public string Name { get; set; } = string.Empty;
}

public class RenameRequest
{
    [Required, MaxLength(Folder.NAME_MAX_LENGTH)]
    public string Name { get; set; } = string.Empty;
}

public class MoveFolderRequest
{
    /// <summary>Nouveau parent ; null = racine (administrateurs).</summary>
    public long? ParentId { get; set; }
}

public class MoveDocumentRequest
{
    public long FolderId { get; set; }
}

public class UpdateDocumentRequest
{
    [Required, MaxLength(Document.NAME_MAX_LENGTH)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(Document.DESCRIPTION_MAX_LENGTH)]
    public string? Description { get; set; }
}

public class PermissionEntryRequest
{
    public long? UserId { get; set; }
    public long? GroupId { get; set; }
    public PermissionLevel Level { get; set; }
}

public class SetPermissionsRequest
{
    public bool InheritPermissions { get; set; } = true;
    public List<PermissionEntryRequest> Entries { get; set; } = [];
}
