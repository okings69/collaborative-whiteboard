namespace CollaborativeBoard.Entities;

public sealed class DrawingElement
{
    public Guid Id { get; set; }
    public Guid BoardPageId { get; set; }
    public Guid? CreatedByParticipantId { get; set; }
    public DrawingElementType ElementType { get; set; }
    public string StrokeColor { get; set; } = "#111827";
    public string? FillColor { get; set; }
    public float StrokeWidth { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public float FontSize { get; set; } = 28;
    public string? TextContent { get; set; }
    public string? PointsJson { get; set; }
    public string? MetadataJson { get; set; }
    public string? VersionToken { get; set; }
    public int LayerOrder { get; set; }
    public string CreatedByNickname { get; set; } = "Guest";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public BoardPage? BoardPage { get; set; }
    public Participant? CreatedByParticipant { get; set; }
}
