import { useEffect, useRef, useState } from 'react'

interface Props {
  value: string
  onSave: (value: string) => Promise<void> | void
  label: string
  className?: string
  maxLength?: number
}

/** Text that turns into an input on click; Enter or blur saves, Escape cancels. */
export function InlineEdit({ value, onSave, label, className = '', maxLength = 120 }: Props) {
  const [editing, setEditing] = useState(false)
  const [draft, setDraft] = useState(value)
  const inputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    if (editing) inputRef.current?.select()
  }, [editing])

  function start() {
    setDraft(value)
    setEditing(true)
  }

  async function commit() {
    setEditing(false)
    const next = draft.trim()
    if (next && next !== value) await onSave(next)
  }

  if (!editing) {
    return (
      <button type="button" className={`inline-edit ${className}`} onClick={start} title={`Rename ${label}`}>
        {value}
      </button>
    )
  }

  return (
    <input
      ref={inputRef}
      className={`inline-edit-input ${className}`}
      aria-label={`Rename ${label}`}
      value={draft}
      maxLength={maxLength}
      onChange={(e) => setDraft(e.target.value)}
      onBlur={() => void commit()}
      onKeyDown={(e) => {
        if (e.key === 'Enter') void commit()
        if (e.key === 'Escape') setEditing(false)
      }}
    />
  )
}
