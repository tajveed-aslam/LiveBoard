using LiveBoard.Api.Infrastructure;
using LiveBoard.Api.Models;
using LiveBoard.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LiveBoard.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/boards")]
public sealed class BoardsController(BoardService boards) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<BoardSummaryDto>> List(CancellationToken ct) => boards.ListAsync(User.GetUserId(), ct);

    [HttpPost]
    [EnableRateLimiting(RateLimiting.WritePolicy)]
    [ProducesResponseType<BoardDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<BoardDto>> Create(CreateBoardRequest request, CancellationToken ct)
    {
        var board = await boards.CreateAsync(User.GetUserId(), User.IsGuest(), request.Title, request.WithDefaultColumns, ct);
        return CreatedAtAction(nameof(Get), new { boardId = board.Id }, board);
    }

    [HttpGet("{boardId:guid}")]
    [ProducesResponseType<BoardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<BoardDto> Get(Guid boardId, CancellationToken ct) => boards.GetAsync(boardId, User.GetUserId(), ct);

    [HttpPatch("{boardId:guid}")]
    [EnableRateLimiting(RateLimiting.WritePolicy)]
    public async Task<IActionResult> Rename(Guid boardId, RenameRequest request, CancellationToken ct)
    {
        await boards.RenameBoardAsync(boardId, User.GetActor(), request.Title, ct);
        return NoContent();
    }

    [HttpDelete("{boardId:guid}")]
    [EnableRateLimiting(RateLimiting.WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(Guid boardId, CancellationToken ct)
    {
        await boards.DeleteBoardAsync(boardId, User.GetActor(), ct);
        return NoContent();
    }

    /// <summary>Owner only: issues a new share link, so the old one stops working.</summary>
    [HttpPost("{boardId:guid}/share-token")]
    [EnableRateLimiting(RateLimiting.WritePolicy)]
    public async Task<ShareTokenDto> RegenerateShareToken(Guid boardId, CancellationToken ct) =>
        new(await boards.RegenerateShareTokenAsync(boardId, User.GetUserId(), ct));

    /// <summary>Joins the board behind a share link as an editor (no-op if already a member).</summary>
    [HttpPost("join/{shareToken}")]
    [EnableRateLimiting(RateLimiting.WritePolicy)]
    public Task<JoinResultDto> Join(string shareToken, CancellationToken ct) =>
        boards.JoinAsync(shareToken, User.GetActor(), User.IsGuest(), ct);

    [HttpDelete("{boardId:guid}/members/me")]
    [EnableRateLimiting(RateLimiting.WritePolicy)]
    public async Task<IActionResult> Leave(Guid boardId, CancellationToken ct)
    {
        await boards.LeaveAsync(boardId, User.GetUserId(), ct);
        return NoContent();
    }
}
