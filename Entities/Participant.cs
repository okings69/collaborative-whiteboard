namespace CollaborativeBoard.Entities;

public sealed class Participant
{
    public Guid Id { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string AccentColor { get; set; } = "#2563eb";
    public string? LastConnectionId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastSeenAtUtc { get; set; }

    public ICollection<Board> OwnedBoards { get; set; } = [];
    public ICollection<DrawingElement> AuthoredElements { get; set; } = [];
}
