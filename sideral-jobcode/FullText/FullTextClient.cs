using System.Net.Http.Json;
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
}
