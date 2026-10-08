import {
  closestCenter,
  closestCorners,
  DndContext,
  DragOverlay,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
  type CollisionDetection,
  type DragEndEvent,
  type DragOverEvent,
  type DragStartEvent,
} from '@dnd-kit/core'
import { arrayMove, horizontalListSortingStrategy, SortableContext, sortableKeyboardCoordinates } from '@dnd-kit/sortable'
import { useMemo, useState, type Dispatch } from 'react'
import { ApiError, api, type Board, type Card } from '../api'
import { CardFace, type EditorBadge } from './CardView'
import { ColumnView } from './ColumnView'
import { columnDragId } from './ids'
import { planCardMove, type BoardAction } from './state'

interface Props {
  board: Board
  dispatch: Dispatch<BoardAction>
  editorsByCard: Record<string, EditorBadge[]>
  onOpenCard: (cardId: string) => void
  onError: (message: string) => void
  onResync: () => void
}

type Active = { type: 'card'; id: string } | { type: 'column'; id: string }
type Layout = Record<string, string[]> // columnId -> card ids, while a card is being dragged

/** Columns only collide with columns; cards collide with cards and column bodies. */
const collisionDetection: CollisionDetection = (args) => {
  const type = args.active.data.current?.type
  const containers = args.droppableContainers.filter((c) =>
    type === 'column' ? c.data.current?.type === 'column' : c.data.current?.type !== 'column',
  )
  return (type === 'column' ? closestCenter : closestCorners)({ ...args, droppableContainers: containers })
}

export function BoardCanvas({ board, dispatch, editorsByCard, onOpenCard, onError, onResync }: Props) {
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  )
  const [active, setActive] = useState<Active | null>(null)
  // While a card is dragged, its column membership is previewed here; the board itself only changes on drop.
  const [layout, setLayout] = useState<Layout | null>(null)

  const cardsById = useMemo(() => new Map(board.columns.flatMap((c) => c.cards).map((card) => [card.id, card])), [board])
  const columns = layout
    ? board.columns.map((c) => ({
        ...c,
        cards: (layout[c.id] ?? []).map((id) => cardsById.get(id)).filter((x): x is Card => !!x),
      }))
    : board.columns

  const columnOf = (cardId: string, l: Layout) => Object.keys(l).find((col) => l[col].includes(cardId))

  function onDragStart(e: DragStartEvent) {
    const type = e.active.data.current?.type
    if (type === 'card') {
      setActive({ type: 'card', id: String(e.active.id) })
      setLayout(Object.fromEntries(board.columns.map((c) => [c.id, c.cards.map((x) => x.id)])))
    } else if (type === 'column') {
      setActive({ type: 'column', id: e.active.data.current?.columnId })
    }
  }

  function onDragOver(e: DragOverEvent) {
    if (active?.type !== 'card' || !layout || !e.over) return
    const cardId = active.id
    const from = columnOf(cardId, layout)
    const overData = e.over.data.current
    const to = overData?.type === 'card' ? overData.columnId : overData?.columnId
    if (!from || !to || from === to) return

    // Hop into the other column, at the hovered card's position (or the end of an empty/body area).
    setLayout((current) => {
      if (!current) return current
      const target = current[to].filter((id) => id !== cardId)
      const overIndex = overData?.type === 'card' ? target.indexOf(String(e.over!.id)) : -1
      target.splice(overIndex >= 0 ? overIndex : target.length, 0, cardId)
      return { ...current, [from]: current[from].filter((id) => id !== cardId), [to]: target }
    })
  }

  function onDragEnd(e: DragEndEvent) {
    const current = active
    const currentLayout = layout
    setActive(null)
    setLayout(null)
    if (!current || !e.over) return

    if (current.type === 'column') {
      const ids = board.columns.map((c) => c.id)
      const overColumnId = e.over.data.current?.columnId as string | undefined
      const from = ids.indexOf(current.id)
      const to = overColumnId ? ids.indexOf(overColumnId) : -1
      if (from < 0 || to < 0 || from === to) return
      dispatch({ type: 'columnsReordered', columnIds: arrayMove(ids, from, to) })
      api.moveColumn(board.id, current.id, to).catch(fail)
      return
    }

    if (!currentLayout) return
    const cardId = current.id
    const columnId = columnOf(cardId, currentLayout)
    if (!columnId) return

    // Final in-column position: reorder relative to the card it was dropped on.
    let ids = currentLayout[columnId]
    if (e.over.data.current?.type === 'card' && e.over.id !== cardId) {
      const overIndex = ids.indexOf(String(e.over.id))
      if (overIndex >= 0) ids = arrayMove(ids, ids.indexOf(cardId), overIndex)
    }
    const toIndex = ids.indexOf(cardId)

    const original = board.columns.find((c) => c.cards.some((x) => x.id === cardId))
    if (original?.id === columnId && original.cards.findIndex((x) => x.id === cardId) === toIndex) return

    // Optimistic: show the move now; the server's CardMoved broadcast then confirms the final order.
    const orders = planCardMove(board, cardId, columnId, toIndex)
    if (orders) dispatch({ type: 'cardMoved', columnOrders: orders })
    api.moveCard(board.id, cardId, columnId, toIndex).catch(fail)
  }

  function fail(e: unknown) {
    onError(e instanceof ApiError ? e.message : 'That change could not be saved.')
    onResync() // put the board back to the server's truth
  }

  const activeCard = active?.type === 'card' ? cardsById.get(active.id) : undefined
  const activeColumn = active?.type === 'column' ? board.columns.find((c) => c.id === active.id) : undefined

  return (
    <DndContext
      sensors={sensors}
      collisionDetection={collisionDetection}
      onDragStart={onDragStart}
      onDragOver={onDragOver}
      onDragEnd={onDragEnd}
      onDragCancel={() => {
        setActive(null)
        setLayout(null)
      }}
    >
      <SortableContext items={board.columns.map((c) => columnDragId(c.id))} strategy={horizontalListSortingStrategy}>
        <div className="columns">
          {columns.map((column) => (
            <ColumnView
              key={column.id}
              boardId={board.id}
              column={column}
              editorsByCard={editorsByCard}
              onOpenCard={onOpenCard}
              onError={onError}
            />
          ))}
          <AddColumn boardId={board.id} onError={onError} />
        </div>
      </SortableContext>

      <DragOverlay dropAnimation={{ duration: 180, easing: 'ease-out' }}>
        {activeCard && <CardFace card={activeCard} dragging />}
        {activeColumn && (
          <div className="column column-overlay">
            <header className="column-header"><strong>{activeColumn.title}</strong></header>
            <p className="muted small">{activeColumn.cards.length} cards</p>
          </div>
        )}
      </DragOverlay>
    </DndContext>
  )
}

function AddColumn({ boardId, onError }: { boardId: string; onError: (message: string) => void }) {
  const [title, setTitle] = useState('')
  const [open, setOpen] = useState(false)

  async function submit() {
    const clean = title.trim()
    if (!clean) return
    try {
      await api.createColumn(boardId, clean)
      setTitle('')
      setOpen(false)
    } catch (e) {
      onError(e instanceof ApiError ? e.message : 'Could not add the column.')
    }
  }

  if (!open) {
    return (
      <button type="button" className="add-column" onClick={() => setOpen(true)}>
        + Add column
      </button>
    )
  }
  return (
    <form
      className="add-column-form"
      onSubmit={(e) => {
        e.preventDefault()
        void submit()
      }}
    >
      <input autoFocus maxLength={120} placeholder="Column title" value={title} onChange={(e) => setTitle(e.target.value)} aria-label="New column title" onKeyDown={(e) => e.key === 'Escape' && setOpen(false)} />
      <div className="add-card-actions">
        <button className="btn btn-primary btn-sm" disabled={!title.trim()}>Add column</button>
        <button type="button" className="btn btn-ghost btn-sm" onClick={() => setOpen(false)}>Cancel</button>
      </div>
    </form>
  )
}
