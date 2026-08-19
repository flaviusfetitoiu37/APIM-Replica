export type HealthStatus = 'healthy' | 'unhealthy' | 'down' | 'unknown';

export interface ApiSummary {
  id: number;
  name: string;
  type: string;
  baseUrl: string;
  healthStatus: HealthStatus;
  lastLatencyMs: number | null;
  lastCheckedAt: string | null;
  createdAt: string;
}

export interface ApiDetail extends ApiSummary {
  schemaUrl: string | null;
  healthCheckUrl: string | null;
  /** String JSON escapat din GET /apis/{id} — nu obiect. */
  schema: string | null;
}

/** Răspuns 201 POST /apis/rest — fără câmp schema. */
export interface CreateRestApiResponse {
  id: number;
  name: string;
  type: string;
  baseUrl: string;
  schemaUrl: string | null;
  healthCheckUrl: string | null;
  healthStatus: HealthStatus;
  createdAt: string;
}

export interface CreateRestApiRequest {
  name: string;
  baseUrl: string;
  schemaUrl: string;
  healthCheckUrl?: string;
}

export interface SchemaVersion {
  versionNumber: number;
  fetchedAt: string;
  sizeBytes: number;
}

export interface SchemaDiff {
  from: number;
  to: number;
  added: string[];
  removed: string[];
  unchanged: number;
}

export interface RefreshResult {
  message: string;
  version: number;
}

export interface AskResult {
  question: string;
  answer: string;
}

export interface OpenApiEndpoint {
  method: string;
  path: string;
  summary?: string;
}

export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

/** Parsează schema escapată (string JSON) din GET /apis/{id}. */
export function parseSchema(raw: string | null | undefined): Record<string, unknown> | null {
  if (raw == null || raw === '') return null;

  let value: unknown = raw;
  for (let i = 0; i < 2 && typeof value === 'string'; i++) {
    value = JSON.parse(value);
  }
  return typeof value === 'object' && value !== null ? (value as Record<string, unknown>) : null;
}

/** Aceleași 8 verbe pe care le acceptă backendul în diff (ApisController.OperationNames). */
const HTTP_OPERATIONS = new Set(['get', 'put', 'post', 'delete', 'options', 'head', 'patch', 'trace']);

export function parseEndpoints(schemaRaw: string | null | undefined): OpenApiEndpoint[] {
  const doc = parseSchema(schemaRaw);
  const paths = doc?.paths as Record<string, Record<string, { summary?: string }>> | undefined;
  if (!paths) return [];

  const list: OpenApiEndpoint[] = [];
  for (const [path, methods] of Object.entries(paths)) {
    for (const [method, info] of Object.entries(methods)) {
      if (HTTP_OPERATIONS.has(method.toLowerCase())) {
        list.push({ method: method.toUpperCase(), path, summary: info.summary });
      }
    }
  }
  return list.sort((a, b) => a.path.localeCompare(b.path));
}

/** Date ISO 8601 UTC din backend. */
export function formatDate(iso: string | null | undefined): string {
  if (!iso) return '—';
  return `${new Date(iso).toLocaleString('en-US', { timeZone: 'UTC' })} UTC`;
}
