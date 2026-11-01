import { useEffect, useRef, useState } from 'react';
import { getIssues, updateStatus } from './api/issues';
import { priorityLabels, statusLabels, type Issue, type Status } from './types/issue';

const statuses: Status[] = ['open', 'in_progress', 'resolved'];
const formatTime = (value: string) => new Date(value).toLocaleString('zh-CN', { hour12: false });
const errorMessage = (value: unknown) => value instanceof Error ? value.message : '操作失败，请稍后重试。';

function IssueRow({ issue, onSave, busy }: { issue: Issue; onSave: (id: string, status: Status) => Promise<boolean>; busy: boolean }) {
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
        <button className="save" disabled={busy || status === issue.status} onClick={save}>{busy ? '保存中…' : '保存'}</button>
      </div>
    </div>
    <details><summary>查看记录信息</summary><dl><dt>问题 ID</dt><dd>{issue.id}</dd><dt>最后更新</dt><dd>{formatTime(issue.updatedAt)}</dd><dt>精确坐标</dt><dd>x={issue.position.x}, y={issue.position.y}, z={issue.position.z}</dd></dl></details>
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

  const shown = filter === 'all' ? issues : issues.filter(i => i.status === filter);
  const busy = loading || saving !== null;
  return <div className="app-shell">
    <aside className="sidebar"><div className="brand"><span className="brand-mark">巡</span><div>INSPECTION<span>虚拟巡检现场</span></div></div><div className="nav-label">工作空间</div><div className="nav-active"><span>▦</span> 问题管理 <span className="nav-count">{loaded ? issues.length : '—'}</span></div><div className="sidebar-note"><span className="small-label">现场到处理，全程记录</span><p>在 Unity 中发现问题，<br/>在这里跟进处理进度。</p><span className="version">LOCAL WORKSPACE · 01</span></div></aside>
    <main>
      <header><div className="breadcrumb">巡检工作台 <span>/</span> 问题管理</div><span className="workspace-tag">设备间 01</span></header>
      <section className="page-title"><div><p className="eyebrow">ISSUE MANAGEMENT</p><h1>每个问题，都有进展。</h1><p>集中查看现场上报，跟进设备问题的处理状态。</p></div><button className="refresh" disabled={busy} onClick={refresh}><span aria-hidden>↻</span> {loading ? '刷新中…' : '刷新列表'}</button></section>
      <section className="stats" aria-label="问题统计">{[{label:'全部问题',value:issues.length,kind:'total'},...statuses.map(s=>({label:statusLabels[s],value:issues.filter(i=>i.status===s).length,kind:s}))].map((s,index)=><div className={`stat ${s.kind}`} key={s.kind}><span>{s.label}</span><strong>{loaded ? String(s.value).padStart(2,'0') : '—'}</strong><span className="stat-index">0{index+1}</span></div>)}</section>
      {error && <div role="alert" className="alert error"><div><strong>操作未完成</strong><p>{error}{loaded ? ' 当前保留上次成功获取的数据。' : ''}</p></div><button disabled={busy} onClick={refresh}>重试加载</button></div>}
      {notice && <div role="status" className="alert success">{notice}</div>}
      <section className="list-section"><div className="list-heading"><div><h2>现场问题</h2><span>{loaded ? `${shown.length} 条记录` : '等待获取数据'}</span></div><div className="filters" aria-label="按状态筛选">{(['all',...statuses] as const).map(s=><button key={s} aria-pressed={filter===s} className={filter===s?'selected':''} onClick={()=>setFilter(s)}>{s==='all'?'全部':statusLabels[s]}</button>)}</div></div>
        {loading && !loaded ? <div className="empty" role="status"><span className="empty-symbol">◌</span><h3>正在获取现场问题</h3><p>连接巡检服务中，请稍候。</p></div>
        : !loaded && error ? <div className="empty"><span className="empty-symbol">!</span><h3>暂时无法获取问题</h3><p>服务恢复后，点击“重试加载”继续。</p></div>
        : shown.length===0 ? <div className="empty"><span className="empty-symbol">✓</span><h3>{filter==='all'?'暂无上报问题':'该状态下暂无问题'}</h3><p>{filter==='all'?'在 Unity 场景中点击设备并提交问题后，刷新即可查看。':'切换其他状态，查看现场处理进展。'}</p></div>
        : <div className="issue-list" aria-busy={loading}>{shown.map(i=><IssueRow issue={i} key={i.id} busy={busy} onSave={save}/>)}</div>}
      </section><footer>坐标采用 Unity 世界坐标 · 状态以服务端保存结果为准</footer>
    </main>
  </div>;
}
