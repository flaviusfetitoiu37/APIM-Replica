import { ApiError } from './types';

/** Citește erori application/problem+json sau text/plain (contract API). */
export async function parseApiError(response: Response): Promise<string> {
  const ct = response.headers.get('content-type') ?? '';

  if (ct.includes('application/problem+json')) {
    try {
      const p = (await response.json()) as { detail?: string; title?: string; errors?: Record<string, string[]> };
      if (p.errors) return Object.values(p.errors).flat().join(' ');
      return p.detail ?? p.title ?? `HTTP ${response.status}`;
    } catch {
      return `HTTP ${response.status}`;
    }
  }

  try {
    const text = (await response.text()).trim();
    if (text) return text;
  } catch {
    // corp lipsă (ex. 429 la /proxy)
  }

  if (response.status === 429) return 'Too many requests. Try again in 10 seconds.';
  return `HTTP ${response.status}`;
}

export function getErrorMessage(err: unknown, fallback: string): string {
  return err instanceof ApiError ? err.message : fallback;
}

export function formatResponseBody(body: string): string {
  try {
    return JSON.stringify(JSON.parse(body), null, 2);
  } catch {
    return body;
  }
}

export function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export const PROXY_RATE_LIMIT_MS = 10_000;
