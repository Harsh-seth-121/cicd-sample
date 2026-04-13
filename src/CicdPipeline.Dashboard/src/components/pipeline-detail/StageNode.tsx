import { cn } from '../../lib/cn';

type StageState = 'completed' | 'active' | 'failed' | 'pending';

interface Props {
  label: string;
  state: StageState;
}

export function StageNode({ label, state }: Props) {
  return (
    <div className="flex flex-col items-center gap-1.5">
      <div
        className={cn(
          'flex h-8 w-8 items-center justify-center rounded-full border-2 text-xs font-bold',
          state === 'completed' && 'border-green-500 bg-green-500 text-white',
          state === 'active' && 'border-blue-500 bg-blue-500 text-white animate-pulse',
          state === 'failed' && 'border-red-500 bg-red-500 text-white',
          state === 'pending' && 'border-gray-300 bg-white text-gray-400',
        )}
      >
        {state === 'completed' ? '\u2713' : state === 'failed' ? '\u2717' : '\u2022'}
      </div>
      <span
        className={cn(
          'text-[10px] font-medium leading-tight text-center max-w-[60px]',
          state === 'active' && 'text-blue-700',
          state === 'completed' && 'text-green-700',
          state === 'failed' && 'text-red-700',
          state === 'pending' && 'text-gray-400',
        )}
      >
        {label}
      </span>
    </div>
  );
}
