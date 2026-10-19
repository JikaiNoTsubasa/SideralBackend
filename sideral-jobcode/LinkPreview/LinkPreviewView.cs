namespace sideral_jobcode.LinkPreview;

/// <summary>Aperçu d'un lien (balises OpenGraph, oEmbed YouTube). YoutubeVideoId renseigné pour une vidéo YouTube.</summary>
public record LinkPreviewView(string Url, string? Title, string? Description, string? ImageUrl, string? SiteName, string? YoutubeVideoId);
