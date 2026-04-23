namespace CollaborativeBoard.Entities;

public sealed class Board
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShareCode { get; set; } = string.Empty;
    public string AccentColor { get; set; } = "#2563eb";
    public string CreatedByNickname { get; set; } = "Guest";
    public Guid? OwnerParticipantId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public Participant? OwnerParticipant { get; set; }
    public ICollection<BoardPage> Pages { get; set; } = [];
}
