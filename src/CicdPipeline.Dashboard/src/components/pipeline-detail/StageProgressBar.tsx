import type { PipelineStatus } from '../../api/types';
import { PIPELINE_STAGES, STAGE_LABELS, TERMINAL_STATUSES } from '../../lib/constants';
import { StageNode } from './StageNode';
import { cn } from '../../lib/cn';

interface Props {
  status: PipelineStatus;
  currentStage: string;
}

function getStageState(
  stageIndex: number,
  activeIndex: number,
  isFailed: boolean,
): 'completed' | 'active' | 'failed' | 'pending' {
  if (stageIndex < activeIndex) return 'completed';
  if (stageIndex === activeIndex) return isFailed ? 'failed' : 'active';
  return 'pending';
}

export function StageProgressBar({ status, currentStage }: Props) {
  const isFailed = status === 'Failed';
  const isTerminal = TERMINAL_STATUSES.includes(status);
  const isSucceeded = status === 'Succeeded';

  // Find the active stage index based on currentStage or status
  let activeIndex = PIPELINE_STAGES.indexOf(status as PipelineStatus);
  if (activeIndex === -1) {
    // Terminal status — find stage from currentStage string
    activeIndex = PIPELINE_STAGES.findIndex(
      (s) => s.toLowerCase() === currentStage?.toLowerCase(),
    );
  }
  if (activeIndex === -1) activeIndex = 0;

  // If succeeded, all stages are completed
  if (isSucceeded) activeIndex = PIPELINE_STAGES.length;

  return (
    <div className="overflow-x-auto">
      <div className="flex items-start gap-0 min-w-max">
        {PIPELINE_STAGES.map((stage, i) => (
          <div key={stage} className="flex items-start">
            <StageNode
              label={STAGE_LABELS[stage] ?? stage}
              state={
                isSucceeded
                  ? 'completed'
                  : getStageState(i, activeIndex, isFailed)
              }
            />
            {i < PIPELINE_STAGES.length - 1 && (
              <div className="mt-3.5 flex items-center">
                <div
                  className={cn(
                    'h-0.5 w-6',
                    i < activeIndex ? 'bg-green-400' : 'bg-gray-200',
                  )}
                />
              </div>
            )}
          </div>
        ))}
      </div>
      {isTerminal && (
        <div className="mt-3 text-sm font-medium text-gray-600">
          Final status: <span className="font-semibold">{status}</span>
        </div>
      )}
    </div>
  );
}
