import type { PipelineStageStatus } from '../../api/types';
import { StatusBadge } from '../pipelines/StatusBadge';
import { StageProgressBar } from './StageProgressBar';
import { FailurePanel } from './FailurePanel';
import { ControlBar } from './ControlBar';

export function PipelineDetail({ data }: { data: PipelineStageStatus }) {
  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <h2 className="text-lg font-semibold text-gray-900">{data.pipelineId}</h2>
        <StatusBadge status={data.status} />
        {data.isPaused && (
          <span className="text-sm text-yellow-600 font-medium">PAUSED</span>
        )}
      </div>

      <div className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
        <h3 className="mb-4 text-sm font-medium text-gray-500 uppercase tracking-wider">
          Stage Progression
        </h3>
        <StageProgressBar status={data.status} currentStage={data.currentStage} />
      </div>

      <div className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
        <h3 className="mb-4 text-sm font-medium text-gray-500 uppercase tracking-wider">
          Operator Controls
        </h3>
        <ControlBar pipelineStatus={data} />
      </div>

      <FailurePanel failures={data.failures} />

      <div className="text-xs text-gray-400">
        Last updated: {new Date(data.lastUpdated).toLocaleString()}
      </div>
    </div>
  );
}
