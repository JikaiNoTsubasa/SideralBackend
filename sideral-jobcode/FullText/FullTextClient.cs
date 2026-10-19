using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using dependancies.SharedClasses;

namespace sideral_jobcode.FullText;

/// <summary>Erreur temporaire d'appel au serveur full text (injoignable, erreur serveur) : l'indexation sera retentée.</summary>
public class FullTextUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Client du serveur full text SokaFT (https://github.com/JikaiNoTsubasa/SokaFT).
/// POST /api/index/queue {name, content, identifier, application} : ajoute ou met à jour le document (même identifiant)
/// et le met en file d'indexation côté serveur. identifier = Document.FullTextIdentifier (GUID), renvoyé par la recherche.
/// </summary>
public class FullTextClient : ISingleton
{
    private static readonly TimeSpan TIMEOUT = TimeSpan.FromSeconds(30);

    private readonly IHttpClientFactory _httpClientFactory;

    public FullTextClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task Index(FullTextSettings settings, string identifier, string name, string content, CancellationToken ct)
    {
        // SokaFT exige un contenu non vide : sans texte extrait, on indexe le nom
        var body = new { name, content = string.IsNullOrWhiteSpace(content) ? name : content, identifier, application = settings.ApplicationCode };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.Url}/api/index/queue") { Content = JsonContent.Create(body) };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TIMEOUT);
        HttpResponseMessage response;
        try
        {
            response = await _httpClientFactory.CreateClient(FullTextManager.HTTP_CLIENT).SendAsync(request, timeout.Token);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new FullTextUnavailableException($"Pas de réponse du serveur full text au bout de {TIMEOUT.TotalSeconds:0} s.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new FullTextUnavailableException($"Serveur full text injoignable : {ex.Message}", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync(ct);
                throw new FullTextUnavailableException($"Le serveur full text a répondu {(int)response.StatusCode} : {detail[..Math.Min(detail.Length, 300)]}");
            }
        }
    }

    /// <summary>
    /// Recherche plein texte : POST /api/index/search {search, application}. Renvoie les identifiants des documents
    /// trouvés (Document.FullTextIdentifier). SokaFT limite le résultat à 10 documents et ne les trie pas par pertinence.
    /// </summary>
    public async Task<List<string>> Search(FullTextSettings settings, string query, CancellationToken ct)
    {
        var body = new { search = ToLuceneQuery(query), application = settings.ApplicationCode };
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.Url}/api/index/search") { Content = JsonContent.Create(body) };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(SEARCH_TIMEOUT);
        try
        {
            using var response = await _httpClientFactory.CreateClient(FullTextManager.HTTP_CLIENT).SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                // SokaFT renvoie 500 si aucun document n'a encore été indexé sous ce code application
                throw new FullTextUnavailableException($"Le serveur full text a répondu {(int)response.StatusCode}.");
            }
            var results = await response.Content.ReadFromJsonAsync<List<SearchResult>>(_JSON, timeout.Token) ?? [];
            return results.Select(r => r.Identifier).OfType<string>().ToList();
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new FullTextUnavailableException($"Pas de réponse du serveur full text au bout de {SEARCH_TIMEOUT.TotalSeconds:0} s.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new FullTextUnavailableException($"Serveur full text injoignable : {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            throw new FullTextUnavailableException("Réponse du serveur full text illisible.", ex);
        }
    }

    /// <summary>
    /// Texte saisi → requête Lucene (syntaxe classique) : les caractères spéciaux sont échappés pour qu'une saisie
    /// libre ne provoque pas d'erreur de syntaxe ; seul le joker « * » en fin de mot est conservé (ex. contr*).
    /// </summary>
    public static string ToLuceneQuery(string query)
    {
        const string special = "+-&|!(){}[]^\"~*?:\\/";
        var words = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(word =>
        {
            var wildcard = word.Length > 1 && word.EndsWith('*') && word.TrimEnd('*').Length > 0;
            var core = wildcard ? word.TrimEnd('*') : word;
            var escaped = new StringBuilder();
            foreach (var c in core)
            {
                if (special.Contains(c))
                    escaped.Append('\\');
                escaped.Append(c);
            }
            return wildcard ? escaped + "*" : escaped.ToString();
        });
        return string.Join(' ', words);
    }

    private static readonly TimeSpan SEARCH_TIMEOUT = TimeSpan.FromSeconds(10);
    private static readonly JsonSerializerOptions _JSON = new(JsonSerializerDefaults.Web);

    private sealed record SearchResult(string? Identifier);
}
