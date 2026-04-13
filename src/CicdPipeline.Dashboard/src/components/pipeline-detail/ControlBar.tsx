import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { pausePipeline, cancelPipeline, resumePipeline } from '../../api/pipelines';
import type { PipelineStageStatus } from '../../api/types';
import { TERMINAL_STATUSES } from '../../lib/constants';
import { cn } from '../../lib/cn';

interface Props {
  pipelineStatus: PipelineStageStatus;
}

export function ControlBar({ pipelineStatus }: Props) {
  const queryClient = useQueryClient();
  const { pipelineId, status, isPaused, isCancelled } = pipelineStatus;
  const isTerminal = TERMINAL_STATUSES.includes(status) && status !== 'Paused';

  const [reason, setReason] = useState('');
  const [operatorId, setOperatorId] = useState('');
  const [showResumeForm, setShowResumeForm] = useState(false);

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ['pipeline-status', pipelineId] });

  const pauseMut = useMutation({
    mutationFn: () => pausePipeline(pipelineId, reason || 'Paused from dashboard'),
    onSuccess: () => { invalidate(); setReason(''); },
  });

  const cancelMut = useMutation({
    mutationFn: () => cancelPipeline(pipelineId, reason || 'Cancelled from dashboard'),
    onSuccess: () => { invalidate(); setReason(''); },
  });

  const resumeMut = useMutation({
    mutationFn: () => resumePipeline(pipelineId, operatorId, reason),
    onSuccess: () => { invalidate(); setShowResumeForm(false); setReason(''); setOperatorId(''); },
  });

  if (isTerminal || isCancelled) return null;

  const btnBase = 'rounded-md px-3 py-1.5 text-sm font-medium shadow-sm disabled:opacity-50';

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-3">
        {!isPaused && (
          <>
            <button
              onClick={() => pauseMut.mutate()}
              disabled={pauseMut.isPending}
              className={cn(btnBase, 'bg-yellow-500 text-white hover:bg-yellow-600')}
            >
              {pauseMut.isPending ? 'Pausing...' : 'Pause'}
            </button>
            <button
              onClick={() => cancelMut.mutate()}
              disabled={cancelMut.isPending}
              className={cn(btnBase, 'bg-red-500 text-white hover:bg-red-600')}
            >
              {cancelMut.isPending ? 'Cancelling...' : 'Cancel'}
            </button>
          </>
        )}
        {isPaused && (
          <button
            onClick={() => setShowResumeForm(true)}
            className={cn(btnBase, 'bg-green-600 text-white hover:bg-green-700')}
          >
            Resume
          </button>
        )}
        <input
          type="text"
          placeholder="Reason (optional)"
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          className="rounded-md border border-gray-300 px-3 py-1.5 text-sm"
        />
      </div>

      {showResumeForm && (
        <div className="flex items-center gap-3 rounded-md border border-gray-200 bg-gray-50 p-3">
          <input
            type="text"
            placeholder="Operator ID"
            value={operatorId}
            onChange={(e) => setOperatorId(e.target.value)}
            className="rounded-md border border-gray-300 px-3 py-1.5 text-sm"
          />
          <button
            onClick={() => resumeMut.mutate()}
            disabled={!operatorId || resumeMut.isPending}
            className={cn(btnBase, 'bg-green-600 text-white hover:bg-green-700')}
          >
            {resumeMut.isPending ? 'Resuming...' : 'Confirm Resume'}
          </button>
          <button
            onClick={() => setShowResumeForm(false)}
            className={cn(btnBase, 'bg-gray-200 text-gray-700 hover:bg-gray-300')}
          >
            Cancel
          </button>
        </div>
      )}

      {(pauseMut.isError || cancelMut.isError || resumeMut.isError) && (
        <p className="text-sm text-red-600">
          {(pauseMut.error ?? cancelMut.error ?? resumeMut.error)?.message}
        </p>
      )}
    </div>
  );
}
