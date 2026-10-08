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
[Route("api/boards/{boardId:guid}/cards")]
public sealed class CardsController(BoardService boards) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<CardDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CardDto>> Create(Guid boardId, CreateCardRequest request, CancellationToken ct)
    {
        var card = await boards.CreateCardAsync(boardId, User.GetActor(), request, ct);
        return StatusCode(StatusCodes.Status201Created, card);
    }

    [HttpPatch("{cardId:guid}")]
    public Task<CardDto> Update(Guid boardId, Guid cardId, UpdateCardRequest request, CancellationToken ct) =>
        boards.UpdateCardAsync(boardId, cardId, User.GetActor(), request, ct);

    [HttpDelete("{cardId:guid}")]
    public async Task<IActionResult> Delete(Guid boardId, Guid cardId, CancellationToken ct)
    {
        await boards.DeleteCardAsync(boardId, cardId, User.GetActor(), ct);
        return NoContent();
    }

    /// <summary>Moves a card within or across columns; returns the final order of every column touched.</summary>
    [HttpPost("{cardId:guid}/move")]
    public Task<CardMovedEvent> Move(Guid boardId, Guid cardId, MoveCardRequest request, CancellationToken ct) =>
        boards.MoveCardAsync(boardId, cardId, User.GetActor(), request, ct);
}
