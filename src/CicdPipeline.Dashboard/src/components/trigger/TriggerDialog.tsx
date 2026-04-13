import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { useNavigate } from 'react-router';
import { triggerPipeline } from '../../api/webhooks';
import { cn } from '../../lib/cn';

export function TriggerDialog({ onClose }: { onClose: () => void }) {
  const navigate = useNavigate();
  const [repository, setRepository] = useState('');
  const [commitSha, setCommitSha] = useState('');
  const [ref, setRef] = useState('refs/heads/');
  const [eventType, setEventType] = useState('push');

  const mutation = useMutation({
    mutationFn: () =>
      triggerPipeline({ repository, commitSha, ref, eventType }),
    onSuccess: (data) => {
      onClose();
      navigate(`/pipelines/${encodeURIComponent(data.workflowId)}`);
    },
  });

  const canSubmit = repository && commitSha && ref && eventType;
  const btnBase = 'rounded-md px-4 py-2 text-sm font-medium shadow-sm';

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="w-full max-w-md rounded-lg bg-white p-6 shadow-xl">
        <h2 className="mb-4 text-lg font-semibold text-gray-900">Trigger Pipeline</h2>

        <div className="space-y-3">
          <div>
            <label className="block text-sm font-medium text-gray-700">Repository</label>
            <input
              type="text"
              placeholder="owner/repo"
              value={repository}
              onChange={(e) => setRepository(e.target.value)}
              className="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 text-sm"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700">Commit SHA</label>
            <input
              type="text"
              placeholder="abc1234567890"
              value={commitSha}
              onChange={(e) => setCommitSha(e.target.value)}
              className="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 text-sm"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700">Ref</label>
            <input
              type="text"
              placeholder="refs/heads/main"
              value={ref}
              onChange={(e) => setRef(e.target.value)}
              className="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 text-sm"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700">Event Type</label>
            <select
              value={eventType}
              onChange={(e) => setEventType(e.target.value)}
              className="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 text-sm"
            >
              <option value="push">push</option>
              <option value="pull_request">pull_request</option>
              <option value="tag">tag</option>
            </select>
          </div>
        </div>

        {mutation.isError && (
          <p className="mt-3 text-sm text-red-600">{mutation.error.message}</p>
        )}

        <div className="mt-6 flex justify-end gap-3">
          <button
            onClick={onClose}
            className={cn(btnBase, 'bg-gray-100 text-gray-700 hover:bg-gray-200')}
          >
            Cancel
          </button>
          <button
            onClick={() => mutation.mutate()}
            disabled={!canSubmit || mutation.isPending}
            className={cn(btnBase, 'bg-blue-600 text-white hover:bg-blue-700 disabled:opacity-50')}
          >
            {mutation.isPending ? 'Triggering...' : 'Trigger'}
          </button>
        </div>
      </div>
    </div>
  );
}
