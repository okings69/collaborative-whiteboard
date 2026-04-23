# Boardspace

Boardspace is a real-time collaborative whiteboard built with ASP.NET Core MVC, SignalR, Entity Framework Core, PostgreSQL, Razor views, modular JavaScript, and HTML5 Canvas.

The goal is simple: users enter with a nickname, create or join a shared board, draw together live, and keep every drawing element persisted so the board can be reopened later.

## Stack

- ASP.NET Core MVC
- SignalR for live collaboration
- Entity Framework Core with PostgreSQL via Npgsql
- Razor Views and vanilla JavaScript
- HTML5 Canvas for rendering and export
- Docker-ready deployment for Render

## Delivered Features

- Nickname entry without registration or classic authentication
- Board list with unique board names and invite links
- Create and join existing boards
- Real-time drawing synchronization through SignalR groups
- Live remote cursors and "is drawing" presence
- Persistent structured drawing elements in PostgreSQL
- Reload board history after refresh or reopening
- Multiple pages per board
- Page add, remove, switch, and preview thumbnails
- Pen, rectangle, circle, text, and eraser tools
- Stroke color, fill color, and stroke size controls
- Board-list preview thumbnails generated from saved elements
- JPEG export for the current page
- Responsive product-style interface

## Optional Requirements Status

| Requirement | Status |
| --- | --- |
| Multiple pages with preview/add/remove | Implemented |
| User permissions | Partially prepared: participants and owner are stored, but role-based view/edit/manage permissions are not enforced yet |
| Erase previously drawn elements | Implemented |
| Tools: text, rectangle, circle, colors | Implemented |
| Preview thumbnails in board list | Implemented |
| Export to JPEG | Implemented |

## Architecture

- `Entities/` contains `Board`, `BoardPage`, `DrawingElement`, `Participant`, and drawing type definitions.
- `Data/AppDbContext.cs` configures PostgreSQL tables, relationships, indexes, and JSONB columns for drawing payloads.
- `Data/Migrations/` contains the first PostgreSQL schema migration.
- `Contracts/` contains DTOs and request contracts shared by controllers, SignalR, and the frontend.
- `Services/BoardService.cs` owns board persistence, page operations, participant lookup, drawing storage, and preview mapping.
- `Services/BoardPresenceService.cs` tracks connected users, cursors, and drawing state in memory.
- `Hubs/BoardHub.cs` handles SignalR board groups, drawing events, page events, cursors, and presence.
- `Controllers/` exposes MVC pages and JSON endpoints used by the client.
- `Views/Boards/` contains the board list and workspace Razor views.
- `wwwroot/js/board.js` contains canvas interactions, rendering, real-time sync, page previews, erasing, and export.
- `wwwroot/css/site.css` contains the polished responsive UI.

## Local Setup

Install PostgreSQL and create a database named `boardspace`, then set the connection string as an environment variable.

PowerShell example:

```powershell
cd D:\Camp\CS\Task6\CollaborativeBoard
$env:ConnectionStrings__Postgres="Host=localhost;Port=5432;Database=boardspace;Username=postgres;Password=YOUR_PASSWORD"
dotnet restore
dotnet ef database update
dotnet run
```

Open the URL printed by `dotnet run`.

## How To Test Collaboration

1. Open the app in one browser and enter a nickname.
2. Create a board.
3. Use "Copy invite link" or open the same board URL in another browser/private window.
4. Change the nickname in the URL if needed.
5. Draw with the pen, rectangle, circle, or text tool.
6. Confirm the other browser receives the drawing at the same position.
7. Refresh both pages and confirm the content remains visible.
8. Add a second page, draw on it, switch pages, and remove it if there is more than one page.
9. Use the eraser on an existing element.
10. Export the current page as JPEG.

## Render Deployment

This repository includes `Dockerfile` and `render.yaml`.

Recommended Render setup:

- Create a new Blueprint from this repository, or create a Web Service using Docker.
- Create a Render PostgreSQL database.
- Set environment variable `ConnectionStrings__Postgres` to the Render database connection string.
- Render provides `PORT`; `Program.cs` binds the app to that port automatically.
- On startup, EF Core applies migrations with `Database.MigrateAsync()`.

## Files Not To Commit

The project ignores generated and sensitive local files:

- `bin/`
- `obj/`
- `App_Data/`
- local SQLite/database files
- Data Protection keys
- `.env` files
- IDE user files

## Current Limitations

- Role-based permissions are not enforced yet.
- Presence is in-memory and resets when the server restarts.
- Object selection, movement, resizing, and layer controls are planned but not currently exposed in the UI.
- Undo/redo is not implemented yet.
