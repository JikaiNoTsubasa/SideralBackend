namespace sideral_context.Dms;

/// <summary>Niveau d'accès à un dossier (et aux documents qu'il contient). Les niveaux sont cumulatifs.</summary>
public enum PermissionLevel
{
    None = 0,
    /// <summary>Lecteur : voir, prévisualiser et télécharger.</summary>
    Read = 1,
    /// <summary>Contributeur : ajouter des documents, extraire / archiver, renommer.</summary>
    Write = 2,
    /// <summary>Gestionnaire : sous-dossiers, droits, déplacement, suppression et corbeille.</summary>
    Manage = 3
}
