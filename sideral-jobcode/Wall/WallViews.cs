namespace sideral_jobcode.Wall;

public record AuthorView(long Id, string Firstname, string Lastname, string? JobTitle, string? Avatar);

public record CommentView(long Id, long PostId, AuthorView Author, string Content, DateTime CreatedOn);

public record PostView(
    long Id,
    AuthorView Author,
    string Content,
    bool IsAnnouncement,
    DateTime CreatedOn,
    DateTime? UpdatedOn,
    int LikeCount,
    bool LikedByMe,
    int CommentCount,
    List<CommentView> LatestComments);

public record LikeView(long PostId, int LikeCount, bool LikedByMe);
