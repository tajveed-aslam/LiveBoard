import { useDroppable } from '@dnd-kit/core'
import { SortableContext, useSortable, verticalListSortingStrategy } from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { useState, type FormEvent } from 'react'
import { ApiError, api, type Column } from '../api'
import { InlineEdit } from '../components/InlineEdit'
import { CardView, type EditorBadge } from './CardView'
import { columnBodyId, columnDragId } from './ids'

interface Props {
  boardId: string
  column: Column
  editorsByCard: Record<string, EditorBadge[]>
  onOpenCard: (cardId: string) => void
  onError: (message: string) => void
}

export function ColumnView({ boardId, column, editorsByCard, onOpenCard, onError }: Props) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id: columnDragId(column.id),
    data: { type: 'column', columnId: column.id },
  })
  // Separate droppable for the card list, so empty columns still accept cards.
  const { setNodeRef: setBodyRef, isOver } = useDroppable({
    id: columnBodyId(column.id),
    data: { type: 'column-body', columnId: column.id },
  })

  async function rename(title: string) {
    try {
      await api.renameColumn(boardId, column.id, title)
    } catch (e) {
      onError(e instanceof ApiError ? e.message : 'Could not rename the column.')
    }
  }

  async function remove() {
    const count = column.cards.length
    if (!window.confirm(`Delete "${column.title}"${count ? ` and its ${count} card${count === 1 ? '' : 's'}` : ''}?`)) return
    try {
      await api.deleteColumn(boardId, column.id)
    } catch (e) {
      onError(e instanceof ApiError ? e.message : 'Could not delete the column.')
    }
  }

  return (
    <section
      ref={setNodeRef}
      style={{ transform: CSS.Translate.toString(transform), transition }}
      className={`column ${isDragging ? 'column-placeholder' : ''}`}
      aria-label={`${column.title}, ${column.cards.length} cards`}
    >
      <header className="column-header">
        <span
          className="column-handle"
          {...attributes}
          {...listeners}
          aria-roledescription="draggable column"
          aria-label={`Move column ${column.title}`}
          title="Drag to reorder columns"
        >
          ⠿
        </span>
        <InlineEdit value={column.title} onSave={rename} label="column" className="column-title" />
        <span className="column-count">{column.cards.length}</span>
        <button type="button" className="icon-button column-delete" aria-label={`Delete column ${column.title}`} onClick={() => void remove()}>
          ×
        </button>
      </header>

      <SortableContext items={column.cards.map((c) => c.id)} strategy={verticalListSortingStrategy}>
        <ol ref={setBodyRef} className={`card-list ${isOver ? 'card-list-over' : ''}`}>
          {column.cards.map((card) => (
            <CardView key={card.id} card={card} columnId={column.id} editors={editorsByCard[card.id] ?? []} onOpen={onOpenCard} />
          ))}
          {column.cards.length === 0 && <li className="card-list-empty">Drop cards here</li>}
        </ol>
      </SortableContext>

      <AddCard boardId={boardId} columnId={column.id} onError={onError} />
    </section>
  )
}

function AddCard({ boardId, columnId, onError }: { boardId: string; columnId: string; onError: (message: string) => void }) {
  const [open, setOpen] = useState(false)
  const [title, setTitle] = useState('')
  const [busy, setBusy] = useState(false)

  async function submit(e: FormEvent) {
    e.preventDefault()
    const clean = title.trim()
    if (!clean) return
    setBusy(true)
    try {
      // The new card arrives through the CardCreated broadcast, like everyone else's.
      await api.createCard(boardId, columnId, clean)
      setTitle('')
    } catch (err) {
      onError(err instanceof ApiError ? err.message : 'Could not add the card.')
    } finally {
      setBusy(false)
    }
  }

  if (!open) {
    return (
      <button type="button" className="add-card-button" onClick={() => setOpen(true)}>
        + Add a card
      </button>
    )
  }

  return (
    <form className="add-card" onSubmit={submit}>
      <textarea
        autoFocus
        rows={2}
        maxLength={200}
        placeholder="Card title"
        value={title}
        onChange={(e) => setTitle(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault()
            e.currentTarget.form?.requestSubmit()
          }
          if (e.key === 'Escape') setOpen(false)
        }}
        aria-label="New card title"
      />
      <div className="add-card-actions">
        <button className="btn btn-primary btn-sm" disabled={busy || !title.trim()}>Add card</button>
        <button type="button" className="btn btn-ghost btn-sm" onClick={() => setOpen(false)}>Cancel</button>
      </div>
    </form>
  )
}
