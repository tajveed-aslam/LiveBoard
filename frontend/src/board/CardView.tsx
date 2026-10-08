import { useSortable } from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import type { Card } from '../api'
import { labelHex } from '../colors'
import { Avatar } from '../components/Avatar'

export interface EditorBadge {
  userId: string
  displayName: string
}

interface Props {
  card: Card
  columnId: string
  editors: EditorBadge[]
  onOpen: (cardId: string) => void
}

export function CardFace({ card, editors = [], dragging = false }: { card: Card; editors?: EditorBadge[]; dragging?: boolean }) {
  const label = labelHex(card.color)
  return (
    <div className={`card ${dragging ? 'card-dragging' : ''}`}>
      {label && <span className="card-label" style={{ background: label }} aria-label={`${card.color} label`} />}
      <p className="card-title">{card.title}</p>
      {(card.description || editors.length > 0) && (
        <div className="card-meta">
          {card.description && <span className="card-has-desc" title="Has a description" aria-label="Has a description">≡</span>}
          {editors.length > 0 && (
            <span className="card-editing" title={`${editors.map((e) => e.displayName).join(', ')} editing now`}>
              {editors.map((e) => <Avatar key={e.userId} userId={e.userId} name={e.displayName} size={20} />)}
              <span>editing</span>
            </span>
          )}
        </div>
      )}
    </div>
  )
}

/** A draggable card. Click (without dragging) opens the editor; the pointer sensor needs a 6px move to start a drag. */
export function CardView({ card, columnId, editors, onOpen }: Props) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id: card.id,
    data: { type: 'card', columnId },
  })

  return (
    <li
      ref={setNodeRef}
      style={{ transform: CSS.Translate.toString(transform), transition }}
      className={`card-slot ${isDragging ? 'card-placeholder' : ''}`}
      {...attributes}
      {...listeners}
      aria-roledescription="draggable card"
      aria-label={`${card.title}. Press space to pick up, arrow keys to move, enter to open.`}
      onClick={() => onOpen(card.id)}
      onKeyDown={(e) => {
        listeners?.onKeyDown?.(e)
        if (e.key === 'Enter' && !e.defaultPrevented) onOpen(card.id)
      }}
    >
      <CardFace card={card} editors={editors} />
    </li>
  )
}
