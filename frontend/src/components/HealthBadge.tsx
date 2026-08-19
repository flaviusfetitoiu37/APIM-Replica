import { HealthStatus } from '../types';

const LABELS: Record<HealthStatus, string> = {
  healthy: 'Healthy',
  unhealthy: 'Unhealthy',
  down: 'Down',
  unknown: 'Unknown',
};

export function HealthBadge({ status }: { status: HealthStatus }) {
  return <span className={`badge badge-${status}`}>{LABELS[status]}</span>;
}
