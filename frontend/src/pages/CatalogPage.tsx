import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/client';
import { HealthBadge } from '../components/HealthBadge';
import { formatDate } from '../types';
import { getErrorMessage } from '../utils';

export function CatalogPage() {
  const [apis, setApis] = useState<Awaited<ReturnType<typeof api.listApis>>>([]);
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    api
      .listApis()
      .then(setApis)
      .catch((err) => setError(getErrorMessage(err, 'Failed to load APIs.')))
      .finally(() => setLoading(false));
  }, []);

  const q = search.trim().toLowerCase();
  const filtered = q
    ? apis.filter((a) => [a.name, a.baseUrl, a.type].some((v) => v.toLowerCase().includes(q)))
    : apis;

  return (
    <>
      <header className="page-header">
        <h1>APIs</h1>
      </header>

      <div className="toolbar">
        <input
          className="search-input"
          placeholder="Search..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <Link to="/add" className="btn btn-primary">
          Add API
        </Link>
      </div>

      {error && <div className="alert alert-error">{error}</div>}

      <div className="panel">
        {loading && <p className="muted">Loading...</p>}
        {!loading && filtered.length === 0 && (
          <p className="empty-state">{search ? 'No results.' : 'No APIs registered.'}</p>
        )}
        {!loading && filtered.length > 0 && (
          <table className="data-table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Type</th>
                <th>Health</th>
                <th>Latency</th>
                <th>Last checked</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((a) => (
                <tr key={a.id}>
                  <td>
                    <Link to={`/apis/${a.id}`}>{a.name}</Link>
                  </td>
                  <td>{a.type.toUpperCase()}</td>
                  <td>
                    <HealthBadge status={a.healthStatus} />
                  </td>
                  <td>{a.lastLatencyMs != null ? `${a.lastLatencyMs} ms` : '—'}</td>
                  <td>{formatDate(a.lastCheckedAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </>
  );
}
