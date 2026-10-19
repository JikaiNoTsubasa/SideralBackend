using dependancies.SharedClasses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using sideral_context;
using sideral_context.Dms;
using sideral_jobcode.FullText;

namespace sideral_jobcode.Dms;

/// <summary>
/// Recherche de documents : nom / description (base Sideral) + contenu (serveur full text, si activé).
/// Les résultats sont fusionnés puis filtrés : documents hors corbeille, dans un dossier lisible par l'utilisateur.
/// </summary>
public class DocumentSearchManager : IScoped
{
    public const int QUERY_MIN_LENGTH = 2;
    public const int QUERY_MAX_LENGTH = 200;
    public const int MAX_NAME_RESULTS = 50;

    private readonly SideralDbContext _db;
    private readonly DmsAccess _access;
    private readonly FullTextManager _fullTextManager;
    private readonly FullTextClient _fullTextClient;
    private readonly ILogger<DocumentSearchManager> _logger;

    public DocumentSearchManager(SideralDbContext db, DmsAccess access, FullTextManager fullTextManager, FullTextClient fullTextClient, ILogger<DocumentSearchManager> logger)
    {
        _db = db;
        _access = access;
        _fullTextManager = fullTextManager;
        _fullTextClient = fullTextClient;
        _logger = logger;
    }

    public async Task<DocumentSearchView> Search(long userId, bool isAdmin, string? query, CancellationToken ct = default)
    {
        query = query?.Trim() ?? string.Empty;
        if (query.Length < QUERY_MIN_LENGTH || query.Length > QUERY_MAX_LENGTH)
        {
            throw new ApiException("Invalid search", $"La recherche doit contenir entre {QUERY_MIN_LENGTH} et {QUERY_MAX_LENGTH} caractères.", ApiException.TypeError.MODEL_ERROR, "DMS_050");
        }

        var tree = await _access.Load(userId, isAdmin, ct);
        bool Visible(long folderId) => tree.Can(folderId, PermissionLevel.Read);

        // ---- Nom / description (tous les mots, sans tenir compte de la casse)
        var terms = query.Replace("*", " ").ToLower().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var byName = _db.Documents.AsNoTracking().Where(d => d.DeletedOn == null);
        foreach (var term in terms)
        {
            byName = byName.Where(d => d.Name.ToLower().Contains(term) || (d.Description != null && d.Description.ToLower().Contains(term)));
        }
        var nameMatches = (await byName.Select(d => new { d.Id, d.FolderId }).ToListAsync(ct))
            .Where(d => Visible(d.FolderId))
            .Take(MAX_NAME_RESULTS)
            .ToDictionary(d => d.Id, d => d.FolderId);

        // ---- Contenu (serveur full text)
        var settings = await _fullTextManager.GetSettings(ct);
        var fullTextEnabled = settings.Enabled && settings.Url is not null && settings.ApplicationCode is not null;
        string? fullTextError = null;
        var contentMatches = new Dictionary<long, long>();
        if (fullTextEnabled)
        {
            try
            {
                var identifiers = (await _fullTextClient.Search(settings, query, ct))
                    .Select(i => Guid.TryParse(i, out var guid) ? guid : (Guid?)null)
                    .OfType<Guid>()
                    .ToList();

                // Identifiants inconnus (documents supprimés définitivement, ancien format...) : ignorés
                contentMatches = (await _db.Documents
                        .AsNoTracking()
                        .Where(d => identifiers.Contains(d.FullTextIdentifier) && d.DeletedOn == null)
                        .Select(d => new { d.Id, d.FolderId })
                        .ToListAsync(ct))
                    .Where(d => Visible(d.FolderId))
                    .ToDictionary(d => d.Id, d => d.FolderId);
            }
            catch (FullTextUnavailableException ex)
            {
                _logger.LogWarning("Full text search failed: {Message}", ex.Message);
                fullTextError = "La recherche dans le contenu est momentanément indisponible : seuls les noms et descriptions ont été cherchés.";
            }
        }

        // ---- Fusion : trouvés dans le contenu et le nom, puis le contenu seul, puis le nom seul
        var folderOf = nameMatches.Concat(contentMatches).DistinctBy(p => p.Key).ToDictionary(p => p.Key, p => p.Value);
        var ids = folderOf.Keys.ToList();
        var items = await DocumentManager.QueryItems(_db.Documents.Where(d => ids.Contains(d.Id)), ct);

        var results = items
            .Select(i => new SearchResultView(i, folderOf[i.Id], tree.PathOf(folderOf[i.Id]), nameMatches.ContainsKey(i.Id), contentMatches.ContainsKey(i.Id)))
            .OrderByDescending(r => r.MatchedContent && r.MatchedName)
            .ThenByDescending(r => r.MatchedContent)
            .ThenBy(r => r.Document.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new DocumentSearchView(query, results, fullTextEnabled, fullTextError);
    }
}
