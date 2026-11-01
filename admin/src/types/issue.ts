export type Priority = 'low' | 'medium' | 'high';
export type Status = 'open' | 'in_progress' | 'resolved';
export interface Issue {
  id: string;
  title: string;
  description: string;
  priority: Priority;
  status: Status;
  position: { x: number; y: number; z: number };
  createdAt: string;
  updatedAt: string;
}
export const statusLabels: Record<Status, string> = { open: '待处理', in_progress: '处理中', resolved: '已解决' };
export const priorityLabels: Record<Priority, string> = { low: '低', medium: '中', high: '高' };

export function isIssue(value: unknown): value is Issue {
  if (!value || typeof value !== 'object') return false;
  const i = value as Issue;
  return typeof i.id === 'string' && typeof i.title === 'string' && typeof i.description === 'string'
    && ['low', 'medium', 'high'].includes(i.priority) && ['open', 'in_progress', 'resolved'].includes(i.status)
    && !!i.position && [i.position.x, i.position.y, i.position.z].every(v => typeof v === 'number' && Number.isFinite(v))
    && typeof i.createdAt === 'string' && typeof i.updatedAt === 'string';
}
