using LiveBoard.Api.Infrastructure;
using LiveBoard.Api.Models;
using LiveBoard.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LiveBoard.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting(RateLimiting.WritePolicy)]
[Route("api/boards/{boardId:guid}/columns")]
public sealed class ColumnsController(BoardService boards) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ColumnDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ColumnDto>> Create(Guid boardId, RenameRequest request, CancellationToken ct)
    {
        var column = await boards.CreateColumnAsync(boardId, User.GetActor(), request.Title, ct);
        return StatusCode(StatusCodes.Status201Created, column);
    }

    [HttpPatch("{columnId:guid}")]
    public Task<ColumnDto> Rename(Guid boardId, Guid columnId, RenameRequest request, CancellationToken ct) =>
        boards.RenameColumnAsync(boardId, columnId, User.GetActor(), request.Title, ct);

    [HttpDelete("{columnId:guid}")]
    public async Task<IActionResult> Delete(Guid boardId, Guid columnId, CancellationToken ct)
    {
        await boards.DeleteColumnAsync(boardId, columnId, User.GetActor(), ct);
        return NoContent();
    }

    /// <summary>Moves the column to a new position; returns the full resulting column order.</summary>
    [HttpPost("{columnId:guid}/move")]
    public Task<IReadOnlyList<Guid>> Move(Guid boardId, Guid columnId, MoveColumnRequest request, CancellationToken ct) =>
        boards.MoveColumnAsync(boardId, columnId, User.GetActor(), request.ToIndex, ct);
}
