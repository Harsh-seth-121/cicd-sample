import { useState } from 'react';
import { usePipelines } from '../hooks/usePipelines';
import { PipelineList } from '../components/pipelines/PipelineList';
import { PipelineFilters } from '../components/pipelines/PipelineFilters';
import { TriggerDialog } from '../components/trigger/TriggerDialog';

export function DashboardPage() {
  const [repository, setRepository] = useState('');
  const [status, setStatus] = useState('');
  const [showTrigger, setShowTrigger] = useState(false);

  const filters = {
    repository: repository || undefined,
    status: status || undefined,
  };
  const { data: pipelines = [], isLoading } = usePipelines(filters);

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-gray-900">Pipelines</h1>
        <button
          onClick={() => setShowTrigger(true)}
          className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-blue-700"
        >
          Trigger Pipeline
        </button>
      </div>

      <PipelineFilters
        repository={repository}
        status={status}
        onRepositoryChange={setRepository}
        onStatusChange={setStatus}
      />

      <PipelineList pipelines={pipelines} isLoading={isLoading} />

      {showTrigger && <TriggerDialog onClose={() => setShowTrigger(false)} />}
    </div>
  );
}
