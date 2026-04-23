using System.Collections.Concurrent;
using CollaborativeBoard.Contracts;

namespace CollaborativeBoard.Services;

public sealed class BoardPresenceService
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, BoardParticipantDto>> _presence = new();

    public BoardParticipantDto AddOrUpdate(Guid boardId, string connectionId, BoardParticipantDto participant)
    {
        var boardPresence = _presence.GetOrAdd(boardId, _ => new ConcurrentDictionary<string, BoardParticipantDto>());
        participant.ConnectionId = connectionId;
        participant.LastSeenAtUtc = DateTime.UtcNow;
        boardPresence[connectionId] = participant;
        return participant;
    }

    public BoardParticipantDto? UpdateCursor(Guid boardId, string connectionId, float x, float y, Guid? pageId)
    {
        if (!_presence.TryGetValue(boardId, out var boardPresence) || !boardPresence.TryGetValue(connectionId, out var participant))
        {
            return null;
        }

        participant.CursorX = x;
        participant.CursorY = y;
        participant.ActivePageId = pageId;
        participant.LastSeenAtUtc = DateTime.UtcNow;
        return participant;
    }

    public BoardParticipantDto? UpdateActivity(Guid boardId, string connectionId, bool isDrawing, Guid? pageId)
    {
        if (!_presence.TryGetValue(boardId, out var boardPresence) || !boardPresence.TryGetValue(connectionId, out var participant))
        {
            return null;
        }

        participant.IsDrawing = isDrawing;
        participant.ActivePageId = pageId;
        participant.LastSeenAtUtc = DateTime.UtcNow;
        return participant;
    }

    public void Remove(Guid boardId, string connectionId)
    {
        if (!_presence.TryGetValue(boardId, out var boardPresence))
        {
            return;
        }

        boardPresence.TryRemove(connectionId, out _);
        if (boardPresence.IsEmpty)
        {
            _presence.TryRemove(boardId, out _);
        }
    }

    public PresenceSnapshotDto Snapshot(Guid boardId)
    {
        if (!_presence.TryGetValue(boardId, out var boardPresence))
        {
            return new PresenceSnapshotDto();
        }

        return new PresenceSnapshotDto
        {
            Participants = boardPresence.Values
                .OrderBy(participant => participant.Nickname, StringComparer.OrdinalIgnoreCase)
                .Select(Clone)
                .ToList()
        };
    }

    private static BoardParticipantDto Clone(BoardParticipantDto participant)
    {
        return new BoardParticipantDto
        {
            ConnectionId = participant.ConnectionId,
            Nickname = participant.Nickname,
            AccentColor = participant.AccentColor,
            ActivePageId = participant.ActivePageId,
            CursorX = participant.CursorX,
            CursorY = participant.CursorY,
            IsDrawing = participant.IsDrawing,
            LastSeenAtUtc = participant.LastSeenAtUtc
        };
    }
}
