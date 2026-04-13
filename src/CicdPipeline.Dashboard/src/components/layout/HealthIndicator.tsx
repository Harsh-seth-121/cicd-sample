import { useHealth } from '../../hooks/useHealth';
import { cn } from '../../lib/cn';

export function HealthIndicator() {
  const { apiHealthy, temporalHealthy, isLoading } = useHealth();

  const allHealthy = apiHealthy && temporalHealthy;
  const partial = apiHealthy || temporalHealthy;

  return (
    <div className="flex items-center gap-2 text-sm">
      <span
        className={cn(
          'inline-block h-2.5 w-2.5 rounded-full',
          isLoading && 'bg-gray-400',
          !isLoading && allHealthy && 'bg-green-500',
          !isLoading && !allHealthy && partial && 'bg-yellow-500',
          !isLoading && !allHealthy && !partial && 'bg-red-500',
        )}
      />
      <span className="text-gray-500">
        {isLoading
          ? 'Checking...'
          : allHealthy
            ? 'All systems healthy'
            : partial
              ? 'Degraded'
              : 'Unhealthy'}
      </span>
    </div>
  );
}
