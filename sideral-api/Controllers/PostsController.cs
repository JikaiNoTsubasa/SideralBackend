using Microsoft.AspNetCore.Mvc;
using sideral_api.Models.Wall;
using sideral_context.Users;
using sideral_jobcode.Wall;

namespace sideral_api.Controllers;

/// <summary>Mur d'accueil : messages, commentaires et "j'aime".</summary>
[Route("api/posts")]
public class PostsController : BaseController
{
    private readonly WallManager _wallManager;

    public PostsController(WallManager wallManager)
    {
        _wallManager = wallManager;
    }

    private bool IsAdmin => User.IsInRole(Role.ADMIN);

    /// <summary>Messages du plus récent au plus ancien (?page=&amp;limit=). Pagination renvoyée dans les en-têtes.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PostDto>>> GetAll(CancellationToken ct)
    {
        var result = await _wallManager.GetPosts(LoggedUserId, _pagination, ct);
        return OkWithMeta(result.Items.Select(PostDto.From), result.Meta);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<PostDto>> Get(long id, CancellationToken ct)
    {
        return PostDto.From(await _wallManager.GetPost(LoggedUserId, id, ct));
    }

    [HttpPost]
    [ProducesResponseType<PostDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<PostDto>> Create([FromBody] PostRequest request, CancellationToken ct)
    {
        var post = await _wallManager.CreatePost(LoggedUserId, request.Content, ct);
        return CreatedAtAction(nameof(Get), new { id = post.Id }, PostDto.From(post));
    }

    /// <summary>Modification réservée à l'auteur.</summary>
    [HttpPut("{id:long}")]
    public async Task<ActionResult<PostDto>> Update(long id, [FromBody] PostRequest request, CancellationToken ct)
    {
        return PostDto.From(await _wallManager.UpdatePost(LoggedUserId, id, request.Content, ct));
    }

    /// <summary>Suppression par l'auteur ou un administrateur.</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await _wallManager.DeletePost(LoggedUserId, IsAdmin, id, ct);
        return NoContent();
    }

    [HttpPut("{id:long}/like")]
    public async Task<ActionResult<LikeDto>> Like(long id, CancellationToken ct)
    {
        return LikeDto.From(await _wallManager.Like(LoggedUserId, id, ct));
    }

    [HttpDelete("{id:long}/like")]
    public async Task<ActionResult<LikeDto>> Unlike(long id, CancellationToken ct)
    {
        return LikeDto.From(await _wallManager.Unlike(LoggedUserId, id, ct));
    }

    /// <summary>Tous les commentaires d'un message, du plus ancien au plus récent.</summary>
    [HttpGet("{id:long}/comments")]
    public async Task<ActionResult<IEnumerable<CommentDto>>> GetComments(long id, CancellationToken ct)
    {
        return (await _wallManager.GetComments(id, ct)).Select(CommentDto.From).ToList();
    }

    [HttpPost("{id:long}/comments")]
    [ProducesResponseType<CommentDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CommentDto>> AddComment(long id, [FromBody] CommentRequest request, CancellationToken ct)
    {
        var comment = await _wallManager.AddComment(LoggedUserId, id, request.Content, ct);
        return StatusCode(StatusCodes.Status201Created, CommentDto.From(comment));
    }

    /// <summary>Suppression par l'auteur du commentaire, l'auteur du message ou un administrateur.</summary>
    [HttpDelete("{id:long}/comments/{commentId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteComment(long id, long commentId, CancellationToken ct)
    {
        await _wallManager.DeleteComment(LoggedUserId, IsAdmin, id, commentId, ct);
        return NoContent();
    }
}
