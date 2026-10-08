/** Drag-and-drop ids: columns and their card lists need ids distinct from card ids in the same DndContext. */
export const columnDragId = (id: string) => `col:${id}`
export const columnBodyId = (id: string) => `body:${id}`
