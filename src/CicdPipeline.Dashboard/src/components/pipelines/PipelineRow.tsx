import { Link } from 'react-router';
import type { PipelineListItem, PipelineStatus } from '../../api/types';
import { StatusBadge } from './StatusBadge';

function formatTime(iso: string | null): string {
  if (!iso) return '-';
  return new Date(iso).toLocaleString();
}

export function PipelineRow({ pipeline }: { pipeline: PipelineListItem }) {
  return (
    <tr className="border-b border-gray-100 hover:bg-gray-50">
      <td className="px-4 py-3 text-sm">
        <Link
          to={`/pipelines/${encodeURIComponent(pipeline.id)}`}
          className="font-medium text-blue-600 hover:underline"
        >
          {pipeline.id}
        </Link>
      </td>
      <td className="px-4 py-3 text-sm">
        <StatusBadge status={pipeline.status as PipelineStatus} />
      </td>
      <td className="px-4 py-3 text-sm text-gray-600">{formatTime(pipeline.startTime)}</td>
      <td className="px-4 py-3 text-sm text-gray-600">{formatTime(pipeline.closeTime)}</td>
    </tr>
  );
}
