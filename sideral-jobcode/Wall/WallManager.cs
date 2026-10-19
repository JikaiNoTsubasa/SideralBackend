using System.Linq.Expressions;
using dependancies.Global;
using dependancies.SharedClasses;
using Microsoft.EntityFrameworkCore;
using sideral_context;
using sideral_context.Wall;

namespace sideral_jobcode.Wall;

/// <summary>Mur d'accueil : messages, commentaires et "j'aime".</summary>
public class WallManager : IScoped
{
    /// <summary>Nombre de commentaires renvoyés avec chaque message (les plus récents).</summary>
    public const int LATEST_COMMENTS_COUNT = 3;

    private readonly SideralDbContext _db;

    public WallManager(SideralDbContext db)
    {
        _db = db;
    }

    public async Task<PaginatedResult<PostView>> GetPosts(long userId, Pagination pagination, CancellationToken ct = default)
    {
        return await _db.Posts
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedOn).ThenByDescending(p => p.Id)
            .Select(_ToPostView(userId))
            .ToPaginatedAsync(pagination, ct);
    }

    public async Task<PostView> GetPost(long userId, long postId, CancellationToken ct = default)
    {
        return await _db.Posts
            .AsNoTracking()
            .Where(p => p.Id == postId)
            .Select(_ToPostView(userId))
            .FirstOrDefaultAsync(ct)
            ?? throw _PostNotFound(postId);
    }

    public async Task<PostView> CreatePost(long userId, string content, CancellationToken ct = default)
    {
        var post = new Post { AuthorId = userId, Content = _CheckContent(content, Post.CONTENT_MAX_LENGTH) };
        _db.Posts.Add(post);
        await _db.SaveChangesAsync(ct);
        return await GetPost(userId, post.Id, ct);
    }

    /// <summary>Seul l'auteur peut modifier son message.</summary>
    public async Task<PostView> UpdatePost(long userId, long postId, string content, CancellationToken ct = default)
    {
        var post = await _FindPost(postId, ct);
        if (post.AuthorId != userId)
        {
            throw new ApiException("Not post author", "Vous ne pouvez modifier que vos propres messages.", ApiException.TypeError.FORBIDDEN, "WALL_002");
        }

        post.Content = _CheckContent(content, Post.CONTENT_MAX_LENGTH);
        post.UpdatedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await GetPost(userId, postId, ct);
    }

    /// <summary>L'auteur ou un administrateur peut supprimer un message (commentaires et "j'aime" supprimés en cascade).</summary>
    public async Task DeletePost(long userId, bool isAdmin, long postId, CancellationToken ct = default)
    {
        var post = await _FindPost(postId, ct);
        if (post.AuthorId != userId && !isAdmin)
        {
            throw new ApiException("Not post author", "Vous ne pouvez supprimer que vos propres messages.", ApiException.TypeError.FORBIDDEN, "WALL_003");
        }

        _db.Posts.Remove(post);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<LikeView> Like(long userId, long postId, CancellationToken ct = default)
    {
        await _EnsurePostExists(postId, ct);

        if (!await _db.PostLikes.AnyAsync(l => l.PostId == postId && l.UserId == userId, ct))
        {
            _db.PostLikes.Add(new PostLike { PostId = postId, UserId = userId });
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Double clic concurrent : le "j'aime" existe déjà, l'état final est le bon
            }
        }

        return await _GetLikeView(userId, postId, ct);
    }

    public async Task<LikeView> Unlike(long userId, long postId, CancellationToken ct = default)
    {
        await _EnsurePostExists(postId, ct);
        await _db.PostLikes.Where(l => l.PostId == postId && l.UserId == userId).ExecuteDeleteAsync(ct);
        return await _GetLikeView(userId, postId, ct);
    }

    public async Task<List<CommentView>> GetComments(long postId, CancellationToken ct = default)
    {
        await _EnsurePostExists(postId, ct);

        return await _db.PostComments
            .AsNoTracking()
            .Where(c => c.PostId == postId)
            .OrderBy(c => c.CreatedOn).ThenBy(c => c.Id)
            .Select(_ToCommentView)
            .ToListAsync(ct);
    }

    public async Task<CommentView> AddComment(long userId, long postId, string content, CancellationToken ct = default)
    {
        await _EnsurePostExists(postId, ct);

        var comment = new PostComment { PostId = postId, AuthorId = userId, Content = _CheckContent(content, PostComment.CONTENT_MAX_LENGTH) };
        _db.PostComments.Add(comment);
        await _db.SaveChangesAsync(ct);

        return await _db.PostComments.AsNoTracking().Where(c => c.Id == comment.Id).Select(_ToCommentView).FirstAsync(ct);
    }

    /// <summary>L'auteur du commentaire, l'auteur du message ou un administrateur peut supprimer un commentaire.</summary>
    public async Task DeleteComment(long userId, bool isAdmin, long postId, long commentId, CancellationToken ct = default)
    {
        var comment = await _db.PostComments.Include(c => c.Post).FirstOrDefaultAsync(c => c.Id == commentId && c.PostId == postId, ct)
            ?? throw new ApiException("Comment not found", $"Commentaire {commentId} introuvable.", ApiException.TypeError.RESSOURCE_NOT_FOUND, "WALL_004");

        if (comment.AuthorId != userId && comment.Post!.AuthorId != userId && !isAdmin)
        {
            throw new ApiException("Not comment author", "Vous ne pouvez pas supprimer ce commentaire.", ApiException.TypeError.FORBIDDEN, "WALL_005");
        }

        _db.PostComments.Remove(comment);
        await _db.SaveChangesAsync(ct);
    }

    private static readonly Expression<Func<PostComment, CommentView>> _ToCommentView = c => new CommentView(
        c.Id,
        c.PostId,
        new AuthorView(c.Author!.Id, c.Author.Firstname, c.Author.Lastname, c.Author.JobTitle, c.Author.Avatar),
        c.Content,
        c.CreatedOn);

    private static Expression<Func<Post, PostView>> _ToPostView(long userId) => p => new PostView(
        p.Id,
        new AuthorView(p.Author!.Id, p.Author.Firstname, p.Author.Lastname, p.Author.JobTitle, p.Author.Avatar),
        p.Content,
        p.CreatedOn,
        p.UpdatedOn,
        p.Likes.Count,
        p.Likes.Any(l => l.UserId == userId),
        p.Comments.Count,
        // Les plus récents, renvoyés dans l'ordre chronologique côté client
        p.Comments
            .OrderByDescending(c => c.CreatedOn).ThenByDescending(c => c.Id)
            .Take(LATEST_COMMENTS_COUNT)
            .Select(c => new CommentView(
                c.Id,
                c.PostId,
                new AuthorView(c.Author!.Id, c.Author.Firstname, c.Author.Lastname, c.Author.JobTitle, c.Author.Avatar),
                c.Content,
                c.CreatedOn))
            .ToList());

    private async Task<LikeView> _GetLikeView(long userId, long postId, CancellationToken ct)
    {
        var count = await _db.PostLikes.CountAsync(l => l.PostId == postId, ct);
        var liked = await _db.PostLikes.AnyAsync(l => l.PostId == postId && l.UserId == userId, ct);
        return new LikeView(postId, count, liked);
    }

    private async Task<Post> _FindPost(long postId, CancellationToken ct)
    {
        return await _db.Posts.FirstOrDefaultAsync(p => p.Id == postId, ct) ?? throw _PostNotFound(postId);
    }

    private async Task _EnsurePostExists(long postId, CancellationToken ct)
    {
        if (!await _db.Posts.AnyAsync(p => p.Id == postId, ct))
        {
            throw _PostNotFound(postId);
        }
    }

    private static ApiException _PostNotFound(long postId) =>
        new("Post not found", $"Message {postId} introuvable.", ApiException.TypeError.RESSOURCE_NOT_FOUND, "WALL_001");

    private static string _CheckContent(string content, int maxLength)
    {
        content = content?.Trim() ?? string.Empty;
        if (content.Length == 0)
        {
            throw new ApiException("Empty content", "Le message ne peut pas être vide.", ApiException.TypeError.MODEL_ERROR, "WALL_006");
        }
        if (content.Length > maxLength)
        {
            throw new ApiException("Content too long", $"Le message ne peut pas dépasser {maxLength} caractères.", ApiException.TypeError.MODEL_ERROR, "WALL_007");
        }
        return content;
    }
}
