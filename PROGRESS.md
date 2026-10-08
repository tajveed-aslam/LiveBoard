# Progress — LiveBoard
_Last updated: 2026-10-08 by Claude Code_

## Goal
Real-time collaborative Kanban board: boards with columns and draggable cards, every change broadcast live to
everyone on the board via SignalR, share-by-link with simultaneous multi-user editing, and who's-online presence.
Stack: ASP.NET Core 8 Web API + SignalR, React + TS (Vite), PostgreSQL (EF Core), JWT auth. No LLM.

## Done
- Backend foundation copied from FitCheck (JWT auth, guest demo, ProblemDetails errors, rate limiting, Neon URL
  parsing, Dockerfile/render.yaml) with all AI/CV code removed. API port **5082**, UI port **5175**.
- Model: `users` (DisplayName), `boards` (ShareToken), `board_members` (Owner/Editor), `board_columns`, `cards`
  (int positions, contiguous per column). Migration `InitialCreate`. Neon database `liveboard` created.
- `BoardService`: every write = membership check → per-board lock (`BoardLocks`) → apply + save → broadcast via
  `IHubContext<BoardHub>`. Events carry full resulting state (e.g. `CardMoved.ColumnOrders` = final order of each
  touched column) so clients converge. Pure ordering helpers in `Ordering`.
- REST: `/api/boards` (list/create/get/rename/delete), `/share-token` (owner resets link), `/join/{token}`,
  `/members/me` (leave), `/columns` (create/rename/delete/move), `/cards` (create/patch/delete/move).
- `BoardHub` at `/hubs/board`: `JoinBoard(boardId)` (members only) → presence; `SetEditing(cardId|null)`;
  disconnect clears presence + editing. JWT via `?access_token=` for the hub path only.
- Guests get a friendly random name ("Swift Otter") and a sample "Welcome to LiveBoard" board
  (`/api/auth/guest?withSampleBoard=false` for guests arriving via a share link).
- Tests (`backend/LiveBoard.Tests`, **net10.0**): 37 passing — unit (ordering, presence, display names),
  API integration on SQLite via WebApplicationFactory (permissions, share links, moves, column delete, 40
  concurrent moves from two users), and real-time tests with two live SignalR clients.

## In progress
- Frontend (`frontend/`) — not started yet.

## Next steps
1. Frontend from the FitCheck shell: routes `/`, `/login`, `/boards`, `/b/:boardId`, `/join/:token`;
   @dnd-kit drag-and-drop (cards across columns + column reorder), @microsoft/signalr live sync with
   reconnect + resync, presence avatars, "X is editing" badges, share modal, activity feed.
2. Playwright E2E with two browser contexts (two users) proving live sync.
3. README, deploy (Render + Vercel), portfolio card + screenshots (Rule 1).

## Decisions & gotchas
- Writes go through REST (validation, status codes, rate limits); the hub is broadcast + presence only.
- Single API instance assumed (Render free): `BoardLocks` and `PresenceTracker` are in-memory. Scaling out needs
  a DB-level lock and a SignalR backplane (Redis).
- The model avoids Postgres-only types so tests can run on SQLite. `cards.BoardId` is an indexed column, not an FK.
- Tests target net10.0 because only the .NET 10 runtime is installed locally and the 8.x TestServer breaks on the
  10.x System.Text.Json (PipeWriter.UnflushedBytes). The API itself stays net8.0.
- VS Code's C# extension locks freshly copied project folders; rename by copying into new folders instead.
