using System.ComponentModel.DataAnnotations;
using sideral_context.Wall;

namespace sideral_api.Models.Wall;

public class PostRequest
{
    [Required, MaxLength(Post.CONTENT_MAX_LENGTH)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Annonce mise en avant sur le mur (administrateurs uniquement).</summary>
    public bool IsAnnouncement { get; set; }
}

public class CommentRequest
{
    [Required, MaxLength(PostComment.CONTENT_MAX_LENGTH)]
    public string Content { get; set; } = string.Empty;
}
