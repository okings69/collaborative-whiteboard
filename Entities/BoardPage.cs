namespace CollaborativeBoard.Entities;

public sealed class BoardPage
{
    public Guid Id { get; set; }
    public Guid BoardId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Board? Board { get; set; }
    public ICollection<DrawingElement> Elements { get; set; } = [];
}
