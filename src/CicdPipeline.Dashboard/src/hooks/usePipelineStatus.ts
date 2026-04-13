import { useQuery } from '@tanstack/react-query';
import { getPipelineStatus } from '../api/pipelines';
import { POLL_INTERVALS } from '../lib/constants';

export function usePipelineStatus(workflowId: string) {
  return useQuery({
    queryKey: ['pipeline-status', workflowId],
    queryFn: () => getPipelineStatus(workflowId),
    refetchInterval: POLL_INTERVALS.pipelineDetail,
    enabled: !!workflowId,
  });
}
