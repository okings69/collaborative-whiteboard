using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CollaborativeBoard.Contracts;
using CollaborativeBoard.Data;
using CollaborativeBoard.Entities;
using Microsoft.EntityFrameworkCore;

namespace CollaborativeBoard.Services;

public sealed class BoardService(AppDbContext dbContext) : IBoardService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<BoardListItemDto>> GetBoardsAsync()
    {
        var boards = await dbContext.Boards
            .AsNoTracking()
            .Include(board => board.Pages.OrderBy(page => page.SortOrder))
            .ThenInclude(page => page.Elements.OrderBy(element => element.LayerOrder).ThenBy(element => element.CreatedAtUtc))
            .OrderByDescending(board => board.UpdatedAtUtc)
            .ToListAsync();

        return boards.Select(MapBoardListItem).ToList();
    }

    public async Task<BoardWorkspaceDto?> GetBoardWorkspaceAsync(Guid boardId)
    {
        var board = await LoadBoardGraph(boardId);
        return board is null ? null : MapBoard(board);
    }

    public async Task<IReadOnlyList<DrawingElementDto>> GetElementsAsync(Guid boardId, Guid pageId)
    {
        return await dbContext.DrawingElements
            .AsNoTracking()
            .Where(element => element.BoardPageId == pageId && element.BoardPage!.BoardId == boardId)
            .OrderBy(element => element.LayerOrder)
            .ThenBy(element => element.CreatedAtUtc)
            .Select(element => MapElement(element))
            .ToListAsync();
    }

    public async Task<BoardListItemDto?> FindBoardByNameAsync(string boardName)
    {
        var normalizedName = boardName.Trim().ToLowerInvariant();

        var board = await dbContext.Boards
            .AsNoTracking()
            .Include(entry => entry.Pages.OrderBy(page => page.SortOrder))
            .ThenInclude(page => page.Elements.OrderBy(element => element.LayerOrder).ThenBy(element => element.CreatedAtUtc))
            .Where(board => board.Name.ToLower() == normalizedName)
            .OrderByDescending(board => board.UpdatedAtUtc)
            .FirstOrDefaultAsync();

        return board is null ? null : MapBoardListItem(board);
    }

    public async Task<BoardWorkspaceDto?> FindBoardByShareCodeAsync(string shareCode)
    {
        var board = await dbContext.Boards
            .AsNoTracking()
            .Include(entry => entry.Pages.OrderBy(page => page.SortOrder))
            .ThenInclude(page => page.Elements.OrderBy(element => element.LayerOrder))
            .FirstOrDefaultAsync(entry => entry.ShareCode == shareCode);

        return board is null ? null : MapBoard(board);
    }

    public async Task<BoardWorkspaceDto?> CreateBoardAsync(string boardName, string nickname)
    {
        var owner = await EnsureParticipantEntityAsync(nickname);
        var now = DateTime.UtcNow;

        var board = new Board
        {
            Id = Guid.NewGuid(),
            Name = boardName.Trim(),
            ShareCode = CreateShareCode(),
            CreatedByNickname = owner.Nickname,
            OwnerParticipantId = owner.Id,
            AccentColor = owner.AccentColor,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Pages =
            [
                new BoardPage
                {
                    Id = Guid.NewGuid(),
                    Title = "Page 1",
                    SortOrder = 1,
                    CreatedAtUtc = now
                }
            ]
        };

        dbContext.Boards.Add(board);
        await dbContext.SaveChangesAsync();
        return await GetBoardWorkspaceAsync(board.Id);
    }

    public async Task<BoardPageDto?> AddPageAsync(AddPageRequest request)
    {
        var board = await dbContext.Boards
            .FirstOrDefaultAsync(entry => entry.Id == request.BoardId);

        if (board is null)
        {
            return null;
        }

        var nextSortOrder = await dbContext.BoardPages
            .Where(entry => entry.BoardId == request.BoardId)
            .Select(entry => (int?)entry.SortOrder)
            .MaxAsync() ?? 0;

        var page = new BoardPage
        {
            Id = Guid.NewGuid(),
            BoardId = request.BoardId,
            Title = string.IsNullOrWhiteSpace(request.Title) ? $"Page {nextSortOrder + 1}" : request.Title.Trim(),
            SortOrder = nextSortOrder + 1,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.BoardPages.Add(page);
        board.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        return new BoardPageDto
        {
            Id = page.Id,
            Title = page.Title,
            SortOrder = page.SortOrder,
            Elements = []
        };
    }

    public async Task<Guid?> RemovePageAsync(RemovePageRequest request)
    {
        var board = await dbContext.Boards
            .Include(entry => entry.Pages.OrderBy(page => page.SortOrder))
            .ThenInclude(page => page.Elements)
            .FirstOrDefaultAsync(entry => entry.Id == request.BoardId);

        if (board is null || board.Pages.Count <= 1)
        {
            return null;
        }

        var page = board.Pages.FirstOrDefault(entry => entry.Id == request.PageId);
        if (page is null)
        {
            return null;
        }

        dbContext.BoardPages.Remove(page);

        var sortOrder = 1;
        foreach (var remainingPage in board.Pages.Where(entry => entry.Id != page.Id).OrderBy(entry => entry.SortOrder))
        {
            remainingPage.SortOrder = sortOrder++;
        }

        board.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        return await dbContext.BoardPages
            .Where(entry => entry.BoardId == request.BoardId)
            .OrderBy(entry => entry.SortOrder)
            .Select(entry => (Guid?)entry.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<DrawingElementDto?> UpsertElementAsync(UpsertElementRequest request)
    {
        var page = await dbContext.BoardPages
            .Include(entry => entry.Board)
            .FirstOrDefaultAsync(entry => entry.Id == request.PageId && entry.BoardId == request.BoardId);

        if (page is null)
        {
            return null;
        }

        var user = await EnsureParticipantEntityAsync(request.Nickname);
        var elementId = request.Element.Id == Guid.Empty ? Guid.NewGuid() : request.Element.Id;
        var entity = await dbContext.DrawingElements
            .FirstOrDefaultAsync(entry => entry.Id == elementId && entry.BoardPageId == page.Id);
        var isNew = entity is null;

        if (entity is null)
        {
            entity = new DrawingElement
            {
                Id = elementId,
                BoardPageId = page.Id,
                CreatedAtUtc = DateTime.UtcNow
            };
            dbContext.DrawingElements.Add(entity);
        }

        await ApplyElementValuesAsync(entity, request, user, page.Id);

        page.Board!.UpdatedAtUtc = DateTime.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException) when (!isNew)
        {
            var existsInDatabase = await dbContext.DrawingElements
                .AsNoTracking()
                .AnyAsync(entry => entry.Id == elementId && entry.BoardPageId == page.Id);

            if (existsInDatabase)
            {
                throw;
            }

            dbContext.Entry(entity).State = EntityState.Detached;

            entity = new DrawingElement
            {
                Id = elementId,
                BoardPageId = page.Id,
                CreatedAtUtc = DateTime.UtcNow
            };

            await ApplyElementValuesAsync(entity, request, user, page.Id);
            dbContext.DrawingElements.Add(entity);
            page.Board!.UpdatedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
        }

        return MapElement(entity);
    }

    public async Task<bool> RemoveElementAsync(RemoveElementRequest request)
    {
        var entity = await dbContext.DrawingElements
            .Include(element => element.BoardPage!)
            .ThenInclude(page => page.Board)
            .FirstOrDefaultAsync(element => element.Id == request.ElementId && element.BoardPageId == request.PageId && element.BoardPage!.BoardId == request.BoardId);

        if (entity is null)
        {
            return false;
        }

        dbContext.DrawingElements.Remove(entity);
        entity.BoardPage!.Board!.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<BoardParticipantDto> EnsureUserAsync(string nickname, Guid? activePageId = null)
    {
        var user = await EnsureParticipantEntityAsync(nickname);
        return new BoardParticipantDto
        {
            Nickname = user.Nickname,
            AccentColor = user.AccentColor,
            ActivePageId = activePageId,
            LastSeenAtUtc = user.LastSeenAtUtc
        };
    }

    private async Task<Board?> LoadBoardGraph(Guid boardId)
    {
        return await dbContext.Boards
            .AsNoTracking()
            .Include(entry => entry.Pages.OrderBy(page => page.SortOrder))
            .ThenInclude(page => page.Elements.OrderBy(element => element.LayerOrder).ThenBy(element => element.CreatedAtUtc))
            .FirstOrDefaultAsync(entry => entry.Id == boardId);
    }

    private async Task<Participant> EnsureParticipantEntityAsync(string nickname)
    {
        var trimmedNickname = string.IsNullOrWhiteSpace(nickname) ? "Guest" : nickname.Trim();
        var normalizedNickname = trimmedNickname.ToLowerInvariant();
        var user = await dbContext.Participants.FirstOrDefaultAsync(entry => entry.Nickname.ToLower() == normalizedNickname);

        if (user is null)
        {
            user = new Participant
            {
                Id = Guid.NewGuid(),
                Nickname = trimmedNickname,
                AccentColor = PickAccent(trimmedNickname),
                CreatedAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow
            };
            dbContext.Participants.Add(user);
        }
        else
        {
            user.LastSeenAtUtc = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync();
        return user;
    }

    private static BoardListItemDto MapBoardListItem(Board board)
    {
        var previewPage = board.Pages
            .OrderBy(page => page.SortOrder)
            .FirstOrDefault(page => page.Elements.Count > 0)
            ?? board.Pages.OrderBy(page => page.SortOrder).FirstOrDefault();

        var previewElements = previewPage?.Elements
            .OrderBy(element => element.LayerOrder)
            .ThenBy(element => element.CreatedAtUtc)
            .Take(10)
            .Select(MapElement)
            .ToList()
            ?? [];

        return new BoardListItemDto
        {
            Id = board.Id,
            Name = board.Name,
            ShareCode = board.ShareCode,
            AccentColor = board.AccentColor,
            UpdatedAtUtc = board.UpdatedAtUtc,
            PageCount = board.Pages.Count,
            ElementCount = board.Pages.SelectMany(page => page.Elements).Count(),
            PreviewElements = previewElements
        };
    }

    private static BoardWorkspaceDto MapBoard(Board board)
    {
        return new BoardWorkspaceDto
        {
            Id = board.Id,
            Name = board.Name,
            ShareCode = board.ShareCode,
            AccentColor = board.AccentColor,
            CreatedByNickname = board.CreatedByNickname,
            Pages = board.Pages
                .OrderBy(page => page.SortOrder)
                .Select(page => new BoardPageDto
                {
                    Id = page.Id,
                    Title = page.Title,
                    SortOrder = page.SortOrder,
                    Elements = page.Elements
                        .OrderBy(element => element.LayerOrder)
                        .ThenBy(element => element.CreatedAtUtc)
                        .Select(MapElement)
                        .ToList()
                })
                .ToList()
        };
    }

    private static DrawingElementDto MapElement(DrawingElement element)
    {
        var points = string.IsNullOrWhiteSpace(element.PointsJson)
            ? []
            : JsonSerializer.Deserialize<List<CanvasPointDto>>(element.PointsJson, JsonOptions) ?? [];

        return new DrawingElementDto
        {
            Id = element.Id,
            ElementType = element.ElementType.ToString().ToLowerInvariant(),
            StrokeColor = element.StrokeColor,
            FillColor = element.FillColor,
            StrokeWidth = element.StrokeWidth,
            X = element.X,
            Y = element.Y,
            Width = element.Width,
            Height = element.Height,
            FontSize = element.FontSize,
            LayerOrder = element.LayerOrder,
            TextContent = element.TextContent,
            MetadataJson = element.MetadataJson,
            VersionToken = element.VersionToken,
            Points = points,
            CreatedByNickname = element.CreatedByNickname,
            TimestampUtc = element.UpdatedAtUtc == default ? element.CreatedAtUtc : element.UpdatedAtUtc
        };
    }

    private async Task ApplyElementValuesAsync(DrawingElement entity, UpsertElementRequest request, Participant user, Guid pageId)
    {
        entity.CreatedByParticipantId = user.Id;
        entity.ElementType = ParseElementType(request.Element.ElementType);
        entity.StrokeColor = request.Element.StrokeColor;
        entity.FillColor = request.Element.FillColor;
        entity.StrokeWidth = request.Element.StrokeWidth;
        entity.X = request.Element.X;
        entity.Y = request.Element.Y;
        entity.Width = request.Element.Width;
        entity.Height = request.Element.Height;
        entity.FontSize = request.Element.FontSize;
        entity.TextContent = request.Element.TextContent;
        entity.PointsJson = JsonSerializer.Serialize(request.Element.Points, JsonOptions);
        entity.MetadataJson = request.Element.MetadataJson;
        entity.VersionToken = Guid.NewGuid().ToString("n");
        entity.LayerOrder = request.Element.LayerOrder == 0
            ? await GetNextLayerAsync(pageId, entity.Id)
            : request.Element.LayerOrder;
        entity.CreatedByNickname = user.Nickname;
        entity.UpdatedAtUtc = DateTime.UtcNow;
    }

    private async Task<int> GetNextLayerAsync(Guid pageId, Guid currentId)
    {
        var maxLayer = await dbContext.DrawingElements
            .Where(entry => entry.BoardPageId == pageId && entry.Id != currentId)
            .Select(entry => (int?)entry.LayerOrder)
            .MaxAsync() ?? 0;

        return maxLayer + 1;
    }

    private static DrawingElementType ParseElementType(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "rectangle" => DrawingElementType.Rectangle,
            "circle" => DrawingElementType.Circle,
            "text" => DrawingElementType.Text,
            _ => DrawingElementType.Pen
        };
    }

    private static string CreateShareCode()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(5)).ToLowerInvariant();
    }

    private static string PickAccent(string value)
    {
        var palette = new[]
        {
            "#2563eb",
            "#0f766e",
            "#ea580c",
            "#7c3aed",
            "#be123c",
            "#0891b2"
        };

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return palette[hash[0] % palette.Length];
    }
}
