export const projectStates = [
  { value: 'idea', label: 'Idea' },
  { value: 'active', label: 'Active' },
  { value: 'on-hold', label: 'On hold' },
  { value: 'completed', label: 'Completed' },
  { value: 'archived', label: 'Archived' },
]

export function projectState(project) {
  return project.archivedAt ? projectStates[4] : projectStates[project.status] || projectStates[0]
}
