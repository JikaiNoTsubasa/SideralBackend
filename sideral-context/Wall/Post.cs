using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using sideral_context.Users;

namespace sideral_context.Wall;

/// <summary>Message publié sur le mur d'accueil.</summary>
public class Post
{
    public const int CONTENT_MAX_LENGTH = 5000;

    [Key]
    public long Id { get; set; }

    [ForeignKey(nameof(Author))]
    public long AuthorId { get; set; }
    public User? Author { get; set; }

    [MaxLength(CONTENT_MAX_LENGTH)]
    public required string Content { get; set; }

    /// <summary>Annonce officielle (mise en avant sur le mur) : réservée aux administrateurs.</summary>
    public bool IsAnnouncement { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedOn { get; set; }

    public ICollection<PostComment> Comments { get; set; } = [];
    public ICollection<PostLike> Likes { get; set; } = [];
}
