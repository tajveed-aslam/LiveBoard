import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr'
import { useCallback, useEffect, useReducer, useRef, useState } from 'react'
import {
  API_BASE,
  ApiError,
  api,
  currentToken,
  type Actor,
  type Board,
  type Card,
  type CardMovedEvent,
  type Column,
  type Member,
  type PresenceUser,
} from '../api'
import { boardReducer, type BoardAction } from './state'

export type ConnectionStatus = 'connecting' | 'live' | 'reconnecting' | 'offline'

export interface Activity {
  id: number
  actor: Actor
  text: string
  at: number
}

export interface EditingInfo {
  displayName: string
  cardId: string
}

const MAX_ACTIVITY = 30

/**
 * Loads a board and keeps it in sync: REST for the initial state, SignalR for every change after that.
 * On reconnect the board is re-joined and reloaded, so nothing missed while offline is lost.
 */
export function useBoard(boardId: string) {
  const [board, dispatch] = useReducer(boardReducer, null)
  // Event handlers read the latest board (for titles in the activity feed) without re-subscribing.
  const boardRef = useRef<Board | null>(board)
  useEffect(() => {
    boardRef.current = board
  }, [board])

  const [error, setError] = useState<{ status: number; message: string } | null>(null)
  const [status, setStatus] = useState<ConnectionStatus>('connecting')
  const [presence, setPresence] = useState<PresenceUser[]>([])
  const [editing, setEditing] = useState<Record<string, EditingInfo>>({})
  const [activity, setActivity] = useState<Activity[]>([])
  const [deletedBy, setDeletedBy] = useState<Actor | null>(null)
  const connectionRef = useRef<HubConnection | null>(null)
  const activityId = useRef(0)

  const reload = useCallback(async () => {
    try {
      dispatch({ type: 'load', board: await api.getBoard(boardId) })
      setError(null)
    } catch (e) {
      setError(e instanceof ApiError ? { status: e.status, message: e.message } : { status: 0, message: 'Could not load the board.' })
    }
  }, [boardId])

  useEffect(() => {
    // One hook instance per board: the page is keyed by board id, so switching boards starts from fresh state.
    let disposed = false
    // Fetching is the external sync this effect exists for; state is only set after the request resolves.
    // oxlint-disable-next-line react/set-state-in-effect
    void reload()

    const log = (actor: Actor, text: string) =>
      setActivity((items) => [{ id: ++activityId.current, actor, text, at: Date.now() }, ...items].slice(0, MAX_ACTIVITY))
    const columnTitle = (id: string) => boardRef.current?.columns.find((c) => c.id === id)?.title ?? 'a column'

    const connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE}/hubs/board`, { accessTokenFactory: () => currentToken() ?? '', withCredentials: false })
      .withAutomaticReconnect([0, 1000, 3000, 5000, 10000, 20000, 30000])
      .configureLogging(LogLevel.Warning)
      .build()
    connectionRef.current = connection

    const on = <T,>(name: string, handler: (e: T) => void) => connection.on(name, handler)
    const apply = (action: BoardAction) => dispatch(action)

    on<{ title: string; actor: Actor }>('BoardUpdated', (e) => {
      apply({ type: 'boardRenamed', title: e.title })
      log(e.actor, `renamed the board to "${e.title}"`)
    })
    on<{ actor: Actor }>('BoardDeleted', (e) => setDeletedBy(e.actor))
    on<{ column: Column; actor: Actor }>('ColumnCreated', (e) => {
      apply({ type: 'columnUpsert', column: e.column })
      log(e.actor, `added the column "${e.column.title}"`)
    })
    on<{ column: Column; actor: Actor }>('ColumnUpdated', (e) => {
      const before = columnTitle(e.column.id)
      apply({ type: 'columnUpsert', column: e.column })
      log(e.actor, `renamed "${before}" to "${e.column.title}"`)
    })
    on<{ columnId: string; title: string; actor: Actor }>('ColumnDeleted', (e) => {
      apply({ type: 'columnDeleted', columnId: e.columnId })
      log(e.actor, `deleted the column "${e.title}"`)
    })
    on<{ columnIds: string[]; actor: Actor }>('ColumnsReordered', (e) => {
      apply({ type: 'columnsReordered', columnIds: e.columnIds })
      log(e.actor, 'reordered the columns')
    })
    on<{ card: Card; actor: Actor }>('CardCreated', (e) => {
      apply({ type: 'cardUpsert', card: e.card })
      log(e.actor, `added "${e.card.title}" to ${columnTitle(e.card.columnId)}`)
    })
    on<{ card: Card; actor: Actor }>('CardUpdated', (e) => {
      apply({ type: 'cardUpsert', card: e.card })
      log(e.actor, `edited "${e.card.title}"`)
    })
    on<{ cardId: string; title: string; actor: Actor }>('CardDeleted', (e) => {
      apply({ type: 'cardDeleted', cardId: e.cardId })
      log(e.actor, `deleted "${e.title}"`)
    })
    on<CardMovedEvent>('CardMoved', (e) => {
      apply({ type: 'cardMoved', card: e.card, columnOrders: e.columnOrders })
      log(e.actor, e.fromColumnId === e.card.columnId
        ? `reordered "${e.card.title}" in ${columnTitle(e.card.columnId)}`
        : `moved "${e.card.title}" to ${columnTitle(e.card.columnId)}`)
    })
    on<{ member: Member }>('MemberJoined', (e) => {
      apply({ type: 'memberJoined', member: e.member })
      log({ userId: e.member.userId, displayName: e.member.displayName }, 'joined the board')
    })
    on<{ boardId: string; users: PresenceUser[] }>('PresenceChanged', (e) => {
      if (e.boardId !== boardId) return
      setPresence(e.users)
      // Anyone who left can't still be editing.
      const online = new Set(e.users.map((u) => u.userId))
      setEditing((current) => Object.fromEntries(Object.entries(current).filter(([userId]) => online.has(userId))))
    })
    on<{ userId: string; displayName: string; cardId: string | null }>('EditingChanged', (e) =>
      setEditing((current) => {
        const next = { ...current }
        if (e.cardId) next[e.userId] = { displayName: e.displayName, cardId: e.cardId }
        else delete next[e.userId]
        return next
      }),
    )

    const join = async () => {
      const users = await connection.invoke<PresenceUser[]>('JoinBoard', boardId)
      if (!disposed) setPresence(users)
    }

    connection.onreconnecting(() => !disposed && setStatus('reconnecting'))
    connection.onreconnected(async () => {
      if (disposed) return
      try {
        await join()
        await reload() // catch up on anything that changed while we were disconnected
        setStatus('live')
      } catch {
        setStatus('offline')
      }
    })
    connection.onclose(() => !disposed && setStatus('offline'))

    connection
      .start()
      .then(join)
      .then(() => !disposed && setStatus('live'))
      .catch(() => !disposed && setStatus('offline'))

    return () => {
      disposed = true
      connectionRef.current = null
      void connection.stop()
    }
  }, [boardId, reload])

  const setEditingCard = useCallback((cardId: string | null) => {
    const connection = connectionRef.current
    if (connection?.state === HubConnectionState.Connected) void connection.invoke('SetEditing', cardId).catch(() => {})
  }, [])

  return { board, dispatch, error, status, presence, editing, activity, deletedBy, reload, setEditingCard }
}
