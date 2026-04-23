using CollaborativeBoard.Contracts;

namespace CollaborativeBoard.ViewModels;

public sealed class BoardsIndexViewModel
{
    public string Nickname { get; set; } = string.Empty;
    public IReadOnlyList<BoardListItemDto> Boards { get; set; } = [];
}

public sealed class BoardWorkspaceViewModel
{
    public string Nickname { get; set; } = string.Empty;
    public BoardWorkspaceDto Board { get; set; } = new();
}
