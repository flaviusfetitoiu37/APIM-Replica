import { FormEvent, useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { api } from '../api/client';
import { HealthBadge } from '../components/HealthBadge';
import {
  ApiDetail,
  OpenApiEndpoint,
  SchemaDiff,
  SchemaVersion,
  formatDate,
  parseEndpoints,
} from '../types';
import { formatResponseBody, getErrorMessage } from '../utils';

export function ApiDetailPage() {
  const apiId = Number(useParams().id);
  const navigate = useNavigate();

  const [detail, setDetail] = useState<ApiDetail | null>(null);
  const [versions, setVersions] = useState<SchemaVersion[]>([]);
  const [endpoints, setEndpoints] = useState<OpenApiEndpoint[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [info, setInfo] = useState('');

  const [busy, setBusy] = useState<'refresh' | 'delete' | 'diff' | 'ask' | 'proxy' | null>(null);
  const [diffFrom, setDiffFrom] = useState('1');
  const [diffTo, setDiffTo] = useState('1');
  const [diff, setDiff] = useState<SchemaDiff | null>(null);
  const [question, setQuestion] = useState('');
  const [answer, setAnswer] = useState('');
  const [proxyPath, setProxyPath] = useState('/pet/1');
  const [proxyMethod, setProxyMethod] = useState<'GET' | 'POST' | 'PUT' | 'DELETE'>('GET');
  const [proxyBody, setProxyBody] = useState('');
  const [proxyHeaders, setProxyHeaders] = useState<{ key: string; value: string }[]>([]);
  const [proxyResult, setProxyResult] = useState<{ status: number; body: string } | null>(null);

  async function load() {
    if (!Number.isFinite(apiId)) {
      setError('Invalid ID.');
      setLoading(false);
      return;
    }
    setLoading(true);
    setError('');
    try {
      const [d, v] = await Promise.all([api.getApi(apiId), api.getVersions(apiId)]);
      setDetail(d);
      setVersions(v);
      setEndpoints(parseEndpoints(d.schema));
      if (v.length >= 2) {
        setDiffFrom(String(v[1].versionNumber));
        setDiffTo(String(v[0].versionNumber));
      }
    } catch (err) {
      setError(getErrorMessage(err, 'API not found.'));
      setDetail(null);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    load();
  }, [apiId]);

  async function run<T>(action: typeof busy, fn: () => Promise<T>, onOk?: (r: T) => void) {
    setBusy(action);
    setError('');
    try {
      const result = await fn();
      onOk?.(result);
    } catch (err) {
      setError(getErrorMessage(err, 'Something went wrong.'));
    } finally {
      setBusy(null);
    }
  }

  if (loading) return <p className="muted">Loading...</p>;
  if (!detail) {
    return (
      <>
        <div className="alert alert-error">{error}</div>
        <Link to="/">← Back to catalog</Link>
      </>
    );
  }

  const proxyBase = `/proxy/${detail.name.toLowerCase()}`;

  return (
    <>
      <p className="breadcrumb">
        <Link to="/">APIs</Link> / {detail.name}
      </p>
      <header className="page-header">
        <h1>{detail.name}</h1>
      </header>

      {error && <div className="alert alert-error">{error}</div>}
      {info && <div className="alert alert-success">{info}</div>}

      <div className="toolbar">
        <button
          type="button"
          className="btn btn-secondary"
          disabled={busy !== null}
          onClick={() =>
            run('refresh', () => api.refreshSchema(apiId), (r) => {
              setInfo(r.message);
              load();
            })
          }
        >
          {busy === 'refresh' ? '...' : 'Refresh schema'}
        </button>
        <button
          type="button"
          className="btn btn-danger"
          disabled={busy !== null}
          onClick={() => {
            if (!confirm(`Delete "${detail.name}"?`)) return;
            run('delete', () => api.deleteApi(apiId), () => navigate('/'));
          }}
        >
          {busy === 'delete' ? '...' : 'Delete'}
        </button>
      </div>

      <section className="panel">
        <h2>Overview</h2>
        <dl className="meta-grid">
          <div>
            <dt>Type</dt>
            <dd>{detail.type.toUpperCase()}</dd>
          </div>
          <div>
            <dt>Health</dt>
            <dd>
              <HealthBadge status={detail.healthStatus} />
            </dd>
          </div>
          <div>
            <dt>Base URL</dt>
            <dd>{detail.baseUrl}</dd>
          </div>
          <div>
            <dt>Schema URL</dt>
            <dd>{detail.schemaUrl ?? '—'}</dd>
          </div>
          <div>
            <dt>Health check</dt>
            <dd>{detail.healthCheckUrl ?? detail.baseUrl}</dd>
          </div>
          <div>
            <dt>Latency</dt>
            <dd>{detail.lastLatencyMs != null ? `${detail.lastLatencyMs} ms` : '—'}</dd>
          </div>
          <div>
            <dt>Last checked</dt>
            <dd>{formatDate(detail.lastCheckedAt)}</dd>
          </div>
          <div>
            <dt>Proxy</dt>
            <dd>
              {proxyBase}/{'{path}'}
            </dd>
          </div>
        </dl>
      </section>

      <section className="panel">
        <h2>Endpoints ({endpoints.length})</h2>
        {endpoints.length === 0 ? (
          <p className="muted">None.</p>
        ) : (
          <table className="data-table">
            <thead>
              <tr>
                <th>Method</th>
                <th>Path</th>
                <th>Summary</th>
              </tr>
            </thead>
            <tbody>
              {endpoints.map((ep) => (
                <tr key={`${ep.method}${ep.path}`}>
                  <td>
                    <span className={`method-tag method-${ep.method.toLowerCase()}`}>{ep.method}</span>
                  </td>
                  <td>{ep.path}</td>
                  <td>{ep.summary ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      <section className="panel">
        <h2>Playground</h2>
        <p className="hint">Via proxy · max 5 req / 10s · auto 10s wait on 429</p>
        <div className="playground-row">
          <select value={proxyMethod} onChange={(e) => setProxyMethod(e.target.value as typeof proxyMethod)}>
            <option value="GET">GET</option>
            <option value="POST">POST</option>
            <option value="PUT">PUT</option>
            <option value="DELETE">DELETE</option>
          </select>
          <code className="proxy-prefix">{proxyBase}</code>
          <input value={proxyPath} onChange={(e) => setProxyPath(e.target.value)} />
          <button
            type="button"
            className="btn btn-primary"
            disabled={busy !== null}
            onClick={() => {
              if (proxyMethod !== 'GET' && proxyBody.trim()) {
                try {
                  JSON.parse(proxyBody);
                } catch {
                  setError('Body is not valid JSON.');
                  return;
                }
              }

              const headers: Record<string, string> = {};
              for (const h of proxyHeaders) {
                if (h.key.trim()) headers[h.key.trim()] = h.value;
              }
              if (
                proxyMethod !== 'GET' &&
                proxyBody.trim() &&
                !Object.keys(headers).some((k) => k.toLowerCase() === 'content-type')
              ) {
                headers['Content-Type'] = 'application/json';
              }

              run(
                'proxy',
                () =>
                  api.proxyRequest(detail.name, proxyPath, {
                    method: proxyMethod,
                    body: proxyMethod === 'GET' ? undefined : proxyBody.trim() || undefined,
                    headers,
                  }),
                (r) => {
                  if (r.status === 429) {
                    setError('Rate limit (429). Try again in 10 seconds.');
                    setProxyResult(null);
                    return;
                  }
                  setProxyResult({ status: r.status, body: formatResponseBody(r.body) });
                },
              );
            }}
          >
            {busy === 'proxy' ? '...' : 'Send'}
          </button>
        </div>

        <div className="playground-headers">
          <p className="hint">Headers</p>
          {proxyHeaders.map((h, i) => (
            <div key={i} className="playground-header-row">
              <input
                placeholder="Header name"
                value={h.key}
                onChange={(e) =>
                  setProxyHeaders((prev) => prev.map((row, idx) => (idx === i ? { ...row, key: e.target.value } : row)))
                }
              />
              <input
                placeholder="Value"
                value={h.value}
                onChange={(e) =>
                  setProxyHeaders((prev) =>
                    prev.map((row, idx) => (idx === i ? { ...row, value: e.target.value } : row)),
                  )
                }
              />
              <button
                type="button"
                className="btn btn-secondary"
                onClick={() => setProxyHeaders((prev) => prev.filter((_, idx) => idx !== i))}
              >
                Remove
              </button>
            </div>
          ))}
          <button
            type="button"
            className="btn btn-secondary"
            onClick={() => setProxyHeaders((prev) => [...prev, { key: '', value: '' }])}
          >
            Add header
          </button>
        </div>

        {proxyMethod !== 'GET' && (
          <div className="playground-body">
            <p className="hint">Body (JSON)</p>
            <textarea
              rows={6}
              placeholder='{"key": "value"}'
              value={proxyBody}
              onChange={(e) => setProxyBody(e.target.value)}
            />
          </div>
        )}

        {proxyResult && (
          <>
            <p className="hint">HTTP {proxyResult.status}</p>
            <pre className="response-box">{proxyResult.body || '(empty)'}</pre>
          </>
        )}
      </section>

      <section className="panel">
        <h2>Versions</h2>
        {versions.length === 0 ? (
          <p className="muted">No versions.</p>
        ) : (
          <table className="data-table">
            <thead>
              <tr>
                <th>#</th>
                <th>Date</th>
                <th>Bytes</th>
              </tr>
            </thead>
            <tbody>
              {versions.map((v) => (
                <tr key={v.versionNumber}>
                  <td>v{v.versionNumber}</td>
                  <td>{formatDate(v.fetchedAt)}</td>
                  <td>{v.sizeBytes.toLocaleString('en-US')}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}

        {versions.length >= 2 && (
          <form
            className="diff-form"
            onSubmit={(e: FormEvent) => {
              e.preventDefault();
              run('diff', () => api.getDiff(apiId, Number(diffFrom), Number(diffTo)), setDiff);
            }}
          >
            <label>
              From
              <input type="number" min={1} value={diffFrom} onChange={(e) => setDiffFrom(e.target.value)} />
            </label>
            <label>
              To
              <input type="number" min={1} value={diffTo} onChange={(e) => setDiffTo(e.target.value)} />
            </label>
            <button type="submit" className="btn btn-secondary" disabled={busy !== null}>
              {busy === 'diff' ? '...' : 'Diff'}
            </button>
          </form>
        )}

        {diff && (
          <div className="diff-result">
            <p>
              v{diff.from} → v{diff.to}, {diff.unchanged} unchanged
            </p>
            {diff.added.length > 0 && (
              <ul className="diff-added">
                {diff.added.map((x) => (
                  <li key={x}>+ {x}</li>
                ))}
              </ul>
            )}
            {diff.removed.length > 0 && (
              <ul className="diff-removed">
                {diff.removed.map((x) => (
                  <li key={x}>- {x}</li>
                ))}
              </ul>
            )}
            {diff.added.length === 0 && diff.removed.length === 0 && <p className="muted">No differences.</p>}
          </div>
        )}
      </section>

      <section className="panel">
        <h2>AI Assistant</h2>
        <p className="hint">First request may take up to 2 minutes.</p>
        <form
          onSubmit={(e) => {
            e.preventDefault();
            if (!question.trim()) return;
            run('ask', () => api.ask(apiId, question.trim()), (r) => setAnswer(r.answer));
          }}
        >
          <textarea value={question} onChange={(e) => setQuestion(e.target.value)} required />
          <button type="submit" className="btn btn-primary" disabled={busy !== null}>
            {busy === 'ask' ? '...' : 'Ask'}
          </button>
        </form>
        {answer && <pre className="answer-box">{answer}</pre>}
      </section>
    </>
  );
}
