import type { PipelineListItem } from '../../api/types';
import { PipelineRow } from './PipelineRow';

export function PipelineList({ pipelines, isLoading }: { pipelines: PipelineListItem[]; isLoading: boolean }) {
  if (isLoading) {
    return <div className="py-12 text-center text-gray-500">Loading pipelines...</div>;
  }

  if (pipelines.length === 0) {
    return (
      <div className="py-12 text-center text-gray-500">
        No pipelines found. Trigger one to get started.
      </div>
    );
  }

  return (
    <div className="overflow-hidden rounded-lg border border-gray-200 bg-white shadow-sm">
      <table className="min-w-full">
        <thead>
          <tr className="border-b border-gray-200 bg-gray-50">
            <th className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500">Workflow ID</th>
            <th className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500">Status</th>
            <th className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500">Started</th>
            <th className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500">Closed</th>
          </tr>
        </thead>
        <tbody>
          {pipelines.map((p) => (
            <PipelineRow key={p.id} pipeline={p} />
          ))}
        </tbody>
      </table>
    </div>
  );
}
