import {
  ApiDetail,
  ApiError,
  ApiSummary,
  AskResult,
  CreateRestApiRequest,
  CreateRestApiResponse,
  RefreshResult,
  SchemaDiff,
  SchemaVersion,
} from '../types';
import { PROXY_RATE_LIMIT_MS, parseApiError, sleep } from '../utils';

const base = import.meta.env.VITE_API_BASE ?? '';

const TIMEOUT = {
  default: 15_000,
  create: 30_000,
  refresh: 30_000,
  ask: 120_000,
} as const;

async function fetchJson<T>(path: string, init?: RequestInit, timeoutMs: number = TIMEOUT.default): Promise<T> {
  const ctrl = new AbortController();
  const timer = setTimeout(() => ctrl.abort(), timeoutMs);
  try {
    const res = await fetch(`${base}${path}`, {
      ...init,
      signal: ctrl.signal,
      headers: {
        Accept: 'application/json',
        ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
        ...init?.headers,
      },
    });
    if (!res.ok) throw new ApiError(res.status, await parseApiError(res));
    if (res.status === 204) return undefined as T;
    return res.json() as Promise<T>;
  } catch (err) {
    if (err instanceof DOMException && err.name === 'AbortError') {
      throw new ApiError(504, 'Timeout.');
    }
    throw err;
  } finally {
    clearTimeout(timer);
  }
}

async function fetchText(
  path: string,
  init?: RequestInit,
  timeoutMs: number = TIMEOUT.default,
): Promise<{ status: number; body: string }> {
  const ctrl = new AbortController();
  const timer = setTimeout(() => ctrl.abort(), timeoutMs);
  try {
    const res = await fetch(`${base}${path}`, { ...init, signal: ctrl.signal });
    return { status: res.status, body: await res.text() };
  } catch (err) {
    if (err instanceof DOMException && err.name === 'AbortError') {
      throw new ApiError(504, 'Timeout.');
    }
    throw err;
  } finally {
    clearTimeout(timer);
  }
}

/** 429 de la /proxy: fără corp, fără Retry-After → backoff fix 10s. */
async function fetchProxy(path: string, init?: RequestInit): Promise<{ status: number; body: string }> {
  let res = await fetchText(path, init);
  if (res.status === 429) {
    await sleep(PROXY_RATE_LIMIT_MS);
    res = await fetchText(path, init);
  }
  return res;
}

export const api = {
  listApis: () => fetchJson<ApiSummary[]>('/apis'),

  getApi: (id: number) => fetchJson<ApiDetail>(`/apis/${id}`),

  /** 201 — răspunsul nu conține schema; folosește getApi() după. */
  createRestApi: (body: CreateRestApiRequest) =>
    fetchJson<CreateRestApiResponse>(
      '/apis/rest',
      { method: 'POST', body: JSON.stringify(body) },
      TIMEOUT.create,
    ),

  deleteApi: (id: number) => fetchJson<void>(`/apis/${id}`, { method: 'DELETE' }),

  refreshSchema: (id: number) =>
    fetchJson<RefreshResult>(`/apis/${id}/refresh`, { method: 'POST' }, TIMEOUT.refresh),

  /** 404 dacă API-ul nu există. */
  getVersions: (id: number) => fetchJson<SchemaVersion[]>(`/apis/${id}/versions`),

  getDiff: (id: number, from: number, to: number) =>
    fetchJson<SchemaDiff>(`/apis/${id}/diff?from=${from}&to=${to}`),

  ask: (id: number, question: string) =>
    fetchJson<AskResult>(
      `/apis/${id}/ask`,
      { method: 'POST', body: JSON.stringify({ question }) },
      TIMEOUT.ask,
    ),

  /** Trimite orice metodă către /proxy/{name}/{path}; body/headers opționale pentru non-GET. */
  proxyRequest: (
    name: string,
    path: string,
    options?: { method?: string; body?: string; headers?: Record<string, string> },
  ) => {
    const p = path.startsWith('/') ? path : `/${path}`;
    const method = options?.method ?? 'GET';
    const init: RequestInit = { method };
    if (options?.body && method !== 'GET' && method !== 'HEAD') {
      init.body = options.body;
    }
    if (options?.headers && Object.keys(options.headers).length > 0) {
      init.headers = options.headers;
    }
    return fetchProxy(`/proxy/${name.toLowerCase()}${p}`, init);
  },
};
