import { useQuery } from '@tanstack/react-query';
import { listPipelines } from '../api/pipelines';
import { POLL_INTERVALS } from '../lib/constants';

export function usePipelines(filters?: { repository?: string; status?: string }) {
  return useQuery({
    queryKey: ['pipelines', filters],
    queryFn: () => listPipelines(filters),
    refetchInterval: POLL_INTERVALS.pipelineList,
  });
}
