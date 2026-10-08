import type { Board, Card, Column, Member } from '../api'

export type BoardAction =
  | { type: 'load'; board: Board }
  | { type: 'boardRenamed'; title: string }
  | { type: 'columnUpsert'; column: Column }
  | { type: 'columnDeleted'; columnId: string }
  | { type: 'columnsReordered'; columnIds: string[] }
  | { type: 'cardUpsert'; card: Card }
  | { type: 'cardDeleted'; cardId: string }
  | { type: 'cardMoved'; card?: Card; columnOrders: Record<string, string[]> }
  | { type: 'memberJoined'; member: Member }

/**
 * Applies server events (and local optimistic changes) to the board. Every action carries resulting state rather
 * than a diff, so applying the same event twice — e.g. our own optimistic move followed by the server's
 * broadcast of it — is harmless, and every client ends up with the server's order.
 */
export function boardReducer(board: Board | null, action: BoardAction): Board | null {
  if (action.type === 'load') return action.board
  if (!board) return board

  switch (action.type) {
    case 'boardRenamed':
      return { ...board, title: action.title }

    case 'columnUpsert': {
      const exists = board.columns.some((c) => c.id === action.column.id)
      return {
        ...board,
        columns: exists
          ? board.columns.map((c) => (c.id === action.column.id ? { ...c, title: action.column.title } : c))
          : [...board.columns, { ...action.column, cards: action.column.cards ?? [] }],
      }
    }

    case 'columnDeleted':
      return { ...board, columns: board.columns.filter((c) => c.id !== action.columnId) }

    case 'columnsReordered': {
      const byId = new Map(board.columns.map((c) => [c.id, c]))
      const ordered = action.columnIds.map((id) => byId.get(id)).filter((c): c is Column => !!c)
      const rest = board.columns.filter((c) => !action.columnIds.includes(c.id))
      return { ...board, columns: [...ordered, ...rest] }
    }

    case 'cardUpsert': {
      const card = action.card
      const existsIn = board.columns.find((c) => c.cards.some((x) => x.id === card.id))
      if (existsIn) {
        return {
          ...board,
          columns: board.columns.map((c) => ({ ...c, cards: c.cards.map((x) => (x.id === card.id ? card : x)) })),
        }
      }
      return {
        ...board,
        columns: board.columns.map((c) => {
          if (c.id !== card.columnId) return c
          const cards = [...c.cards]
          cards.splice(Math.min(card.position, cards.length), 0, card)
          return { ...c, cards }
        }),
      }
    }

    case 'cardDeleted':
      return {
        ...board,
        columns: board.columns.map((c) => ({ ...c, cards: c.cards.filter((x) => x.id !== action.cardId) })),
      }

    case 'cardMoved': {
      const byId = new Map(board.columns.flatMap((c) => c.cards).map((card) => [card.id, card]))
      if (action.card) byId.set(action.card.id, action.card)
      const moved = new Set(Object.values(action.columnOrders).flat())
      return {
        ...board,
        columns: board.columns.map((c) => {
          const order = action.columnOrders[c.id]
          if (!order) return { ...c, cards: c.cards.filter((x) => !moved.has(x.id)) }
          return {
            ...c,
            cards: order
              .map((id) => byId.get(id))
              .filter((x): x is Card => !!x)
              .map((x) => (x.columnId === c.id ? x : { ...x, columnId: c.id })),
          }
        }),
      }
    }

    case 'memberJoined':
      return board.members.some((m) => m.userId === action.member.userId)
        ? board
        : { ...board, members: [...board.members, action.member] }
  }
}

/** Where a card is and the resulting orders after moving it — used for optimistic local moves. */
export function planCardMove(board: Board, cardId: string, toColumnId: string, toIndex: number): Record<string, string[]> | null {
  const from = board.columns.find((c) => c.cards.some((x) => x.id === cardId))
  const to = board.columns.find((c) => c.id === toColumnId)
  if (!from || !to) return null

  const fromIds = from.cards.map((x) => x.id).filter((id) => id !== cardId)
  if (from.id === to.id) {
    fromIds.splice(Math.max(0, Math.min(toIndex, fromIds.length)), 0, cardId)
    return { [from.id]: fromIds }
  }
  const toIds = to.cards.map((x) => x.id)
  toIds.splice(Math.max(0, Math.min(toIndex, toIds.length)), 0, cardId)
  return { [from.id]: fromIds, [to.id]: toIds }
}
