using CollaborativeBoard.Contracts;
using CollaborativeBoard.Services;
using CollaborativeBoard.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CollaborativeBoard.Controllers;

public sealed class BoardsController(IBoardService boardService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? nickname)
    {
        var model = new BoardsIndexViewModel
        {
            Nickname = nickname?.Trim() ?? string.Empty,
            Boards = await boardService.GetBoardsAsync()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateBoardInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Nickname) || string.IsNullOrWhiteSpace(input.BoardName))
        {
            TempData["BoardError"] = "Nickname and board name are required.";
            return RedirectToAction(nameof(Index), new { nickname = input.Nickname });
        }

        var existingBoard = await boardService.FindBoardByNameAsync(input.BoardName);
        if (existingBoard is not null)
        {
            TempData["BoardError"] = $"A board named \"{existingBoard.Name}\" already exists. You were redirected to it instead.";
            return RedirectToAction(nameof(Workspace), new
            {
                id = existingBoard.Id,
                nickname = input.Nickname.Trim()
            });
        }

        var createdBoard = await boardService.CreateBoardAsync(input.BoardName, input.Nickname);
        if (createdBoard is null)
        {
            TempData["BoardError"] = "Board creation failed.";
            return RedirectToAction(nameof(Index), new { nickname = input.Nickname.Trim() });
        }

        return RedirectToAction(nameof(Workspace), new
        {
            id = createdBoard.Id,
            nickname = input.Nickname.Trim()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Join(string nickname, string boardReference)
    {
        nickname = nickname?.Trim() ?? string.Empty;
        boardReference = boardReference?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(nickname) || string.IsNullOrWhiteSpace(boardReference))
        {
            TempData["BoardError"] = "Nickname and board reference are required.";
            return RedirectToAction(nameof(Index), new { nickname });
        }

        if (!TryParseBoardReference(boardReference, out var boardId))
        {
            TempData["BoardError"] = "Board reference is invalid. Use a board ID or a full invite link.";
            return RedirectToAction(nameof(Index), new { nickname });
        }

        var board = await boardService.GetBoardWorkspaceAsync(boardId);
        if (board is null)
        {
            TempData["BoardError"] = "Board not found. Check the board ID or invite link.";
            return RedirectToAction(nameof(Index), new { nickname });
        }

        return RedirectToAction(nameof(Workspace), new
        {
            id = boardId,
            nickname
        });
    }

    [HttpGet]
    public async Task<IActionResult> Workspace(Guid id, [FromQuery] string? nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
        {
            return RedirectToAction(nameof(Index));
        }

        var board = await boardService.GetBoardWorkspaceAsync(id);
        if (board is null)
        {
            return NotFound();
        }

        var model = new BoardWorkspaceViewModel
        {
            Nickname = nickname.Trim(),
            Board = board
        };

        return View(model);
    }

    private static bool TryParseBoardReference(string input, out Guid boardId)
    {
        if (Guid.TryParse(input, out boardId))
        {
            return true;
        }

        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            boardId = Guid.Empty;
            return false;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var candidate = segments.LastOrDefault();
        return Guid.TryParse(candidate, out boardId);
    }
}
