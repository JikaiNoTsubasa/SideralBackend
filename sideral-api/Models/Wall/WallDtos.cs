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

/// <summary>Message du mur. LatestComments contient les derniers commentaires, du plus ancien au plus récent.</summary>
public record PostDto(
    long Id,
    AuthorDto Author,
    string Content,
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
