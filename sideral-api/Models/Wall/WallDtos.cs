using sideral_jobcode.Wall;

namespace sideral_api.Models.Wall;

public record AuthorDto(long Id, string Firstname, string Lastname, string? JobTitle, string? Avatar)
{
    public static AuthorDto From(AuthorView author) => new(author.Id, author.Firstname, author.Lastname, author.JobTitle, author.Avatar);
}

public record CommentDto(long Id, long PostId, AuthorDto Author, string Content, DateTime CreatedOn)
{
    public static CommentDto From(CommentView comment) => new(
        comment.Id,
        comment.PostId,
        AuthorDto.From(comment.Author),
        comment.Content,
        comment.CreatedOn);
}

/// <summary>Message du mur (IsAnnouncement = annonce mise en avant). LatestComments : derniers commentaires, du plus ancien au plus récent.</summary>
public record PostDto(
    long Id,
    AuthorDto Author,
    string Content,
    bool IsAnnouncement,
    DateTime CreatedOn,
    DateTime? UpdatedOn,
    int LikeCount,
    bool LikedByMe,
    int CommentCount,
    IEnumerable<CommentDto> LatestComments)
{
    public static PostDto From(PostView post) => new(
        post.Id,
        AuthorDto.From(post.Author),
        post.Content,
        post.IsAnnouncement,
        post.CreatedOn,
        post.UpdatedOn,
        post.LikeCount,
        post.LikedByMe,
        post.CommentCount,
        post.LatestComments.OrderBy(c => c.CreatedOn).ThenBy(c => c.Id).Select(CommentDto.From).ToList());
}

public record LikeDto(long PostId, int LikeCount, bool LikedByMe)
{
    public static LikeDto From(LikeView like) => new(like.PostId, like.LikeCount, like.LikedByMe);
}

/// <summary>Aperçu d'un lien publié sur le mur. YoutubeVideoId renseigné pour une vidéo YouTube (lecteur intégré).</summary>
public record LinkPreviewDto(string Url, string? Title, string? Description, string? ImageUrl, string? SiteName, string? YoutubeVideoId)
{
    public static LinkPreviewDto From(sideral_jobcode.LinkPreview.LinkPreviewView p) => new(p.Url, p.Title, p.Description, p.ImageUrl, p.SiteName, p.YoutubeVideoId);
}
