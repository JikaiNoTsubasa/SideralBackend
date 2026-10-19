using sideral_api.Models.Principals;
using sideral_context.Dms;
using sideral_jobcode.Dms;

namespace sideral_api.Models.Dms;

public record UserRefDto(long Id, string Firstname, string Lastname, string? Avatar)
{
    public static UserRefDto From(UserRefView u) => new(u.Id, u.Firstname, u.Lastname, u.Avatar);

    public static UserRefDto? FromNullable(UserRefView? u) => u is null ? null : From(u);
}

public record FolderRefDto(long Id, string Name)
{
    public static FolderRefDto From(FolderRefView f) => new(f.Id, f.Name);
}

public record FolderItemDto(long Id, string Name, DateTime CreatedOn, DateTime? UpdatedOn, UserRefDto CreatedBy, PermissionLevel MyLevel)
{
    public static FolderItemDto From(FolderItemView f) => new(f.Id, f.Name, f.CreatedOn, f.UpdatedOn, UserRefDto.From(f.CreatedBy), f.MyLevel);
}

/// <summary>Document dans une liste. Version = libellé de la version courante ("1.2").</summary>
public record DocumentItemDto(
    long Id,
    string Name,
    string? Description,
    string Version,
    long Size,
    string ContentType,
    DateTime UpdatedOn,
    UserRefDto UpdatedBy,
    UserRefDto? CheckedOutBy,
    DateTime? CheckedOutOn)
{
    public static DocumentItemDto From(DocumentItemView d) => new(
        d.Id, d.Name, d.Description, d.Version, d.Size, d.ContentType, d.UpdatedOn,
        UserRefDto.From(d.UpdatedBy), UserRefDto.FromNullable(d.CheckedOutBy), d.CheckedOutOn);
}

/// <summary>Contenu d'un dossier. Folder null = racine.</summary>
public record FolderContentDto(
    FolderRefDto? Folder,
    bool InheritPermissions,
    IEnumerable<FolderRefDto> Breadcrumb,
    PermissionLevel MyLevel,
    bool CanCreateFolder,
    bool CanUpload,
    IEnumerable<FolderItemDto> Folders,
    IEnumerable<DocumentItemDto> Documents)
{
    public static FolderContentDto From(FolderContentView c) => new(
        c.Folder is null ? null : FolderRefDto.From(c.Folder),
        c.InheritPermissions,
        c.Breadcrumb.Select(FolderRefDto.From).ToList(),
        c.MyLevel,
        c.CanCreateFolder,
        c.CanUpload,
        c.Folders.Select(FolderItemDto.From).ToList(),
        c.Documents.Select(DocumentItemDto.From).ToList());
}

public record VersionDto(long Id, string Label, int Major, int Minor, string OriginalName, string ContentType, long Size, string Sha256, string? Comment, DateTime CreatedOn, UserRefDto CreatedBy)
{
    public static VersionDto From(VersionView v) => new(v.Id, v.Label, v.Major, v.Minor, v.OriginalName, v.ContentType, v.Size, v.Sha256, v.Comment, v.CreatedOn, UserRefDto.From(v.CreatedBy));
}

/// <summary>Fiche d'un document. Versions : de la plus récente (courante) à la plus ancienne.</summary>
public record DocumentDto(
    long Id,
    long FolderId,
    string Name,
    string? Description,
    DateTime CreatedOn,
    UserRefDto CreatedBy,
    DateTime? UpdatedOn,
    UserRefDto? CheckedOutBy,
    DateTime? CheckedOutOn,
    PermissionLevel MyLevel,
    bool CanCancelCheckout,
    IEnumerable<FolderRefDto> Breadcrumb,
    IEnumerable<VersionDto> Versions)
{
    public static DocumentDto From(DocumentView d) => new(
        d.Id, d.FolderId, d.Name, d.Description, d.CreatedOn, UserRefDto.From(d.CreatedBy), d.UpdatedOn,
        UserRefDto.FromNullable(d.CheckedOutBy), d.CheckedOutOn, d.MyLevel, d.CanCancelCheckout,
        d.Breadcrumb.Select(FolderRefDto.From).ToList(),
        d.Versions.Select(VersionDto.From).ToList());
}

public record CheckedOutDocumentDto(DocumentItemDto Document, long FolderId, string Location)
{
    public static CheckedOutDocumentDto From(CheckedOutDocumentView c) => new(DocumentItemDto.From(c.Document), c.FolderId, c.Location);
}

public record PermissionEntryDto(PrincipalDto Principal, PermissionLevel Level)
{
    public static PermissionEntryDto From(PermissionEntryView e) => new(PrincipalDto.From(e.Principal), e.Level);
}

public record InheritedPermissionDto(PrincipalDto Principal, PermissionLevel Level, FolderRefDto From)
{
    public static InheritedPermissionDto Of(InheritedPermissionView e) => new(PrincipalDto.From(e.Principal), e.Level, FolderRefDto.From(e.From));
}

public record FolderPermissionsDto(long FolderId, bool IsRoot, bool InheritPermissions, bool CanManage, IEnumerable<PermissionEntryDto> Entries, IEnumerable<InheritedPermissionDto> Inherited)
{
    public static FolderPermissionsDto From(FolderPermissionsView p) => new(
        p.FolderId, p.IsRoot, p.InheritPermissions, p.CanManage,
        p.Entries.Select(PermissionEntryDto.From).ToList(),
        p.Inherited.Select(InheritedPermissionDto.Of).ToList());
}

public record ActivityDto(long Id, DmsAction Action, string? Details, DateTime CreatedOn, UserRefDto User, long FolderId, string FolderName, long? DocumentId, string? DocumentName)
{
    public static ActivityDto From(ActivityView a) => new(a.Id, a.Action, a.Details, a.CreatedOn, UserRefDto.From(a.User), a.FolderId, a.FolderName, a.DocumentId, a.DocumentName);
}

/// <summary>Élément de la corbeille (Type "folder" ou "document"). Location = emplacement d'origine.</summary>
public record TrashItemDto(string Type, long Id, string Name, string Location, DateTime DeletedOn, UserRefDto? DeletedBy)
{
    public static TrashItemDto From(TrashItemView t) => new(t.Type, t.Id, t.Name, t.Location, t.DeletedOn, UserRefDto.FromNullable(t.DeletedBy));
}

/// <summary>Jeton à passer à GET /api/documents/content?token=...</summary>
public record ContentLinkDto(string Token, DateTime ExpiresOn)
{
    public static ContentLinkDto From(ContentLinkView l) => new(l.Token, l.ExpiresOn);
}
