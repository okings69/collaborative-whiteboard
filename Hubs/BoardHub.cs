using CollaborativeBoard.Contracts;
using CollaborativeBoard.Services;
using Microsoft.AspNetCore.SignalR;

namespace CollaborativeBoard.Hubs;

public sealed class BoardHub(IBoardService boardService, BoardPresenceService presenceService) : Hub
{
    public async Task JoinBoard(JoinBoardRequest request)
    {
        var participant = await boardService.EnsureUserAsync(request.Nickname, request.PageId);
        participant = presenceService.AddOrUpdate(request.BoardId, Context.ConnectionId, participant);

        Context.Items["boardId"] = request.BoardId;
        await Groups.AddToGroupAsync(Context.ConnectionId, BoardGroup(request.BoardId));

        await PublishPresenceAsync(request.BoardId);
    }

    public async Task UpsertElement(UpsertElementRequest request)
    {
        var element = await boardService.UpsertElementAsync(request);
        if (element is null)
        {
            return;
        }

        await PublishToBoardAsync(
            request.BoardId,
            "ElementUpserted",
            "element.upserted",
            new
            {
                pageId = request.PageId,
                element
            });
    }

    public async Task BroadcastDraftElement(UpsertElementRequest request)
    {
        if (request.Element is null)
        {
            return;
        }

        await PublishToOthersAsync(
            request.BoardId,
            "DraftElementChanged",
            "element.draft",
            new
            {
                pageId = request.PageId,
                element = request.Element
            });
    }

    public async Task RemoveElement(RemoveElementRequest request)
    {
        var removed = await boardService.RemoveElementAsync(request);
        if (!removed)
        {
            return;
        }

        await PublishToBoardAsync(
            request.BoardId,
            "ElementRemoved",
            "element.removed",
            new
            {
                pageId = request.PageId,
                elementId = request.ElementId
            });
    }

    public async Task AddPage(AddPageRequest request)
    {
        var page = await boardService.AddPageAsync(request);
        if (page is null)
        {
            return;
        }

        await PublishToBoardAsync(request.BoardId, "PageAdded", "page.added", page);
    }

    public async Task RemovePage(RemovePageRequest request)
    {
        var nextPageId = await boardService.RemovePageAsync(request);
        if (nextPageId is null)
        {
            return;
        }

        await PublishToBoardAsync(
            request.BoardId,
            "PageRemoved",
            "page.removed",
            new
            {
                pageId = request.PageId,
                nextPageId
            });
    }

    public async Task UpdateCursor(CursorEventRequest request)
    {
        var participant = presenceService.UpdateCursor(request.BoardId, Context.ConnectionId, request.X, request.Y, request.PageId);
        if (participant is null)
        {
            return;
        }

        await PublishToOthersAsync(
            request.BoardId,
            "CursorChanged",
            "cursor.changed",
            new CursorChangedDto { Participant = participant });
    }

    public async Task SetDrawingState(ActivityEventRequest request)
    {
        var participant = presenceService.UpdateActivity(request.BoardId, Context.ConnectionId, request.IsDrawing, request.PageId);
        if (participant is null)
        {
            return;
        }

        await PublishToBoardAsync(
            request.BoardId,
            "ActivityChanged",
            "activity.changed",
            new ActivityChangedDto { Participant = participant });

        await PublishPresenceAsync(request.BoardId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue("boardId", out var boardIdValue) && boardIdValue is Guid boardId)
        {
            presenceService.Remove(boardId, Context.ConnectionId);
            await PublishPresenceAsync(boardId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private Task PublishPresenceAsync(Guid boardId)
    {
        return PublishToBoardAsync(
            boardId,
            "PresenceChanged",
            "presence.changed",
            presenceService.Snapshot(boardId));
    }

    private Task PublishToBoardAsync<TPayload>(Guid boardId, string clientMethod, string eventType, TPayload payload)
    {
        return Clients.Group(BoardGroup(boardId)).SendAsync(clientMethod, Envelope(eventType, payload));
    }

    private Task PublishToOthersAsync<TPayload>(Guid boardId, string clientMethod, string eventType, TPayload payload)
    {
        return Clients.OthersInGroup(BoardGroup(boardId)).SendAsync(clientMethod, Envelope(eventType, payload));
    }

    private static DrawingEventEnvelope<TPayload> Envelope<TPayload>(string eventType, TPayload payload)
    {
        return new DrawingEventEnvelope<TPayload>
        {
            Type = eventType,
            Payload = payload
        };
    }

    private static string BoardGroup(Guid boardId) => $"board:{boardId}";
}
