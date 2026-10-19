namespace sideral_jobcode.Storage;

public class StorageSettings
{
    public const string SECTION = "Storage";

    /// <summary>Dossier racine des fichiers envoyés. Un chemin relatif est résolu depuis le dossier de l'API.</summary>
    public string RootPath { get; set; } = "storage";
}
