import { useEffect, useRef, type ReactNode } from 'react';
import { priorityLabels, statusLabels, type Issue } from '../types/issue';

function Modal({ title, busy, onClose, children }: { title: string; busy: boolean; onClose: () => void; children: ReactNode }) {
  const ref = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    const dialog = ref.current!;
    const previous = document.activeElement as HTMLElement | null;
    dialog.showModal();
    return () => { dialog.close(); if (previous?.isConnected) previous.focus(); };
  }, []);
  return <dialog ref={ref} className="issue-dialog" aria-labelledby="dialog-title" onCancel={e => { e.preventDefault(); if (!busy) onClose(); }}>
    <div className="dialog-heading"><h2 id="dialog-title">{title}</h2><button className="dialog-close" disabled={busy} onClick={onClose} aria-label="关闭弹窗">×</button></div>
    {children}
  </dialog>;
}

export function IssueDetails({ issue, onClose, onDelete }: { issue: Issue; onClose: () => void; onDelete: () => void }) {
  return <Modal title="问题详情" busy={false} onClose={onClose}>
    <div className="detail-badges"><span className={`priority ${issue.priority}`}>{priorityLabels[issue.priority]}优先级</span><span className={`status ${issue.status}`}>{statusLabels[issue.status]}</span></div>
    <h3 className="detail-title">{issue.title}</h3>
    <section className="detail-section"><h4>问题描述</h4><p>{issue.description || '暂无问题描述'}</p></section>
    <dl className="detail-fields"><dt>问题 ID</dt><dd>{issue.id}</dd><dt>世界坐标</dt><dd>x={issue.position.x}<br/>y={issue.position.y}<br/>z={issue.position.z}</dd><dt>创建时间</dt><dd>{new Date(issue.createdAt).toLocaleString('zh-CN', {hour12:false})}</dd><dt>最后更新</dt><dd>{new Date(issue.updatedAt).toLocaleString('zh-CN', {hour12:false})}</dd></dl>
    <div className="dialog-actions"><button className="danger-outline" onClick={onDelete}>删除此问题</button><button className="refresh" onClick={onClose}>关闭详情</button></div>
  </Modal>;
}

export function DeleteConfirmation({ issue, busy, error, onCancel, onConfirm }: { issue: Issue; busy: boolean; error: string; onCancel: () => void; onConfirm: () => void }) {
  return <Modal title="确认删除问题？" busy={busy} onClose={onCancel}>
    <p className="delete-title">{issue.title}</p>
    <p className="delete-explanation">删除后，该问题将从列表隐藏，Unity 下次刷新时会移除对应标记。数据库保留原记录，本版暂未提供恢复入口。</p>
    {error && <p className="dialog-error" role="alert">{error}</p>}
    <div className="dialog-actions"><button className="neutral" disabled={busy} onClick={onCancel}>取消</button><button className="danger" disabled={busy} onClick={onConfirm}>{busy ? '删除中…' : '确认删除'}</button></div>
  </Modal>;
}
