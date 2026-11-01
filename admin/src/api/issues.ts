import { isIssue, type Issue, type Status } from '../types/issue';

const baseURL = (import.meta.env.VITE_API_BASE_URL || 'http://localhost:8080').replace(/\/+$/, '');

async function request(path: string, options: RequestInit = {}): Promise<unknown> {
  const controller = new AbortController();
  const timer = window.setTimeout(() => controller.abort(), 10000);
  try {
    const response = await fetch(`${baseURL}${path}`, { ...options, signal: controller.signal });
    const body = await response.json().catch(() => null);
    if (!response.ok) throw new Error(body?.error?.message || `请求失败（HTTP ${response.status}）`);
    if (!body || !('data' in body)) throw new Error('服务返回了无法识别的数据，请检查后端版本。');
    return body.data;
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw new Error('请求超时，请确认后端已启动后重试。');
    if (error instanceof TypeError) throw new Error('无法连接巡检服务，请确认后端已启动、地址及跨域配置正确。');
    throw error;
  } finally { window.clearTimeout(timer); }
}

export async function getIssues(): Promise<Issue[]> {
  const data = await request('/api/issues');
  if (!Array.isArray(data) || !data.every(isIssue)) throw new Error('问题列表格式不正确，请检查后端版本。');
  return data;
}

export async function updateStatus(id: string, status: Status): Promise<Issue> {
  const data = await request(`/api/issues/${encodeURIComponent(id)}/status`, {
    method: 'PATCH', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ status }),
  });
  if (!isIssue(data)) throw new Error('状态更新响应格式不正确，请刷新列表确认。');
  return data;
}
