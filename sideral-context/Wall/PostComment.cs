using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using sideral_context.Users;

namespace sideral_context.Wall;

public class PostComment
{
    public const int CONTENT_MAX_LENGTH = 2000;

    [Key]
    public long Id { get; set; }

    [ForeignKey(nameof(Post))]
    public long PostId { get; set; }
    public Post? Post { get; set; }

    [ForeignKey(nameof(Author))]
    public long AuthorId { get; set; }
    public User? Author { get; set; }

    [MaxLength(CONTENT_MAX_LENGTH)]
    public required string Content { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
}
