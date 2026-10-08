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

- Frontend (`frontend/`, violet theme): routes `/`, `/login`, `/boards`, `/b/:boardId` (keyed per board),
  `/join/:token`. `board/useBoard.ts` = REST load + SignalR events + auto-reconnect with full resync;
  `board/state.ts` = idempotent reducer + `planCardMove` for optimistic drags; `board/BoardCanvas.tsx` = dnd-kit
  (cards across columns, column reorder, keyboard sensor). Card editor shows "also editing" and
  "someone else saved → Load their version". Share dialog, presence stack, activity feed, connection status pill.
- Two-user Playwright run (two isolated browser contexts) against local dev: 20/20 checks pass, no console
  errors. Found + fixed: guest token race on the first request after sign-in; noisy SignalR logs from React's
  dev double-mount; rename buttons lacked an accessible name.
- README written.

- **Deployed and live (2026-10-08):**
  - Frontend (Vercel project `liveboard-tajveed`): https://liveboard-tajveed.vercel.app
  - API (Render, Docker, Frankfurt, free): https://liveboard-api-hzdt.onrender.com (WebSockets work through Render)
  - Render env: `ConnectionStrings__Default` (Neon `liveboard` DB), `Jwt__Key` (generated),
    `Cors__Origins__0 = https://liveboard-tajveed.vercel.app`. Vercel env: `VITE_API_BASE_URL` = Render URL.
  - Two-user Playwright run against production: 20/20 (card ~0.8 s, drag ~1.6 s to the other user).
    Fixed after going live: share-dialog tip layout, "(you)" leaking into presence-avatar initials.
- Portfolio: featured card + 4-shot gallery in `tajveed-portfolio/public/projects/liveboard/`, About mention.

## Status
Project complete. Possible follow-ups (not requested): card comments/due dates, per-board activity history
persisted in the DB, Redis backplane if it ever needs more than one API instance.

## Next steps
- None pending.

## Decisions & gotchas
- Writes go through REST (validation, status codes, rate limits); the hub is broadcast + presence only.
- Single API instance assumed (Render free): `BoardLocks` and `PresenceTracker` are in-memory. Scaling out needs
  a DB-level lock and a SignalR backplane (Redis).
- The model avoids Postgres-only types so tests can run on SQLite. `cards.BoardId` is an indexed column, not an FK.
- Tests target net10.0 because only the .NET 10 runtime is installed locally and the 8.x TestServer breaks on the
  10.x System.Text.Json (PipeWriter.UnflushedBytes). The API itself stays net8.0.
- VS Code's C# extension locks freshly copied project folders; rename by copying into new folders instead.
