import { useEffect, useRef, useState } from 'react';
import { deleteIssue, getIssue, getIssues, updateStatus } from './api/issues';
import { priorityLabels, statusLabels, type Issue, type Status, type Priority } from './types/issue';
import { DeleteConfirmation, IssueDetails } from './components/IssueDialogs';

const statuses: Status[] = ['open', 'in_progress', 'resolved'];
const formatTime = (value: string) => new Date(value).toLocaleString('zh-CN', { hour12: false });
const errorMessage = (value: unknown) => value instanceof Error ? value.message : '操作失败，请稍后重试。';

function IssueRow({ issue, onSave, busy, saving, reading, onDetails, onDelete }: { issue: Issue; onSave: (id: string, status: Status) => Promise<boolean>; busy: boolean; saving: boolean; reading: boolean; onDetails: () => void; onDelete: () => void }) {
  const [status, setStatus] = useState<Status>(issue.status);
  useEffect(() => setStatus(issue.status), [issue.status]);
  async function save() {
    if (!await onSave(issue.id, status)) setStatus(issue.status);
  }
  return <article className="issue-card">
    <div className="issue-top">
      <div className="issue-heading"><span className={`priority ${issue.priority}`}>{priorityLabels[issue.priority]}优先级</span><h2>{issue.title}</h2></div>
      <span className={`status ${issue.status}`}><span />{statusLabels[issue.status]}</span>
    </div>
    <p className={`description ${issue.description ? '' : 'muted'}`}>{issue.description || '暂无问题描述'}</p>
    <div className="issue-bottom">
      <div className="metadata"><span className="coordinate">位置 · {issue.position.x.toFixed(2)} / {issue.position.y.toFixed(2)} / {issue.position.z.toFixed(2)}</span><span>上报 {formatTime(issue.createdAt)}</span></div>
      <div className="status-editor"><label htmlFor={`status-${issue.id}`}>处理状态</label>
        <select id={`status-${issue.id}`} value={status} disabled={busy} onChange={e => setStatus(e.target.value as Status)}>
          {statuses.map(s => <option value={s} key={s}>{statusLabels[s]}</option>)}
        </select>
        <button className="save" disabled={busy || status === issue.status} onClick={save}>{saving ? '保存中…' : '保存'}</button>
      </div>
    </div>
    <div className="row-actions"><span className="record-id">ID · {issue.id.slice(0, 8)}</span><button className="text-button" disabled={busy} onClick={onDetails} aria-label={`查看详情：${issue.title}`}>{reading ? '读取中…' : '查看详情'}</button><button className="text-button danger-text" disabled={busy} onClick={onDelete} aria-label={`删除：${issue.title}`}>删除</button></div>
  </article>;
}

export default function App() {
  const [issues, setIssues] = useState<Issue[]>([]);
  const [loaded, setLoaded] = useState(false);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState<string | null>(null);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [filter, setFilter] = useState<Status | 'all'>('all');
  const [priority, setPriority] = useState<Priority | 'all'>('all');
  const [query, setQuery] = useState('');
  const [sort, setSort] = useState('newest');
  const [details, setDetails] = useState<Issue | null>(null);
  const [reading, setReading] = useState<string | null>(null);
  const [pendingDelete, setPendingDelete] = useState<Issue | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState('');
  // A synchronous lock prevents double clicks and stale refreshes overwriting a saved status.
  const operation = useRef(false);

  async function refresh() {
    if (operation.current) return;
    operation.current = true; setLoading(true); setError(''); setNotice('');
    try { setIssues(await getIssues()); setLoaded(true); }
    catch (e) { setError(errorMessage(e)); }
    finally { operation.current = false; setLoading(false); }
  }
  useEffect(() => { void refresh(); }, []);

  async function save(id: string, status: Status): Promise<boolean> {
    if (operation.current) return false;
    operation.current = true; setSaving(id); setError(''); setNotice('');
    try {
      const updated = await updateStatus(id, status);
      setIssues(current => current.map(i => i.id === id ? updated : i));
      setNotice(`“${updated.title}”已更新为${statusLabels[updated.status]}。`);
      return true;
    } catch (e) { setError(errorMessage(e)); return false; }
    finally { operation.current = false; setSaving(null); }
  }

  async function openDetails(id: string) {
    if (operation.current) return;
    operation.current = true; setReading(id); setError(''); setNotice('');
    try {
      const latest = await getIssue(id);
      setIssues(current => current.map(i => i.id === id ? latest : i));
      setDetails(latest);
    } catch (e) { setError(errorMessage(e)); }
    finally { operation.current = false; setReading(null); }
  }

  function askDelete(issue: Issue) { setDetails(null); setDeleteError(''); setPendingDelete(issue); }
  async function confirmDelete() {
    if (!pendingDelete || operation.current) return;
    operation.current = true; setDeleting(true); setDeleteError(''); setError(''); setNotice('');
    try {
      await deleteIssue(pendingDelete.id);
      setIssues(current => current.filter(i => i.id !== pendingDelete.id));
      setNotice(`“${pendingDelete.title}”已删除。Unity 刷新后会移除对应标记。`);
      setPendingDelete(null);
    } catch (e) { setDeleteError(`${errorMessage(e)} 若请求中断，请取消后刷新列表确认结果。`); }
    finally { operation.current = false; setDeleting(false); }
  }

  const search = query.trim().toLocaleLowerCase();
  const shown = issues.filter(i => (filter === 'all' || i.status === filter) && (priority === 'all' || i.priority === priority)
    && (!search || [i.title, i.description, i.id].some(value => value.toLocaleLowerCase().includes(search))));
  const rank: Record<Priority, number> = {high: 0, medium: 1, low: 2};
  shown.sort((a, b) => sort === 'priority' ? rank[a.priority] - rank[b.priority] || b.createdAt.localeCompare(a.createdAt)
    : sort === 'oldest' ? a.createdAt.localeCompare(b.createdAt) : b.createdAt.localeCompare(a.createdAt));
  const filtered = filter !== 'all' || priority !== 'all' || search !== '';
  function resetFilters() { setFilter('all'); setPriority('all'); setQuery(''); setSort('newest'); }
  const busy = loading || saving !== null || reading !== null || deleting;
  return <div className="app-shell">
    <aside className="sidebar"><div className="brand"><span className="brand-mark">巡</span><div>INSPECTION<span>虚拟巡检现场</span></div></div><div className="nav-label">工作空间</div><div className="nav-active"><span>▦</span> 问题管理 <span className="nav-count">{loaded ? issues.length : '—'}</span></div><div className="sidebar-note"><span className="small-label">现场到处理，全程记录</span><p>在 Unity 中发现问题，<br/>在这里跟进处理进度。</p><span className="version">LOCAL WORKSPACE · 01</span></div></aside>
    <main>
      <header><div className="breadcrumb">巡检工作台 <span>/</span> 问题管理</div><span className="workspace-tag">设备间 01</span></header>
      <section className="page-title"><div><p className="eyebrow">ISSUE MANAGEMENT</p><h1>每个问题，都有进展。</h1><p>集中查看现场上报，跟进设备问题的处理状态。</p></div><button className="refresh" disabled={busy} onClick={refresh}><span aria-hidden>↻</span> {loading ? '刷新中…' : '刷新列表'}</button></section>
      <section className="stats" aria-label="问题统计">{[{label:'全部问题',value:issues.length,kind:'total'},...statuses.map(s=>({label:statusLabels[s],value:issues.filter(i=>i.status===s).length,kind:s}))].map((s,index)=><div className={`stat ${s.kind}`} key={s.kind}><span>{s.label}</span><strong>{loaded ? String(s.value).padStart(2,'0') : '—'}</strong><span className="stat-index">0{index+1}</span></div>)}</section>
      {error && <div role="alert" className="alert error"><div><strong>操作未完成</strong><p>{error}{loaded ? ' 当前保留上次成功获取的数据。' : ''}</p></div><button disabled={busy} onClick={refresh}>重试加载</button></div>}
      {notice && <div role="status" className="alert success">{notice}</div>}
      <section className="list-section"><div className="list-heading"><div><h2>现场问题</h2><span>{loaded ? `${shown.length} / ${issues.length} 条记录` : '等待获取数据'}</span></div><div className="filters" aria-label="按状态筛选">{(['all',...statuses] as const).map(s=><button key={s} aria-pressed={filter===s} className={filter===s?'selected':''} onClick={()=>setFilter(s)}>{s==='all'?'全部':statusLabels[s]}</button>)}</div></div>
        <div className="search-toolbar">
          <label className="search-field"><span>搜索问题</span><input type="search" placeholder="搜索标题、描述或问题 ID" value={query} onChange={e=>setQuery(e.target.value)}/></label>
          <label><span>优先级</span><select value={priority} onChange={e=>setPriority(e.target.value as Priority | 'all')}>
            <option value="all">全部优先级</option><option value="high">高优先级</option><option value="medium">中优先级</option><option value="low">低优先级</option>
          </select></label>
          <label><span>排序</span><select value={sort} onChange={e=>setSort(e.target.value)}>
            <option value="newest">最新上报</option><option value="oldest">最早上报</option><option value="priority">优先级从高到低</option>
          </select></label>
          <button className="neutral" onClick={resetFilters} disabled={!filtered && sort==='newest'}>重置</button>
        </div>
        {loading && !loaded ? <div className="empty" role="status"><span className="empty-symbol">◌</span><h3>正在获取现场问题</h3><p>连接巡检服务中，请稍候。</p></div>
        : !loaded && error ? <div className="empty"><span className="empty-symbol">!</span><h3>暂时无法获取问题</h3><p>服务恢复后，点击“重试加载”继续。</p></div>
        : shown.length===0 ? <div className="empty"><span className="empty-symbol">{filtered ? '⌕' : '✓'}</span><h3>{filtered ? '没有符合条件的问题' : '暂无上报问题'}</h3><p>{filtered ? '调整搜索词、状态或优先级，或重置筛选条件。' : '在 Unity 场景中点击设备并提交问题后，刷新即可查看。'}</p>{filtered && <button className="neutral empty-reset" onClick={resetFilters}>重置筛选</button>}</div>
        : <div className="issue-list" aria-busy={loading}>{shown.map(i=><IssueRow issue={i} key={i.id} busy={busy} saving={saving===i.id} reading={reading===i.id} onSave={save} onDetails={()=>void openDetails(i.id)} onDelete={()=>askDelete(i)}/>)}</div>}
      </section><footer>坐标采用 Unity 世界坐标 · 状态以服务端保存结果为准</footer>
    </main>
    {details && <IssueDetails issue={details} onClose={()=>setDetails(null)} onDelete={()=>askDelete(details)}/>}
    {pendingDelete && <DeleteConfirmation issue={pendingDelete} busy={deleting} error={deleteError} onCancel={()=>setPendingDelete(null)} onConfirm={()=>void confirmDelete()}/>}
  </div>;
}
