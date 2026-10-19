namespace sideral_context.Dms;

/// <summary>État de l'indexation d'un document sur le serveur full text.</summary>
public enum FullTextStatus
{
    /// <summary>À indexer (nouveau document, nouvelle version, renommage, changement de code application...).</summary>
    PENDING,
    INDEXED,
    /// <summary>Échec définitif (texte non extractible) : relancé uniquement à la demande ou à la prochaine version.</summary>
    FAILED
}
