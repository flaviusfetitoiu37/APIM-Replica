import { FormEvent, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import { getErrorMessage } from '../utils';

const PETSTORE = {
  name: 'Petstore',
  baseUrl: 'https://petstore3.swagger.io/api/v3',
  schemaUrl: 'https://petstore3.swagger.io/api/v3/openapi.json',
};

export function AddApiPage() {
  const navigate = useNavigate();
  const [form, setForm] = useState({ name: '', baseUrl: '', schemaUrl: '', healthCheckUrl: '' });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  function set(field: keyof typeof form, value: string) {
    setForm((prev) => ({ ...prev, [field]: value }));
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setLoading(true);
    setError('');
    try {
      const created = await api.createRestApi({
        name: form.name.trim(),
        baseUrl: form.baseUrl.trim(),
        schemaUrl: form.schemaUrl.trim(),
        healthCheckUrl: form.healthCheckUrl.trim() || undefined,
      });
      navigate(`/apis/${created.id}`);
    } catch (err) {
      setError(getErrorMessage(err, 'Registration failed.'));
    } finally {
      setLoading(false);
    }
  }

  return (
    <>
      <header className="page-header">
        <h1>Register REST API</h1>
      </header>

      {error && <div className="alert alert-error">{error}</div>}

      <div className="panel">
        <form className="form-grid" onSubmit={onSubmit}>
          <label className="form-field">
            Name
            <input value={form.name} onChange={(e) => set('name', e.target.value)} required />
          </label>
          <label className="form-field">
            Base URL
            <input type="url" value={form.baseUrl} onChange={(e) => set('baseUrl', e.target.value)} required />
          </label>
          <label className="form-field">
            OpenAPI URL
            <input type="url" value={form.schemaUrl} onChange={(e) => set('schemaUrl', e.target.value)} required />
          </label>
          <label className="form-field">
            Health check URL <span className="muted">(optional)</span>
            <input type="url" value={form.healthCheckUrl} onChange={(e) => set('healthCheckUrl', e.target.value)} />
          </label>
          <div className="form-actions">
            <button type="submit" className="btn btn-primary" disabled={loading}>
              {loading ? '...' : 'Save'}
            </button>
            <button type="button" className="btn btn-secondary" onClick={() => setForm({ ...PETSTORE, healthCheckUrl: '' })}>
              Petstore example
            </button>
          </div>
        </form>
      </div>
    </>
  );
}
