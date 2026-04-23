using CollaborativeBoard.Contracts;
using CollaborativeBoard.Services;
using Microsoft.AspNetCore.Mvc;

namespace CollaborativeBoard.Controllers;

[ApiController]
[Route("api/boards")]
public sealed class BoardsApiController(IBoardService boardService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BoardListItemDto>>> GetBoards()
    {
        return Ok(await boardService.GetBoardsAsync());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BoardWorkspaceDto>> GetBoard(Guid id)
    {
        var board = await boardService.GetBoardWorkspaceAsync(id);
        return board is null ? NotFound() : Ok(board);
    }

    [HttpGet("share/{shareCode}")]
    public async Task<ActionResult<BoardWorkspaceDto>> GetBoardByShareCode(string shareCode)
    {
        var board = await boardService.FindBoardByShareCodeAsync(shareCode);
        return board is null ? NotFound() : Ok(board);
    }

    [HttpGet("{boardId:guid}/pages/{pageId:guid}/elements")]
    public async Task<ActionResult<IReadOnlyList<DrawingElementDto>>> GetElements(Guid boardId, Guid pageId)
    {
        return Ok(await boardService.GetElementsAsync(boardId, pageId));
    }

    [HttpPost]
    public async Task<ActionResult<BoardWorkspaceDto>> CreateBoard(CreateBoardApiRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Nickname))
        {
            return BadRequest("Name and nickname are required.");
        }

        var existingBoard = await boardService.FindBoardByNameAsync(request.Name);
        if (existingBoard is not null)
        {
            return Conflict(existingBoard);
        }

        var board = await boardService.CreateBoardAsync(request.Name, request.Nickname);
        return board is null ? BadRequest() : Ok(board);
    }

    [HttpPost("{boardId:guid}/pages")]
    public async Task<ActionResult<BoardPageDto>> AddPage(Guid boardId, [FromBody] AddPageRequest? request)
    {
        request ??= new AddPageRequest();
        request.BoardId = boardId;
        var page = await boardService.AddPageAsync(request);
        return page is null ? NotFound() : Ok(page);
    }

    [HttpDelete("{boardId:guid}/pages/{pageId:guid}")]
    public async Task<IActionResult> RemovePage(Guid boardId, Guid pageId)
    {
        var nextPageId = await boardService.RemovePageAsync(new RemovePageRequest
        {
            BoardId = boardId,
            PageId = pageId
        });

        return nextPageId is null ? NotFound() : Ok(new { nextPageId });
    }

    [HttpPost("{boardId:guid}/pages/{pageId:guid}/elements")]
    public async Task<ActionResult<DrawingElementDto>> SaveElement(Guid boardId, Guid pageId, UpsertElementRequest request)
    {
        request.BoardId = boardId;
        request.PageId = pageId;
        var element = await boardService.UpsertElementAsync(request);
        return element is null ? NotFound() : Ok(element);
    }
}
