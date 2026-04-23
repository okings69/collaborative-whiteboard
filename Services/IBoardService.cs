using CollaborativeBoard.Contracts;

namespace CollaborativeBoard.Services;

public interface IBoardService
{
    Task<IReadOnlyList<BoardListItemDto>> GetBoardsAsync();
    Task<BoardWorkspaceDto?> GetBoardWorkspaceAsync(Guid boardId);
    Task<IReadOnlyList<DrawingElementDto>> GetElementsAsync(Guid boardId, Guid pageId);
    Task<BoardListItemDto?> FindBoardByNameAsync(string boardName);
    Task<BoardWorkspaceDto?> FindBoardByShareCodeAsync(string shareCode);
    Task<BoardWorkspaceDto?> CreateBoardAsync(string boardName, string nickname);
    Task<BoardPageDto?> AddPageAsync(AddPageRequest request);
    Task<Guid?> RemovePageAsync(RemovePageRequest request);
    Task<DrawingElementDto?> UpsertElementAsync(UpsertElementRequest request);
    Task<bool> RemoveElementAsync(RemoveElementRequest request);
    Task<BoardParticipantDto> EnsureUserAsync(string nickname, Guid? activePageId = null);
}
