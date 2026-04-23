namespace CollaborativeBoard.Contracts;

public sealed class BoardListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShareCode { get; set; } = string.Empty;
    public string AccentColor { get; set; } = string.Empty;
    public int PageCount { get; set; }
    public int ElementCount { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public IReadOnlyList<DrawingElementDto> PreviewElements { get; set; } = [];
}

public sealed class BoardWorkspaceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShareCode { get; set; } = string.Empty;
    public string AccentColor { get; set; } = string.Empty;
    public string CreatedByNickname { get; set; } = string.Empty;
    public IReadOnlyList<BoardPageDto> Pages { get; set; } = [];
}

public sealed class BoardPageDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public IReadOnlyList<DrawingElementDto> Elements { get; set; } = [];
}

public sealed class DrawingElementDto
{
    public Guid Id { get; set; }
    public string ElementType { get; set; } = "pen";
    public string StrokeColor { get; set; } = "#111827";
    public string? FillColor { get; set; }
    public float StrokeWidth { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public float FontSize { get; set; }
    public int LayerOrder { get; set; }
    public string? TextContent { get; set; }
    public string? MetadataJson { get; set; }
    public string? VersionToken { get; set; }
    public IReadOnlyList<CanvasPointDto> Points { get; set; } = [];
    public string CreatedByNickname { get; set; } = "Guest";
    public DateTime TimestampUtc { get; set; }
}

public sealed class CanvasPointDto
{
    public float X { get; set; }
    public float Y { get; set; }
}

public sealed class CreateBoardInput
{
    public string Nickname { get; set; } = string.Empty;
    public string BoardName { get; set; } = string.Empty;
}

public sealed class CreateBoardApiRequest
{
    public string Nickname { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class AddPageRequest
{
    public Guid BoardId { get; set; }
    public string? Title { get; set; }
}

public sealed class RemovePageRequest
{
    public Guid BoardId { get; set; }
    public Guid PageId { get; set; }
}

public sealed class UpsertElementRequest
{
    public Guid BoardId { get; set; }
    public Guid PageId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public DrawingElementDto Element { get; set; } = new();
}

public sealed class RemoveElementRequest
{
    public Guid BoardId { get; set; }
    public Guid PageId { get; set; }
    public Guid ElementId { get; set; }
}

public sealed class JoinBoardRequest
{
    public Guid BoardId { get; set; }
    public Guid? PageId { get; set; }
    public string Nickname { get; set; } = string.Empty;
}

public sealed class CursorEventRequest
{
    public Guid BoardId { get; set; }
    public Guid? PageId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public float X { get; set; }
    public float Y { get; set; }
}

public sealed class ActivityEventRequest
{
    public Guid BoardId { get; set; }
    public Guid? PageId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public bool IsDrawing { get; set; }
}

public sealed class BoardParticipantDto
{
    public string ConnectionId { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public string AccentColor { get; set; } = "#2563eb";
    public Guid? ActivePageId { get; set; }
    public float? CursorX { get; set; }
    public float? CursorY { get; set; }
    public bool IsDrawing { get; set; }
    public DateTime LastSeenAtUtc { get; set; }
}

public sealed class PresenceSnapshotDto
{
    public IReadOnlyList<BoardParticipantDto> Participants { get; set; } = [];
}

public sealed class CursorChangedDto
{
    public BoardParticipantDto Participant { get; set; } = new();
}

public sealed class ActivityChangedDto
{
    public BoardParticipantDto Participant { get; set; } = new();
}

public sealed class DrawingEventEnvelope<TPayload>
{
    public string Type { get; set; } = string.Empty;
    public TPayload Payload { get; set; } = default!;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
