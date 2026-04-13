import { useQuery } from '@tanstack/react-query';
import { checkHealth, checkTemporalHealth } from '../api/health';
import { POLL_INTERVALS } from '../lib/constants';

export function useHealth() {
  const api = useQuery({
    queryKey: ['health-api'],
    queryFn: checkHealth,
    refetchInterval: POLL_INTERVALS.health,
  });

  const temporal = useQuery({
    queryKey: ['health-temporal'],
    queryFn: checkTemporalHealth,
    refetchInterval: POLL_INTERVALS.health,
  });

  return {
    apiHealthy: api.data ?? false,
    temporalHealthy: temporal.data ?? false,
    isLoading: api.isLoading || temporal.isLoading,
  };
}
