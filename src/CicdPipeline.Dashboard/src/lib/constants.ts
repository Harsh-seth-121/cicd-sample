import type { PipelineStatus } from '../api/types';

export const PIPELINE_STAGES: PipelineStatus[] = [
  'Received',
  'Validating',
  'Building',
  'Testing',
  'Scanning',
  'Versioning',
  'Publishing',
  'DeployingDev',
  'VerifyingDev',
  'DeployingQa',
  'VerifyingQa',
];

export const TERMINAL_STATUSES: PipelineStatus[] = [
  'Succeeded',
  'Failed',
  'Cancelled',
  'Paused',
  'Skipped',
];

export const STATUS_COLORS: Record<PipelineStatus, { bg: string; text: string }> = {
  Received: { bg: 'bg-gray-100', text: 'text-gray-700' },
  Validating: { bg: 'bg-blue-100', text: 'text-blue-700' },
  Building: { bg: 'bg-blue-100', text: 'text-blue-700' },
  Testing: { bg: 'bg-blue-100', text: 'text-blue-700' },
  Scanning: { bg: 'bg-blue-100', text: 'text-blue-700' },
  Versioning: { bg: 'bg-indigo-100', text: 'text-indigo-700' },
  Publishing: { bg: 'bg-indigo-100', text: 'text-indigo-700' },
  DeployingDev: { bg: 'bg-amber-100', text: 'text-amber-700' },
  VerifyingDev: { bg: 'bg-amber-100', text: 'text-amber-700' },
  DeployingQa: { bg: 'bg-purple-100', text: 'text-purple-700' },
  VerifyingQa: { bg: 'bg-purple-100', text: 'text-purple-700' },
  Succeeded: { bg: 'bg-green-100', text: 'text-green-700' },
  Failed: { bg: 'bg-red-100', text: 'text-red-700' },
  Cancelled: { bg: 'bg-gray-100', text: 'text-gray-500' },
  Paused: { bg: 'bg-yellow-100', text: 'text-yellow-700' },
  Skipped: { bg: 'bg-gray-100', text: 'text-gray-500' },
};

export const STAGE_LABELS: Record<string, string> = {
  Received: 'Received',
  Validating: 'Validate',
  Building: 'Build',
  Testing: 'Test',
  Scanning: 'Scan',
  Versioning: 'Version',
  Publishing: 'Publish',
  DeployingDev: 'Deploy DEV',
  VerifyingDev: 'Verify DEV',
  DeployingQa: 'Deploy QA',
  VerifyingQa: 'Verify QA',
};

export const POLL_INTERVALS = {
  pipelineList: 5000,
  pipelineDetail: 3000,
  health: 30000,
} as const;
