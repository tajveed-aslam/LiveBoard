export type BoardRole = 'owner' | 'editor'

export interface Card {
  id: string
  columnId: string
  title: string
  description: string
  color: string | null
  position: number
  updatedAt: string
}

export interface Column {
  id: string
  title: string
  position: number
  cards: Card[]
}

export interface Member {
  userId: string
  displayName: string
  role: BoardRole
  isGuest: boolean
}

export interface Board {
  id: string
  title: string
  shareToken: string
  myRole: BoardRole
  columns: Column[]
  members: Member[]
  updatedAt: string
}

export interface BoardSummary {
  id: string
  title: string
  myRole: BoardRole
  memberCount: number
  cardCount: number
  updatedAt: string
}

export interface AuthResponse {
  token: string
  expiresAt: string
  userId: string
  email: string
  displayName: string
  isGuest: boolean
}

// ---------- Real-time event payloads (mirror backend Models/Events.cs) ----------

export interface Actor {
  userId: string
  displayName: string
}

export interface PresenceUser {
  userId: string
  displayName: string
  isGuest: boolean
  connections: number
}

export interface CardMovedEvent {
  boardId: string
  card: Card
  fromColumnId: string
  columnOrders: Record<string, string[]>
  actor: Actor
}

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

// Paths below already start with /api, so accept the base URL with or without a trailing "/api".
export const API_BASE = (import.meta.env.VITE_API_BASE_URL ?? '').trim().replace(/\/+$/, '').replace(/\/api$/i, '')

let authToken: string | null = null
let onUnauthorized: (() => void) | null = null

export function configureAuth(token: string | null, unauthorizedHandler: (() => void) | null) {
  authToken = token
  onUnauthorized = unauthorizedHandler
}

export function currentToken(): string | null {
  return authToken
}

interface ProblemDetails {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

async function request<T>(path: string, init: RequestInit & { json?: unknown } = {}): Promise<T> {
  const { json, ...rest } = init
  const headers = new Headers(rest.headers)
  if (authToken) headers.set('Authorization', `Bearer ${authToken}`)
  if (json !== undefined) headers.set('Content-Type', 'application/json')

  let response: Response
  try {
    response = await fetch(API_BASE + path, {
      ...rest,
      headers,
      body: json !== undefined ? JSON.stringify(json) : rest.body,
    })
  } catch {
    throw new ApiError(0, "Can't reach the server. Check your connection and try again.")
  }

  if (response.status === 401 && authToken) onUnauthorized?.()

  if (!response.ok) {
    throw new ApiError(response.status, await readError(response))
  }

  return (response.status === 204 ? undefined : await response.json()) as T
}

async function readError(response: Response): Promise<string> {
  try {
    const problem = (await response.json()) as ProblemDetails
    const fieldErrors = problem.errors ? Object.values(problem.errors).flat() : []
    if (fieldErrors.length > 0) return fieldErrors.join(' ')
    if (problem.detail) return problem.detail
    if (problem.title) return problem.title
  } catch {
    // Non-JSON error body; fall through.
  }
  return `Request failed (${response.status}).`
}

const board = (id: string) => `/api/boards/${id}`

export const api = {
  health: () => request<{ status: string }>('/api/health'),

  register: (email: string, password: string, displayName?: string) =>
    request<AuthResponse>('/api/auth/register', { method: 'POST', json: { email, password, displayName } }),
  login: (email: string, password: string) =>
    request<AuthResponse>('/api/auth/login', { method: 'POST', json: { email, password } }),
  guest: (withSampleBoard = true) =>
    request<AuthResponse>(`/api/auth/guest?withSampleBoard=${withSampleBoard}`, { method: 'POST' }),

  listBoards: () => request<BoardSummary[]>('/api/boards'),
  getBoard: (id: string) => request<Board>(board(id)),
  createBoard: (title: string) => request<Board>('/api/boards', { method: 'POST', json: { title, withDefaultColumns: true } }),
  renameBoard: (id: string, title: string) => request<void>(board(id), { method: 'PATCH', json: { title } }),
  deleteBoard: (id: string) => request<void>(board(id), { method: 'DELETE' }),
  resetShareLink: (id: string) => request<{ shareToken: string }>(`${board(id)}/share-token`, { method: 'POST' }),
  join: (shareToken: string) =>
    request<{ boardId: string; alreadyMember: boolean }>(`/api/boards/join/${encodeURIComponent(shareToken)}`, { method: 'POST' }),
  leave: (id: string) => request<void>(`${board(id)}/members/me`, { method: 'DELETE' }),

  createColumn: (boardId: string, title: string) =>
    request<Column>(`${board(boardId)}/columns`, { method: 'POST', json: { title } }),
  renameColumn: (boardId: string, columnId: string, title: string) =>
    request<Column>(`${board(boardId)}/columns/${columnId}`, { method: 'PATCH', json: { title } }),
  deleteColumn: (boardId: string, columnId: string) =>
    request<void>(`${board(boardId)}/columns/${columnId}`, { method: 'DELETE' }),
  moveColumn: (boardId: string, columnId: string, toIndex: number) =>
    request<string[]>(`${board(boardId)}/columns/${columnId}/move`, { method: 'POST', json: { toIndex } }),

  createCard: (boardId: string, columnId: string, title: string) =>
    request<Card>(`${board(boardId)}/cards`, { method: 'POST', json: { columnId, title } }),
  updateCard: (boardId: string, cardId: string, patch: { title?: string; description?: string; color?: string }) =>
    request<Card>(`${board(boardId)}/cards/${cardId}`, { method: 'PATCH', json: patch }),
  deleteCard: (boardId: string, cardId: string) =>
    request<void>(`${board(boardId)}/cards/${cardId}`, { method: 'DELETE' }),
  moveCard: (boardId: string, cardId: string, toColumnId: string, toIndex: number) =>
    request<CardMovedEvent>(`${board(boardId)}/cards/${cardId}/move`, { method: 'POST', json: { toColumnId, toIndex } }),
}
