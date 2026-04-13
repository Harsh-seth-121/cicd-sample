import type { PipelineStatus } from '../../api/types';

const STATUS_OPTIONS: PipelineStatus[] = [
  'Received', 'Validating', 'Building', 'Testing', 'Scanning',
  'Versioning', 'Publishing', 'DeployingDev', 'VerifyingDev',
  'DeployingQa', 'VerifyingQa', 'Succeeded', 'Failed', 'Cancelled', 'Paused',
];

interface Props {
  repository: string;
  status: string;
  onRepositoryChange: (value: string) => void;
  onStatusChange: (value: string) => void;
}

export function PipelineFilters({ repository, status, onRepositoryChange, onStatusChange }: Props) {
  return (
    <div className="flex items-center gap-4">
      <input
        type="text"
        placeholder="Filter by repository..."
        value={repository}
        onChange={(e) => onRepositoryChange(e.target.value)}
        className="rounded-md border border-gray-300 px-3 py-1.5 text-sm shadow-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
      />
      <select
        value={status}
        onChange={(e) => onStatusChange(e.target.value)}
        className="rounded-md border border-gray-300 px-3 py-1.5 text-sm shadow-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
      >
        <option value="">All statuses</option>
        {STATUS_OPTIONS.map((s) => (
          <option key={s} value={s}>{s}</option>
        ))}
      </select>
    </div>
  );
}
