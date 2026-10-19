using System.ComponentModel.DataAnnotations.Schema;
using sideral_context.Users;

namespace sideral_context.Wall;

/// <summary>"J'aime" d'un utilisateur sur un message (clé composite PostId + UserId).</summary>
public class PostLike
{
    [ForeignKey(nameof(Post))]
    public long PostId { get; set; }
    public Post? Post { get; set; }

    [ForeignKey(nameof(User))]
    public long UserId { get; set; }
    public User? User { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
}
