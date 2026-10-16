using dependancies.Global;
using dependancies.SharedClasses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using sideral_jobcode.Auth;

namespace sideral_api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
public abstract class BaseController : ControllerBase, IActionFilter
{
    protected Pagination _pagination = new();
    protected string? _search;

    /// <summary>Id de l'utilisateur connecté (claim "sub" du bearer).</summary>
    protected long LoggedUserId
    {
        get
        {
            var value = User.FindFirst(AuthorizationTokenGenerator.USER_ID_CLAIM)?.Value;
            return long.TryParse(value, out var id)
                ? id
                : throw new ApiException("Not authenticated", "Utilisateur non authentifié.", ApiException.TypeError.UNAUTHORIZED);
        }
    }

    protected string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    [NonAction]
    public virtual void OnActionExecuting(ActionExecutingContext context)
    {
        var query = context.HttpContext.Request.Query;

        if (int.TryParse(query["page"], out var page) && page > 0)
            _pagination.Page = page;

        if (int.TryParse(query["limit"], out var limit) && limit > 0)
            _pagination.Limit = Math.Min(limit, Pagination.MAX_LIMIT);

        _search = query["search"];
    }

    [NonAction]
    public virtual void OnActionExecuted(ActionExecutedContext context)
    {
    }

    /// <summary>Renvoie les éléments en 200 et les métadonnées (pagination...) dans les en-têtes.</summary>
    [NonAction]
    protected OkObjectResult OkWithMeta(object? value, IMeta meta)
    {
        foreach (var item in meta.GenerateDictionary())
        {
            Response.Headers[item.Key] = item.Value;
        }
        return Ok(value);
    }
}
