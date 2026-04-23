const boardData = JSON.parse(document.getElementById("board-initial-data").textContent);
const nickname = JSON.parse(document.getElementById("board-nickname").textContent);

const CURSOR_THROTTLE_MS = 32;
const DRAWING_SYNC_THROTTLE_MS = 48;

const state = {
    nickname,
    board: boardData,
    activePageId: boardData.pages[0]?.id ?? null,
    tool: "pen",
    strokeColor: "#111827",
    fillColor: "#dbeafe",
    strokeWidth: 4,
    zoom: 1,
    draft: null,
    connection: null,
    participants: [],
    remoteDrafts: new Map(),
    lastCursorBroadcastAt: 0,
    lastDraftBroadcastAt: 0,
    elementSyncPromise: Promise.resolve(),
    isDrawingAnnounced: false
};

const elements = {
    canvas: document.getElementById("boardCanvas"),
    canvasStage: document.getElementById("canvasStage"),
    remoteCursorLayer: document.getElementById("remoteCursorLayer"),
    pagesList: document.getElementById("pagesList"),
    presenceList: document.getElementById("presenceList"),
    toolButtons: [...document.querySelectorAll("[data-tool]")],
    strokeColor: document.getElementById("strokeColor"),
    fillColor: document.getElementById("fillColor"),
    strokeWidth: document.getElementById("strokeWidth"),
    strokeWidthValue: document.getElementById("strokeWidthValue"),
    addPageButton: document.getElementById("addPageButton"),
    exportButton: document.getElementById("exportButton"),
    copyInviteButton: document.getElementById("copyInviteButton"),
    inviteFeedback: document.getElementById("inviteFeedback"),
    zoomInButton: document.getElementById("zoomInButton"),
    zoomOutButton: document.getElementById("zoomOutButton"),
    zoomLabel: document.getElementById("zoomLabel")
};

const context = elements.canvas.getContext("2d");
const BOARD_WIDTH = Number(elements.canvas.getAttribute("width") || 1920);
const BOARD_HEIGHT = Number(elements.canvas.getAttribute("height") || 1080);
const PAGE_PREVIEW_WIDTH = 288;
const PAGE_PREVIEW_HEIGHT = 160;
let resizeObserver;

boot();

async function boot() {
    setupCanvasResolution();
    bindUi();
    renderPresence([]);
    renderPages();
    renderCanvas();
    await connectSignalR();
}

function bindUi() {
    elements.toolButtons.forEach((button) => {
        button.addEventListener("click", () => setTool(button.dataset.tool));
    });

    elements.strokeColor.addEventListener("input", (event) => {
        state.strokeColor = event.target.value;
    });

    elements.fillColor.addEventListener("input", (event) => {
        state.fillColor = event.target.value;
    });

    elements.strokeWidth.addEventListener("input", (event) => {
        state.strokeWidth = Number(event.target.value);
        elements.strokeWidthValue.textContent = `${state.strokeWidth} px`;
    });

    elements.canvas.addEventListener("pointerdown", onPointerDown);
    elements.canvas.addEventListener("pointermove", onPointerMove);
    elements.canvas.addEventListener("pointerup", onPointerUp);
    elements.canvas.addEventListener("pointerleave", onPointerLeave);

    elements.addPageButton.addEventListener("click", addPage);
    elements.exportButton.addEventListener("click", exportJpeg);
    elements.copyInviteButton.addEventListener("click", copyInviteLink);
    elements.zoomInButton.addEventListener("click", () => setZoom(state.zoom + 0.1));
    elements.zoomOutButton.addEventListener("click", () => setZoom(state.zoom - 0.1));

    window.addEventListener("resize", handleCanvasViewportChanged);
    resizeObserver = new ResizeObserver(() => handleCanvasViewportChanged());
    resizeObserver.observe(elements.canvasStage);
}

async function connectSignalR() {
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/boards")
        .withAutomaticReconnect()
        .build();

    registerHubHandlers(connection);

    connection.onreconnected(async () => {
        await joinCurrentBoard(connection);
    });

    await connection.start();
    await joinCurrentBoard(connection);
    state.connection = connection;
}

function registerHubHandlers(connection) {
    connection.on("PresenceChanged", (message) => {
        state.participants = message.payload.participants || [];
        renderPresence(state.participants);
        renderRemoteCursors();
    });

    connection.on("CursorChanged", (message) => {
        mergeParticipant(message.payload.participant);
        renderPresence(state.participants);
        renderRemoteCursors();
    });

    connection.on("ActivityChanged", (message) => {
        mergeParticipant(message.payload.participant);
        renderPresence(state.participants);
    });

    connection.on("ElementUpserted", (message) => {
        const { pageId, element } = message.payload;
        removeRemoteDraft(pageId, element.id);
        updatePageElements(pageId, (page) => {
            upsertIntoCollection(page.elements, element);
            sortElements(page.elements);
        });
    });

    connection.on("ElementRemoved", (message) => {
        const { pageId, elementId } = message.payload;
        removeRemoteDraft(pageId, elementId);
        updatePageElements(pageId, (page) => {
            page.elements = page.elements.filter((element) => element.id !== elementId);
        });
    });

    connection.on("DraftElementChanged", (message) => {
        const { pageId, element } = message.payload;
        upsertRemoteDraft(pageId, element);
        renderCanvas();
    });

    connection.on("PageAdded", (message) => {
        state.board.pages.push(message.payload);
        sortPages();
        state.activePageId = message.payload.id;
        refreshBoardView();
    });

    connection.on("PageRemoved", (message) => {
        state.board.pages = state.board.pages.filter((page) => page.id !== message.payload.pageId);
        state.activePageId = message.payload.nextPageId;
        refreshBoardView();
    });
}

async function joinCurrentBoard(connection) {
    await connection.invoke("JoinBoard", {
        boardId: state.board.id,
        pageId: state.activePageId,
        nickname: state.nickname
    });
}

function mergeParticipant(participant) {
    const index = state.participants.findIndex((entry) => entry.connectionId === participant.connectionId);
    if (index >= 0) {
        state.participants[index] = participant;
    } else {
        state.participants.push(participant);
    }
}

function setTool(tool) {
    state.tool = tool;
    elements.toolButtons.forEach((button) => {
        button.classList.toggle("is-active", button.dataset.tool === tool);
    });
}

async function onPointerDown(event) {
    if (!state.activePageId) {
        return;
    }

    const point = getCanvasPoint(event);
    await broadcastCursor(point);

    if (state.tool === "eraser") {
        eraseAtPoint(point);
        return;
    }

    if (state.tool === "text") {
        const text = window.prompt("Type the note you want to place on this page.");
        if (!text) {
            return;
        }

        const element = createElementPayload({
            elementType: "text",
            x: point.x,
            y: point.y,
            layerOrder: getNextLayerOrder(),
            textContent: text,
            fontSize: 30
        });

        await announceDrawingState(true);
        await sendUpsert(element);
        await announceDrawingState(false);
        return;
    }

    elements.canvas.setPointerCapture(event.pointerId);
    state.draft = {
        id: crypto.randomUUID(),
        elementType: state.tool,
        strokeColor: state.strokeColor,
        fillColor: state.tool === "pen" ? null : `${state.fillColor}AA`,
        strokeWidth: state.strokeWidth,
        layerOrder: getNextLayerOrder(),
        start: point,
        current: point,
        points: [point]
    };
    state.lastDraftBroadcastAt = 0;

    await announceDrawingState(true);
}

async function onPointerMove(event) {
    const point = getCanvasPoint(event);
    await broadcastCursor(point);

    if (!state.draft) {
        return;
    }

    state.draft.current = point;
    state.draft.points.push(point);
    renderCanvas();
    drawElement(draftToElement(state.draft), true);
    void broadcastDraftUpdate();
}

async function onPointerUp(event) {
    if (!state.draft || !state.activePageId) {
        return;
    }

    if (elements.canvas.hasPointerCapture(event.pointerId)) {
        elements.canvas.releasePointerCapture(event.pointerId);
    }

    const point = getCanvasPoint(event);
    state.draft.current = point;
    const element = draftToElement(state.draft);
    state.draft = null;
    state.lastDraftBroadcastAt = 0;
    renderCanvas();

    if (element) {
        await sendUpsert(element);
    }

    await announceDrawingState(false);
}

async function onPointerLeave(event) {
    if (state.draft) {
        await onPointerUp(event);
        return;
    }

    await announceDrawingState(false);
}

function draftToElement(draft) {
    if (!draft) {
        return null;
    }

    if (draft.elementType === "pen") {
        if (draft.points.length < 2) {
            return null;
        }

        return createElementPayload({
            id: draft.id,
            elementType: "pen",
            strokeColor: draft.strokeColor,
            strokeWidth: draft.strokeWidth,
            layerOrder: draft.layerOrder,
            points: draft.points
        });
    }

    const width = draft.current.x - draft.start.x;
    const height = draft.current.y - draft.start.y;
    if (Math.abs(width) < 4 || Math.abs(height) < 4) {
        return null;
    }

    return createElementPayload({
        id: draft.id,
        elementType: draft.elementType,
        strokeColor: draft.strokeColor,
        fillColor: draft.fillColor,
        strokeWidth: draft.strokeWidth,
        x: draft.start.x,
        y: draft.start.y,
        width,
        height,
        layerOrder: draft.layerOrder
    });
}

function renderCanvas() {
    setupCanvasResolution();
    context.clearRect(0, 0, BOARD_WIDTH, BOARD_HEIGHT);
    context.fillStyle = "#ffffff";
    context.fillRect(0, 0, BOARD_WIDTH, BOARD_HEIGHT);

    const page = getActivePage();
    if (!page) {
        return;
    }

    sortElements(page.elements);
    for (const element of page.elements) {
        drawElement(element, false);
    }

    for (const draft of getRemoteDraftsForActivePage()) {
        drawElement(draft, true);
    }
}

function drawElement(element, isDraft) {
    if (!element) {
        return;
    }

    context.save();
    context.globalAlpha = isDraft ? 0.78 : 1;
    context.lineWidth = Number(element.strokeWidth || 4);
    context.strokeStyle = element.strokeColor || "#111827";
    context.fillStyle = element.fillColor || "transparent";

    if (element.elementType === "pen") {
        const points = element.points || [];
        if (points.length < 2) {
            context.restore();
            return;
        }

        context.beginPath();
        context.moveTo(points[0].x, points[0].y);
        for (const point of points.slice(1)) {
            context.lineTo(point.x, point.y);
        }
        context.stroke();
        context.restore();
        return;
    }

    if (element.elementType === "rectangle") {
        const rect = normalizeRect(element);
        context.beginPath();
        context.roundRect(rect.x, rect.y, rect.width, rect.height, 18);
        if (element.fillColor) {
            context.fill();
        }
        context.stroke();
        context.restore();
        return;
    }

    if (element.elementType === "circle") {
        const rect = normalizeRect(element);
        context.beginPath();
        context.ellipse(
            rect.x + rect.width / 2,
            rect.y + rect.height / 2,
            rect.width / 2,
            rect.height / 2,
            0,
            0,
            Math.PI * 2
        );
        if (element.fillColor) {
            context.fill();
        }
        context.stroke();
        context.restore();
        return;
    }

    if (element.elementType === "text") {
        context.fillStyle = element.strokeColor || "#111827";
        context.font = `${element.fontSize || 30}px Bahnschrift`;
        context.fillText(element.textContent || "", element.x, element.y);
    }

    context.restore();
}

function renderPages() {
    elements.pagesList.innerHTML = "";

    sortPages();
    for (const page of state.board.pages) {
        elements.pagesList.append(createPageCard(page));
    }
}

function createPageCard(page) {
    const card = document.createElement("article");
    card.className = `page-card${page.id === state.activePageId ? " is-active" : ""}`;

    const preview = document.createElement("canvas");
    preview.className = "page-preview";
    preview.width = PAGE_PREVIEW_WIDTH;
    preview.height = PAGE_PREVIEW_HEIGHT;
    renderPagePreview(preview, page.elements);

    const header = document.createElement("div");
    header.className = "page-card-header";

    const title = document.createElement("h3");
    title.textContent = page.title;

    const removeButton = document.createElement("button");
    removeButton.type = "button";
    removeButton.textContent = "Delete";
    removeButton.disabled = state.board.pages.length === 1;
    removeButton.addEventListener("click", async (event) => {
        event.stopPropagation();
        await removePage(page.id);
    });

    header.append(title, removeButton);

    const meta = document.createElement("p");
    meta.textContent = `${page.elements.length} item${page.elements.length === 1 ? "" : "s"}`;

    card.append(preview, header, meta);
    card.addEventListener("click", async () => {
        state.activePageId = page.id;
        refreshBoardView();
        renderRemoteCursors();
        if (state.connection) {
            await joinCurrentBoard(state.connection);
        }
    });

    return card;
}

function renderPresence(participants) {
    elements.presenceList.innerHTML = "";

    const otherParticipants = (participants || [])
        .filter((participant) => participant.nickname.toLowerCase() !== state.nickname.toLowerCase())
        .sort((left, right) => left.nickname.localeCompare(right.nickname));

    if (otherParticipants.length === 0) {
        const empty = document.createElement("span");
        empty.className = "presence-empty";
        empty.textContent = "Only you are on this board";
        elements.presenceList.append(empty);
        return;
    }

    for (const participant of otherParticipants) {
        const pill = document.createElement("span");
        pill.className = "presence-pill";
        pill.textContent = participant.isDrawing ? `${participant.nickname} is drawing` : participant.nickname;
        pill.style.background = `${participant.accentColor}1A`;
        pill.style.color = participant.accentColor;
        elements.presenceList.append(pill);
    }
}

function renderRemoteCursors() {
    elements.remoteCursorLayer.innerHTML = "";

    const visibleParticipants = state.participants.filter((participant) =>
        participant.nickname.toLowerCase() !== state.nickname.toLowerCase() &&
        participant.activePageId === state.activePageId &&
        participant.cursorX !== null &&
        participant.cursorY !== null &&
        participant.cursorX !== undefined &&
        participant.cursorY !== undefined
    );

    for (const participant of visibleParticipants) {
        const cursor = document.createElement("div");
        cursor.className = "remote-cursor";
        cursor.style.left = `${participant.cursorX}px`;
        cursor.style.top = `${participant.cursorY}px`;

        const dot = document.createElement("span");
        dot.className = "remote-cursor-dot";
        dot.style.background = participant.accentColor;

        const label = document.createElement("span");
        label.className = "remote-cursor-label";
        label.textContent = participant.isDrawing ? `${participant.nickname} is drawing` : participant.nickname;

        cursor.append(dot, label);
        elements.remoteCursorLayer.append(cursor);
    }
}

async function addPage() {
    const request = {
        boardId: state.board.id,
        title: `Page ${state.board.pages.length + 1}`
    };

    try {
        await invokeHub("AddPage", request);
    } catch {
        const page = await createPageViaApi(request.title);
        if (!page) {
            setInviteFeedback("The page could not be created right now.");
            return;
        }

        state.board.pages.push(page);
        sortPages();
        state.activePageId = page.id;
        refreshBoardView();
        setInviteFeedback("A new page was added.");
    }
}

async function removePage(pageId) {
    if (state.board.pages.length <= 1) {
        setInviteFeedback("A board must keep at least one page.");
        return;
    }

    const request = {
        boardId: state.board.id,
        pageId
    };

    try {
        await invokeHub("RemovePage", request);
    } catch {
        const nextPageId = await removePageViaApi(pageId);
        if (!nextPageId) {
            setInviteFeedback("The page could not be removed right now.");
            return;
        }

        state.board.pages = state.board.pages.filter((page) => page.id !== pageId);
        state.activePageId = nextPageId;
        refreshBoardView();
        setInviteFeedback("The page was removed.");
    }
}

function eraseAtPoint(point) {
    const page = getActivePage();
    if (!page || !state.connection) {
        return;
    }

    const match = [...page.elements].reverse().find((element) => hitTest(element, point));
    if (!match) {
        return;
    }

    invokeHub("RemoveElement", {
        boardId: state.board.id,
        pageId: page.id,
        elementId: match.id
    });
}

function hitTest(element, point) {
    if (element.elementType === "pen") {
        return (element.points || []).some((item) => distance(item, point) <= Math.max(10, element.strokeWidth * 2));
    }

    if (element.elementType === "text") {
        return distance({ x: element.x, y: element.y }, point) < 80;
    }

    const rect = normalizeRect(element);
    return point.x >= rect.x && point.x <= rect.x + rect.width && point.y >= rect.y && point.y <= rect.y + rect.height;
}

function distance(a, b) {
    return Math.hypot(a.x - b.x, a.y - b.y);
}

function normalizeRect(element) {
    return {
        x: element.width < 0 ? element.x + element.width : element.x,
        y: element.height < 0 ? element.y + element.height : element.y,
        width: Math.abs(element.width),
        height: Math.abs(element.height)
    };
}

function getCanvasPoint(event) {
    const bounds = elements.canvasStage.getBoundingClientRect();
    const rawX = ((event.clientX - bounds.left) / bounds.width) * BOARD_WIDTH;
    const rawY = ((event.clientY - bounds.top) / bounds.height) * BOARD_HEIGHT;

    return {
        x: clamp(rawX, 0, BOARD_WIDTH),
        y: clamp(rawY, 0, BOARD_HEIGHT)
    };
}

async function broadcastCursor(point) {
    if (!state.connection || !state.activePageId) {
        return;
    }

    const now = performance.now();
    if (now - state.lastCursorBroadcastAt < CURSOR_THROTTLE_MS) {
        return;
    }

    state.lastCursorBroadcastAt = now;
    await invokeHub("UpdateCursor", {
        boardId: state.board.id,
        pageId: state.activePageId,
        nickname: state.nickname,
        x: point.x,
        y: point.y
    });
}

async function broadcastDraftUpdate() {
    if (!state.connection || !state.activePageId || !state.draft) {
        return;
    }

    const now = performance.now();
    if (now - state.lastDraftBroadcastAt < DRAWING_SYNC_THROTTLE_MS) {
        return;
    }

    const element = draftToElement(state.draft);
    if (!element) {
        return;
    }

    state.lastDraftBroadcastAt = now;

    try {
        await invokeHub("BroadcastDraftElement", {
            boardId: state.board.id,
            pageId: state.activePageId,
            nickname: state.nickname,
            element
        });
    } catch (error) {
        console.error("Failed to broadcast draft update", error);
    }
}

async function announceDrawingState(isDrawing) {
    if (!state.connection || !state.activePageId || state.isDrawingAnnounced === isDrawing) {
        return;
    }

    state.isDrawingAnnounced = isDrawing;
    await invokeHub("SetDrawingState", {
        boardId: state.board.id,
        pageId: state.activePageId,
        nickname: state.nickname,
        isDrawing
    });
}

async function sendUpsert(element) {
    if (!state.connection || !state.activePageId) {
        return;
    }

    const page = getActivePage();
    upsertIntoCollection(page.elements, element);
    sortElements(page.elements);
    renderPages();
    renderCanvas();

    await invokeElementUpsert(element);
}

function invokeElementUpsert(element) {
    if (!state.connection || !state.activePageId) {
        return Promise.resolve();
    }

    state.elementSyncPromise = state.elementSyncPromise
        .catch(() => undefined)
        .then(() => invokeHub("UpsertElement", {
            boardId: state.board.id,
            pageId: state.activePageId,
            nickname: state.nickname,
            element
        }));

    return state.elementSyncPromise;
}

function invokeHub(method, payload) {
    if (!state.connection) {
        return Promise.reject(new Error("Realtime connection is not ready."));
    }

    return state.connection.invoke(method, payload);
}

async function createPageViaApi(title) {
    const response = await fetch(`/api/boards/${state.board.id}/pages`, {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify({ title })
    });

    if (!response.ok) {
        return null;
    }

    return response.json();
}

async function removePageViaApi(pageId) {
    const response = await fetch(`/api/boards/${state.board.id}/pages/${pageId}`, {
        method: "DELETE"
    });

    if (!response.ok) {
        return null;
    }

    const payload = await response.json();
    return payload.nextPageId || null;
}

function upsertIntoCollection(collection, element) {
    const index = collection.findIndex((entry) => entry.id === element.id);
    if (index >= 0) {
        collection[index] = element;
    } else {
        collection.push(element);
    }
}

function upsertRemoteDraft(pageId, element) {
    state.remoteDrafts.set(`${pageId}:${element.id}`, { pageId, element });
}

function removeRemoteDraft(pageId, elementId) {
    state.remoteDrafts.delete(`${pageId}:${elementId}`);
}

function getRemoteDraftsForActivePage() {
    const drafts = [];
    for (const entry of state.remoteDrafts.values()) {
        if (entry.pageId === state.activePageId) {
            drafts.push(entry.element);
        }
    }

    return drafts;
}

function updatePageElements(pageId, update) {
    const page = getPage(pageId);
    if (!page) {
        return;
    }

    update(page);
    refreshBoardView();
}

function sortElements(collection) {
    collection.sort((left, right) => (left.layerOrder || 0) - (right.layerOrder || 0));
}

function sortPages() {
    state.board.pages.sort((left, right) => left.sortOrder - right.sortOrder);
}

function getPage(pageId) {
    return state.board.pages.find((page) => page.id === pageId);
}

function getActivePage() {
    return getPage(state.activePageId);
}

function getNextLayerOrder() {
    const page = getActivePage();
    if (!page || page.elements.length === 0) {
        return 1;
    }

    return Math.max(...page.elements.map((element) => element.layerOrder || 0)) + 1;
}

function refreshBoardView() {
    renderPages();
    renderCanvas();
}

function setZoom(value) {
    state.zoom = Math.max(0.5, Math.min(1.8, Number(value.toFixed(2))));
    elements.canvasStage.style.transform = `scale(${state.zoom})`;
    elements.zoomLabel.textContent = `${Math.round(state.zoom * 100)}%`;
    renderRemoteCursors();
}

function exportJpeg() {
    const link = document.createElement("a");
    link.href = elements.canvas.toDataURL("image/jpeg", 0.92);
    link.download = `${slugify(state.board.name)}.jpg`;
    link.click();
}

async function copyInviteLink() {
    const inviteUrl = new URL(window.location.href);
    inviteUrl.searchParams.set("nickname", "guest");

    try {
        await navigator.clipboard.writeText(inviteUrl.toString());
        setInviteFeedback("Invite link copied. Open it in another browser and replace nickname=guest with a real name.");
    } catch {
        setInviteFeedback(`Clipboard access failed. Copy this link manually: ${inviteUrl.toString()}`);
    }
}

function setInviteFeedback(message) {
    elements.inviteFeedback.textContent = message;
}

function slugify(value) {
    return value.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/(^-|-$)/g, "") || "boardspace-export";
}

function createElementPayload({
    id = crypto.randomUUID(),
    elementType,
    strokeColor = state.strokeColor,
    fillColor = null,
    strokeWidth = state.strokeWidth,
    x = 0,
    y = 0,
    width = 0,
    height = 0,
    fontSize = 0,
    layerOrder = 0,
    textContent = null,
    points = [],
    metadataJson = null,
    versionToken = null
}) {
    return {
        id,
        elementType,
        strokeColor,
        fillColor,
        strokeWidth,
        x,
        y,
        width,
        height,
        fontSize,
        layerOrder,
        textContent,
        metadataJson,
        versionToken,
        points,
        createdByNickname: state.nickname,
        timestampUtc: new Date().toISOString()
    };
}

function renderPagePreview(canvas, pageElements) {
    const previewContext = canvas.getContext("2d");
    if (!previewContext) {
        return;
    }

    previewContext.clearRect(0, 0, PAGE_PREVIEW_WIDTH, PAGE_PREVIEW_HEIGHT);
    previewContext.fillStyle = "#fbfdff";
    previewContext.fillRect(0, 0, PAGE_PREVIEW_WIDTH, PAGE_PREVIEW_HEIGHT);
    previewContext.save();
    previewContext.scale(PAGE_PREVIEW_WIDTH / BOARD_WIDTH, PAGE_PREVIEW_HEIGHT / BOARD_HEIGHT);
    previewContext.lineCap = "round";
    previewContext.lineJoin = "round";

    const orderedElements = [...pageElements].sort((left, right) => (left.layerOrder || 0) - (right.layerOrder || 0));
    for (const element of orderedElements) {
        previewContext.save();
        previewContext.lineWidth = Number(element.strokeWidth || 4);
        previewContext.strokeStyle = element.strokeColor || "#111827";
        previewContext.fillStyle = element.fillColor || "transparent";

        if (element.elementType === "pen") {
            const points = element.points || [];
            if (points.length > 1) {
                previewContext.beginPath();
                previewContext.moveTo(points[0].x, points[0].y);
                for (const point of points.slice(1)) {
                    previewContext.lineTo(point.x, point.y);
                }
                previewContext.stroke();
            }
        } else if (element.elementType === "rectangle") {
            const rect = normalizeRect(element);
            previewContext.beginPath();
            previewContext.roundRect(rect.x, rect.y, rect.width, rect.height, 18);
            if (element.fillColor) {
                previewContext.fill();
            }
            previewContext.stroke();
        } else if (element.elementType === "circle") {
            const rect = normalizeRect(element);
            previewContext.beginPath();
            previewContext.ellipse(
                rect.x + rect.width / 2,
                rect.y + rect.height / 2,
                rect.width / 2,
                rect.height / 2,
                0,
                0,
                Math.PI * 2
            );
            if (element.fillColor) {
                previewContext.fill();
            }
            previewContext.stroke();
        } else if (element.elementType === "text") {
            previewContext.fillStyle = element.strokeColor || "#111827";
            previewContext.font = `${element.fontSize || 30}px Bahnschrift`;
            previewContext.fillText(element.textContent || "", element.x, element.y);
        }

        previewContext.restore();
    }

    previewContext.restore();
}

function setupCanvasResolution() {
    const devicePixelRatio = Math.max(window.devicePixelRatio || 1, 1);
    const backingWidth = Math.round(BOARD_WIDTH * devicePixelRatio);
    const backingHeight = Math.round(BOARD_HEIGHT * devicePixelRatio);

    if (elements.canvas.width !== backingWidth || elements.canvas.height !== backingHeight) {
        elements.canvas.width = backingWidth;
        elements.canvas.height = backingHeight;
    }

    context.setTransform(devicePixelRatio, 0, 0, devicePixelRatio, 0, 0);
    context.lineCap = "round";
    context.lineJoin = "round";
}

function handleCanvasViewportChanged() {
    setupCanvasResolution();
    renderCanvas();
    renderRemoteCursors();
}

function clamp(value, min, max) {
    return Math.min(Math.max(value, min), max);
}
