using sideral_context.Dms;
using sideral_jobcode.Principals;

namespace sideral_jobcode.Dms;

public record UserRefView(long Id, string Firstname, string Lastname, string? Avatar);

public record FolderRefView(long Id, string Name);

public record FolderItemView(long Id, string Name, DateTime CreatedOn, DateTime? UpdatedOn, UserRefView CreatedBy, PermissionLevel MyLevel);

public record DocumentItemView(
    long Id,
    string Name,
    string? Description,
    string Version,
    long Size,
    string ContentType,
    DateTime UpdatedOn,
    UserRefView UpdatedBy,
    UserRefView? CheckedOutBy,
    DateTime? CheckedOutOn);

/// <summary>Contenu d'un dossier. Folder null = racine de la GED.</summary>
public record FolderContentView(
    FolderRefView? Folder,
    bool InheritPermissions,
    List<FolderRefView> Breadcrumb,
    PermissionLevel MyLevel,
    bool CanCreateFolder,
    bool CanUpload,
    List<FolderItemView> Folders,
    List<DocumentItemView> Documents);

public record VersionView(
    long Id,
    string Label,
    int Major,
    int Minor,
    string OriginalName,
    string ContentType,
    long Size,
    string Sha256,
    string? Comment,
    DateTime CreatedOn,
    UserRefView CreatedBy);

/// <summary>Fiche d'un document. Versions : de la plus récente (courante) à la plus ancienne.</summary>
public record DocumentView(
    long Id,
    long FolderId,
    string Name,
    string? Description,
    DateTime CreatedOn,
    UserRefView CreatedBy,
    DateTime? UpdatedOn,
    UserRefView? CheckedOutBy,
    DateTime? CheckedOutOn,
    PermissionLevel MyLevel,
    bool CanCancelCheckout,
    List<FolderRefView> Breadcrumb,
    List<VersionView> Versions);

/// <summary>Résultat de recherche : MatchedName = nom ou description, MatchedContent = contenu (serveur full text).</summary>
public record SearchResultView(DocumentItemView Document, long FolderId, string Location, bool MatchedName, bool MatchedContent);

/// <summary>
/// Résultats de recherche. FullTextEnabled = recherche dans le contenu active ; FullTextError = elle a échoué
/// (les résultats sur le nom restent renvoyés).
/// </summary>
public record DocumentSearchView(string Query, List<SearchResultView> Results, bool FullTextEnabled, string? FullTextError);

public record CheckedOutDocumentView(DocumentItemView Document, long FolderId, string Location);

public record PermissionEntryView(PrincipalView Principal, PermissionLevel Level);

public record InheritedPermissionView(PrincipalView Principal, PermissionLevel Level, FolderRefView From);

/// <summary>IsRoot : dossier racine (pas de parent, donc pas d'héritage possible).</summary>
public record FolderPermissionsView(
    long FolderId,
    bool IsRoot,
    bool InheritPermissions,
    bool CanManage,
    List<PermissionEntryView> Entries,
    List<InheritedPermissionView> Inherited);

/// <summary>Entrée de droit à enregistrer : UserId OU GroupId.</summary>
public record PermissionEntryInput(long? UserId, long? GroupId, PermissionLevel Level);

public record ActivityView(long Id, DmsAction Action, string? Details, DateTime CreatedOn, UserRefView User, long FolderId, string FolderName, long? DocumentId, string? DocumentName);

public record TrashItemView(string Type, long Id, string Name, string Location, DateTime DeletedOn, UserRefView? DeletedBy);

public record ContentLinkView(string Token, DateTime ExpiresOn);

/// <summary>Fichier à servir : Inline = affichable dans le navigateur (aperçu), sinon téléchargement.</summary>
public record DocumentContent(string Path, string ContentType, string FileName, bool Inline);
