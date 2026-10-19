using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using dependancies.SharedClasses;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace sideral_jobcode.LinkPreview;

/// <summary>
/// Aperçus des liens publiés sur le mur : titre, description, image et site (balises OpenGraph / Twitter / title),
/// oEmbed pour YouTube. Les pages sont lues par l'API (le navigateur ne peut pas lire les autres sites) avec
/// SafeHttpHandler (pas d'adresse interne), une taille et un délai maximum ; résultats mis en cache.
/// </summary>
public partial class LinkPreviewManager : ISingleton
{
    public const string HTTP_CLIENT = "linkpreview";
    public const int URL_MAX_LENGTH = 2000;

    private const int MAX_HTML_BYTES = 512 * 1024;
    private static readonly TimeSpan TIMEOUT = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromHours(6);
    private static readonly TimeSpan FAILURE_CACHE_DURATION = TimeSpan.FromMinutes(30);
    private static readonly JsonSerializerOptions _JSON = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<LinkPreviewManager> _logger;

    static LinkPreviewManager()
    {
        // Pages encore servies en windows-1252, iso-8859-15...
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public LinkPreviewManager(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<LinkPreviewManager> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>Aperçu du lien, ou null s'il n'est pas disponible (le lien reste cliquable côté interface).</summary>
    public async Task<LinkPreviewView?> Get(string? url, CancellationToken ct = default)
    {
        if (url is null || url.Length > URL_MAX_LENGTH || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ApiException("Invalid URL", "Adresse de lien invalide.", ApiException.TypeError.MODEL_ERROR, "LNK_001");
        }

        var key = $"link-preview:{uri.AbsoluteUri}";
        if (_cache.TryGetValue(key, out LinkPreviewView? cached))
        {
            return cached;
        }

        LinkPreviewView? preview = null;
        try
        {
            preview = YoutubeId(uri) is { } videoId ? await _Youtube(uri, videoId, ct) : await _OpenGraph(uri, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Page externe inaccessible, invalide ou exotique : pas d'aperçu (jamais d'erreur pour l'utilisateur)
            _logger.LogInformation("Link preview unavailable for {Url}: {Message}", uri, ex.Message);
        }

        _cache.Set(key, preview, preview is null ? FAILURE_CACHE_DURATION : CACHE_DURATION);
        return preview;
    }

    /// <summary>Identifiant d'une vidéo YouTube (youtube.com/watch?v=, youtu.be/, /shorts/, /embed/, /live/), sinon null.</summary>
    public static string? YoutubeId(Uri uri)
    {
        var host = uri.Host.ToLowerInvariant();
        string? id = null;
        if (host is "youtu.be")
        {
            id = uri.AbsolutePath.Trim('/').Split('/')[0];
        }
        else if (host is "youtube.com" or "www.youtube.com" or "m.youtube.com" or "music.youtube.com")
        {
            var segments = uri.AbsolutePath.Trim('/').Split('/');
            id = segments[0] == "watch"
                ? System.Web.HttpUtility.ParseQueryString(uri.Query)["v"]
                : segments.Length >= 2 && segments[0] is "shorts" or "embed" or "live" ? segments[1] : null;
        }
        return id is not null && _YoutubeIdRegex().IsMatch(id) ? id : null;
    }

    private async Task<LinkPreviewView?> _Youtube(Uri uri, string videoId, CancellationToken ct)
    {
        var watchUrl = $"https://www.youtube.com/watch?v={videoId}";
        var oembed = await _Send<OEmbed>($"https://www.youtube.com/oembed?format=json&url={Uri.EscapeDataString(watchUrl)}", ct);
        return new LinkPreviewView(
            uri.AbsoluteUri,
            _Clean(oembed?.Title, 300),
            _Clean(oembed?.AuthorName, 300),
            $"https://i.ytimg.com/vi/{videoId}/hqdefault.jpg",
            "YouTube",
            videoId);
    }

    private async Task<T?> _Send<T>(string url, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TIMEOUT);
        using var response = await _httpClientFactory.CreateClient(HTTP_CLIENT).GetAsync(url, timeout.Token);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<T>(_JSON, timeout.Token) : default;
    }

    private async Task<LinkPreviewView?> _OpenGraph(Uri uri, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TIMEOUT);

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml");
        request.Headers.AcceptLanguage.ParseAdd("fr-FR,fr;q=0.9,en;q=0.8");
        using var response = await _httpClientFactory.CreateClient(HTTP_CLIENT).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType is not ("text/html" or "application/xhtml+xml"))
        {
            return null;
        }

        // Lecture limitée : les balises utiles sont dans <head>
        var buffer = new byte[MAX_HTML_BYTES];
        var length = 0;
        await using (var stream = await response.Content.ReadAsStreamAsync(timeout.Token))
        {
            int read;
            while (length < buffer.Length && (read = await stream.ReadAsync(buffer.AsMemory(length), timeout.Token)) > 0)
            {
                length += read;
            }
        }
        var html = _Encoding(response.Content.Headers.ContentType?.CharSet).GetString(buffer, 0, length);
        var finalUri = response.RequestMessage?.RequestUri ?? uri;

        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match tag in _MetaTagRegex().Matches(html))
        {
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match attribute in _AttributeRegex().Matches(tag.Value))
            {
                attributes.TryAdd(attribute.Groups[1].Value, attribute.Groups[3].Value);
            }
            var name = attributes.GetValueOrDefault("property") ?? attributes.GetValueOrDefault("name");
            if (name is not null && attributes.TryGetValue("content", out var content))
            {
                meta.TryAdd(name, WebUtility.HtmlDecode(content));
            }
        }

        var title = meta.GetValueOrDefault("og:title") ?? meta.GetValueOrDefault("twitter:title")
            ?? (_TitleRegex().Match(html) is { Success: true } t ? WebUtility.HtmlDecode(t.Groups[1].Value) : null);
        var description = meta.GetValueOrDefault("og:description") ?? meta.GetValueOrDefault("twitter:description") ?? meta.GetValueOrDefault("description");
        var image = meta.GetValueOrDefault("og:image") ?? meta.GetValueOrDefault("og:image:url") ?? meta.GetValueOrDefault("twitter:image");
        var siteName = meta.GetValueOrDefault("og:site_name") ?? finalUri.Host;

        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        // Image : URL absolue http(s) uniquement (relative résolue par rapport à la page)
        string? imageUrl = null;
        if (!string.IsNullOrWhiteSpace(image) && Uri.TryCreate(finalUri, image.Trim(), out var imageUri)
            && (imageUri.Scheme == Uri.UriSchemeHttps || imageUri.Scheme == Uri.UriSchemeHttp))
        {
            imageUrl = imageUri.AbsoluteUri;
        }

        return new LinkPreviewView(uri.AbsoluteUri, _Clean(title, 300), _Clean(description, 500), imageUrl, _Clean(siteName, 100), null);
    }

    private static Encoding _Encoding(string? charset)
    {
        try
        {
            return string.IsNullOrWhiteSpace(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset.Trim('"'));
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }

    private static string? _Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        value = _Whitespace().Replace(value, " ").Trim();
        return value.Length > maxLength ? value[..(maxLength - 1)] + "…" : value;
    }

    private sealed record OEmbed(string? Title, [property: System.Text.Json.Serialization.JsonPropertyName("author_name")] string? AuthorName);

    [GeneratedRegex("^[A-Za-z0-9_-]{11}$")]
    private static partial Regex _YoutubeIdRegex();

    [GeneratedRegex(@"<meta\s[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex _MetaTagRegex();

    [GeneratedRegex(@"([\w:-]+)\s*=\s*([""'])(.*?)\2", RegexOptions.Singleline)]
    private static partial Regex _AttributeRegex();

    [GeneratedRegex(@"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex _TitleRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex _Whitespace();
}
